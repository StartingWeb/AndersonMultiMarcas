using System.Text.Json;
using Project.Shared;
using Xunit;

namespace Project.Tests.Shared;

public sealed class SeoJsonLdTests
{
    [Fact]
    public void VehicleUsesSchemaKeysAndEscapesUntrustedContent()
    {
        const string name = "Carro \"especial\" </script><script>alert(1)</script>";
        var json = SeoJsonLd.Vehicle(name, "Marca \"A\"", 2026, "Prata", "Flex", "Manual", ["https://example.test/image.jpg"], 59990.50m, "https://example.test/veiculo/1/");
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal("https://schema.org", root.GetProperty("@context").GetString());
        Assert.Equal("Vehicle", root.GetProperty("@type").GetString());
        Assert.Equal("Brand", root.GetProperty("brand").GetProperty("@type").GetString());
        Assert.Equal("Offer", root.GetProperty("offers").GetProperty("@type").GetString());
        Assert.Equal(59990.50m, root.GetProperty("offers").GetProperty("price").GetDecimal());
        Assert.Equal(name, root.GetProperty("name").GetString());
        Assert.DoesNotContain("</script>", json, StringComparison.OrdinalIgnoreCase);
        Assert.False(root.TryGetProperty("type", out _));
    }

    [Fact]
    public void ItemListUsesSchemaKeysAndStablePositions()
    {
        using var document = JsonDocument.Parse(SeoJsonLd.ItemList(2, [(10, "A"), (20, "B")], "https://example.test/"));
        Assert.Equal("ItemList", document.RootElement.GetProperty("@type").GetString());
        var items = document.RootElement.GetProperty("itemListElement");
        Assert.Equal("ListItem", items[0].GetProperty("@type").GetString());
        Assert.Equal(2, items[1].GetProperty("position").GetInt32());
        Assert.Equal("https://example.test/veiculo/20/", items[1].GetProperty("url").GetString());
    }
}
