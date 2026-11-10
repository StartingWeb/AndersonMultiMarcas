namespace Project.Shared;

public static class PhoneLinkHelper
{
    public static string ToInternational(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.Length is 10 or 11 ? "55" + digits : digits;
    }
}
