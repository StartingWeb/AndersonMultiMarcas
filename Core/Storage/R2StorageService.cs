using System.Net;
using System.Runtime.CompilerServices;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Storage;

public sealed class R2StorageService : IStorageService, IDisposable
{
    private readonly IOptions<StorageOptions> options;
    private readonly ILogger<R2StorageService> logger;
    private readonly Lazy<AmazonS3Client> client;
    private readonly bool ownsClient;

    public R2StorageService(IOptions<StorageOptions> options, ILogger<R2StorageService> logger, AmazonS3Client? clientOverride = null)
    {
        this.options = options;
        this.logger = logger;
        ownsClient = clientOverride is null;
        client = new Lazy<AmazonS3Client>(() => clientOverride ?? CreateClient());
    }

    public bool IsConfigured => options.Value.R2.IsConfigured;

    public async Task<StoredFile> SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        EnsureConfigured();
        var normalizedKey = StoragePath.NormalizeKey(key);
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var request = new PutObjectRequest
        {
            BucketName = BucketName,
            Key = normalizedKey,
            InputStream = content,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            UseChunkEncoding = false,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
            AutoCloseStream = false
        };

        PutObjectResponse response;
        try
        {
            response = await client.Value.PutObjectAsync(request, ct);
        }
        catch (AmazonServiceException ex)
        {
            throw new InvalidOperationException(
                $"Falha ao enviar {normalizedKey} para o R2. Status={(int)ex.StatusCode}; ErrorCode={ex.ErrorCode}; RequestId={ex.RequestId}; Bucket={BucketName}; Endpoint={options.Value.R2.ResolveServiceUrl()}.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Falha ao enviar {normalizedKey} para o R2. Bucket={BucketName}; Tipo={ex.GetType().Name}.", ex);
        }
        if (response.HttpStatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            throw new InvalidOperationException($"Falha ao enviar {normalizedKey} para o R2. Status: {(int)response.HttpStatusCode}.");
        }

        // A successful PUT is authoritative: missing HEAD permission must not make a saved upload look failed.
        var size = content.CanSeek ? content.Length : 0;
        try
        {
            var metadata = await GetMetadataAsync(normalizedKey, ct);
            if (metadata?.SizeBytes is { } confirmedSize) size = confirmedSize;
            else logger.LogWarning("Upload de {StorageKey} aceito pelo R2, sem confirmacao de metadados via HEAD.", normalizedKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Upload de {StorageKey} aceito pelo R2, mas a consulta HEAD falhou.", normalizedKey);
        }

        return new StoredFile(
            normalizedKey,
            GetPublicUrl(normalizedKey),
            StoragePath.GetFileName(normalizedKey),
            BucketName,
            request.ContentType,
            size);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
        => await GetMetadataAsync(key, ct) is not null;

    public async Task<StorageObjectMetadata?> GetMetadataAsync(string key, CancellationToken ct)
    {
        EnsureConfigured();
        var normalizedKey = StoragePath.NormalizeKey(key);

        try
        {
            var response = await client.Value.GetObjectMetadataAsync(BucketName, normalizedKey, ct);
            return new StorageObjectMetadata(
                normalizedKey,
                BucketName,
                response.Headers.ContentType,
                response.ContentLength,
                response.LastModified == default ? null : response.LastModified,
                response.ETag);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (AmazonS3Exception ex) when (string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    public async IAsyncEnumerable<StorageObjectMetadata> ListAsync(
        string prefix,
        [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureConfigured();
        var bucketName = BucketName;
        var normalizedPrefix = NormalizePrefix(prefix);
        string? continuationToken = null;

        do
        {
            var response = await client.Value.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = normalizedPrefix,
                ContinuationToken = continuationToken
            }, ct);

            foreach (var item in response.S3Objects)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                yield return new StorageObjectMetadata(
                    StoragePath.NormalizeKey(item.Key),
                    bucketName,
                    null,
                    item.Size,
                    item.LastModified == default ? null : item.LastModified,
                    item.ETag);
            }

            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (!string.IsNullOrWhiteSpace(continuationToken));
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        EnsureConfigured();
        var normalizedKey = StoragePath.NormalizeKey(key);

        try
        {
            using var response = await client.Value.GetObjectAsync(BucketName, normalizedKey, ct);
            var memory = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memory, ct);
            memory.Position = 0;
            return memory;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (AmazonS3Exception ex) when (string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        EnsureConfigured();
        var normalizedKey = StoragePath.NormalizeKey(key);

        try
        {
            await client.Value.DeleteObjectAsync(BucketName, normalizedKey, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            logger.LogDebug(ex, "Arquivo {StorageKey} nao existia no R2 durante remocao.", normalizedKey);
        }
    }

    public string GetPublicUrl(string key)
    {
        var normalizedKey = StoragePath.NormalizeKey(key);
        var publicBaseUrl = options.Value.R2.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            publicBaseUrl = options.Value.PublicBaseUrl;
        }

        return string.IsNullOrWhiteSpace(publicBaseUrl)
            ? StoragePath.ToPublicPath(normalizedKey)
            : $"{publicBaseUrl.TrimEnd('/')}/{normalizedKey}";
    }

    public void Dispose()
    {
        if (ownsClient && client.IsValueCreated)
        {
            client.Value.Dispose();
        }
    }

    private AmazonS3Client CreateClient()
    {
        EnsureConfigured();
        var r2 = options.Value.R2;
        var credentials = new BasicAWSCredentials(r2.AccessKeyId, r2.SecretAccessKey);
        var config = new AmazonS3Config
        {
            ServiceURL = r2.ResolveServiceUrl(),
            ForcePathStyle = true,
            AuthenticationRegion = string.IsNullOrWhiteSpace(r2.Region) ? "auto" : r2.Region.Trim(),
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        return new AmazonS3Client(credentials, config);
    }

    private string BucketName
    {
        get
        {
            EnsureConfigured();
            return options.Value.R2.BucketName!.Trim();
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Cloudflare R2 nao esta configurado. Verifique Storage:R2.");
        }
    }

    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var normalized = StoragePath.NormalizeKey(prefix);
        return prefix.Trim().Replace('\\', '/').EndsWith("/", StringComparison.Ordinal)
            ? normalized + "/"
            : normalized;
    }
}
