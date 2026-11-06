using ImageMagick;

namespace Project.Infrastructure.Images;

/// <summary>Decodes only bounded JPEG, PNG and WebP raster data; strips private metadata on output.</summary>
public static class SafeRasterImage
{
    public const int MaxInputBytes = 20 * 1024 * 1024;
    public const long MaxPixels = 40_000_000;

    public static async Task<MagickImage> ReadAsync(Stream input, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + count > MaxInputBytes)
                throw new InvalidDataException("Imagem excede o limite de 20 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var format = DetectFormat(bytes);
        var settings = new MagickReadSettings { Format = format, FrameCount = 1 };
        buffer.Position = 0;
        var image = new MagickImage();
        try
        {
            await image.PingAsync(buffer, settings, ct);
            if (image.Width == 0 || image.Height == 0 || image.Width > 16000 || image.Height > 16000
                || (long)image.Width * image.Height > MaxPixels)
                throw new InvalidDataException("Dimensoes da imagem excedem o limite permitido.");
            buffer.Position = 0;
            await image.ReadAsync(buffer, settings, ct);
            image.AutoOrient();
            image.Strip();
            return image;
        }
        catch { image.Dispose(); throw; }
    }

    private static MagickFormat DetectFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return MagickFormat.Jpeg;
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
            return MagickFormat.Png;
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8,4).SequenceEqual("WEBP"u8))
            return MagickFormat.WebP;
        throw new InvalidDataException("Somente imagens JPEG, PNG e WebP sao permitidas.");
    }
}
