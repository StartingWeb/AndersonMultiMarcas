using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Project.Tests.Features.Storage;

/// <summary>In-memory S3 boundary. No real endpoint, credential or bucket is contacted.</summary>
public sealed class R2IsolationContractTests
{
    [Theory]
    [InlineData("uploads/veiculos/1/ação teste.webp")]
    [InlineData("uploads/veiculos/1/a.webp")]
    public async Task UploadReadOverwriteAndDeletePreserveBytes(string key)
    {
        using var client = new MemoryS3(); using var service = Create(client);
        await using var first = new MemoryStream([1, 2, 3]);
        var saved = await service.SaveAsync(key, first, "image/webp", default);
        Assert.Equal(key, saved.Key);
        Assert.Equal(3, (await service.GetMetadataAsync(key, default))!.SizeBytes);
        await using (var read = await service.OpenReadAsync(key, default))
        {
            Assert.NotNull(read); using var data = new MemoryStream(); await read.CopyToAsync(data);
            Assert.Equal(new byte[] { 1, 2, 3 }, data.ToArray());
        }
        await using var replacement = new MemoryStream([4, 5]);
        await service.SaveAsync(key, replacement, "image/webp", default);
        Assert.Equal(new byte[] { 4, 5 }, client.Objects[key]);
        await service.DeleteAsync(key, default);
        Assert.False(await service.ExistsAsync(key, default));
        Assert.Null(await service.OpenReadAsync(key, default));
        await service.DeleteAsync(key, default); // Missing synthetic object is safe.
    }

    [Fact]
    public async Task MaximumAcceptedMediaSizeSurvivesIntegrityRoundTrip()
    {
        using var client = new MemoryS3(); using var service = Create(client);
        var bytes = new byte[10 * 1024 * 1024]; RandomNumberGenerator.Fill(bytes);
        await using var content = new MemoryStream(bytes);
        await service.SaveAsync("uploads/veiculos/1/large.webp", content, "image/webp", default);
        await using var read = await service.OpenReadAsync("uploads/veiculos/1/large.webp", default);
        Assert.Equal(SHA256.HashData(bytes), await SHA256.HashDataAsync(read!));
    }

    [Fact]
    public async Task DistinctGeneratedKeysDoNotCollideForTheSameOriginalFileName()
    {
        using var client = new MemoryS3(); using var service = Create(client);
        foreach (var id in new[] { "generated-a", "generated-b" })
        {
            await using var content = new MemoryStream([1]);
            await service.SaveAsync("uploads/veiculos/1/" + id + ".webp", content, "image/webp", default);
        }
        Assert.Equal(2, client.Objects.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task CredentialOrTemporaryFailureDoesNotInventASuccessfulUpload(HttpStatusCode status)
    {
        using var client = new MemoryS3 { Failure = new AmazonS3Exception("synthetic") { StatusCode = status } };
        using var service = Create(client); await using var content = new MemoryStream([1]);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync("uploads/test.webp", content, "image/webp", default));
        Assert.DoesNotContain("synthetic-secret", error.Message); Assert.Empty(client.Objects);
    }

    [Fact]
    public async Task TimeoutAndCancellationDoNotBecomeSuccessfulWrites()
    {
        using var client = new MemoryS3 { Failure = new TimeoutException("synthetic") };
        using var service = Create(client); await using var content = new MemoryStream([1]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync("uploads/test.webp", content, "image/webp", default));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        client.Failure = null;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveAsync("uploads/test.webp", content, "image/webp", cancelled.Token));
        Assert.Empty(client.Objects);
    }

    [Fact]
    public async Task RejectedDeleteLeavesTheObjectRecoverable()
    {
        using var client = new MemoryS3(); using var service = Create(client);
        await using var content = new MemoryStream([7]);
        await service.SaveAsync("uploads/test.webp", content, "image/webp", default);
        client.Failure = new AmazonS3Exception("synthetic") { StatusCode = HttpStatusCode.Forbidden };
        await Assert.ThrowsAsync<AmazonS3Exception>(() => service.DeleteAsync("uploads/test.webp", default));
        Assert.Single(client.Objects);
    }

    private static R2StorageService Create(MemoryS3 client) => new(Options.Create(new StorageOptions
    {
        Provider = StorageProviders.R2,
        R2 = new R2StorageOptions { ServiceUrl = "https://isolated.invalid", BucketName = "test-only",
            AccessKeyId = "synthetic-key", SecretAccessKey = "synthetic-secret", PublicBaseUrl = "https://cdn.invalid" }
    }), NullLogger<R2StorageService>.Instance, client);

    private sealed class MemoryS3() : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config
        { ServiceURL = "https://isolated.invalid", AuthenticationRegion = "auto" })
    {
        public Dictionary<string, byte[]> Objects { get; } = [];
        public Exception? Failure { get; set; }
        private void Check(CancellationToken token) { token.ThrowIfCancellationRequested(); if (Failure is not null) throw Failure; }
        public override async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
        {
            Check(cancellationToken); using var data = new MemoryStream();
            await request.InputStream.CopyToAsync(data, cancellationToken); Objects[request.Key] = data.ToArray();
            return new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK };
        }
        public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            Check(cancellationToken);
            if (!Objects.TryGetValue(key, out var bytes)) throw new AmazonS3Exception("synthetic missing") { StatusCode = HttpStatusCode.NotFound };
            return Task.FromResult(new GetObjectMetadataResponse { ContentLength = bytes.Length });
        }
        public override Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            Check(cancellationToken);
            if (!Objects.TryGetValue(key, out var bytes)) throw new AmazonS3Exception("synthetic missing") { StatusCode = HttpStatusCode.NotFound };
            return Task.FromResult(new GetObjectResponse { HttpStatusCode = HttpStatusCode.OK, ResponseStream = new MemoryStream(bytes) });
        }
        public override Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            Check(cancellationToken); Objects.Remove(key);
            return Task.FromResult(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });
        }
    }
}
