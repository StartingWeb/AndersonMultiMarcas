using Core.Storage;
using Xunit;

namespace Project.Tests.Features.Storage;

public sealed class StoragePathTests
{
    [Theory]
    [InlineData("/img/carroDefault.png", "img/carroDefault.png")]
    [InlineData("/img/carroDefault.png?v=1#preview", "img/carroDefault.png")]
    [InlineData("~/img/carroDefault.png", "img/carroDefault.png")]
    [InlineData("wwwroot/img/carroDefault.png", "img/carroDefault.png")]
    [InlineData("/uploads/veiculos/car.png", "uploads/veiculos/car.png")]
    public void RootedWebPathsResolveWithoutUnixFileUriInterpretation(string source, string expected)
    {
        Assert.True(StoragePath.TryGetKeyFromSource(source, [], out var key));
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData("//external.invalid/uploads/photo.png")]
    [InlineData("file:///uploads/veiculos/photo.png")]
    [InlineData("file://external.invalid/uploads/veiculos/photo.png")]
    [InlineData("/uploads/../private.png")]
    [InlineData("/../private.png")]
    [InlineData("/")]
    public void UnsupportedLocationsAndTraversalAreRejected(string source)
    {
        Assert.False(StoragePath.TryGetKeyFromSource(source, [], out var key));
        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void ConfiguredPublicOriginAndLegacyVehicleUrlsStillResolve()
    {
        Assert.True(StoragePath.TryGetKeyFromSource("https://media.example/img/photo.png", ["https://media.example"], out var publicKey));
        Assert.Equal("img/photo.png", publicKey);
        Assert.True(StoragePath.TryGetKeyFromSource("https://legacy.example/anderson-multimarcas/veiculos/2/photo.png", [], out var legacyKey));
        Assert.Equal("anderson-multimarcas/veiculos/2/photo.png", legacyKey);
    }
}
