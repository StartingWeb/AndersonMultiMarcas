using System.ComponentModel.DataAnnotations;
using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Project.Pages.Admin.Cadastros.Lojas;

[Authorize]
public sealed class UpsertModel(ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public LojaInputModel Loja { get; set; } = new();

    public IReadOnlyList<SelectListItem> Ufs { get; private set; } = [];
    public string PageTitle { get; private set; } = "Nova loja";
    public string PageSubtitle { get; private set; } = "Preencha os dados para cadastrar uma nova loja";
    public string SubmitLabel { get; private set; } = "Cadastrar loja";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        LoadUfs();

        if (!Id.HasValue)
        {
            ViewData["Title"] = PageTitle;
            return Page();
        }

        var loja = await db.Lojas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id.Value, ct);
        if (loja is null)
        {
            TempData["ErrorMessage"] = "Loja nao encontrada.";
            return RedirectToPage("/Admin/Cadastros/Lojas/Index");
        }

        Loja = new LojaInputModel
        {
            Id = loja.Id,
            Nome = loja.Nome,
            RazaoSocial = loja.RazaoSocial,
            Cnpj = loja.Cnpj?.Valor,
            Email = loja.Email.Valor,
            Telefone = loja.Telefone.Valor,
            Logradouro = loja.Endereco.Logradouro,
            Numero = loja.Endereco.Numero,
            Complemento = loja.Endereco.Complemento,
            Bairro = loja.Endereco.Bairro,
            Cidade = loja.Endereco.Cidade,
            Uf = loja.Endereco.Uf,
            Cep = loja.Endereco.Cep
        };

        ApplyEditTexts();
        ViewData["Title"] = PageTitle;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        LoadUfs();
        NormalizeCnpjModelState();

        if (!ModelState.IsValid)
        {
            if (Loja.Id > 0) ApplyEditTexts();
            return Page();
        }

        var cnpj = string.IsNullOrWhiteSpace(Loja.Cnpj) ? (Documento?)null : new Documento(Loja.Cnpj);
        if (cnpj.HasValue && await db.Lojas.AsNoTracking().AnyAsync(x => x.Id != Loja.Id && x.Cnpj == cnpj, ct))
        {
            ModelState.AddModelError("Loja.Cnpj", "Ja existe uma loja com este CNPJ.");
            if (Loja.Id > 0) ApplyEditTexts();
            return Page();
        }

        try
        {
            var endereco = new Endereco(
                Loja.Logradouro,
                Loja.Numero,
                Loja.Complemento,
                Loja.Bairro,
                Loja.Cidade,
                Loja.Uf,
                CleanDigits(Loja.Cep));

            if (Loja.Id > 0)
            {
                var loja = await db.Lojas.FirstOrDefaultAsync(x => x.Id == Loja.Id, ct);
                if (loja is null)
                {
                    TempData["ErrorMessage"] = "Loja nao encontrada.";
                    return RedirectToPage("/Admin/Cadastros/Lojas/Index");
                }

                loja.Update(Loja.Nome, Loja.RazaoSocial, cnpj, new Email(Loja.Email), new Telefone(Loja.Telefone), endereco);
            }
            else
            {
                db.Lojas.Add(new Loja(Loja.Nome, Loja.RazaoSocial, cnpj, new Email(Loja.Email), new Telefone(Loja.Telefone), endereco));
            }

            await db.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = Loja.Id > 0 ? "Loja atualizada com sucesso." : "Loja cadastrada com sucesso.";
            return RedirectToPage("/Admin/Cadastros/Lojas/Index");
        }
        catch (DbUpdateException ex) when (LojaDatabaseErrors.IsCnpjDuplicate(ex))
        {
            ModelState.AddModelError("Loja.Cnpj", "Ja existe uma loja com este CNPJ.");
            if (Loja.Id > 0) ApplyEditTexts();
            return Page();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            if (Loja.Id > 0) ApplyEditTexts();
            return Page();
        }
    }

    private void NormalizeCnpjModelState()
    {
        if (LojaCnpj.TryNormalizeOptional(Loja.Cnpj, out var normalized, out var error))
        {
            Loja.Cnpj = normalized;
            if (normalized is null) ModelState.Remove("Loja.Cnpj");
        }
        else
        {
            ModelState.AddModelError("Loja.Cnpj", error ?? LojaCnpj.InvalidMessage);
        }
    }

    private void LoadUfs()
        => Ufs = Enum.GetValues<Uf>().Select(x => new SelectListItem(x.ToString(), ((int)x).ToString())).ToList();

    private void ApplyEditTexts()
    {
        PageTitle = "Editar loja";
        PageSubtitle = "Atualize os dados da loja cadastrada";
        SubmitLabel = "Salvar alteracoes";
    }

    private static string CleanDigits(string value) => new(value.Where(char.IsDigit).ToArray());

    public sealed class LojaInputModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Informe o nome da loja.")]
        [StringLength(150)]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a razao social.")]
        [StringLength(200)]
        [Display(Name = "Razao social")]
        public string RazaoSocial { get; set; } = string.Empty;

        [StringLength(18)]
        [Display(Name = "CNPJ")]
        public string? Cnpj { get; set; }

        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail valido.")]
        [StringLength(180)]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o telefone.")]
        [StringLength(20)]
        [Display(Name = "Telefone")]
        public string Telefone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o endereco.")]
        [StringLength(180)]
        [Display(Name = "Endereco")]
        public string Logradouro { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o numero.")]
        [StringLength(20)]
        [Display(Name = "Numero")]
        public string Numero { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Complemento")]
        public string? Complemento { get; set; }

        [Required(ErrorMessage = "Informe o bairro.")]
        [StringLength(100)]
        [Display(Name = "Bairro")]
        public string Bairro { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a cidade.")]
        [StringLength(100)]
        [Display(Name = "Cidade")]
        public string Cidade { get; set; } = string.Empty;

        [Range(1, 27, ErrorMessage = "Informe a UF.")]
        [Display(Name = "UF")]
        public Uf Uf { get; set; } = Uf.SP;

        [Required(ErrorMessage = "Informe o CEP.")]
        [StringLength(10)]
        [Display(Name = "CEP")]
        public string Cep { get; set; } = string.Empty;
    }
}
