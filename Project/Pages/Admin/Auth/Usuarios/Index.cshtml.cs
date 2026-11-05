using Domain.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace Project.Pages.Admin.Auth.Usuarios;

[Authorize(Roles = "Desenvolvedor")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(
    UserManager<AspNetCoreUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager) : PageModel
{

    [BindProperty(SupportsGet = true)]
    public string? Busca { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Perfil { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Status { get; set; } = "todos";

    [BindProperty]
    public UserInput Input { get; set; } = new();

    public IReadOnlyList<UserRow> Usuarios { get; private set; } = [];
    public IReadOnlyList<string> Perfis { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Usuarios";
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        ViewData["Title"] = "Usuarios";

        if (!ModelState.IsValid)
        {
            TempData["WarningMessage"] = "Confira os campos informados.";
            await LoadAsync();
            return Page();
        }

        var roleExists = await roleManager.RoleExistsAsync(Input.Perfil);
        if (!roleExists)
        {
            TempData["ErrorMessage"] = "Perfil informado nao existe.";
            await LoadAsync();
            return Page();
        }

        var isNew = Input.Id is null;
        var user = isNew
            ? new AspNetCoreUser { Id = Guid.NewGuid(), EmailConfirmed = true }
            : await userManager.FindByIdAsync(Input.Id!.Value.ToString());

        if (user is null)
        {
            TempData["ErrorMessage"] = "Usuario nao encontrado.";
            return RedirectToPage();
        }

        user.NomeCompleto = Input.NomeCompleto.Trim();
        user.UserName = Input.UserName.Trim();
        user.Email = Input.Email.Trim();
        user.EmailConfirmed = Input.EmailConfirmado;
        user.LockoutEnabled = true;
        user.LockoutEnd = Input.Ativo ? null : DateTimeOffset.MaxValue;

        IdentityResult result;
        if (isNew)
        {
            if (string.IsNullOrWhiteSpace(Input.Senha))
            {
                TempData["WarningMessage"] = "Informe uma senha para criar o usuario.";
                await LoadAsync();
                return Page();
            }

            result = await userManager.CreateAsync(user, Input.Senha);
        }
        else
        {
            result = await userManager.UpdateAsync(user);
        }

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(x => x.Description));
            await LoadAsync();
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(Input.Senha) && !isNew)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            result = await userManager.ResetPasswordAsync(user, token, Input.Senha);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(x => x.Description));
                await LoadAsync();
                return Page();
            }
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            result = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(x => x.Description));
                await LoadAsync();
                return Page();
            }
        }

        result = await userManager.AddToRoleAsync(user, Input.Perfil);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(x => x.Description));
            await LoadAsync();
            return Page();
        }

        TempData["SuccessMessage"] = isNew ? "Usuario criado com sucesso." : "Usuario atualizado com sucesso.";
        return RedirectToPage(new { Busca, Perfil, Status });
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            TempData["ErrorMessage"] = "Usuario nao encontrado.";
            return RedirectToPage();
        }

        var blocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
        user.LockoutEnabled = true;
        user.LockoutEnd = blocked ? null : DateTimeOffset.MaxValue;

        var result = await userManager.UpdateAsync(user);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
            ? (blocked ? "Usuario ativado com sucesso." : "Usuario bloqueado com sucesso.")
            : string.Join(" ", result.Errors.Select(x => x.Description));

        return RedirectToPage(new { Busca, Perfil, Status });
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            TempData["ErrorMessage"] = "Usuario nao encontrado.";
            return RedirectToPage(new { Busca, Perfil, Status });
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        TempData["PasswordResetLink"] = Url.Page("/ResetPassword", null, new { userId = user.Id, code });
        TempData["SuccessMessage"] = "Link individual gerado. Compartilhe por um canal privado com o usuario; validade de uma hora. A senha atual permanece valida ate a confirmacao.";

        return RedirectToPage(new { Busca, Perfil, Status });
    }

    private async Task LoadAsync()
    {
        Perfis = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.Name!)
            .ToListAsync();

        var query = userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(Busca))
        {
            var busca = Busca.Trim();
            query = query.Where(x =>
                (x.NomeCompleto != null && x.NomeCompleto.Contains(busca)) ||
                (x.UserName != null && x.UserName.Contains(busca)) ||
                (x.Email != null && x.Email.Contains(busca)));
        }

        var users = await query.OrderBy(x => x.NomeCompleto).ThenBy(x => x.UserName).ToListAsync();
        var rows = new List<UserRow>();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var mainRole = roles.FirstOrDefault() ?? string.Empty;
            var blocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

            if (!string.IsNullOrWhiteSpace(Perfil) && !roles.Contains(Perfil))
            {
                continue;
            }

            if (Status.Equals("ativos", StringComparison.OrdinalIgnoreCase) && blocked)
            {
                continue;
            }

            if (Status.Equals("bloqueados", StringComparison.OrdinalIgnoreCase) && !blocked)
            {
                continue;
            }

            rows.Add(new UserRow(
                user.Id,
                user.NomeCompleto ?? user.UserName ?? user.Email ?? "Usuario",
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                mainRole,
                user.EmailConfirmed,
                !blocked));
        }

        Usuarios = rows;
    }

    public sealed class UserInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Informe o nome.")]
        public string NomeCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o usuario.")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail valido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o perfil.")]
        public string Perfil { get; set; } = string.Empty;

        public string? Senha { get; set; }
        public bool EmailConfirmado { get; set; } = true;
        public bool Ativo { get; set; } = true;
    }

    public sealed record UserRow(
        Guid Id,
        string NomeCompleto,
        string UserName,
        string Email,
        string Perfil,
        bool EmailConfirmado,
        bool Ativo);
}
