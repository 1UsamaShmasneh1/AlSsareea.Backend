using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Media.Infrastructure;

public sealed class MediaOptions
{
    public const string SectionName = "Media";
    public string StorageRoot { get; init; } = "App_Data/media";
    public string PublicBasePath { get; init; } = "/api/media/assets";
    public long MaximumUploadBytes { get; init; } = 10 * 1024 * 1024;
    public int MaximumWidth { get; init; } = 8000;
    public int MaximumHeight { get; init; } = 8000;
    public long MaximumPixels { get; init; } = 40_000_000;
    public int ThumbnailSize { get; init; } = 160;
    public int SmallSize { get; init; } = 480;
    public int MediumSize { get; init; } = 960;
    public int LargeSize { get; init; } = 1600;
    public int WebPQuality { get; init; } = 82;
    public int TemporaryRetentionHours { get; init; } = 24;
    public int FailedRetentionDays { get; init; } = 7;
    public int CleanupBatchSize { get; init; } = 100;
}

internal sealed class MediaOptionsValidator : IValidateOptions<MediaOptions>
{
    public ValidateOptionsResult Validate(string? name, MediaOptions value)
    {
        if (string.IsNullOrWhiteSpace(value.StorageRoot) || Path.IsPathRooted(value.StorageRoot)) return ValidateOptionsResult.Fail("Media:StorageRoot must be a relative path.");
        if (!value.PublicBasePath.StartsWith('/')) return ValidateOptionsResult.Fail("Media:PublicBasePath must start with '/'.");
        if (value.MaximumUploadBytes <= 0 || value.MaximumWidth <= 0 || value.MaximumHeight <= 0 || value.MaximumPixels <= 0) return ValidateOptionsResult.Fail("Media validation limits must be positive.");
        if (new[] { value.ThumbnailSize, value.SmallSize, value.MediumSize, value.LargeSize }.Any(x => x <= 0) || value.WebPQuality is < 1 or > 100) return ValidateOptionsResult.Fail("Media variant settings are invalid.");
        if (value.CleanupBatchSize is < 1 or > 1000 || value.TemporaryRetentionHours < 1 || value.FailedRetentionDays < 1) return ValidateOptionsResult.Fail("Media cleanup settings are invalid.");
        return ValidateOptionsResult.Success;
    }
}
