using System.Collections.Concurrent;
using Core.Storage;

namespace Project.Tests.Security;

public sealed class MemoryStorageService : IStorageService
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string Type)> files = new();
    public async Task<StoredFile> SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        using var bytes = new MemoryStream();
        await content.CopyToAsync(bytes, ct);
        files[key] = (bytes.ToArray(), contentType);
        return new StoredFile(key, GetPublicUrl(key), Path.GetFileName(key), "synthetic", contentType, bytes.Length);
    }
    public Task<bool> ExistsAsync(string key, CancellationToken ct) => Task.FromResult(files.ContainsKey(key));
    public Task<StorageObjectMetadata?> GetMetadataAsync(string key, CancellationToken ct) => Task.FromResult(files.TryGetValue(key, out var value)
        ? new StorageObjectMetadata(key, "synthetic", value.Type, value.Bytes.Length, DateTimeOffset.UtcNow, null) : null);
    public async IAsyncEnumerable<StorageObjectMetadata> ListAsync(string prefix, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var key in files.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)))
            yield return (await GetMetadataAsync(key, ct))!;
    }
    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream?>(files.TryGetValue(key, out var value) ? new MemoryStream(value.Bytes) : null);
    public Task DeleteAsync(string key, CancellationToken ct) { files.TryRemove(key, out _); return Task.CompletedTask; }
    public string GetPublicUrl(string key) => $"https://storage.example.test/{key}";
}
