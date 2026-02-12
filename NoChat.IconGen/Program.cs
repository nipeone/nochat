using System;
using System.IO;
using SkiaSharp;
using Svg.Skia;

// 构建时由 NoChat.App 调用，从 logo.svg 生成 icon.ico。用法: NoChat.IconGen.exe <logo.svg路径> <icon.ico路径>
if (args.Length < 2)
{
    Console.Error.WriteLine("用法: NoChat.IconGen <logo.svg路径> <icon.ico路径>");
    Environment.Exit(1);
}

var svgPath = args[0];
var icoPath = args[1];

if (!File.Exists(svgPath))
{
    Console.Error.WriteLine("找不到: " + svgPath);
    Environment.Exit(2);
}

const int size = 256;
byte[]? pngBytes = null;

try
{
    using var skSvg = new SKSvg();
    using (var fs = File.OpenRead(svgPath))
        skSvg.Load(fs);
    var bounds = skSvg.Picture?.CullRect ?? default;
    var w = bounds.Width;
    var h = bounds.Height;
    if (w <= 0 || h <= 0)
    {
        Console.Error.WriteLine("SVG 尺寸无效");
        Environment.Exit(3);
    }
    var scale = Math.Min((float)size / w, (float)size / h);
    var iw = (int)(w * scale);
    var ih = (int)(h * scale);
    if (iw <= 0) iw = size;
    if (ih <= 0) ih = size;

    var imageInfo = new SKImageInfo(iw, ih, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var surface = SKSurface.Create(imageInfo);
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    canvas.Scale((float)scale);
    canvas.DrawPicture(skSvg.Picture);
    canvas.Flush();

    using var ms = new MemoryStream();
    using (var image = surface.Snapshot())
    using (var png = image.Encode(SKEncodedImageFormat.Png, 100))
        png.SaveTo(ms);
    pngBytes = ms.ToArray();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.Exit(4);
}

if (pngBytes is null || pngBytes.Length == 0)
{
    Console.Error.WriteLine("未能生成 PNG");
    Environment.Exit(5);
}

var dir = Path.GetDirectoryName(icoPath);
if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    Directory.CreateDirectory(dir);

using (var fs = File.Create(icoPath))
using (var bw = new BinaryWriter(fs))
{
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
    bw.Write((uint)22);    // offset of image data
    bw.Write(pngBytes);
}

Console.WriteLine("已生成: " + icoPath);
