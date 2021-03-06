
using System;
using Sparrow.Geom;
using Sparrow.Rendering;
using Sparrow.Core;

namespace Sparrow.Textures
{
    public abstract class Texture
    {

        /// <summary>
        /// Uploads a texture to the GPU.
        /// Currently only 24 bit RBGA images are supported.
        /// </summary>
        /// <param name="imgData">The image data, either an byte[] or IntPtr</param>
        /// <param name="properties"></param>
        /// <param name="width">in points; number of pixels depends on scale parameter.</param>
        /// <param name="height">in points; number of pixels depends on scale parameter.</param>
        public static Texture FromData(object imgData, TextureOptions properties,
                                       int width, int height)
        {
            if (imgData == null)
            {
                throw new ArgumentException("imgData cannot be null!");
            }
            Texture tex = Empty(width, height, properties.PremultipliedAlpha,
                                properties.NumMipMaps, properties.OptimizeForRenderToTexture,
                                properties.Scale, properties.Format);

            tex.Root.UploadData(imgData);
            return tex;
        }

        /// <summary>
        /// Creates an empty texture of a certain size.
        ///  Beware that the texture can only be used after you either upload some color data
        ///  or clear the texture("Texture.Root.Clear()").
        /// </summary>
        /// <param name="width">in points; number of pixels depends on scale parameter</param>
        /// <param name="height">in points; number of pixels depends on scale parameter</param>
        /// <param name="premultipliedAlpha">the PMA format you will use the texture with. If you will
        ///     use the texture for bitmap data, use "true"; for compressed data, use "false".</param>
        /// <param name="numMipMaps">indicates if mipmaps should be used for this texture. When you 
        ///     upload bitmap data, this decides if mipmaps will be created.
        /// </param>
        /// <param name="optimizeForRenderToTexture">indicates if this texture will be used as render target
        /// </param>
        /// <param name="scale">if you omit this parameter, 'Sparrow.ContentScaleFactor' will be used.
        /// </param>
        /// <param name="format">the OpenGL texture format to use. Pass one of the uncompressed or
        ///     compressed formats to save memory(at the price of reduced image quality).
        /// </param>
        public static Texture Empty(float width, float height, bool premultipliedAlpha = true,
                                    int numMipMaps = 0, bool optimizeForRenderToTexture = false,
                                    float scale = -1, TextureFormat format = null)
        {
            if (format == null) format = TextureFormat.Rgba4444;
            if (scale <= 0.0f) scale = SparrowSharp.ContentScaleFactor;

            float origWidth  = width * scale;
            float origHeight = height * scale;
            
            var actualWidth = (int)Math.Ceiling(origWidth  - 0.000000001d);
            var actualHeight = (int)Math.Ceiling(origHeight - 0.000000001d);
            
            ConcreteTexture concreteTexture = new ConcreteTexture(
                    format, actualWidth, actualHeight, numMipMaps,
                    premultipliedAlpha, optimizeForRenderToTexture, scale);

            if (actualWidth - origWidth < 0.001f && actualHeight - origHeight < 0.001f)
            {
                return concreteTexture;
            }
            return new SubTexture(concreteTexture, Rectangle.Create(0.0f, 0.0f, width, height), true);
        }
        
        public static Texture FromColor(float width, float height,
                                        uint color = 0xffffff, float alpha = 1.0f,
                                        bool optimizeForRenderToTexture = false,
                                        float scale =-1, TextureFormat format = null) {
            if (format == null) format = TextureFormat.Rgba4444;
            var texture = Empty(width, height, true, 0, optimizeForRenderToTexture, scale, format);
            texture.Root.Clear(color, alpha);
            return texture;
        }

        /// <summary>
        /// Disposes the underlying texture data. Note that not all textures need to be disposed:
        /// SubTextures(created with 'Texture.FromTexture') just reference other textures and
        /// and do not take up resources themselves; this is also true for textures from an
        /// atlas.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Creates a texture that contains a region (in pixels) of another texture. The new
        /// texture will reference the base texture; no data is duplicated.
        /// </summary>
        /// <param name="texture">The texture you want to create a SubTexture from.</param>
        /// <param name="region">The region of the parent texture that the SubTexture will show
        ///     (in points).
        /// </param>
        /// <param name="frame">If the texture was trimmed, the frame rectangle can be used to restore
        ///     the trimmed area.
        /// </param>
        /// <param name="rotated">If true, the SubTexture will show the parent region rotated by
        ///     90 degrees (CCW).
        /// </param>
        /// <param name="scaleModifier">The scale factor of the new texture will be calculated by
        ///     multiplying the parent texture's scale factor with this value.
        /// </param>
        public static SubTexture FromTexture(Texture texture, Rectangle region = null,
                                          Rectangle frame = null, bool rotated = false,
                                          float scaleModifier = 1.0f)
        {
            return new SubTexture(texture, region, false, frame, rotated, scaleModifier);
        }

        /** Sets up a VertexData instance with the correct positions for 4 vertices so that
        *  the texture can be mapped onto it unscaled. If the texture has a <code>frame</code>,
        *  the vertices will be offset accordingly.
        *
        *  @param vertexData  the VertexData instance to which the positions will be written.
        *  @param vertexID    the start position within the VertexData instance.
        *  @param bounds      useful only for textures with a frame. This will position the
        *                     vertices at the correct position within the given bounds,
        *                     distorted appropriately.
        */
        public void SetupVertexPositions(VertexData vertexData, int vertexId = 0,
                                         Rectangle bounds = null)
        {
            Rectangle frame = Frame;
            float width     = Width;
            float height    = Height;

            Rectangle sRectangle = Rectangle.Create();
            if (frame != null)
                sRectangle.SetTo(-frame.X, -frame.Y, width, height);
            else
                sRectangle.SetTo(0, 0, width, height);
            
            vertexData.SetPoint(vertexId,     sRectangle.Left,  sRectangle.Top);
            vertexData.SetPoint(vertexId + 1, sRectangle.Right, sRectangle.Top);
            vertexData.SetPoint(vertexId + 2, sRectangle.Left,  sRectangle.Bottom);
            vertexData.SetPoint(vertexId + 3, sRectangle.Right, sRectangle.Bottom);

            if (bounds != null)
            {
                float scaleX = bounds.Width  / FrameWidth;
                float scaleY = bounds.Height / FrameHeight;

                if (scaleX != 1.0 || scaleY != 1.0 || bounds.X != 0 || bounds.Y != 0)
                {
                    Matrix2D sMatrix = Matrix2D.Create();
                    sMatrix.Identity();
                    sMatrix.Scale(scaleX, scaleY);
                    sMatrix.Translate(bounds.X, bounds.Y);
                    vertexData.TransformVertices(sMatrix, vertexId, 4);
                }
            }
        }

        /** Sets up a VertexData instance with the correct texture coordinates for
         *  4 vertices so that the texture is mapped to the complete quad.
         *
         *  @param vertexData  the vertex data to which the texture coordinates will be written.
         *  @param vertexID    the start position within the VertexData instance.
         */
        public void SetupTextureCoordinates(VertexData vertexData, int vertexId = 0)
        {
            SetTexCoords(vertexData, vertexId    , 0.0f, 0.0f);
            SetTexCoords(vertexData, vertexId + 1, 1.0f, 0.0f);
            SetTexCoords(vertexData, vertexId + 2, 0.0f, 1.0f);
            SetTexCoords(vertexData, vertexId + 3, 1.0f, 1.0f);
        }

        /// <summary>
        /// Transforms the given texture coordinates from the local coordinate system
        /// into the root texture's coordinate system. 
        /// </summary>
        public Point LocalToGlobal(float u, float v)
        {
            Point outP = Point.Create();
            if (this == Root)
            {
                outP.X = u;
                outP.Y = v;
            } 
            else outP = TransformationMatrixToRoot.TransformPoint(u, v);
            return outP;
        }

        /// <summary>
        /// Transforms the given texture coordinates from the root texture's coordinate system
        /// to the local coordinate system.
        /// </summary>
        public Point GlobalToLocal(float u, float v)
        {
            Point outP = Point.Create(u, v);
            if (this == Root)
            {
                outP.X = u;
                outP.Y = v;
            }
            else
            {
                Matrix2D sMatrix = Matrix2D.Create();
                sMatrix.CopyFromMatrix(TransformationMatrixToRoot);
                sMatrix.Invert();
                outP = sMatrix.TransformPoint(u, v);
            }
            return outP;
        }

        /** Writes the given texture coordinates to a VertexData instance after transforming
         *  them into the root texture's coordinate system. That way, the texture coordinates
         *  can be used directly to sample the texture in the fragment shader. */
        public void SetTexCoords(VertexData vertexData, int vertexId, float u, float v)
        {
            Point sPoint = LocalToGlobal(u, v);
            vertexData.SetTexCoords(vertexId, sPoint);
        }

        /** Reads a pair of texture coordinates from the given VertexData instance and transforms
         *  them into the current texture's coordinate system. (Remember, the VertexData instance
         *  will always contain the coordinates in the root texture's coordinate system!) */
        public Point GetTexCoords(VertexData vertexData, int vertexId)
        {
            var outP = vertexData.GetTexCoords(vertexId);
            return GlobalToLocal(outP.X, outP.Y);
        }

        // properties

        /** The texture frame if it has one (see class description), otherwise <code>null</code>.
         *  <p>CAUTION: not a copy, but the actual object! Do not modify!</p> */
        public virtual Rectangle Frame { get { return null; } }

        /** The height of the texture in points, taking into account the frame rectangle
         *  (if there is one). */
        public virtual float FrameWidth { get { return Frame != null ? Frame.Width : Width; } }

        /** The width of the texture in points, taking into account the frame rectangle
         *  (if there is one). */
        public virtual float FrameHeight { get { return Frame != null ? Frame.Height : Height; } }

        /// <summary>
        /// The width of the texture in points.
        /// </summary>
        public virtual float Width { get { return 0; } }

        /// <summary>
        /// The height of the texture in points.
        /// </summary>
        public virtual float Height { get { return 0; } }

        /** The width of the texture in pixels (without scale adjustment). */
        public virtual float NativeWidth { get { return 0; } }

        /** The height of the texture in pixels (without scale adjustment). */
        public virtual float NativeHeight { get { return 0f; } }
        
        /** The scale factor, which influences width and height properties. */
        public virtual float Scale { get { return 1.0f; } }

        /// <summary>
        /// The OpenGL texture object the texture is based on.
        /// </summary>
        public virtual uint Base { get { return 0; } }

        /// <summary>
        /// The concrete texture the texture is based on.
        /// </summary>
        public virtual ConcreteTexture Root { get { return null; } }

        /// <summary>
        /// The <code>TextureFormat</code> of the underlying texture data.
        /// </summary>
        public virtual TextureFormat Format => TextureFormat.Rgba4444;

        /// <summary>
        /// Indicates if the texture contains mip maps.
        /// </summary>
        public virtual int NumMipMaps { get { return 0; } }

        /// <summary>
        /// Indicates if the alpha values are premultiplied into the RGB values.
        /// </summary>
        public virtual bool PremultipliedAlpha { get { return false; } }

        /** The matrix that is used to transform the texture coordinates into the coordinate
         *  space of the parent texture, if there is one. @default null
         *
         *  <p>CAUTION: not a copy, but the actual object! Never modify this matrix!</p> */
        public virtual Matrix2D TransformationMatrix { get { return null; } }

        /** The matrix that is used to transform the texture coordinates into the coordinate
         *  space of the root texture, if this instance is not the root. @default null
         *
         *  <p>CAUTION: not a copy, but the actual object! Never modify this matrix!</p> */
        public virtual Matrix2D TransformationMatrixToRoot { get { return null; } }

        /** Returns the maximum size constraint (for both width and height) for textures in the
         *  current OpenGL profile. */
        public const int MaxSize = 4096;

    }
}

