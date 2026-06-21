using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PneumaticCalibratorSimHub
{
    internal static class LogoLoader
    {
        private static ImageSource _icon;

        public static ImageSource Icon
        {
            get
            {
                if (_icon == null) _icon = Load();
                return _icon;
            }
        }

        private static ImageSource Load()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream("PneumaticCalibratorSimHub.Assets.logo.png"))
                {
                    if (stream == null) return null;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch { return null; }
        }
    }
}
