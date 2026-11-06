using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Project.Infrastructure.Storage;

namespace Project.ViewComponents;

public sealed class SellerContactModalViewComponent(
    ApplicationDbContext db,
    IStorageImageResolver imageResolver,
    IMemoryCache cache) : ViewComponent
{
    private const string CacheKey = "seller-contact-modal:model:v1";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<IViewComponentResult> InvokeAsync()
        => View(await GetModelAsync());

    private async Task<IReadOnlyCollection<SellerContactModalStoreGroupViewModel>> GetModelAsync()
        => await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await BuildModelAsync();
        }) ?? [];

    private async Task<IReadOnlyCollection<SellerContactModalStoreGroupViewModel>> BuildModelAsync()
    {
        var grupos = await db.Vendedores
            .AsNoTracking()
            .Where(x => x.Ativo && x.Loja.Ativo)
            .Select(x => new
            {
                LojaNome = x.Loja.Nome,
                x.Nome,
                Telefone = x.Whatsapp.HasValue ? x.Whatsapp.Value.Valor : (x.Telefone.HasValue ? x.Telefone.Value.Valor : string.Empty),
                x.FotoUrl
            })
            .ToListAsync();

        var vendedores = new List<(string LojaNome, SellerContactModalSellerViewModel Vendedor)>();
        foreach (var grupo in grupos.Where(x => !string.IsNullOrWhiteSpace(x.Telefone)))
        {
            vendedores.Add((
                grupo.LojaNome,
                new SellerContactModalSellerViewModel(
                    grupo.Nome,
                    grupo.Telefone,
                    imageResolver.ResolveSellerPhoto(grupo.FotoUrl))));
        }

        return vendedores
            .GroupBy(x => string.IsNullOrWhiteSpace(x.LojaNome) ? "Sem loja vinculada" : x.LojaNome)
            .OrderBy(x => x.Key)
            .Select(x => new SellerContactModalStoreGroupViewModel(
                x.Key,
                x.Select(v => v.Vendedor).OrderBy(v => v.Nome).ToList()))
            .Where(x => x.Vendedores.Count > 0)
            .ToList();
    }
}

public sealed record SellerContactModalStoreGroupViewModel(
    string LojaNome,
    IReadOnlyList<SellerContactModalSellerViewModel> Vendedores);

public sealed record SellerContactModalSellerViewModel(
    string Nome,
    string Telefone,
    string? FotoUrl);
