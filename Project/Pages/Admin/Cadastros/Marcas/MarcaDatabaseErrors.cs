using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Project.Pages.Admin.Cadastros.Marcas;

public static class MarcaDatabaseErrors
{
    public static bool IsNameDuplicate(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
                return sql.Errors.Cast<SqlError>().Any(error => IsNameDuplicateSqlError(error.Number, error.Message));
        }

        return false;
    }

    public static bool IsNameDuplicateSqlError(int number, string message)
    {
        if (number is not (2601 or 2627)) return false;

        // SQL Server identifies object/index (2601) or constraint/object (2627)
        // before the duplicate value. Do not mistake that user-controlled value for an index name.
        var identifiers = Regex.Matches(message, "'([^']+)'", RegexOptions.CultureInvariant);
        if (identifiers.Count < 2) return false;

        var index = identifiers[number == 2601 ? 1 : 0].Groups[1].Value;
        var table = identifiers[number == 2601 ? 0 : 1].Groups[1].Value;
        return index.Equals("IX_Marca_Nome", StringComparison.OrdinalIgnoreCase)
            && (table.Equals("dbo.Marca", StringComparison.OrdinalIgnoreCase)
                || table.Equals("Marca", StringComparison.OrdinalIgnoreCase));
    }
}
