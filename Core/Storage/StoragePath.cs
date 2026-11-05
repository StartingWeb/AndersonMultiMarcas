using System.Collections.Specialized;

namespace Core.Storage;

public static class StoragePath
{
    public const string VehiclePrefix = "uploads/veiculos/";
    public const string LegacyImportedVehiclePrefix = "anderson-multimarcas/veiculos/";
    public const string SellerPrefix = "uploads/vendedores/";

    public static string Combine(params string?[] segments)
        => NormalizeKey(string.Join("/", segments.Where(x => !string.IsNullOrWhiteSpace(x))));

    public static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Storage key obrigatorio.", nameof(key));
        }

        var normalized = key.Trim().Replace('\\', '/');
        normalized = StripQueryAndFragment(normalized);

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            normalized = uri.AbsolutePath;
        }

        if (normalized.StartsWith("~/", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        if (normalized.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["wwwroot/".Length..];
        }

        normalized = normalized.TrimStart('/');

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(x => x is "." or ".."))
        {
            throw new ArgumentException("Storage key invalido.", nameof(key));
        }

        return string.Join("/", parts);
    }

    public static string ToPublicPath(string key)
        => "/" + NormalizeKey(key);

    public static string GetFileName(string key)
        => NormalizeKey(key).Split('/').Last();

    public static string GetContainer(string key)
    {
        var normalized = NormalizeKey(key);
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0 ? string.Empty : normalized[..lastSlash];
    }

    public static bool IsVehicleKey(string? key)
        => TryNormalizeKey(key, out var normalized) && IsKnownVehicleKey(normalized);

    public static bool IsSellerKey(string? key)
        => TryNormalizeKey(key, out var normalized) && normalized.StartsWith(SellerPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool TryGetKeyFromSource(string? source, IEnumerable<string?> publicBaseUrls, out string key)
    {
        key = string.Empty;
        var normalized = NormalizeSource(source);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        // On Unix, Uri.TryCreate("/img/photo.png", Absolute) also succeeds as
        // a file URI. A rooted web path must be handled before URI detection;
        // removing its single slash keeps NormalizeKey's traversal check intact.
        if (normalized.StartsWith('/'))
        {
            return !normalized.StartsWith("//", StringComparison.Ordinal)
                && TryNormalizeKey(normalized[1..], out key);
        }

        foreach (var publicBaseUrl in publicBaseUrls.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var baseUrl = publicBaseUrl!.TrimEnd('/');
            if (normalized.StartsWith(baseUrl + "/", StringComparison.OrdinalIgnoreCase))
            {
                return TryNormalizeKey(normalized[(baseUrl.Length + 1)..], out key);
            }
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }
            var absolutePath = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
            var knownPrefixIndex = FindKnownStoragePrefixIndex(absolutePath);
            if (knownPrefixIndex >= 0)
            {
                return TryNormalizeKey(absolutePath[knownPrefixIndex..], out key);
            }

            return false;
        }

        if (normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("anderson-multimarcas/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("wwwroot/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return TryNormalizeKey(normalized, out key);
        }

        return false;
    }

    public static bool TryGetKey(StorageImageReference reference, IEnumerable<string?> publicBaseUrls, out string key)
    {
        key = string.Empty;

        if (!string.IsNullOrWhiteSpace(reference.BlobName))
        {
            var blobName = reference.BlobName.Trim();
            if (blobName.Contains('/') || blobName.Contains('\\'))
            {
                return TryNormalizeKey(blobName, out key);
            }

            if (!string.IsNullOrWhiteSpace(reference.Container)
                && reference.Container.Trim().Replace('\\', '/').StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                return TryNormalizeKey(Combine(reference.Container, blobName), out key);
            }
        }

        if (!string.IsNullOrWhiteSpace(reference.Url)
            && TryGetKeyFromSource(reference.Url, publicBaseUrls, out key))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(reference.NomeArquivo)
            && !string.IsNullOrWhiteSpace(reference.Container)
            && reference.Container.Trim().Replace('\\', '/').StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return TryNormalizeKey(Combine(reference.Container, reference.NomeArquivo), out key);
        }

        return false;
    }

    public static string NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var normalized = source.Trim().Replace('\\', '/');
        if (normalized.StartsWith("/media/img", StringComparison.OrdinalIgnoreCase))
        {
            normalized = ExtractMediaSource(normalized);
        }

        if (normalized.StartsWith("~/", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (normalized.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["wwwroot".Length..];
        }

        if (normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }

    private static bool TryNormalizeKey(string? value, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            key = NormalizeKey(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsKnownVehicleKey(string key)
        => key.StartsWith(VehiclePrefix, StringComparison.OrdinalIgnoreCase)
            || key.StartsWith(LegacyImportedVehiclePrefix, StringComparison.OrdinalIgnoreCase);

    private static int FindKnownStoragePrefixIndex(string path)
    {
        var uploadsIndex = path.IndexOf("uploads/", StringComparison.OrdinalIgnoreCase);
        var importedIndex = path.IndexOf("anderson-multimarcas/", StringComparison.OrdinalIgnoreCase);

        if (uploadsIndex < 0)
        {
            return importedIndex;
        }

        if (importedIndex < 0)
        {
            return uploadsIndex;
        }

        return Math.Min(uploadsIndex, importedIndex);
    }

    private static string StripQueryAndFragment(string value)
    {
        var end = value.IndexOfAny(['?', '#']);
        return end >= 0 ? value[..end] : value;
    }

    private static string ExtractMediaSource(string value)
    {
        var queryStart = value.IndexOf('?');
        if (queryStart < 0)
        {
            return value;
        }

        var query = ParseQuery(value[(queryStart + 1)..]);
        return query["src"] ?? value;
    }

    private static NameValueCollection ParseQuery(string query)
    {
        var values = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            values[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1].Replace('+', ' '));
        }

        return values;
    }
}
