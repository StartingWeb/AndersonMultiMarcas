using Core.Storage;
using Microsoft.Extensions.Options;
using ImageMagick;
using Project.Infrastructure.Images;

namespace Project.Features.Veiculos.Services;

public sealed class VeiculoMediaService(
    IStorageService storage,
    IOptions<StorageOptions> storageOptions,
    ILogger<VeiculoMediaService>? logger = null) : IVeiculoMediaService
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const string OutputContentType = "image/webp";

    public async Task<IReadOnlyCollection<VeiculoMediaUploadResult>> ProcessarUploadAsync(
        int veiculoId,
        IReadOnlyCollection<IFormFile> arquivos,
        CancellationToken ct)
    {
        // Validate and encode the whole batch before the first storage write.
        // Keep only bounded WebP output; release each decoded image immediately.
        var prepared = new List<(string Key, string FileName, byte[] Bytes)>();
        foreach (var arquivo in arquivos)
        {
            var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) throw new InvalidDataException("Formato de imagem nao suportado.");
            if (arquivo.Length <= 0 || arquivo.Length > 10 * 1024 * 1024)
                throw new InvalidDataException("Arquivo invalido ou muito grande (maximo 10 MB).");

            await using var readStream = arquivo.OpenReadStream();
            using var image = await SafeRasterImage.ReadAsync(readStream, ct);
            if (image.Width > 1920 || image.Height > 1920) image.Resize(new MagickGeometry(1920, 1920));
            var fileName = $"{Guid.NewGuid():N}.webp";
            var key = StoragePath.Combine("uploads", "veiculos", veiculoId.ToString(), fileName);
            await using var output = new MemoryStream();
            image.Quality = 75;
            await image.WriteAsync(output, MagickFormat.WebP, ct);
            prepared.Add((key, fileName, output.ToArray()));
        }

        var resultado = new List<VeiculoMediaUploadResult>();
        var attemptedKeys = new List<string>();
        try
        {
            foreach (var image in prepared)
            {
                attemptedKeys.Add(image.Key);
                await using var content = new MemoryStream(image.Bytes, writable: false);
                var stored = await storage.SaveAsync(image.Key, content, OutputContentType, ct);
                resultado.Add(new VeiculoMediaUploadResult(stored.Url, image.FileName, stored.Key,
                    stored.Container, stored.ContentType, stored.SizeBytes));
            }
            return resultado;
        }
        catch
        {
            // All keys were generated above, so none can represent an existing upload.
            // Include the failing attempt because a backend may accept a write before a timeout.
            foreach (var key in attemptedKeys)
            {
                try { await storage.DeleteAsync(key, CancellationToken.None); }
                catch (Exception error) { logger?.LogWarning(error, "Upload incompleto requer revisao de limpeza: {StorageKey}.", key); }
            }
            throw;
        }
    }

    public Task RemoverArquivoAsync(string? caminhoRelativo, CancellationToken ct)
        => RemoverArquivoAsync(new StorageImageReference(caminhoRelativo), ct);

    public async Task RemoverArquivoAsync(StorageImageReference reference, CancellationToken ct)
    {
        if (StoragePath.TryGetKey(reference, PublicBaseUrls(), out var key))
        {
            await storage.DeleteAsync(key, ct);
        }
    }

    private IEnumerable<string?> PublicBaseUrls()
    {
        yield return storageOptions.Value.PublicBaseUrl;
        yield return storageOptions.Value.R2.PublicBaseUrl;
        yield return storageOptions.Value.R2.ServiceUrl;
    }
}
