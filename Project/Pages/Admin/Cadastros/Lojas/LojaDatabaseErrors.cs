using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Project.Pages.Admin.Cadastros.Lojas;

public static class LojaDatabaseErrors
{
    public static bool IsCnpjDuplicateSqlError(int number, string message)
        => number is 2601 or 2627 && message.Contains("Cnpj", StringComparison.OrdinalIgnoreCase);

    public static bool IsCnpjDuplicate(DbUpdateException exception)
        => exception.GetBaseException() is SqlException sql
            && sql.Errors.Cast<SqlError>().Any(error => IsCnpjDuplicateSqlError(error.Number, error.Message));
}
