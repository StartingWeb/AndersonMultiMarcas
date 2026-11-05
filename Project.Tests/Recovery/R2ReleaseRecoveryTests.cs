using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Project.Tests.Recovery;

public sealed class R2ReleaseRecoveryTests
{
    [Fact]
    public async Task UploadUsesR2CompatibleSigningAndLeavesCallerStreamOpen()
    {
        using var fake = new FakeClient();
        using var service = Create(fake);
        await using var stream = new MemoryStream([1, 2, 3]);
        stream.Position = 2;
        var file = await service.SaveAsync("uploads/veiculos/1/a.jpg", stream, "image/jpeg", default);
        Assert.NotNull(fake.Request);
        Assert.False(fake.Request.UseChunkEncoding);
        Assert.True(fake.Request.DisablePayloadSigning);
        Assert.True(fake.Request.DisableDefaultChecksumValidation);
        Assert.False(fake.Request.AutoCloseStream);
        Assert.Equal(0, fake.PositionAtPut);
        Assert.True(stream.CanRead);
        Assert.Equal(3, file.SizeBytes);
        Assert.Equal("https://cdn.example.test/uploads/veiculos/1/a.jpg", file.Url);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AcceptedPutIsNotReportedFailedWhenHeadIsUnavailable(bool missing)
    {
        using var fake = new FakeClient
        {
            HeadFailure = new AmazonS3Exception("synthetic HEAD failure")
            { StatusCode = missing ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden }
        };
        using var service = Create(fake);
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var file = await service.SaveAsync("uploads/veiculos/1/a.jpg", stream, "image/jpeg", default);
        Assert.Equal(4, file.SizeBytes);
    }

    [Fact]
    public async Task RejectedPutIncludesActionableDiagnosticsAndDoesNotRunHead()
    {
        using var fake = new FakeClient
        {
            PutFailure = new AmazonS3Exception("synthetic failure")
            { StatusCode = HttpStatusCode.Forbidden, ErrorCode = "AccessDenied", RequestId = "synthetic-request-id" }
        };
        using var service = Create(fake);
        await using var stream = new MemoryStream([1]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync("uploads/veiculos/1/a.jpg", stream, "image/jpeg", default));
        Assert.Contains("Status=403", ex.Message);
        Assert.Contains("ErrorCode=AccessDenied", ex.Message);
        Assert.Contains("RequestId=synthetic-request-id", ex.Message);
        Assert.DoesNotContain("fake-secret", ex.Message);
        Assert.False(fake.HeadCalled);
    }

    [Fact]
    public async Task UploadCancellationPropagatesWithoutBecomingSuccessful()
    {
        using var fake = new FakeClient { PutFailure = new OperationCanceledException() };
        using var service = Create(fake);
        await using var stream = new MemoryStream([1]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveAsync("uploads/veiculos/1/a.jpg", stream, "image/jpeg", default));
        Assert.False(fake.HeadCalled);
    }

    private static R2StorageService Create(AmazonS3Client fake) => new(Options.Create(new StorageOptions
    {
        Provider = StorageProviders.R2,
        R2 = new R2StorageOptions
        {
            AccessKeyId = "fake-key", SecretAccessKey = "fake-secret", BucketName = "synthetic-bucket",
            ServiceUrl = "https://r2.example.test", PublicBaseUrl = "https://cdn.example.test"
        }
    }), NullLogger<R2StorageService>.Instance, fake);

    private sealed class FakeClient() : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config
    { ServiceURL = "https://r2.example.test", AuthenticationRegion = "auto" })
    {
        public PutObjectRequest? Request { get; private set; }
        public long PositionAtPut { get; private set; }
        public bool HeadCalled { get; private set; }
        public Exception? PutFailure { get; init; }
        public Exception? HeadFailure { get; init; }

        public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            PositionAtPut = request.InputStream.Position;
            return PutFailure is not null ? Task.FromException<PutObjectResponse>(PutFailure)
                : Task.FromResult(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });
        }

        public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            HeadCalled = true;
            return HeadFailure is not null ? Task.FromException<GetObjectMetadataResponse>(HeadFailure)
                : Task.FromResult(new GetObjectMetadataResponse { ContentLength = 3 });
        }
    }
}
