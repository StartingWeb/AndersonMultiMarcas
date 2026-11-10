using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Project.Pages.Admin.Cadastros.Marcas;

[Authorize]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Filtro { get; set; }

    public IReadOnlyList<MarcaListItem> Marcas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        ViewData["Title"] = "Marcas";
        ViewData["Robots"] = "noindex,nofollow";

        var query = db.Marcas.AsNoTracking().Where(x => x.Ativo);

        if (!string.IsNullOrWhiteSpace(Filtro))
        {
            var term = Filtro.Trim();
            query = query.Where(x => x.Nome.Contains(term));
        }

        Marcas = await query
            .OrderBy(x => x.Nome)
            // A sale also deactivates the vehicle. Keep sold vehicles in the count,
            // while excluding inactive unsold vehicles (the existing soft-delete state).
            // Count in SQL through the FK, preserving query filters without loading vehicles.
            .Select(x => new MarcaListItem(x.Id, x.Nome, string.IsNullOrWhiteSpace(x.LogoUrl) ? "-" : x.LogoUrl,
                x.Veiculos.Count(v => v.Ativo || v.Vendido)))
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string? filtro, CancellationToken ct)
    {
        var marca = await db.Marcas.FirstOrDefaultAsync(x => x.Id == id, ct);

        if (marca is null)
        {
            TempData["ErrorMessage"] = "Marca não encontrada.";
            return RedirectToPage(new { Filtro = filtro });
        }

        marca.Desativar();
        await db.SaveChangesAsync(ct);

        TempData["SuccessMessage"] = "Marca excluída com sucesso.";
        return RedirectToPage(new { Filtro = filtro });
    }

    public sealed record MarcaListItem(int Id, string Nome, string LogoUrl, int QuantidadeVeiculos);
}
