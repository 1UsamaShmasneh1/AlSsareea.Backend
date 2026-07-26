using AlSsareea.Modules.Media.Application;
using AlSsareea.Modules.Media.Domain;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Media.Infrastructure;

internal sealed class LocalMediaStorage(IOptions<MediaOptions> options) : IMediaStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.StorageRoot, AppContext.BaseDirectory);
    public string CreateKey(MediaAssetId id, string fileName) => $"media/{DateTime.UtcNow:yyyy/MM}/{id.Value:N}/original{Path.GetExtension(fileName).ToLowerInvariant()}";
    public string CreateVariantKey(MediaAssetId id, MediaVariantType type, string extension) => $"media/{DateTime.UtcNow:yyyy/MM}/{id.Value:N}/{type.ToString().ToLowerInvariant()}{extension}";
    public async Task WriteAsync(string key, Stream content, CancellationToken ct)
    {
        string path = Resolve(key); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            { if (content.CanSeek) content.Position = 0; await content.CopyToAsync(output, ct); await output.FlushAsync(ct); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public Task<Stream?> ReadAsync(string key, CancellationToken ct)
    {
        string path = Resolve(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan) : null);
    }
    public Task<bool> ExistsAsync(string key, CancellationToken ct) => Task.FromResult(File.Exists(Resolve(key)));
    public Task DeleteAsync(string key, CancellationToken ct) { string path = Resolve(key); if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }
    private string Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.IsPathRooted(key) || key.Contains("..", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe media storage key.");
        string path = Path.GetFullPath(key.Replace('/', Path.DirectorySeparatorChar), _root);
        string prefix = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path : throw new InvalidOperationException("Media key escapes storage root.");
    }
}
