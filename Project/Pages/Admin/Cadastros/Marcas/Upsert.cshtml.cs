using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Core.Storage;
using Data;
using Domain.Entities;
using ImageMagick;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Images;

namespace Project.Pages.Admin.Cadastros.Marcas;

[Authorize]
[RequestSizeLimit(6 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
public sealed class UpsertModel(ApplicationDbContext db, ILogger<UpsertModel> logger, IStorageService storage) : PageModel
{
    public const int MaxLogoBytes = 5 * 1024 * 1024;
    private const string PersistenceError = "Não foi possível salvar a marca. Tente novamente ou entre em contato com o suporte.";
    private const string DuplicateNameError = "Já existe uma marca cadastrada com este nome. Verifique a lista de marcas antes de continuar.";
    private const string InactiveNameError = "Esta marca já está cadastrada, mas encontra-se inativa. Entre em contato com o administrador para reativá-la.";

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public MarcaInputModel Marca { get; set; } = new();

    [BindProperty]
    [Display(Name = "Logo")]
    public IFormFile? LogoArquivo { get; set; }

    public string? CurrentLogoUrl { get; private set; }

    public string PageTitle { get; private set; } = "Nova marca";
    public string PageSubtitle { get; private set; } = "Preencha os dados para cadastrar uma nova marca";
    public string SubmitLabel { get; private set; } = "Cadastrar marca";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!Id.HasValue)
        {
            ViewData["Title"] = PageTitle;
            return Page();
        }

        var marca = await db.Marcas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id.Value, ct);
        if (marca is null)
        {
            TempData["ErrorMessage"] = "Marca não encontrada.";
            return RedirectToPage("/Admin/Cadastros/Marcas/Index");
        }

        Marca = new MarcaInputModel { Id = marca.Id, Nome = marca.Nome };
        CurrentLogoUrl = marca.LogoUrl;
        ApplyEditTexts();
        ViewData["Title"] = PageTitle;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        TempData.Remove("SuccessMessage");
        string? uploadKey = null;
        string? uploadedUrl = null;
        var saved = false;
        try
        {
            Marca? existing = null;
            if (Marca.Id > 0)
            {
                ApplyEditTexts();
                existing = await db.Marcas.FirstOrDefaultAsync(x => x.Id == Marca.Id, ct);
                if (existing is null)
                {
                    TempData["ErrorMessage"] = "Marca não encontrada.";
                    return RedirectToPage("/Admin/Cadastros/Marcas/Index");
                }

                CurrentLogoUrl = existing.LogoUrl;
            }

            if (!ModelState.IsValid) return Page();

            var logoUrl = CurrentLogoUrl;
            if (LogoArquivo is not null)
            {
                if (LogoArquivo.Length <= 0 || LogoArquivo.Length > MaxLogoBytes)
                {
                    ModelState.AddModelError(nameof(LogoArquivo), "Selecione uma imagem de até 5 MB.");
                    return Page();
                }
                var extension = Path.GetExtension(LogoArquivo.FileName).ToLowerInvariant();
                if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                {
                    ModelState.AddModelError(nameof(LogoArquivo), "Use uma imagem JPG, PNG ou WebP.");
                    return Page();
                }

                await using var content = new MemoryStream();
                try
                {
                    await using var input = LogoArquivo.OpenReadStream();
                    using var image = await SafeRasterImage.ReadAsync(input, ct);
                    if (image.Width > 512 || image.Height > 512) image.Resize(new MagickGeometry(512, 512));
                    image.Quality = 85;
                    await image.WriteAsync(content, MagickFormat.WebP, ct);
                }
                catch (Exception ex) when (ex is InvalidDataException or MagickException)
                {
                    ModelState.AddModelError(nameof(LogoArquivo), "O arquivo não é uma imagem válida. Use JPG, PNG ou WebP.");
                    return Page();
                }

                uploadKey = $"uploads/marcas/{Guid.NewGuid():N}.webp";
                content.Position = 0;
                var uploaded = await storage.SaveAsync(uploadKey, content, "image/webp", ct);
                uploadedUrl = uploaded.Url;
                if (string.IsNullOrWhiteSpace(uploadedUrl) || uploadedUrl.Length > 400)
                {
                    ModelState.AddModelError(nameof(LogoArquivo), "Não foi possível gerar o endereço da logo. Entre em contato com o suporte.");
                    return Page();
                }
                logoUrl = uploadedUrl;
            }

            if (existing is not null) existing.Update(Marca.Nome, logoUrl);
            else db.Marcas.Add(new Marca(Marca.Nome, logoUrl));

            await db.SaveChangesAsync(ct);
            saved = true;
            TempData["SuccessMessage"] = Marca.Id > 0 ? "Marca atualizada com sucesso." : "Marca cadastrada com sucesso.";
            return RedirectToPage("/Admin/Cadastros/Marcas/Index");
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            var nameConflict = MarcaDatabaseErrors.IsNameDuplicate(ex);
            logger.Log(nameConflict ? LogLevel.Warning : LogLevel.Error, ex,
                "Falha ao salvar marca durante {Operation}.", Marca.Id > 0 ? "edição" : "cadastro");

            var message = PersistenceError;
            if (nameConflict)
            {
                try
                {
                    // Let SQL Server compare the trimmed name using the column's collation.
                    // Keep query filters and never disclose a conflicting row outside the visible scope.
                    var name = Marca.Nome.Trim();
                    var active = await db.Marcas.AsNoTracking()
                        .Where(x => x.Id != Marca.Id && x.Nome == name)
                        .Select(x => (bool?)x.Ativo)
                        .FirstOrDefaultAsync(ct);
                    message = active switch { true => DuplicateNameError, false => InactiveNameError, null => PersistenceError };
                }
                catch (Exception lookupException) when (lookupException is DbException or DbUpdateException)
                {
                    logger.LogError(lookupException, "Falha ao consultar conflito de nome após erro ao salvar marca.");
                }
            }

            ModelState.AddModelError(string.Empty, message);
            if (Marca.Id > 0) ApplyEditTexts();
            return Page();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            if (Marca.Id > 0) ApplyEditTexts();
            return Page();
        }
        catch (Exception ex) when (LogoArquivo is not null && ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao enviar logo da marca.");
            ModelState.AddModelError(nameof(LogoArquivo), "Não foi possível enviar a logo. Tente novamente.");
            return Page();
        }
        finally
        {
            if (!saved && uploadKey is not null)
            {
                try
                {
                    // A connection failure may hide a successful commit. Never remove
                    // an uploaded object that is already referenced by a stored brand,
                    // including a filtered row. This internal check uses only the new
                    // object's generated URL and never exposes the matching row.
                    var candidateUrl = uploadedUrl ?? storage.GetPublicUrl(uploadKey);
                    if (!await db.Marcas.IgnoreQueryFilters().AsNoTracking().AnyAsync(x => x.LogoUrl == candidateUrl, CancellationToken.None))
                        await storage.DeleteAsync(uploadKey, CancellationToken.None);
                }
                catch (Exception cleanupError)
                {
                    logger.LogWarning(cleanupError, "Não foi possível confirmar a limpeza do novo upload de logo: {StorageKey}.", uploadKey);
                }
            }
        }
    }

    private void ApplyEditTexts()
    {
        PageTitle = "Editar marca";
        PageSubtitle = "Atualize os dados da marca cadastrada";
        SubmitLabel = "Salvar alterações";
    }

    public sealed class MarcaInputModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Informe o nome da marca.")]
        [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;
    }
}
