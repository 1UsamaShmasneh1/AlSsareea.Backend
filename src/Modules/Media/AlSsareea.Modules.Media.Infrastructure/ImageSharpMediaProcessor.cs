using System.Security.Cryptography;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Media.Application;
using AlSsareea.Modules.Media.Domain;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace AlSsareea.Modules.Media.Infrastructure;

public sealed class ImageSharpMediaProcessor(IOptions<MediaOptions> options) : IMediaImageProcessor
{
    private static readonly string[] DangerousExtensions = [".exe", ".js", ".html", ".svg", ".php", ".cmd", ".bat"];
    private static readonly Dictionary<string, (string Mime, byte[][] Signatures)> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".jpeg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".png"] = ("image/png", [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]]),
        [".webp"] = ("image/webp", [[0x52, 0x49, 0x46, 0x46]]),
    };
    private readonly MediaOptions _options = options.Value;
    public async Task<ValidatedImage> ValidateAsync(MediaUploadRequest request, CancellationToken cancellationToken)
    {
        string name = request.OriginalFileName;
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0) throw new DomainException("Invalid file name.");
        string extension = Path.GetExtension(name).ToLowerInvariant();
        if (!Formats.TryGetValue(extension, out var format) || !string.Equals(format.Mime, request.DeclaredMimeType, StringComparison.OrdinalIgnoreCase)) throw new DomainException("File type is not allowed.");
        string stem = Path.GetFileNameWithoutExtension(name);
        if (DangerousExtensions.Any(x => stem.EndsWith(x, StringComparison.OrdinalIgnoreCase))) throw new DomainException("Double file extensions are not allowed.");
        if (request.DeclaredLength is <= 0 || request.DeclaredLength > _options.MaximumUploadBytes) throw new DomainException("File size is invalid.");
        var buffer = new MemoryStream((int)Math.Min(request.DeclaredLength, _options.MaximumUploadBytes));
        await request.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length is 0 || buffer.Length > _options.MaximumUploadBytes || buffer.Length != request.DeclaredLength) { await buffer.DisposeAsync(); throw new DomainException("File size is invalid."); }
        byte[] bytes = buffer.GetBuffer();
        bool signature = format.Signatures.Any(s => buffer.Length >= s.Length && s.SequenceEqual(bytes.AsSpan(0, s.Length).ToArray()));
        if (extension == ".webp") signature = signature && buffer.Length >= 12 && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        if (!signature) { await buffer.DisposeAsync(); throw new DomainException("File signature does not match the declared type."); }
        buffer.Position = 0;
        try
        {
            using Image image = await Image.LoadAsync(buffer, cancellationToken);
            long pixels = checked((long)image.Width * image.Height);
            if (image.Width > _options.MaximumWidth || image.Height > _options.MaximumHeight || pixels > _options.MaximumPixels) throw new DomainException("Image dimensions exceed configured limits.");
            buffer.Position = 0; string hash = Convert.ToHexString(await SHA256.HashDataAsync(buffer, cancellationToken)).ToLowerInvariant(); buffer.Position = 0;
            return new ValidatedImage(name, format.Mime, extension, buffer.Length, hash, image.Width, image.Height, buffer);
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException or ImageFormatException) { await buffer.DisposeAsync(); throw new DomainException("Image payload cannot be decoded."); }
        catch { await buffer.DisposeAsync(); throw; }
    }
    public async Task<IReadOnlyList<ProcessedMediaVariant>> CreateVariantsAsync(ValidatedImage image, CancellationToken cancellationToken)
    {
        if (image.Content.CanSeek) image.Content.Position = 0;
        using Image source = await Image.LoadAsync(image.Content, cancellationToken);
        source.Mutate(x => x.AutoOrient()); source.Metadata.ExifProfile = null; source.Metadata.IccProfile = null; source.Metadata.XmpProfile = null;
        (MediaVariantType Type, int Size)[] sizes = [(MediaVariantType.Thumbnail, _options.ThumbnailSize), (MediaVariantType.Small, _options.SmallSize), (MediaVariantType.Medium, _options.MediumSize), (MediaVariantType.Large, _options.LargeSize)];
        var output = new List<ProcessedMediaVariant>();
        foreach ((MediaVariantType type, int size) in sizes)
        {
            if (Math.Max(source.Width, source.Height) < size && type != MediaVariantType.Thumbnail) continue;
            using Image variant = source.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(Math.Min(size, source.Width), Math.Min(size, source.Height)) }));
            var stream = new MemoryStream(); await variant.SaveAsWebpAsync(stream, new WebpEncoder { Quality = _options.WebPQuality }, cancellationToken); stream.Position = 0;
            output.Add(new(type, "image/webp", variant.Width, variant.Height, stream));
        }
        return output;
    }
}

internal sealed class NoOpMediaMalwareScanner : IMediaMalwareScanner
{
    public Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken) => Task.FromResult(MalwareScanResult.Safe);
}
