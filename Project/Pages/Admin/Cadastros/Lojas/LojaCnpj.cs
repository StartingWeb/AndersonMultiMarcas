namespace Project.Pages.Admin.Cadastros.Lojas;

public static class LojaCnpj
{
    public const string InvalidMessage = "Informe um CNPJ valido com 14 digitos.";

    public static bool TryNormalizeOptional(string? value, out string? normalized, out string? errorMessage)
    {
        normalized = null;
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(value)) return true;

        var digits = new string(value.Where(c => c is >= '0' and <= '9').ToArray());
        // Empty input masks were accepted by the published application.
        if (digits.Length == 0 && value.All(c => char.IsWhiteSpace(c) || c is '.' or '/' or '-')) return true;
        if (digits.Length != 14 || digits.Distinct().Count() == 1
            || digits[12] != CalculateCheckDigit(digits, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2])
            || digits[13] != CalculateCheckDigit(digits, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]))
        {
            errorMessage = InvalidMessage;
            return false;
        }

        normalized = digits;
        return true;
    }

    private static char CalculateCheckDigit(string digits, ReadOnlySpan<int> weights)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++) sum += (digits[i] - '0') * weights[i];
        var remainder = sum % 11;
        return remainder < 2 ? '0' : (char)('0' + 11 - remainder);
    }
}
