using System;
using System.IO;
using System.Reflection;
using SkiaSharp;
using Svg.Skia;

namespace NoChat.App.Assets;

public static class IconHelper
{
    private const string LogoResourceName = "NoChat.App.logo.svg";
    private const int IconSize = 256;

    /// <param name="opacity">0~1，小于 1 时整体半透明（用于托盘闪烁：不透明/透明切换）</param>
    public static Stream CreateAppIconStream(float opacity = 1f)
    {
        Stream? svgStream = null;
        var assembly = Assembly.GetExecutingAssembly();
        svgStream = assembly.GetManifestResourceStream(LogoResourceName);
        if (svgStream == null)
        {
            var baseDir = AppContext.BaseDirectory;
            var logoPath = Path.Combine(baseDir, "logo.svg");
            if (File.Exists(logoPath))
                svgStream = File.OpenRead(logoPath);
        }
        if (svgStream == null)
            return CreateFallbackIconStream(opacity);
        using (svgStream)
        {
        try
        {
            using var skSvg = new SKSvg();
            skSvg.Load(svgStream);
            var bounds = skSvg.Picture?.CullRect ?? default;
            var width = bounds.Width;
            var height = bounds.Height;
            if (width <= 0 || height <= 0)
                return CreateFallbackIconStream(opacity);

            var scale = Math.Min((float)IconSize / width, (float)IconSize / height);
            var w = (int)(width * scale);
            var h = (int)(height * scale);
            if (w <= 0) w = IconSize;
            if (h <= 0) h = IconSize;

            var imageInfo = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(imageInfo);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)scale);
            canvas.DrawPicture(skSvg.Picture);
            canvas.Flush();

            if (opacity >= 1f)
            {
                var ms = new MemoryStream();
                using (var image = surface.Snapshot())
                using (var png = image.Encode(SKEncodedImageFormat.Png, 100))
                    png.SaveTo(ms);
                ms.Position = 0;
                return ms;
            }
            // 整体透明度：再绘到新 surface 并乘 alpha
            var alpha = (byte)(255 * Math.Clamp(opacity, 0f, 1f));
            using var surface2 = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            var c2 = surface2.Canvas;
            c2.Clear(SKColors.Transparent);
            using (var img = surface.Snapshot())
            using (var layerPaint = new SKPaint { Color = new SKColor(255, 255, 255, alpha) })
            {
                c2.SaveLayer(layerPaint);
                c2.DrawImage(img, 0, 0);
                c2.Restore();
            }
            c2.Flush();
            var ms2 = new MemoryStream();
            using (var image = surface2.Snapshot())
            using (var png = image.Encode(SKEncodedImageFormat.Png, 100))
                png.SaveTo(ms2);
            ms2.Position = 0;
            return ms2;
        }
        catch
        {
            return CreateFallbackIconStream(opacity);
        }
    }
    }

    /// <summary>
    /// 将 logo 生成为 .ico 并写入指定路径，用于 exe 的 ApplicationIcon。
    /// </summary>
    public static void WriteIconToFile(string icoPath)
    {
        using var stream = CreateAppIconStream();
        var pngBytes = new byte[stream.Length];
        stream.Read(pngBytes, 0, pngBytes.Length);
        var dir = Path.GetDirectoryName(icoPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        using var fs = File.Create(icoPath);
        var bw = new BinaryWriter(fs);
        bw.Write((ushort)0);   // reserved
        bw.Write((ushort)1);   // type = ICO
        bw.Write((ushort)1);   // count
        bw.Write((byte)0);     // width 0 = 256
        bw.Write((byte)0);     // height 0 = 256
        bw.Write((byte)0);     // colors
        bw.Write((byte)0);     // reserved
        bw.Write((ushort)1);   // planes
        bw.Write((ushort)32);  // bpp
        bw.Write((uint)pngBytes.Length);
        bw.Write((uint)22);   // offset of image data
        bw.Write(pngBytes);
        bw.Flush();
    }

    private static Stream CreateFallbackIconStream(float opacity = 1f)
    {
        var ms = new MemoryStream();
        using (var image = new SKBitmap(IconSize, IconSize, SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            using var canvas = new SKCanvas(image);
            canvas.Clear(new SKColor(101, 230, 245));
            using var snapshot = SKImage.FromBitmap(image);
            if (opacity >= 1f)
            {
                using var png = snapshot.Encode(SKEncodedImageFormat.Png, 100);
                png.SaveTo(ms);
            }
            else
            {
                using var surface = SKSurface.Create(new SKImageInfo(IconSize, IconSize, SKColorType.Rgba8888, SKAlphaType.Premul));
                surface.Canvas.Clear(SKColors.Transparent);
                var a = (byte)(255 * Math.Clamp(opacity, 0f, 1f));
                using var paint = new SKPaint { Color = new SKColor(255, 255, 255, a) };
                surface.Canvas.DrawImage(snapshot, 0, 0, paint);
                using var img = surface.Snapshot();
                using var png = img.Encode(SKEncodedImageFormat.Png, 100);
                png.SaveTo(ms);
            }
        }
        ms.Position = 0;
        return ms;
    }
}
