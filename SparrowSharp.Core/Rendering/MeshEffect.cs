﻿
using Sparrow.Core;

namespace SparrowSharp.Core.Rendering
{

    public class MeshEffect : FilterEffect
    {

        /** The vertex format expected by <code>uploadVertexData</code>:
         *  <code>"position:float2, texCoords:float2, color:bytes4"</code> */
        public static readonly VertexDataFormat VERTEX_FORMAT =
            FilterEffect.VERTEX_FORMAT.extend("color:bytes4");

        private float _alpha;
        private bool _tinted;
        private bool _optimizeIfNotTinted;

        // helper objects
        private static float[] sRenderAlpha = new float[4];

        /** Creates a new MeshEffect instance. */
        public MeshEffect()
        {
            // Non-tinted meshes may be rendered with a simpler fragment shader, which brings
            // a huge performance benefit on some low-end hardware. However, I don't want
            // subclasses to become any more complicated because of this optimization (they
            // probably use much longer shaders, anyway), so I only apply this optimization if
            // this is actually the "MeshEffect" class.

            _alpha = 1.0f;
            _optimizeIfNotTinted = GetType().Name == "starling.rendering::MeshEffect";
            // todo check
        }
        
        override protected uint ProgramVariantName
        {
            get
            {
                uint noTinting = (_optimizeIfNotTinted && !_tinted && _alpha == 1.0f) == true ? 1u : 0u;
                return base.ProgramVariantName | (noTinting << 3);
            }
        }
        
        override protected Program CreateProgram()
        {
            string vertexShader, fragmentShader;

            if (Texture != null)
            {
                if (_optimizeIfNotTinted && !_tinted && _alpha == 1.0)
                    return base.CreateProgram();

                vertexShader =
                    "m44 op, va0, vc0 \n" + // 4x4 matrix transform to output clip-space
                    "mov v0, va1      \n" + // pass texture coordinates to fragment program
                    "mul v1, va2, vc4 \n";  // multiply alpha (vc4) with color (va2), pass to fp

                fragmentShader =
                    Tex("ft0", "v0", 0, Texture) +
                    "mul oc, ft0, v1  \n";  // multiply color with texel color
            }
            else
            {
                vertexShader =
                    "m44 op, va0, vc0 \n" + // 4x4 matrix transform to output clipspace
                    "mul v0, va2, vc4 \n";  // multiply alpha (vc4) with color (va2)

                fragmentShader =
                    "mov oc, v0       \n";  // output color
            }
            return Program.FromSource(vertexShader, fragmentShader);
        }

        /** This method is called by <code>render</code>, directly before
         *  <code>context.drawTriangles</code>. It activates the program and sets up
         *  the context with the following constants and attributes:
         *
         *  <ul>
         *    <li><code>vc0-vc3</code> — MVP matrix</li>
         *    <li><code>vc4</code> — alpha value (same value for all components)</li>
         *    <li><code>va0</code> — vertex position (xy)</li>
         *    <li><code>va1</code> — texture coordinates (uv)</li>
         *    <li><code>va2</code> — vertex color (rgba), using premultiplied alpha</li>
         *    <li><code>fs0</code> — texture</li>
         *  </ul>
         */
        override protected void BeforeDraw(Context3D context)
        {
            base.BeforeDraw(context);

            sRenderAlpha[0] = sRenderAlpha[1] = sRenderAlpha[2] = sRenderAlpha[3] = _alpha;
            context.setProgramConstantsFromVector(Context3DProgramType.VERTEX, 4, sRenderAlpha);

            if (_tinted || _alpha != 1.0 || !_optimizeIfNotTinted || Texture == null)
                VertexFormat.setVertexBufferAt(2, VertexBuffer, "color");
        }

        /** This method is called by <code>render</code>, directly after
         *  <code>context.drawTriangles</code>. Resets texture and vertex buffer attributes. */
        override protected void AfterDraw(Context3D context)
        {
            context.setVertexBufferAt(2, null);
            base.AfterDraw(context);
        }

        /** The data format that this effect requires from the VertexData that it renders:
         *  <code>"position:float2, texCoords:float2, color:bytes4"</code> */
        override public VertexDataFormat VertexFormat { get { return VERTEX_FORMAT; } }

        /** The alpha value of the object rendered by the effect. Must be taken into account
         *  by all subclasses. */
        public float Alpha {
            get { return _alpha; }
            set { _alpha = value;  }
        }

        /** Indicates if the rendered vertices are tinted in any way, i.e. if there are vertices
         *  that have a different color than fully opaque white. The base <code>MeshEffect</code>
         *  class uses this information to simplify the fragment shader if possible. May be
         *  ignored by subclasses. */
        public bool Tinted
        {
            get { return _tinted; }
            set { _tinted = value; }
        }

}
}