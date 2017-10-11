﻿
using Sparrow.Utils;
using Sparrow.Geom;
using System;
using System.Collections.Generic;
using System.Text;
using OpenGL;
using Sparrow.Core;

namespace Sparrow.Rendering
{
    /** An effect encapsulates all steps of a OpenGL draw operation. It configures the
     *  render context and sets up shader programs as well as index- and vertex-buffers, thus
     *  providing the basic mechanisms of all low-level rendering.
     *
     *  <p><strong>Using the Effect class</strong></p>
     *
     *  <p>Effects are mostly used by the <code>MeshStyle</code> and <code>FragmentFilter</code>
     *  classes. When you extend those classes, you'll be required to provide a custom effect.
     *  Setting it up for rendering is done by the base class, though, so you rarely have to
     *  initiate the rendering yourself. Nevertheless, it's good to know how an effect is doing
     *  its work.</p>
     *
     *  <p>Using an effect always follows steps shown in the example below. You create the
     *  effect, configure it, upload vertex data and then: draw!</p>
     *
     *  <listing>
     *  // create effect
     *  MeshEffect effect = new MeshEffect();
     *  
     *  // configure effect
     *  effect.MvpMatrix3D = Painter.State.MvpMatrix3D;
     *  effect.Texture = GetHeroTexture();
     *  effect.Color = 0xf0f0f0;
     *  
     *  // upload vertex data
     *  effect.UploadIndexData(indexData);
     *  effect.UploadVertexData(vertexData);
     *  
     *  // draw!
     *  effect.Render(0, numTriangles);</listing>
     *
     *  <p>Note that the <code>VertexData</code> being uploaded has to be created with the same
     *  format as the one returned by the effect's <code>vertexFormat</code> property.</p>
     *
     *  <p>Extending the Effect class</p>
     *
     *  <p>The base <code>Effect</code>-class can only render white triangles, which is not much
     *  use in itself. However, it is designed to be extended; subclasses can easily implement any
     *  kinds of shaders.</p>
     *
     *  <p>Normally, you won't extend this class directly, but either <code>FilterEffect</code>
     *  or <code>MeshEffect</code>, depending on your needs (i.e. if you want to create a new
     *  fragment filter or a new mesh style). Whichever base class you're extending, you should
     *  override the following methods:</p>
     *
     *  <ul>
     *    <li><code>Program CreateProgram()</code> — must create the actual program containing 
     *        vertex- and fragment-shaders. A program will be created only once for each render
     *        context; this is taken care of by the base class.</li>
     *    <li><code>uint get programVariantName()</code> (optional) — override this if your
     *        effect requires different programs, depending on its settings. The recommended
     *        way to do this is via a bit-mask that uniquely encodes the current settings.</li>
     *    <li><code>String get VertexFormat()</code> (optional) — must return the
     *        <code>VertexData</code> format that this effect requires for its vertices. If
     *        the effect does not require any special attributes, you can leave this out.</li>
     *    <li><code>beforeDraw()</code> — Set up your context by
     *        configuring program constants and buffer attributes.</li>
     *    <li><code>AfterDraw()</code> — Will be called directly after
     *        <code>context.drawElements()</code>. Clean up any context configuration here.</li>
     *  </ul>
     *
     *  <p>Furthermore, you need to add properties that manage the data you require on rendering,
     *  e.g. the texture(s) that should be used, program constants, etc. I recommend looking at
     *  the implementations of Starling's <code>FilterEffect</code> and <code>MeshEffect</code>
     *  classes to see how to approach sub-classing.</p>
     *
     *  @see FilterEffect
     *  @see MeshEffect
     *  @see Sparrow.Styles.MeshStyle
     *  @see Sparrow.Filters.FragmentFilter
     *  @see Sparrow.Utils.RenderUtil
     */
    public class Effect
    {

        protected uint _vertexBufferName;
        protected uint _vertexColorsBufferName;
        protected uint _indexBufferName;
        protected int _vertexBufferSize; // in number of vertices
        protected int _indexBufferSize;  // in number of indices
        protected bool _indexBufferUsesQuadLayout;

        private readonly Matrix3D _mvpMatrix3D;

        // helper objects
        public readonly Dictionary<string, Dictionary<uint, string>> sProgramNameCache = 
                                        new Dictionary<string, Dictionary<uint, string>>();

        /// <summary>
        /// Creates a new effect.
        /// </summary>
        public Effect()
        {
            _mvpMatrix3D = Matrix3D.Create();
            ProgramBaseName = GetType().Name;
            SparrowSharp.ContextCreated += OnContextCreated;
        }

        /// <summary>
        /// Purges the index- and vertex-buffers.
        /// </summary>
        public void Dispose()
        {
            PurgeBuffers();
            SparrowSharp.ContextCreated -= OnContextCreated;
        }

        private void OnContextCreated()
        {
            PurgeBuffers();
        }

        /// <summary>
        /// Purges one or both of the vertex- and index-buffers.
        /// </summary>
        public void PurgeBuffers(bool vertexBuffer = true, bool indexBuffer = true)
        {
            if (_vertexBufferName != 0 && vertexBuffer)
            {
                uint[] buffers = { _vertexBufferName };
                Gl.DeleteBuffers(buffers);
                _vertexBufferName = 0;
                if (_vertexColorsBufferName != 0)
                {
                    uint[] colorBuffers = { _vertexColorsBufferName };
                    Gl.DeleteBuffers(colorBuffers);
                    _vertexColorsBufferName = 0;
                }
            }

            if (_indexBufferName != 0 && indexBuffer)
            {
                uint[] indexBuffers = { _indexBufferName };
                Gl.DeleteBuffers(indexBuffers);
                _indexBufferName = 0;
            }
        }

        /// <summary>
        /// Uploads the given index data to the internal index buffer. If the buffer is too
        /// small, a new one is created automatically.
        /// </summary>
        /// <param name="indexData">The IndexData instance to upload.</param>
        public void UploadIndexData(IndexData indexData)
        {
            UploadIndexData(indexData, BufferUsage.StaticDraw);
        }
        
        /// <summary>
        /// Uploads the given index data to the internal index buffer. If the buffer is too
        /// small, a new one is created automatically.
        /// </summary>
        /// <param name="indexData">The IndexData instance to upload.</param>
        /// <param name="bufferUsage">The expected buffer usage. Use one of the constants defined in
        ///                    <code>BufferUsageARB</code>. Only used when the method call
        ///                    causes the creation of a new index buffer.</param>
        public void UploadIndexData(IndexData indexData, BufferUsage bufferUsage)
        {
            int numIndices = indexData.NumIndices;
            bool isQuadLayout = indexData.UseQuadLayout;
            bool wasQuadLayout = _indexBufferUsesQuadLayout;

            if (_indexBufferName != 0)
            {
                if (numIndices <= _indexBufferSize)
                {
                    if (!isQuadLayout || !wasQuadLayout)
                    {
                        indexData.UploadToIndexBuffer(_indexBufferName, bufferUsage);
                        _indexBufferUsesQuadLayout = isQuadLayout && numIndices == _indexBufferSize;
                    }
                }
                else
                {
                    PurgeBuffers(false);
                }
            }
            if (_indexBufferName == 0)
            {
                _indexBufferName = indexData.CreateIndexBuffer(true, bufferUsage);
                _indexBufferSize = numIndices;
                _indexBufferUsesQuadLayout = isQuadLayout;
            }
        }

        /// <summary>
        /// Uploads the given vertex data to the internal vertex buffer. If the buffer is too
        /// small, a new one is created automatically.
        /// </summary>
        /// <param name="vertexData">The VertexData instance to upload.</param>
        public void UploadVertexData(VertexData vertexData)
        {
            UploadVertexData(vertexData, BufferUsage.StaticDraw);
        }
        
        /// <summary>
        /// Uploads the given vertex data to the internal vertex buffer. If the buffer is too
        /// small, a new one is created automatically.
        /// </summary>
        /// <param name="vertexData">The VertexData instance to upload.</param>
        /// <param name="bufferUsage"> The expected buffer usage. Use one of the constants defined in
        ///                    <code>BufferUsageARB</code>. Only used when the method call
        ///                    causes the creation of a new vertex buffer.</param>
        public void UploadVertexData(VertexData vertexData, BufferUsage bufferUsage)
        {
            if (_vertexBufferName != 0)
            {
                if (vertexData.NumVertices <= _vertexBufferSize)
                {
                    vertexData.UploadToVertexBuffer(_vertexBufferName, _vertexColorsBufferName, bufferUsage);
                }
                else
                {
                    PurgeBuffers(true, false);
                }
                    
            }
            if (_vertexBufferName == 0)
            {
                uint[] names = vertexData.CreateVertexBuffer(true);
                _vertexBufferName = names[0];
                _vertexColorsBufferName = names[1];
                _vertexBufferSize = vertexData.NumVertices;
            }
        }

        // rendering

        /// <summary>
        /// Draws the triangles described by the index- and vertex-buffers, or a range of them.
        /// This calls <code>BeforeDraw</code>, <code>Gl.DrawElements</code>, and
        /// <code>AfterDraw</code>, in this order.
        /// </summary>
        public virtual void Render(int firstIndex = 0, int numTriangles= -1)
        {
            if (numTriangles < 0) numTriangles = _indexBufferSize;
            if (numTriangles == 0) return;
            
            BeforeDraw();

            Gl.DrawElements(PrimitiveType.Triangles, numTriangles * 3, DrawElementsType.UnsignedShort, IntPtr.Zero);

            AfterDraw();
        }

        /// <summary>
        /// This method is called by <code>Render</code>, directly before
        /// <code>GL.drawElements</code>. It activates the program and sets up
        /// the GL context with the following constants and attributes:
        /// 
        /// <code>uMvpMatrix</code> — MVP matrix
        /// <code>aPosition</code> — vertex position (xy)
        /// </summary>
        protected virtual void BeforeDraw()
        {
            Program.Activate(); // create, upload, use program
            
            //is this the best place for this?
            Gl.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferName);
            Gl.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBufferName);

            uint attribPosition = (uint)Program.Attributes["aPosition"];
            Gl.EnableVertexAttribArray(attribPosition);
            Gl.VertexAttribPointer(attribPosition, 2, VertexAttribType.Float, false, Vertex.Size, (IntPtr)Vertex.PositionOffset);
            
            int uMvpMatrix = Program.Uniforms["uMvpMatrix"];
            Gl.UniformMatrix4(uMvpMatrix, 1, false, MvpMatrix3D.RawData); // 1 is the number of matrices

            // color & alpha are set in subclasses
        }

        /// <summary>
        /// This method is called by <code>Render</code>, directly after
        /// <code>Gl.DrawElements</code>. Resets vertex buffer attributes.
        /// </summary>
        protected virtual void AfterDraw()
        {
            //?? context.setVertexBufferAt(0, null);
            uint attribPosition = (uint)Program.Attributes["aPosition"];
            Gl.DisableVertexAttribArray(attribPosition);
        }

        // program management

        /// <summary>
        /// Creates the program (a combination of vertex- and fragment-shader) used to render
        /// the effect with the current settings. Override this method in a subclass to create
        /// your shaders. This method will only be called once; the program is automatically stored
        /// in the <code>Painter</code> and re-used by all instances of this effect.
        ///
        /// <para>The basic implementation always outputs pure white.</para>
        /// </summary>
        protected virtual Program CreateProgram()
        {
            string vertexShader = AddShaderInitCode() + @"
            in vec4 aPosition;
            uniform mat4 uMvpMatrix;
            
            void main() {
              gl_Position = uMvpMatrix * aPosition;
            }";
            
            string fragmentShader = AddShaderInitCode() + @"
            out lowp vec4 fragColor;

            void main() {
                fragColor = vec4(1, 1, 1, 1);
            }";
            return new Program(vertexShader, fragmentShader);
        }

        /// <summary>
        /// Appends OpenGL shader defines, this is needed for shaders to work on both
        /// desktop OpenGL and OpenGL ES 2+.
        /// </summary>
        public static string AddShaderInitCode()
        {
            string ret;
#if __WINDOWS__
            ret = 
                "#version 430\n" + // TODO rewrite shaders for GL 4+
                "#define highp\n" +
                "#define mediump\n" +
                "#define lowp\n";
#else
            ret = @"#version 100 es\n"; // this should be 300 es
#endif
            return ret;
        }
        
        /// <summary>
        /// Override this method if the effect requires a different program depending on the
        /// current settings. Ideally, you do this by creating a bit mask encoding all the options.
        /// This method is called often, so do not allocate any temporary objects when overriding.
        ///
        /// @default 0
        /// </summary>
        protected virtual uint ProgramVariantName { get { return 0; } }

        /// <summary>
        /// Returns the base name for the program. @default the fully qualified class name
        /// </summary>
        protected string ProgramBaseName;

        /// <summary>
        /// Returns the full name of the program, which is used to register it at the current
        /// <code>Painter</code>.
        ///
        /// <para>The default implementation efficiently combines the program's base and variant
        /// names (e.g. <code>LightEffect#42</code>). It shouldn't be necessary to override
        /// this method.</para>
        /// </summary>
        protected string ProgramName
        {
            get
            {
                string baseName = ProgramBaseName;
                uint variantName = ProgramVariantName;
                Dictionary<uint, string> nameCache;
                if (!sProgramNameCache.ContainsKey(baseName))
                {
                    nameCache = new Dictionary<uint, string>();
                    sProgramNameCache[baseName] = nameCache;
                }
                else
                {
                    nameCache = sProgramNameCache[baseName];
                }

                string name;
                if (nameCache.ContainsKey(variantName))
                {
                    name = nameCache[variantName];
                }
                else
                {
                    if (variantName != 0) name = baseName + "#" + variantName.ToString("X"); // hex string conversion
                    else name = baseName;
                    nameCache[variantName] = name;
                }
                return name;
            }
        }

        /// <summary>
        /// Returns the current program, either by creating a new one (via
        /// <code>CreateProgram</code>) or by getting it from the <code>Painter</code>.
        /// Do not override this method! Instead, implement <code>CreateProgram</code>.
        /// </summary>
        protected Program Program
        {
            get
            {
                string name = ProgramName;
                Painter painter = SparrowSharp.Painter;
                Program program = painter.GetProgram(name);

                if (program == null)
                {
                    program = CreateProgram();
                    painter.RegisterProgram(name, program);
                }
                return program;
            }
        }

        /** The function that you provide here will be called after a context loss.
         *  Call both "Upload..." methods from within the callback to restore any vertex or
         *  index buffers. */
        public Action OnRestore;

        public Matrix3D MvpMatrix3D
        {
            get { return _mvpMatrix3D; }
            set { _mvpMatrix3D.CopyFrom(value); }
        }
    }
}
