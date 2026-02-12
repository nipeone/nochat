using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NoChat.App.Assets;

public static class IconHelper
{
    public static Stream CreateAppIconStream()
    {
        const int size = 32;
        using var image = new Image<Rgba32>(size, size);
        var blue = SixLabors.ImageSharp.Color.FromRgb(91, 141, 238);
        image.Mutate(ctx => ctx.BackgroundColor(blue));
        var ms = new MemoryStream();
        image.SaveAsPng(ms);
        ms.Position = 0;
        return ms;
    }
}
