using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ACPAMS.Services
{
    public static class AvatarService
    {
        public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        public const int AvatarDimension = 128; // 128x128 px

        /// <summary>
        /// Reads image file, checks file size, crops to 1:1 center square,
        /// and resizes to 128x128 px encoded as PNG byte array (~15-35KB).
        /// Uses WPF native imaging primitives without System.Drawing dependency.
        /// </summary>
        public static byte[] ProcessAndCropToSquare(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException("Image file not found.", filePath);

            if (fileInfo.Length > MaxFileSizeBytes)
                throw new InvalidOperationException("File size exceeds 5 MB limit.");

            // Load bitmap
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.EndInit();
            bitmap.Freeze();

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            int minEdge = Math.Min(width, height);

            int x = (width - minEdge) / 2;
            int y = (height - minEdge) / 2;

            // 1. Center crop to 1:1
            var cropped = new CroppedBitmap(bitmap, new System.Windows.Int32Rect(x, y, minEdge, minEdge));

            // 2. Downscale to 128x128
            double scaleX = (double)AvatarDimension / minEdge;
            double scaleY = (double)AvatarDimension / minEdge;
            var scaled = new TransformedBitmap(cropped, new ScaleTransform(scaleX, scaleY));

            // 3. Encode to PNG
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(scaled));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Decodes raw byte array to an in-memory frozen BitmapImage for UI binding.
        /// </summary>
        public static BitmapImage? ByteArrayToBitmap(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(bytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}

