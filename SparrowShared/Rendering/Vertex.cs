
using Sparrow.Geom;
using System.Runtime.InteropServices;

namespace Sparrow.Utils
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        
        public const int Size = 16;
        public const int PositionOffset = 0;
        public const int TextureOffset = 8;
        public Vector2 Position;
        public Vector2 TexCoords;
        
        /// <summary>
        /// Copies the source to the target array. Note that this is an unsafe operation, if you supply wrong offset/count values
        /// it can lead to memory corruption!
        /// </summary>
        public static unsafe void Copy(Vertex[] source, int sourceOffset, Vertex[] target, int targetOffset, int count)
        {
            // The following fixed statement pins the location of the source and 
            // target objects in memory so that they will not be moved by garbage 
            // collection. 
            fixed (Vertex* pSource = source, pTarget = target)
            {
                // Set the starting points in source and target for the copying. 
                Vertex* ps = pSource + sourceOffset;
                Vertex* pt = pTarget + targetOffset;

                // Copy the specified number of bytes from source to target. 
                for (int i = 0; i < count; i++)
                {
                    *pt = *ps;
                    pt++;
                    ps++;
                }
            }
        }

        public override string ToString()
        {
            return "x=" + Position.X + " y=" + Position.Y + " texX=" + TexCoords.X + " texY=" + TexCoords.Y;
        }
    }
}