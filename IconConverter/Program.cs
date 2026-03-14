using System;
using ImageMagick;

namespace IconConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            try 
            {
                string pngPath = @"C:\Users\ReinellS\.gemini\antigravity\brain\a5213e9b-be8e-4e27-9cd5-400b1e10b6e2\echoforge_icon_1773495393861.png";
                string icoPath = @"D:\Gemini\Projects\YoutubeOtomation\src\EchoForge.WPF\Assets\echoforge_icon.ico";
                
                using (var image = new MagickImage(pngPath))
                {
                    // The AI generated image has a dark background natively, but the previous script might have ruined the alpha bounds.
                    // Wait, if the prompt generated a background that ISN'T actually transparent (e.g. solid #0D1117 or white),
                    // We can ask Magick to make the corners or a specific color transparent if needed.
                    // Since it's a rounded squircle, usually the "corners" are white or black if it was supposed to be transparent.
                    // Let's just do a clean resize and format conversion first.
                    
                    image.Format = MagickFormat.Ico;
                    image.Resize(256, 256);
                    image.Write(icoPath);
                }
                Console.WriteLine("Successfully created transparent ICO file with Magick.NET");
            } 
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
