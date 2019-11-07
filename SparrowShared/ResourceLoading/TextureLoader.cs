using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Sparrow.Textures;

namespace Sparrow.ResourceLoading
{
    public class TextureLoader
    {
        private bool _isLoaded;
        private Texture _glTexture;

        public bool IsLoaded => _isLoaded;

        public Texture Texture => _glTexture;

        public event EventHandler<Texture> ResourceLoaded;

        public TextureLoader LoadRemoteImage(string remoteUrl)
        {
            throw new NotImplementedException();
            _isLoaded = false;
            return this; 
        }

        public Texture LoadLocalImage(string pathToFile)
        {
            _isLoaded = false;
            using (Image<Rgba32> image = Image.Load<Rgba32>(pathToFile))
            {
                GenerateTexture(image.GetPixelSpan().ToArray(), image.Width, image.Height);
            }
            return _glTexture;
        }

        public TextureLoader LoadLocalImageAsync(string pathToFile)
        {
            _isLoaded = false;
            LoadLocalBitmapAsync(pathToFile);
            // + check whether the async call can be executed instantly, 
            // because in that case it will be impossible to catch the event
            return this; 
        }

        public Texture LoadFromStream(Stream stream)
        {
            _isLoaded = false;
            using (Image<Rgba32> image = Image.Load<Rgba32>(stream))
            {
                GenerateTexture(image.GetPixelSpan().ToArray(), image.Width, image.Height);
            }
            return _glTexture;
        }

        private async void LoadLocalBitmapAsync(string path)
        {
            throw new NotImplementedException();
        }

        private void GenerateTexture(Rgba32[] data, int width, int height)
        {
            _isLoaded = false;
            
            TextureOptions opts = new TextureOptions(TextureFormat.Rgba8888);
            // Premultiply alpha
            byte[] pmaData = new byte[data.Length * 4];
            for (int i = 0; i < data.Length; i++)
            {
                var aPixel = data[i];
                float alpha = (float)aPixel.A / 255;
                pmaData[4 * i + 0] = (byte)(aPixel.R * alpha);
                pmaData[4 * i + 1] = (byte)(aPixel.G * alpha);
                pmaData[4 * i + 2] = (byte)(aPixel.B * alpha);
                pmaData[4 * i + 3] = (byte)(alpha * 255);
            }
            
            _glTexture = Texture.FromData(pmaData, opts, width, height);

            _isLoaded = true;
            // Make a temporary copy of the event to avoid possibility of 
            // a race condition if the last subscriber unsubscribes 
            // immediately after the null check and before the event is raised.
            EventHandler<Texture> handler = ResourceLoaded;
            handler?.Invoke(this, _glTexture);
        }
      
    }
}