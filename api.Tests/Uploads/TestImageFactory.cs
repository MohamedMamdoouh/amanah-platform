using Amanah.Api.Services.Uploads;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Amanah.Api.Tests.Uploads;

internal static class TestImageFactory
{
    public static byte[] CreateMinimalJpeg()
    {
        using var image = new Image<Rgba32>(2, 2);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }

    public static byte[] CreateMinimalPng()
    {
        using var image = new Image<Rgba32>(2, 2);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    public static byte[] CreateMinimalWebp()
    {
        using var image = new Image<Rgba32>(2, 2);
        using var stream = new MemoryStream();
        image.SaveAsWebp(stream);
        return stream.ToArray();
    }

    public static byte[] CreateMinimalGif()
    {
        return "GIF89a"u8.ToArray();
    }

    public static byte[] CreateOversizedJpeg()
    {
        var bytes = CreateMinimalJpeg();
        var buffer = new byte[ReportImageProcessor.MaxFileSizeBytes + 1];
        Array.Copy(bytes, buffer, bytes.Length);
        return buffer;
    }
}
