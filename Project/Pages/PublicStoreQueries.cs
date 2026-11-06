using System.Data;
using System.Data.Common;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Project.Pages.ViewModels;

namespace Project.Pages;

// Read scalar columns to tolerate nullable legacy rows without constructing strict value objects.
internal static class PublicStoreQueries
{
    private sealed record Store(int Id, string Nome, string Endereco, string Telefone, string Email);
    private sealed record Seller(int LojaId, string Nome, string Telefone, string? Cargo, string? FotoUrl);

    public static async Task<IReadOnlyList<HomeStoreViewModel>> GetHomeStoresAsync(ApplicationDbContext db, int take, CancellationToken ct)
        => (await GetStoresAsync(db, ct)).Take(take).Select(x => new HomeStoreViewModel
        {
            Nome = x.Nome,
            EnderecoCompleto = x.Endereco,
            MapsQuery = Uri.EscapeDataString(x.Endereco)
        }).ToList();

    public static async Task<IReadOnlyList<ContactStoreViewModel>> GetContactStoresAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var stores = await GetStoresAsync(db, ct);
        var sellers = await GetSellersAsync(db, ct);
        return stores.Select(x => new ContactStoreViewModel
        {
            Nome = x.Nome,
            EnderecoCompleto = x.Endereco,
            Telefone = x.Telefone,
            TelefoneExibicao = FormatPhone(x.Telefone),
            Email = x.Email,
            MapsQuery = Uri.EscapeDataString(x.Endereco),
            Vendedores = sellers.Where(s => s.LojaId == x.Id).Select(s => new ContactSellerViewModel
            {
                Nome = s.Nome,
                Telefone = s.Telefone,
                TelefoneExibicao = FormatPhone(s.Telefone),
                Cargo = s.Cargo,
                FotoUrl = s.FotoUrl
            }).ToList()
        }).ToList();
    }

    private static async Task<IReadOnlyList<Store>> GetStoresAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var result = new List<Store>();
        await ReadAsync(db, """
            SELECT [Id], [Nome], [Endereco], [Numero], [Complemento], [Bairro], [Cidade], [Uf], [Cep], [Telefone], [Email]
            FROM [Loja] WHERE [Ativo] = 1 ORDER BY [Id]
            """, async reader =>
        {
            while (await reader.ReadAsync(ct))
            {
                var city = Text(reader, 6);
                var state = Text(reader, 7);
                var address = string.Join(", ", new[] { Text(reader, 2), Text(reader, 3), Text(reader, 4), Text(reader, 5),
                    string.Join(" - ", new[] { city, state }.Where(Filled)), Text(reader, 8) }.Where(Filled));
                result.Add(new Store(reader.GetInt32(0), Text(reader, 1), address, Digits(Text(reader, 9)), Text(reader, 10)));
            }
        }, ct);
        return result;
    }

    private static async Task<IReadOnlyList<Seller>> GetSellersAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var result = new List<Seller>();
        await ReadAsync(db, """
            SELECT v.[LojaId], v.[Nome], v.[Whatsapp], v.[Telefone], v.[Cargo], v.[FotoUrl]
            FROM [Vendedor] v INNER JOIN [Loja] l ON l.[Id] = v.[LojaId]
            WHERE v.[Ativo] = 1 AND l.[Ativo] = 1 AND v.[Nome] IS NOT NULL AND v.[Nome] <> '' ORDER BY v.[Nome]
            """, async reader =>
        {
            while (await reader.ReadAsync(ct))
            {
                var phone = Digits(new[] { Text(reader, 2), Text(reader, 3) }.FirstOrDefault(Filled) ?? "");
                if (phone.Length == 0) continue;
                result.Add(new Seller(reader.GetInt32(0), Text(reader, 1), phone, Optional(reader, 4), Optional(reader, 5)));
            }
        }, ct);
        return result;
    }

    private static async Task ReadAsync(ApplicationDbContext db, string sql, Func<DbDataReader, Task> read, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            await using var reader = await command.ExecuteReaderAsync(ct);
            await read(reader);
        }
        finally
        {
            if (close) await connection.CloseAsync();
        }
    }

    private static string Text(DbDataReader reader, int ordinal) => Optional(reader, ordinal) ?? "";
    private static string? Optional(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
    private static bool Filled(string value) => !string.IsNullOrWhiteSpace(value) && value != "-";
    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());

    private static string FormatPhone(string phone) => phone.Length switch
    {
        10 => $"({phone[..2]}) {phone[2..6]}-{phone[6..]}",
        11 => $"({phone[..2]}) {phone[2..7]}-{phone[7..]}",
        12 when phone.StartsWith("55") => $"({phone[2..4]}) {phone[4..8]}-{phone[8..]}",
        13 when phone.StartsWith("55") => $"({phone[2..4]}) {phone[4..9]}-{phone[9..]}",
        _ => phone
    };
}
