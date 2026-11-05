using System.Text.Json;

namespace Project.Shared;

public static class SeoJsonLd
{
    public const string BrandName = "Anderson Multimarcas";
    public const string SiteDescription = "Veiculos seminovos, 0 km, hibridos, eletricos e motos eletricas com atendimento consultivo em Taquaritinga/SP.";
    public const string City = "Taquaritinga";
    public const string Region = "SP";
    public const string Country = "BR";
    public const string Email = "leads@andersonmultimarcas.com.br";
    public const string InstagramUrl = "https://www.instagram.com/andersonmultimarcastq/";
    public const string FacebookUrl = "https://www.facebook.com/AndersonMultimarcasTaqua";

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Organization(string baseUrl, string logoUrl) => Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "Organization",
        ["@id"] = $"{baseUrl}/#organization",
        ["name"] = BrandName,
        ["url"] = baseUrl,
        ["logo"] = logoUrl,
        ["email"] = Email,
        ["sameAs"] = new[] { InstagramUrl, FacebookUrl }
    });

    public static string LocalBusiness(string baseUrl, string imageUrl) => Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "AutoDealer",
        ["@id"] = $"{baseUrl}/#localbusiness",
        ["name"] = BrandName,
        ["url"] = baseUrl,
        ["image"] = imageUrl,
        ["email"] = Email,
        ["priceRange"] = "$$",
        ["areaServed"] = new object[]
        {
            new Dictionary<string, object?> { ["@type"] = "City", ["name"] = City },
            new Dictionary<string, object?> { ["@type"] = "AdministrativeArea", ["name"] = "Interior de Sao Paulo" }
        },
        ["address"] = new Dictionary<string, object?>
        {
            ["@type"] = "PostalAddress",
            ["addressLocality"] = City,
            ["addressRegion"] = Region,
            ["addressCountry"] = Country
        },
        ["sameAs"] = new[] { InstagramUrl, FacebookUrl }
    });

    public static string WebSite(string baseUrl) => Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "WebSite",
        ["@id"] = $"{baseUrl}/#website",
        ["url"] = baseUrl,
        ["name"] = BrandName,
        ["description"] = SiteDescription,
        ["publisher"] = new Dictionary<string, object?> { ["@id"] = $"{baseUrl}/#organization" },
        ["potentialAction"] = new Dictionary<string, object?>
        {
            ["@type"] = "SearchAction",
            ["target"] = $"{baseUrl}/veiculos?busca={{search_term_string}}",
            ["query-input"] = "required name=search_term_string"
        }
    });

    public static string Breadcrumb(string baseUrl, params (string Name, string Url)[] items) => Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "BreadcrumbList",
        ["itemListElement"] = items.Select((item, index) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = index + 1,
            ["name"] = item.Name,
            ["item"] = item.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? item.Url : $"{baseUrl}{item.Url}"
        }).ToArray()
    });

    public static string Faq(params (string Question, string Answer)[] items) => Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "FAQPage",
        ["mainEntity"] = items.Select(item => new Dictionary<string, object?>
        {
            ["@type"] = "Question",
            ["name"] = item.Question,
            ["acceptedAnswer"] = new Dictionary<string, object?>
            {
                ["@type"] = "Answer",
                ["text"] = item.Answer
            }
        }).ToArray()
    });

    public static string ItemList(int total, IEnumerable<(int Id, string Name)> vehicles, string baseUrl) => Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org", ["@type"] = "ItemList", ["numberOfItems"] = total,
        ["itemListElement"] = vehicles.Select((vehicle, index) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem", ["position"] = index + 1,
            ["url"] = $"{baseUrl.TrimEnd('/')}/veiculo/{vehicle.Id}/", ["name"] = vehicle.Name
        }).ToArray()
    });

    public static string Vehicle(string name, string brand, int year, string? color, string fuel,
        string transmission, IEnumerable<string> images, decimal price, string canonicalUrl) => Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org", ["@type"] = "Vehicle", ["name"] = name,
        ["brand"] = new Dictionary<string, object?> { ["@type"] = "Brand", ["name"] = brand },
        ["vehicleModelDate"] = year.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["color"] = color, ["fuelType"] = fuel, ["vehicleTransmission"] = transmission,
        ["image"] = images.ToArray(), ["url"] = canonicalUrl,
        ["offers"] = new Dictionary<string, object?>
        {
            ["@type"] = "Offer", ["priceCurrency"] = "BRL", ["price"] = price > 0 ? price : null,
            ["availability"] = "https://schema.org/InStock", ["url"] = canonicalUrl
        }
    });

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);
}
