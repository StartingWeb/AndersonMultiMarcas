using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Data;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Project.Pages.Admin.Cadastros.Marcas;

[Authorize]
public sealed class UpsertModel(ApplicationDbContext db, ILogger<UpsertModel> logger) : PageModel
{
    private const string PersistenceError = "Não foi possível salvar a marca. Tente novamente ou entre em contato com o suporte.";
    private const string DuplicateNameError = "Já existe uma marca cadastrada com este nome. Verifique a lista de marcas antes de continuar.";
    private const string InactiveNameError = "Esta marca já está cadastrada, mas encontra-se inativa. Entre em contato com o administrador para reativá-la.";

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public MarcaInputModel Marca { get; set; } = new();

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

        Marca = new MarcaInputModel { Id = marca.Id, Nome = marca.Nome, LogoUrl = marca.LogoUrl };
        ApplyEditTexts();
        ViewData["Title"] = PageTitle;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        TempData.Remove("SuccessMessage");
        if (!ModelState.IsValid)
        {
            if (Marca.Id > 0) ApplyEditTexts();
            return Page();
        }

        try
        {
            if (Marca.Id > 0)
            {
                var marca = await db.Marcas.FirstOrDefaultAsync(x => x.Id == Marca.Id, ct);
                if (marca is null)
                {
                    TempData["ErrorMessage"] = "Marca não encontrada.";
                    return RedirectToPage("/Admin/Cadastros/Marcas/Index");
                }

                marca.Update(Marca.Nome, Marca.LogoUrl);
            }
            else
            {
                db.Marcas.Add(new Marca(Marca.Nome, Marca.LogoUrl));
            }

            await db.SaveChangesAsync(ct);
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

        [StringLength(400, ErrorMessage = "A URL deve ter no máximo 400 caracteres.")]
        [Display(Name = "Logo")]
        public string? LogoUrl { get; set; }
    }
}
