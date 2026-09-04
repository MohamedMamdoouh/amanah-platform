using Amanah.Api.Services.Uploads;
using Amanah.Contracts.Errors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Amanah.Api.Tests.Uploads;
public class ReportImageProcessorTests
{
    private readonly ReportImageProcessor _processor = new();

    [Fact]
    public void Process_accepts_jpeg_and_strips_to_output()
    {
        var input = TestImageFactory.CreateMinimalJpeg();

        var processed = _processor.Process(input, "image/jpeg");

        try
        {
            Assert.True(processed.SizeBytes > 0);
            Assert.Equal("image/jpeg", processed.ContentType);
            Assert.True(processed.ThumbnailStream.Length > 0);
        }
        finally
        {
            processed.OriginalStream.Dispose();
            processed.ThumbnailStream.Dispose();
        }
    }

    [Fact]
    public void Process_accepts_png()
    {
        var input = TestImageFactory.CreateMinimalPng();

        var processed = _processor.Process(input, "image/png");

        try
        {
            Assert.Equal("image/png", processed.ContentType);
        }
        finally
        {
            processed.OriginalStream.Dispose();
            processed.ThumbnailStream.Dispose();
        }
    }

    [Fact]
    public void Process_rejects_oversized_file()
    {
        var input = TestImageFactory.CreateOversizedJpeg();

        var exception = Assert.Throws<ReportImageProcessingException>(() =>
            _processor.Process(input, "image/jpeg"));

        Assert.Equal(ErrorCodes.UploadTooLarge, exception.Code);
    }

    [Fact]
    public void Process_rejects_invalid_format()
    {
        var exception = Assert.Throws<ReportImageProcessingException>(() =>
            _processor.Process("not-an-image"u8.ToArray(), "image/jpeg"));

        Assert.Equal(ErrorCodes.UploadInvalidFormat, exception.Code);
    }

    [Fact]
    public void Process_accepts_webp()
    {
        var input = TestImageFactory.CreateMinimalWebp();

        var processed = _processor.Process(input, "image/webp");

        try
        {
            Assert.Equal("image/webp", processed.ContentType);
        }
        finally
        {
            processed.OriginalStream.Dispose();
            processed.ThumbnailStream.Dispose();
        }
    }

    [Fact]
    public void Process_rejects_gif()
    {
        var exception = Assert.Throws<ReportImageProcessingException>(() =>
            _processor.Process(TestImageFactory.CreateMinimalGif(), "image/gif"));

        Assert.Equal(ErrorCodes.UploadInvalidFormat, exception.Code);
    }

    [Fact]
    public void Process_caps_thumbnail_edge_at_400_pixels()
    {
        using var image = new Image<Rgba32>(800, 600);
        using var inputStream = new MemoryStream();
        image.SaveAsJpeg(inputStream);
        var input = inputStream.ToArray();

        var processed = _processor.Process(input, "image/jpeg");

        try
        {
            using var thumbnail = Image.Load(processed.ThumbnailStream);
            Assert.InRange(Math.Max(thumbnail.Width, thumbnail.Height), 1, 400);
        }
        finally
        {
            processed.OriginalStream.Dispose();
            processed.ThumbnailStream.Dispose();
        }
    }
}
