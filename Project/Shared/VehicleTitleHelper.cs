namespace Project.Shared;

public static class VehicleTitleHelper
{
    public static string Format(string? marca, string? modelo, string? versao = null)
        => string.Join(" ", new[] { marca, modelo, versao }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .SelectMany(part => part!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
}
