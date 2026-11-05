using System.Data;
using Data;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Project.Pages.Admin.Ferramentas;

[Authorize(Roles = "Administrador,AdminConcessionaria,Desenvolvedor")]
public sealed class RevisarCapasVeiculosModel(ApplicationDbContext db, ILogger<RevisarCapasVeiculosModel> logger) : PageModel
{
    public const string FilterWithoutCover = "SemCapa";
    public const string FilterAll = "Todos";
    public const string FilterSingleImage = "UmaImagem";
    public const string FilterMoreThan = "MaisQue";
    public const int DefaultMoreThan = 8;

    public sealed record CoverReviewFilterOption(string Value, string Label);
    public sealed record CoverReviewVehicleItem(int Position, int Id, string Nome, string Marca, string Modelo, int Ano,
        int QuantidadeImagens, int? CurrentCoverMediaId, string? CurrentCoverFileName, string? CurrentCoverUrl);
    public sealed record CoverReviewMediaItem(int Id, string Url, string NomeArquivo, bool Capa, int Ordem);
    public sealed record CoverReviewPageData(int Position, int Total, string Mode, string? Busca, int MaisQue,
        CoverReviewVehicleItem? Previous, CoverReviewVehicleItem? Current, CoverReviewVehicleItem? Next,
        IReadOnlyList<CoverReviewMediaItem> Images)
    {
        public static CoverReviewPageData Empty { get; } = new(0, 0, FilterWithoutCover, null, DefaultMoreThan, null, null, null, []);
    }
    public sealed class CoverReviewSaveRequest
    {
        public int VehicleId { get; set; }
        public int MediaId { get; set; }
        public int Position { get; set; }
        public string? Mode { get; set; }
        public string? Busca { get; set; }
        public int? MaisQue { get; set; }
    }
    public sealed record CoverReviewSaveResult(bool Success, string Message, int? NextPosition, int Total,
        CoverReviewPageData? NextReview, bool ReviewedAll);

    [BindProperty(SupportsGet = true)] public int Position { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string Mode { get; set; } = FilterWithoutCover;
    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }
    [BindProperty(SupportsGet = true)] public int? MaisQue { get; set; } = DefaultMoreThan;

    public CoverReviewPageData Review { get; private set; } = CoverReviewPageData.Empty;
    public IReadOnlyList<CoverReviewFilterOption> FilterOptions { get; } =
    [new(FilterWithoutCover, "Veiculos sem capa"), new(FilterAll, "Todos os veiculos"),
     new(FilterSingleImage, "Veiculos com apenas 1 imagem"), new(FilterMoreThan, "Veiculos com mais de")];

    public async Task OnGetAsync(CancellationToken ct)
    {
        ViewData["Title"] = "Revisar capas dos veiculos";
        ViewData["Robots"] = "noindex,nofollow";
        Mode = NormalizeMode(Mode);
        Busca = NormalizeSearch(Busca);
        MaisQue = NormalizeMoreThan(MaisQue);
        Review = await LoadPageDataAsync(Position, Mode, Busca, MaisQue, ct);
        Position = Math.Max(1, Review.Position);
    }

    public async Task<IActionResult> OnGetVehicleAsync(int position, string? mode, string? busca, int? maisQue, CancellationToken ct)
        => new JsonResult(await LoadPageDataAsync(position, mode, busca, maisQue, ct));

    public async Task<IActionResult> OnPostSaveCoverAsync([FromBody] CoverReviewSaveRequest? request, CancellationToken ct)
    {
        if (request is null || request.VehicleId <= 0 || request.MediaId <= 0)
            return Failure("Informe o veiculo e a imagem.", 400);

        try
        {
            var saved = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
                var available = await db.Veiculos.AnyAsync(x => x.Id == request.VehicleId && x.Ativo, ct);
                if (!available) return false;
                var media = await db.VeiculoMidias.Where(x => x.VeiculoId == request.VehicleId && x.Tipo == TipoMidia.Imagem)
                    .OrderBy(x => x.Ordem).ThenBy(x => x.Id).ToListAsync(ct);
                var selected = media.FirstOrDefault(x => x.Id == request.MediaId && x.Ativo);
                if (selected is null) return false;

                // Clear in a separate SQL batch so the filtered unique cover index cannot be violated by update order.
                foreach (var image in media.Where(x => x.Capa)) db.Entry(image).Property(x => x.Capa).CurrentValue = false;
                await db.SaveChangesAsync(ct);
                var ordered = media.OrderBy(x => x.Id == selected.Id ? 0 : 1).ThenBy(x => x.Ativo ? 0 : 1)
                    .ThenBy(x => x.Ordem).ThenBy(x => x.Id).ToList();
                for (var index = 0; index < ordered.Count; index++)
                {
                    db.Entry(ordered[index]).Property(x => x.Capa).CurrentValue = ordered[index].Id == selected.Id;
                    db.Entry(ordered[index]).Property(x => x.Ordem).CurrentValue = index + 1;
                }
                await db.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return true;
            });
            if (!saved) return Failure("Imagem ativa nao encontrada para este veiculo.", 400);

            var mode = NormalizeMode(request.Mode);
            var moreThan = NormalizeMoreThan(request.MaisQue);
            var total = await BuildFilteredVehicleQuery(mode, request.Busca, moreThan).CountAsync(ct);
            var next = total == 0 ? (int?)null : mode == FilterWithoutCover
                ? Math.Clamp(request.Position, 1, total)
                : request.Position < total ? Math.Max(1, request.Position + 1) : null;
            var review = next.HasValue ? await LoadPageDataAsync(next.Value, mode, request.Busca, moreThan, ct)
                : CoverReviewPageData.Empty with { Mode = mode, Busca = NormalizeSearch(request.Busca), MaisQue = moreThan };
            return new JsonResult(new CoverReviewSaveResult(true, next.HasValue ? "Capa atualizada." : "Todos os veiculos foram revisados.",
                next, total, review, !next.HasValue));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao salvar capa do veiculo {VehicleId}, midia {MediaId}", request.VehicleId, request.MediaId);
            return Failure("Nao foi possivel salvar a capa. Tente novamente.", 500);
        }
    }

    private IQueryable<Domain.Entities.Veiculo> BuildFilteredVehicleQuery(string mode, string? search, int moreThan)
    {
        var images = db.VeiculoMidias.Where(x => x.Ativo && x.Tipo == TipoMidia.Imagem);
        var query = db.Veiculos.AsNoTracking().Where(x => x.Ativo);
        var term = NormalizeSearch(search);
        if (term is not null)
        {
            var numeric = int.TryParse(term, out var id);
            query = query.Where(x => (numeric && x.Id == id) || x.Titulo.Contains(term) || x.Modelo.Contains(term)
                || (x.Versao != null && x.Versao.Contains(term)) || x.Marca.Nome.Contains(term));
        }
        return mode switch
        {
            FilterWithoutCover => query.Where(x => images.Any(m => m.VeiculoId == x.Id) && !images.Any(m => m.VeiculoId == x.Id && m.Capa)),
            FilterSingleImage => query.Where(x => images.Count(m => m.VeiculoId == x.Id) == 1),
            FilterMoreThan => query.Where(x => images.Count(m => m.VeiculoId == x.Id) > moreThan),
            _ => query
        };
    }

    private async Task<CoverReviewPageData> LoadPageDataAsync(int requestedPosition, string? mode, string? search, int? moreThan, CancellationToken ct)
    {
        var normalizedMode = NormalizeMode(mode);
        var threshold = NormalizeMoreThan(moreThan);
        var query = BuildFilteredVehicleQuery(normalizedMode, search, threshold);
        var total = await query.CountAsync(ct);
        if (total == 0) return CoverReviewPageData.Empty with { Mode = normalizedMode, Busca = NormalizeSearch(search), MaisQue = threshold };
        var position = Math.Clamp(requestedPosition, 1, total);
        var skip = Math.Max(0, position - 2);
        var rows = await query.OrderBy(x => x.Id).Skip(skip).Take(3).Select(x => new
        {
            x.Id, x.Titulo, x.Modelo, x.Versao, x.AnoModelo, Marca = x.Marca.Nome,
            ImageCount = x.Midias.Count(m => m.Ativo && m.Tipo == TipoMidia.Imagem),
            Cover = x.Midias.Where(m => m.Ativo && m.Tipo == TipoMidia.Imagem && m.Capa).OrderBy(m => m.Ordem).ThenBy(m => m.Id)
                .Select(m => new { m.Id, m.NomeArquivo, m.Url }).FirstOrDefault()
        }).ToListAsync(ct);
        var vehicles = rows.Select((x, i) => new CoverReviewVehicleItem(skip + i + 1, x.Id,
            string.Join(" ", new[] { x.Titulo, string.Equals(x.Titulo, x.Modelo, StringComparison.OrdinalIgnoreCase) ? null : x.Modelo, x.Versao }
                .Where(s => !string.IsNullOrWhiteSpace(s))), x.Marca, x.Modelo, x.AnoModelo,
            x.ImageCount, x.Cover?.Id, x.Cover?.NomeArquivo, x.Cover?.Url)).ToList();
        var current = vehicles.Single(x => x.Position == position);
        var images = await db.VeiculoMidias.AsNoTracking().Where(x => x.VeiculoId == current.Id && x.Ativo && x.Tipo == TipoMidia.Imagem)
            .OrderBy(x => x.Ordem).ThenBy(x => x.Id).Select(x => new CoverReviewMediaItem(x.Id, x.Url, x.NomeArquivo, x.Capa, x.Ordem)).ToListAsync(ct);
        return new(position, total, normalizedMode, NormalizeSearch(search), threshold,
            vehicles.FirstOrDefault(x => x.Position == position - 1), current, vehicles.FirstOrDefault(x => x.Position == position + 1), images);
    }

    private static string NormalizeMode(string? value) => value?.Trim() is FilterAll or FilterSingleImage or FilterMoreThan ? value.Trim() : FilterWithoutCover;
    private static int NormalizeMoreThan(int? value) => value is > 0 ? value.Value : DefaultMoreThan;
    private static string? NormalizeSearch(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static JsonResult Failure(string message, int status) => new(new CoverReviewSaveResult(false, message, null, 0, null, false)) { StatusCode = status };
}
