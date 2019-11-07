
using Sparrow.Core;
using Sparrow.Textures;

namespace Sparrow.Text
{
    
    public class TextOptions
    {
        // + add here HTML text support

        /// <summary>
        /// Indicates if the text should be wrapped at word boundaries if it does not fit into
        /// the TextField otherwise. Default is <code>true</code>
        /// </summary>
        public bool WordWrap { get; set; }
        
        /// <summary>
        /// Indicates whether the font size is automatically reduced if the complete text does
        /// not fit into the TextField. efault is <code>false</code>
        /// </summary>
        public bool AutoScale { get; set; }
        
        /// <summary>
        /// Specifies the type of auto-sizing set on the TextField.Custom text compositors may
        /// take this into account, though the basic implementation (done by the TextField itself)
        /// is often sufficient: it passes a very big size to the <code>fillMeshBatch</code>
        /// method and then trims the result to the actually used area. Default is TextFieldAutoSize.NONE
        /// </summary>
        public TextFieldAutoSize AutoSize { get; set; }
        
        /// <summary>
        /// The scale factor of any textures that are created during text composition.
        /// Default is SparrowSharp.ContentScaleFactor
        /// </summary>
        public float TextureScale { get; set; }
        
        /// <summary>
        /// The Context3DTextureFormat of any textures that are created during text composition.
        /// Default is TextureFormat.Rgb565
        /// </summary>
        public TextureFormat TextureFormat { get; set; }

        /// <summary>
        /// Creates a new TextOptions instance with the given properties.
        /// </summary>
        public TextOptions(bool wordWrap = true, bool autoScale = false)
        {
            WordWrap = wordWrap;
            AutoScale = autoScale;
            AutoSize = TextFieldAutoSize.NONE;
            TextureScale = SparrowSharp.ContentScaleFactor;
            TextureFormat = TextureFormat.Rgb565; // likely wrong
        }

        /// <summary>
        /// Copies all properties from another TextOptions instance.
        /// </summary>
        public void CopyFrom(TextOptions options)
        {
            WordWrap = options.WordWrap;
            AutoScale = options.AutoScale;
            AutoSize = options.AutoSize;
            TextureScale = options.TextureScale;
            TextureFormat = options.TextureFormat;
        }

        /// <summary>
        /// Creates a clone of this instance.
        /// </summary>
        public TextOptions Clone()
        {
            var clone = new TextOptions();
            clone.CopyFrom(this);
            return clone;
        }

}
}
