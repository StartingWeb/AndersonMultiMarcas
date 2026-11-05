using Core.Storage;
using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Project.Features.Veiculos.Services;
using Project.Tests.Security;
using Xunit;

namespace Project.Tests.Images;

public sealed class VehicleMediaBatchTests
{
    [Fact]
    public async Task InvalidSecondImagePreventsAnyStorageWrite()
    {
        var storage = new FailingStorage();
        var service = new VeiculoMediaService(storage, Options.Create(new StorageOptions()));
        using var validImage = new MagickImage(MagickColors.Red, 2, 2);
        using var valid = new MemoryStream(validImage.ToByteArray(MagickFormat.Png));
        using var invalid = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ProcessarUploadAsync(1,
            [File(valid, "valid.png"), File(invalid, "invalid.png")], default));
        Assert.Equal(0, storage.Attempts);
        Assert.Empty(storage.Removed);
    }

    [Fact]
    public async Task StorageFailureCompensatesOnlyNewBatchKeysIncludingUncertainWrite()
    {
        var storage = new FailingStorage();
        var service = new VeiculoMediaService(storage, Options.Create(new StorageOptions()));
        using var image = new MagickImage(MagickColors.Red, 2, 2);
        using var first = new MemoryStream(image.ToByteArray(MagickFormat.Png));
        using var second = new MemoryStream(image.ToByteArray(MagickFormat.Png));
        await Assert.ThrowsAsync<IOException>(() => service.ProcessarUploadAsync(1, [File(first, "first.png"), File(second, "second.png")], default));
        Assert.Equal(2, storage.Attempts);
        Assert.Equal(storage.Requested, storage.Removed);
        Assert.Equal(2, storage.Removed.Distinct().Count());
        Assert.All(storage.Removed, key => Assert.StartsWith("uploads/veiculos/1/", key));
    }

    private static FormFile File(MemoryStream content, string name) => new(content, 0, content.Length, "arquivos", name)
    { Headers = new HeaderDictionary(), ContentType = "image/png" };

    private sealed class FailingStorage : IStorageService
    {
        public int Attempts { get; private set; }
        public List<string> Requested { get; } = [];
        public List<string> Removed { get; } = [];
        public Task<StoredFile> SaveAsync(string key, Stream stream, string type, CancellationToken ct)
        {
            Attempts++; Requested.Add(key);
            if (Attempts == 2) throw new IOException("Synthetic storage failure after accepting a write.");
            return Task.FromResult(new StoredFile(key, $"https://example.test/{key}", Path.GetFileName(key), "synthetic", type, stream.Length));
        }
        public Task DeleteAsync(string key, CancellationToken ct) { Removed.Add(key); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<StorageObjectMetadata?> GetMetadataAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public IAsyncEnumerable<StorageObjectMetadata> ListAsync(string prefix, CancellationToken ct) => throw new NotSupportedException();
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public string GetPublicUrl(string key) => throw new NotSupportedException();
    }
}
