using Data;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Project.Pages.Admin.Cadastros.Lojas;

[Authorize]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Filtro { get; set; }

    public IReadOnlyList<LojaListItem> Lojas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        ViewData["Title"] = "Lojas";
        ViewData["Robots"] = "noindex,nofollow";

        var sql = """
            SELECT
                [Id],
                COALESCE([Nome], '-') AS [Nome],
                COALESCE([RazaoSocial], '-') AS [RazaoSocial],
                COALESCE([Cnpj], '-') AS [Cnpj],
                COALESCE([Email], '-') AS [Email],
                COALESCE([Telefone], '-') AS [Telefone],
                COALESCE([Cidade], '-') AS [Cidade],
                COALESCE([Uf], '-') AS [Uf]
            FROM [Loja]
            WHERE [Ativo] = 1
            """;

        var parameters = new List<SqlParameter>();

        if (!string.IsNullOrWhiteSpace(Filtro))
        {
            var term = $"%{Filtro.Trim()}%";
            sql += """
                
                AND (
                    COALESCE([Nome], '') LIKE @term
                    OR COALESCE([RazaoSocial], '') LIKE @term
                    OR COALESCE([Cnpj], '') LIKE @term
                )
                """;
            parameters.Add(new SqlParameter("@term", term));
        }

        sql += """

            ORDER BY [Nome]
            """;

        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            await using var reader = await command.ExecuteReaderAsync(ct);
            var lojas = new List<LojaListItem>();

            while (await reader.ReadAsync(ct))
            {
                lojas.Add(new LojaListItem(
                    reader.GetInt32(0),
                    SafeString(reader, 1),
                    SafeString(reader, 2),
                    SafeString(reader, 3),
                    SafeString(reader, 4),
                    SafeString(reader, 5),
                    SafeString(reader, 6),
                    SafeString(reader, 7)));
            }

            Lojas = lojas;
        }
        finally
        {
            if (close) await connection.CloseAsync();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string? filtro, CancellationToken ct)
    {
        var loja = await db.Lojas.FirstOrDefaultAsync(x => x.Id == id, ct);

        if (loja is null)
        {
            TempData["ErrorMessage"] = "Loja nao encontrada.";
            return RedirectToPage(new { Filtro = filtro });
        }

        loja.Desativar();
        await db.SaveChangesAsync(ct);

        TempData["SuccessMessage"] = "Loja excluida com sucesso.";
        return RedirectToPage(new { Filtro = filtro });
    }

    private static string SafeString(DbDataReader reader, int ordinal)
    {
        var value = reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    public sealed record LojaListItem(
        int Id,
        string Nome,
        string RazaoSocial,
        string Cnpj,
        string Email,
        string Telefone,
        string Cidade,
        string Uf);
}
