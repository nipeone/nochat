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

    /// <param name="forAlert">为 true 时绘制高亮效果（用于托盘“新消息”闪烁），图标保持在固定位置切换</param>
    public static Stream CreateAppIconStream(bool forAlert = false)
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
            return CreateFallbackIconStream(forAlert);
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
                return CreateFallbackIconStream(forAlert);

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
            if (forAlert)
            {
                canvas.ResetMatrix();
                using var paint = new SKPaint
                {
                    Color = new SKColor(255, 220, 80, 140),
                    IsAntialias = true
                };
                canvas.DrawRoundRect(0, 0, w, h, 12, 12, paint);
            }
            canvas.Flush();

            var ms = new MemoryStream();
            using (var image = surface.Snapshot())
            using (var png = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                png.SaveTo(ms);
            }
            ms.Position = 0;
            return ms;
        }
        catch
        {
            return CreateFallbackIconStream(forAlert);
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

    private static Stream CreateFallbackIconStream(bool forAlert = false)
    {
        var ms = new MemoryStream();
        using (var image = new SKBitmap(IconSize, IconSize, SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            using var canvas = new SKCanvas(image);
            canvas.Clear(new SKColor(101, 230, 245));
            if (forAlert)
            {
                using var paint = new SKPaint { Color = new SKColor(255, 220, 80, 140), IsAntialias = true };
                canvas.DrawRoundRect(0, 0, IconSize, IconSize, 12, 12, paint);
            }
            using var snapshot = SKImage.FromBitmap(image);
            using var png = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            png.SaveTo(ms);
        }
        ms.Position = 0;
        return ms;
    }
}
