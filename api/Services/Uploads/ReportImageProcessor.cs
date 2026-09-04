using Amanah.Contracts.Errors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Amanah.Api.Services.Uploads;

public sealed class ReportImageProcessor
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private const int MaxThumbnailEdgePixels = 400;

    public ProcessedReportImage Process(Stream input, long length, string? declaredContentType)
    {
        if (length == 0)
        {
            throw new ReportImageProcessingException(ErrorCodes.UploadInvalidFormat, "Photo file is empty.");
        }

        if (length > MaxFileSizeBytes)
        {
            throw new ReportImageProcessingException(ErrorCodes.UploadTooLarge, "Photo must be 5 MB or smaller.");
        }

        using var buffer = new MemoryStream((int)length);
        input.CopyTo(buffer);
        return Process(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), declaredContentType);
    }

    public ProcessedReportImage Process(ReadOnlySpan<byte> input, string? declaredContentType)
    {
        if (input.Length == 0)
        {
            throw new ReportImageProcessingException(ErrorCodes.UploadInvalidFormat, "Photo file is empty.");
        }

        if (input.Length > MaxFileSizeBytes)
        {
            throw new ReportImageProcessingException(ErrorCodes.UploadTooLarge, "Photo must be 5 MB or smaller.");
        }

        if (!TryDetectContentType(input, out var detectedContentType))
        {
            throw new ReportImageProcessingException(
                ErrorCodes.UploadInvalidFormat,
                "Photo must be JPEG, PNG, or WebP.");
        }

        if (declaredContentType is not null
            && !string.Equals(detectedContentType, declaredContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ReportImageProcessingException(
                ErrorCodes.UploadInvalidFormat,
                "Photo must be JPEG, PNG, or WebP.");
        }

        using var image = Image.Load(input);

        var originalStream = new MemoryStream();
        SaveWithoutMetadata(image, detectedContentType, originalStream);

        var thumbnailStream = new MemoryStream();
        using var thumbnail = image.Clone(context => context.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(MaxThumbnailEdgePixels, MaxThumbnailEdgePixels),
        }));

        thumbnail.SaveAsWebp(thumbnailStream, new WebpEncoder
        {
            Quality = 75,
        });

        originalStream.Position = 0;
        thumbnailStream.Position = 0;

        return new ProcessedReportImage(
            originalStream,
            detectedContentType,
            originalStream.Length,
            thumbnailStream);
    }

    private static bool TryDetectContentType(ReadOnlySpan<byte> input, out string contentType)
    {
        if (input.Length >= 3
            && input[0] == 0xFF
            && input[1] == 0xD8
            && input[2] == 0xFF)
        {
            contentType = "image/jpeg";
            return true;
        }

        if (input.Length >= 8
            && input[0] == 0x89
            && input[1] == 0x50
            && input[2] == 0x4E
            && input[3] == 0x47
            && input[4] == 0x0D
            && input[5] == 0x0A
            && input[6] == 0x1A
            && input[7] == 0x0A)
        {
            contentType = "image/png";
            return true;
        }

        if (input.Length >= 12
            && input[0] == 0x52
            && input[1] == 0x49
            && input[2] == 0x46
            && input[3] == 0x46
            && input[8] == 0x57
            && input[9] == 0x45
            && input[10] == 0x42
            && input[11] == 0x50)
        {
            contentType = "image/webp";
            return true;
        }

        contentType = string.Empty;
        return false;
    }

    private static void SaveWithoutMetadata(Image image, string contentType, Stream destination)
    {
        switch (contentType)
        {
            case "image/jpeg":
                image.SaveAsJpeg(destination, new JpegEncoder
                {
                    Quality = 85,
                });
                break;
            case "image/png":
                image.SaveAsPng(destination, new PngEncoder());
                break;
            case "image/webp":
                image.SaveAsWebp(destination, new WebpEncoder
                {
                    Quality = 85,
                });
                break;
            default:
                throw new ReportImageProcessingException(
                    ErrorCodes.UploadInvalidFormat,
                    "Photo must be JPEG, PNG, or WebP.");
        }
    }
}

public sealed record ProcessedReportImage(
    Stream OriginalStream,
    string ContentType,
    long SizeBytes,
    Stream ThumbnailStream);

public sealed class ReportImageProcessingException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
