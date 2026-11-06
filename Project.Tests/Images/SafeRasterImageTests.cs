using ImageMagick;
using Project.Infrastructure.Images;
using Xunit;

namespace Project.Tests.Images;

public sealed class SafeRasterImageTests
{
    [Theory]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.WebP)]
    public async Task AllowedRasterFormatsDecodeResizeAndEncodeWebp(MagickFormat format)
    {
        using var original = new MagickImage(MagickColors.Red, 2400, 1200);
        await using var input = new MemoryStream(original.ToByteArray(format));
        using var decoded = await SafeRasterImage.ReadAsync(input, default);
        Assert.Equal(2400u, decoded.Width);
        decoded.Resize(new MagickGeometry(1920, 1920));
        await using var result = new MemoryStream();
        await decoded.WriteAsync(result, MagickFormat.WebP);
        result.Position = 0;
        using var roundtrip = await SafeRasterImage.ReadAsync(result, default);
        Assert.Equal(1920u, roundtrip.Width);
        Assert.Equal(960u, roundtrip.Height);
    }

    [Fact]
    public async Task SvgAndNonImageDataAreRejectedBeforeNativeDecode()
    {
        await using var svg = new MemoryStream("<svg xmlns='http://www.w3.org/2000/svg'><image href='https://example.org/private'/></svg>"u8.ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => SafeRasterImage.ReadAsync(svg, default));
    }

    [Fact]
    public async Task DecompressionBombDimensionsAreRejected()
    {
        using var huge = new MagickImage(MagickColors.White, 16001, 1);
        await using var input = new MemoryStream(huge.ToByteArray(MagickFormat.Png));
        await Assert.ThrowsAsync<InvalidDataException>(() => SafeRasterImage.ReadAsync(input, default));
    }

    [Fact]
    public async Task OversizedInputIsRejected()
    {
        await using var input = new MemoryStream(new byte[SafeRasterImage.MaxInputBytes + 1]);
        await Assert.ThrowsAsync<InvalidDataException>(() => SafeRasterImage.ReadAsync(input, default));
    }

    [Fact]
    public async Task OrientationIsAppliedAndMetadataIsRemoved()
    {
        using var original = new MagickImage(MagickColors.Red, 120, 60) { Orientation = OrientationType.RightTop };
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, (ushort)6);
        original.SetProfile(exif);
        await using var input = new MemoryStream(original.ToByteArray(MagickFormat.Jpeg));
        using var decoded = await SafeRasterImage.ReadAsync(input, default);
        Assert.Equal(60u, decoded.Width);
        Assert.Equal(120u, decoded.Height);
        Assert.Empty(decoded.ProfileNames);
    }
}
