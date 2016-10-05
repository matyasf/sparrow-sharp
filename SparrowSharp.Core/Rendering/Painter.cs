﻿
using Sparrow.Display;
using Sparrow.Geom;
using Sparrow.Utils;
using System;
using System.Collections.Generic;
using Sparrow.Textures;
using Sparrow.Text;
using System.Diagnostics;
#if __WINDOWS__
using OpenTK.Graphics.OpenGL4;
#elif __ANDROID__
using OpenTK.Graphics.ES30;
#endif

namespace Sparrow.Rendering
{

    /** A class that orchestrates rendering of all Starling display objects.
    *
    *  <p>A Starling instance contains exactly one 'Painter' instance that should be used for all
    *  rendering purposes. Each frame, it is passed to the render methods of all rendered display
    *  objects. To access it outside a render method, call <code>Starling.painter</code>.</p>
    *
    *  <p>The painter is responsible for drawing all display objects to the screen. At its
    *  core, it is a wrapper for many Context3D methods, but that's not all: it also provides
    *  a convenient state mechanism, supports masking and acts as middleman between display
    *  objects and renderers.</p>
    *
    *  <strong>The State Stack</strong>
    *
    *  <p>The most important concept of the Painter class is the state stack. A RenderState
    *  stores a combination of settings that are currently used for rendering, e.g. the current
    *  projection- and modelview-matrices and context-related settings. It can be accessed
    *  and manipulated via the <code>state</code> property. Use the methods
    *  <code>pushState</code> and <code>popState</code> to store a specific state and restore
    *  it later. That makes it easy to write rendering code that doesn't have any side effects.</p>
    *
    *  <listing>
    *  painter.pushState(); // save a copy of the current state on the stack
    *  painter.state.renderTarget = renderTexture;
    *  painter.state.transformModelviewMatrix(object.transformationMatrix);
    *  painter.state.alpha = 0.5;
    *  painter.prepareToDraw(); // apply all state settings at the render context
    *  drawSomething(); // insert Stage3D rendering code here
    *  painter.popState(); // restores previous state</listing>
    *
    *  @see RenderState
    */
    public class Painter
    {

        // members
        private Dictionary<string, Program> programs;
        private readonly Dictionary<int, uint> framebufferCache;

        private int _drawCount;
        private uint _frameID;
        private float _pixelSize;
        private Dictionary<int, int> _stencilReferenceValues;
        private Stack<Rectangle> _clipRectStack;
        private BatchProcessor _batchProcessor;
        private BatchProcessor _batchCache;
        private List<DisplayObject> _batchCacheExclusions;

        private int _actualRenderTarget;
        private uint _actualBlendMode;

        private float _backBufferWidth;
        private float _backBufferHeight;

        private RenderState _state;
        private List<RenderState> _stateStack;
        private int _stateStackPos;
        private int _stateStackLength;

        // helper objects
        private static Matrix sMatrix = Matrix.Create();
        private static Rectangle sClipRect = Rectangle.Create();
        private static Rectangle sBufferRect = Rectangle.Create();
        private static Rectangle sScissorRect = Rectangle.Create();
        private static MeshSubset sMeshSubset = new MeshSubset();
        
        /** Creates a new Painter object. Normally, it's not necessary to create any custom
         *  painters; instead, use the global painter found on the Starling instance. */
        public Painter(float width, float height)
        {
            framebufferCache = new Dictionary<int, uint>();
            _actualBlendMode = 0;

            _backBufferWidth = width;
            _backBufferHeight = height;
            _pixelSize = 1.0f;
            _stencilReferenceValues = new Dictionary<int, int>(); // use weak refs!
            _clipRectStack = new Stack<Rectangle>();

            _batchProcessor = new BatchProcessor();
            _batchProcessor.OnBatchComplete = DrawBatch;

            _batchCache = new BatchProcessor();
            _batchCache.OnBatchComplete = DrawBatch;
            _batchCacheExclusions = new List<DisplayObject>();

            _state = new RenderState();
            _state._onDrawRequired = FinishMeshBatch;
            _stateStack = new List<RenderState>();
            _stateStackPos = -1;
            _stateStackLength = 0;
        }

        /** Disposes all mesh batches, programs, and - if it is not being shared -
        *  the render context. */
        public void Dispose()
        {
            _batchProcessor.Dispose();
            _batchCache.Dispose();
            // + dispose GL context?
        }

        // context handling

        /** Sets the viewport dimensions and other attributes of the rendering buffer.
         *  Starling will call this method internally, so most apps won't need to mess with this.
         *
         *  <p>Beware: if <code>shareContext</code> is enabled, the method will only update the
         *  painter's context-related information (like the size of the back buffer), but won't
         *  make any actual changes to the context.</p>
         *
         * @param viewPort                the position and size of the area that should be rendered
         *                                into, in pixels.
         */
        public void ConfigureBackBuffer(Rectangle viewPort)
        {
            _backBufferWidth  = viewPort.Width;
            _backBufferHeight = viewPort.Height;
        }

        // program management

        /** Registers a program under a certain name.
         *  If the name was already used, the previous program is overwritten. */
        public void RegisterProgram(string name, Program program)
        {
            DeleteProgram(name);
            Programs.Add(name, program);
        }

        /** Deletes the program of a certain name. */
        public void DeleteProgram(string name)
        {
            Program program = GetProgram(name);
            if (program != null)
            {
                program.Dispose();
                Programs.Remove(name);
            }
        }

        /// <summary>
        /// Returns the program registered under a certain name, or null if no program with
        ///  this name has been registered.
        /// </summary>
        public Program GetProgram(string name)
        {
            Program ret;
            Programs.TryGetValue(name, out ret);
            return ret;
        }

        /// <summary>
        /// Indicates if a program is registered under a certain name.
        /// </summary>
        public bool HasProgram(string name)
        {
            return Programs.ContainsKey(name);
        }

        // state stack

        /** Pushes the current render state to a stack from which it can be restored later.
         *
         *  <p>If you pass a BatchToken, it will be updated to point to the current location within
         *  the render cache. That way, you can later reference this location to render a subset of
         *  the cache.</p>
         */
        public void PushState(BatchToken token = null)
        {
            _stateStackPos++;

            if (_stateStackLength < _stateStackPos + 1)
            {
                _stateStackLength++;
                _stateStack.Add(new RenderState());
            }
            if (token != null) _batchProcessor.FillToken(token);

            _stateStack[_stateStackPos].CopyFrom(_state);
        }

        /** Modifies the current state with a transformation matrix, alpha factor, and blend mode.
         *
         *  @param transformationMatrix Used to transform the current <code>modelviewMatrix</code>.
         *  @param alphaFactor          Multiplied with the current alpha value.
         *  @param blendMode            Replaces the current blend mode; except for "auto", which
         *                              means the current value remains unchanged.
         */
        public void SetStateTo(Matrix transformationMatrix, float alphaFactor = 1.0f,
                               uint blendMode = BlendMode.AUTO)
        {
            if (transformationMatrix != null) _state._modelviewMatrix.PrependMatrix(transformationMatrix);
            if (alphaFactor != 1.0f) _state.Alpha *= alphaFactor;
            if (blendMode != BlendMode.AUTO) _state.BlendMode = blendMode;
        }

        /** Restores the render state that was last pushed to the stack. If this changes
        *  blend mode, clipping rectangle, render target or culling, the current batch
        *  will be drawn right away.
        *
        *  <p>If you pass a BatchToken, it will be updated to point to the current location within
        *  the render cache. That way, you can later reference this location to render a subset of
        *  the cache.</p>
        */
        public void PopState(BatchToken token = null)
        {
            if (_stateStackPos < 0)
            {
                throw new IndexOutOfRangeException("Cannot pop empty state stack");
            }
            _state.CopyFrom(_stateStack[_stateStackPos]); // -> might cause 'finishMeshBatch'
            _stateStackPos--;
            if (token != null)
            {
                _batchProcessor.FillToken(token);
            }
        }

        // masks

        /** Draws a display object into the stencil buffer, incrementing the buffer on each
        *  used pixel. The stencil reference value is incremented as well; thus, any subsequent
        *  stencil tests outside of this area will fail.
        *
        *  <p>If 'mask' is part of the display list, it will be drawn at its conventional stage
        *  coordinates. Otherwise, it will be drawn with the current modelview matrix.</p>
        *
        *  <p>As an optimization, this method might update the clipping rectangle of the render
        *  state instead of utilizing the stencil buffer. This is possible when the mask object
        *  is of type <code>starling.display.Quad</code> and is aligned parallel to the stage
        *  axes.</p>
        *
        *  <p>Note that masking breaks the render cache; the masked object must be redrawn anew
        *  in the next frame. If you pass <code>maskee</code>, the method will automatically
        *  call <code>excludeFromCache(maskee)</code> for you.</p>
        */
        public void DrawMask(DisplayObject mask, DisplayObject maskee = null)
        {
            FinishMeshBatch();

            if (IsRectangularMask(mask, maskee, sMatrix))
            {
                sClipRect = mask.GetBounds(mask);
                sClipRect = sClipRect.GetBounds(sMatrix);
                PushClipRect(sClipRect);
            }
            else
            {
                throw new NotImplementedException();
            }

            ExcludeFromCache(maskee);
        }

        /** Draws a display object into the stencil buffer, decrementing the
         *  buffer on each used pixel. This effectively erases the object from the stencil buffer,
         *  restoring the previous state. The stencil reference value will be decremented.
         *
         *  <p>Note: if the mask object meets the requirements of using the clipping rectangle,
         *  it will be assumed that this erase operation undoes the clipping rectangle change
         *  caused by the corresponding <code>drawMask()</code> call.</p>
         */
        public void EraseMask(DisplayObject mask, DisplayObject maskee = null)
        {
           
            FinishMeshBatch();

            if (IsRectangularMask(mask, maskee, sMatrix))
            {
                PopClipRect();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void PushClipRect(Rectangle clipRect)
        {
            Rectangle intersection;
            if (_clipRectStack.Count != 0)
            {
                intersection = clipRect.Intersection(_clipRectStack.Peek());
            }
            else
            {
                intersection = clipRect.Clone();
            }
            _clipRectStack.Push(intersection);
            _state.ClipRect = intersection;
        }

        private void PopClipRect()
        {
            int stackLength = _clipRectStack.Count;
            if (stackLength == 0)
            {
                throw new Exception("Trying to pop from empty clip rectangle stack");
            }
            stackLength--;
            _clipRectStack.Pop();
            _state.ClipRect = stackLength != 0 ? _clipRectStack.Peek() : null;
        }

        /** Figures out if the mask can be represented by a scissor rectangle; this is possible
        *  if it's just a simple (untextured) quad that is parallel to the stage axes. The 'out'
        *  parameter will be filled with the transformation matrix required to move the mask into
        *  stage coordinates. */
        private bool IsRectangularMask(DisplayObject mask, DisplayObject maskee, Matrix outMatrix)
        {
            Quad quad = mask as Quad;
            bool is3D = mask.Is3D || (maskee != null && maskee.Is3D && mask.Stage == null);

            if (quad != null && !is3D && quad.Texture == null)
            {
                if (mask.Stage != null) outMatrix = mask.GetTransformationMatrix(null);
                else
                {
                    outMatrix.CopyFromMatrix(mask.TransformationMatrix);
                    outMatrix.AppendMatrix(_state.ModelviewMatrix);
                }
                return (MathUtil.Equals(outMatrix.A, 0f) && MathUtil.Equals(outMatrix.D, 0f)) ||
                       (MathUtil.Equals(outMatrix.B, 0f) && MathUtil.Equals(outMatrix.C, 0f));
            }
            return false;
        }

        // mesh rendering

        /** Adds a mesh to the current batch of unrendered meshes. If the current batch is not
         *  compatible with the mesh, all previous meshes are rendered at once and the batch
         *  is cleared.
         *
         *  @param mesh    The mesh to batch.
         *  @param subset  The range of vertices to be batched. If <code>null</code>, the complete
         *                 mesh will be used.
         */
        public void BatchMesh(Mesh mesh, MeshSubset subset= null)
        {
            _batchProcessor.AddMesh(mesh, _state, subset);
        }

        /** Finishes the current mesh batch and prepares the next one. */
        public void FinishMeshBatch()
        {
            _batchProcessor.FinishBatch();
        }

        /** Completes all unfinished batches, cleanup procedures. */
        public void FinishFrame()
        {
            if (_frameID % 99 == 0) // odd number -> alternating processors
            {
                _batchProcessor.Trim();
            }
            _batchProcessor.FinishBatch();
            SwapBatchProcessors();
            _batchProcessor.Clear();
            ProcessCacheExclusions();
        }

        private void SwapBatchProcessors()
        {
            BatchProcessor tmp = _batchProcessor;
            _batchProcessor = _batchCache;
            _batchCache = tmp;
        }

        private void ProcessCacheExclusions()
        {
            int i;
            int length = _batchCacheExclusions.Count;
            for (i = 0; i < length; ++i) _batchCacheExclusions[i].ExcludeFromCache();
            _batchCacheExclusions.Clear();
        }


        /** Resets the current state, state stack, batch processor, stencil reference value,
         *  clipping rectangle, and draw count. Furthermore, depth testing is disabled. */
        public void NextFrame()
        {
            // enforce reset of basic context settings
            _actualBlendMode = 0;

            // reset everything else
            _clipRectStack.Clear();
            _drawCount = 0;
            _stateStackPos = -1;
            _batchProcessor.Clear();
            _state.Reset();
        }


        /** Draws all meshes from the render cache between <code>startToken</code> and
         *  (but not including) <code>endToken</code>. The render cache contains all meshes
         *  rendered in the previous frame. */
        public void DrawFromCache(BatchToken startToken, BatchToken endToken)
        {
            MeshBatch meshBatch;
            MeshSubset subset = sMeshSubset;

            if (!startToken.Equals(endToken))
            {
                PushState();

                for (int i = startToken.BatchID; i <= endToken.BatchID; ++i)
                {
                    meshBatch = _batchCache.GetBatchAt(i);
                    subset.SetTo(); // resets subset

                    if (i == startToken.BatchID)
                    {
                        subset.VertexID = startToken.VertexID;
                        subset.IndexID  = startToken.IndexID;
                        subset.NumVertices = meshBatch.NumVertices - subset.VertexID;
                        subset.NumIndices  = meshBatch.NumIndices  - subset.IndexID;
                    }

                    if (i == endToken.BatchID)
                    {
                        subset.NumVertices = endToken.VertexID - subset.VertexID;
                        subset.NumIndices  = endToken.IndexID  - subset.IndexID;
                    }

                    if (subset.NumVertices != 0)
                    {
                        _state.Alpha = 1.0f;
                        _state.BlendMode = meshBatch.BlendMode;
                        _batchProcessor.AddMesh(meshBatch, _state, subset, true);
                    }
                }
                PopState();
            }
        }

        /** Removes all parts of the render cache past the given token. Beware that some display
        *  objects might still reference those parts of the cache! Only call it if you know
        *  exactly what you're doing. */
        public void RewindCacheTo(BatchToken token)
        {
            _batchProcessor.RewindTo(token);
        }

        /** Prevents the object from being drawn from the render cache in the next frame.
         *  Different to <code>setRequiresRedraw()</code>, this does not indicate that the object
         *  has changed in any way, but just that it doesn't support being drawn from cache.
         *
         *  <p>Note that when a container is excluded from the render cache, its children will
         *  still be cached! This just means that batching is interrupted at this object when
         *  the display tree is traversed.</p>
         */
        public void ExcludeFromCache(DisplayObject obj)
        {
            if (obj != null)
            {
                _batchCacheExclusions.Add(obj);
            }
        }

        private void DrawBatch(MeshBatch meshBatch)
        {
            PushState();

            State.BlendMode = meshBatch.BlendMode;
            State.ModelviewMatrix.Identity();
            State.Alpha = 1.0f;

            meshBatch.Render(this);

            PopState();
        }

        // helper methods

        /** Applies all relevant state settings to at the render context. This includes
         *  blend mode, render target and clipping rectangle. Always call this method before
         *  <code>context.drawTriangles()</code>.
         */
        public void PrepareToDraw()
        {
            ApplyBlendMode();
            ApplyRenderTarget();
            ApplyClipRect();
        }

        /** Clears the render context with a certain color and alpha value. Since this also
        *  clears the stencil buffer, the stencil reference value is also reset to '0'. */
        public void Clear(uint rgb = 0, float alpha= 0.0f)
        {
            ApplyRenderTarget();

            float red = ColorUtil.GetR(rgb) / 255.0f;
            float green = ColorUtil.GetG(rgb) / 255.0f;
            float blue = ColorUtil.GetB(rgb) / 255.0f;
            GL.ClearColor(red, green, blue, alpha);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.StencilBufferBit | ClearBufferMask.DepthBufferBit);
    }

        /** Resets the render target to the back buffer */
        public void Present()
        {
            _state.RenderTarget = null;
        }

        private void ApplyBlendMode()
        {
            uint blendMode = _state.BlendMode;

            if (blendMode != _actualBlendMode)
            {
                BlendMode.Get(_state.BlendMode).Activate();
                _actualBlendMode = blendMode;
            }
        }

        private void ApplyRenderTarget()
        {
            int target = _state.RenderTargetBase;
            if (target != _actualRenderTarget)
            {
                if (target != 0)
                {
                    // TODO set this uint antiAlias  = _state.RenderTargetAntiAlias;
                    uint framebuffer;
                    if (!framebufferCache.TryGetValue(target, out framebuffer))
                    {
                        GL.GenFramebuffers(1, out framebuffer);
                        GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
                        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                                TextureTarget.Texture2D, target, 0);

                        if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                        {
                            Debug.WriteLine("Failed to create framebuffer for render texture");
                        }
                        framebufferCache.Add(target, framebuffer);
                    }
                    else
                    {
                        GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
                    }
                    GL.Viewport(0, 0, (int)_state.RenderTarget.NativeWidth, (int)_state.RenderTarget.NativeHeight);
                }
                else
                {
                    // TODO: double check these on a device, the ifdef seems to be unneeded
#if __IOS__
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 1);
#else
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
#endif
                    GL.Viewport(0, 0, (int)_backBufferWidth, (int)_backBufferHeight);
                }
                _actualRenderTarget = target;
            }
        }

        private void DestroyFramebufferForTexture(Texture texture) // TODO call this
        {
            uint framebuffer;
            if (framebufferCache.TryGetValue(texture.Base, out framebuffer))
            {
                GL.DeleteFramebuffers(1, ref framebuffer);
                framebufferCache.Remove(texture.Base);
            }
        }

        private void ApplyClipRect() // used by rectangular masks & render textures
        {
            Rectangle clipRect = _state.ClipRect;

            if (clipRect != null)
            {
                float width, height;
                Matrix3D projMatrix = _state.ProjectionMatrix3D;
                Texture renderTarget = _state.RenderTarget;

                if (renderTarget != null)
                {
                    width = renderTarget.Root.NativeWidth;
                    height = renderTarget.Root.NativeHeight;
                }
                else
                {
                    width = _backBufferWidth;
                    height = _backBufferHeight;
                }

                // convert to pixel coordinates (matrix transformation ends up in range [-1, 1])
                float[] sPoint3D = projMatrix.TransformCoords3D(clipRect.X, clipRect.Y, 0.0f);
                MathUtil.ProjectVector3D(ref sPoint3D); // eliminate w-coordinate
                sClipRect.X = (sPoint3D[0] * 0.5f + 0.5f) * width;
                sClipRect.Y = (0.5f - sPoint3D[1] * 0.5f) * height;

                sPoint3D = projMatrix.TransformCoords3D(clipRect.Right, clipRect.Bottom, 0.0f);
                MathUtil.ProjectVector3D(ref sPoint3D); // eliminate w-coordinate
                sClipRect.Right = (sPoint3D[0] * 0.5f + 0.5f) * width;
                sClipRect.Bottom = (0.5f - sPoint3D[1] * 0.5f) * height;

                if (renderTarget == null)
                { 
                    // OpenGL positions the scissor rectangle from the bottom of the screen :(
                    // flip it, if we're rendering to the backbuffer
                    sClipRect.Y = (int)(_backBufferHeight - sClipRect.Height - sClipRect.Y);
                }
                sBufferRect.SetTo(0, 0, width, height);
                sScissorRect = sClipRect.Intersection(sBufferRect);

                // an empty rectangle is not allowed, so we set it to the smallest possible size
                if (sScissorRect.Width < 1f || sScissorRect.Height < 1f)
                {
                    sScissorRect.SetTo(0, 0, 1, 1);
                    Debug.Write("WARNING: Clip rectangle has zero size, setting it to 1x1");
                }
                GL.Enable(EnableCap.ScissorTest);
                GL.Scissor((int)sScissorRect.X, (int)sScissorRect.Y, (int)sScissorRect.Width, (int)sScissorRect.Height);
            }
            else
            {       
                if (GL.IsEnabled(EnableCap.ScissorTest))
                {
                    GL.Disable(EnableCap.ScissorTest);
                    GL.Clear(ClearBufferMask.ColorBufferBit);
                }
            }
        }

        // properties

        /** Indicates the number of stage3D draw calls. */
        public int DrawCount
        {
            set { _drawCount = value; }
            get { return _drawCount; }
        }

        /** The current render state, containing some of the context settings, projection- and
         *  modelview-matrix, etc. Always returns the same instance, even after calls to "pushState"
         *  and "popState".
         *
         *  <p>When you change the current RenderState, and this change is not compatible with
         *  the current render batch, the batch will be concluded right away. Thus, watch out
         *  for changes of blend mode, clipping rectangle, render target or culling.</p>
         */
        public RenderState State { get { return _state; } }

        /** The number of frames that have been rendered with the current Starling instance. */
        public uint FrameID {
            get { return _frameID; }
            set { _frameID = value;  }
        }

        /** The size (in points) that represents one pixel in the back buffer. */
        public float PixelSize
        {
            get { return _pixelSize; }
            set { _pixelSize = value; }
        }

        /** Returns the current width of the back buffer. In most cases, this value is in pixels;
         *  however, if the app is running on an HiDPI display with an activated
         *  'supportHighResolutions' setting, you have to multiply with 'backBufferPixelsPerPoint'
         *  for the actual pixel count. Alternatively, use the Context3D-property with the
         *  same name: it will return the exact pixel values. */
        public float BackBufferWidth { get { return _backBufferWidth; } }

        /** Returns the current height of the back buffer. In most cases, this value is in pixels;
         *  however, if the app is running on an HiDPI display with an activated
         *  'supportHighResolutions' setting, you have to multiply with 'backBufferPixelsPerPoint'
         *  for the actual pixel count. Alternatively, use the Context3D-property with the
         *  same name: it will return the exact pixel values. */
        public float BackBufferHeight { get { return _backBufferHeight; } }

        private Dictionary<string, Program> Programs {
            get
            {
                if (programs == null)
                {
                    programs = new Dictionary<string, Program>();
                }
                return programs;
            }
        }
    }
}
