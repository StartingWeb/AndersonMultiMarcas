using System.ComponentModel.DataAnnotations;
using Domain.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Project.Pages;

public class LoginModel : PageModel
{
    private readonly SignInManager<AspNetCoreUser> signInManager;
    private readonly UserManager<AspNetCoreUser> userManager;

    public LoginModel(SignInManager<AspNetCoreUser> signInManager, UserManager<AspNetCoreUser> userManager)
    {
        this.signInManager = signInManager;
        this.userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            TempData["SuccessMessage"] = "Voce ja esta conectado.";
            Response.Redirect(await ResolveDefaultReturnUrlAsync());
            return;
        }

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            TempData["WarningMessage"] = "Confira os campos informados.";
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Input.Email)
            ?? await userManager.FindByNameAsync(Input.Email);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "E-mail ou senha invalidos.");
            TempData["ErrorMessage"] = "E-mail ou senha invalidos.";
            return Page();
        }

        var userName = user.UserName ?? Input.Email;
        var result = await signInManager.PasswordSignInAsync(userName, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Login realizado com sucesso.";
            return LocalRedirect(await ResolvePostLoginReturnUrlAsync(user));
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Usuario temporariamente bloqueado.");
            TempData["ErrorMessage"] = "Usuario temporariamente bloqueado.";
            return Page();
        }

        ModelState.AddModelError(string.Empty, "E-mail ou senha invalidos.");
        TempData["ErrorMessage"] = "E-mail ou senha invalidos.";
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await signInManager.SignOutAsync();
        TempData["SuccessMessage"] = "Sessao encerrada com sucesso.";
        return RedirectToPage("/Login");
    }

    private async Task<string> ResolvePostLoginReturnUrlAsync(AspNetCoreUser user)
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && ShouldPreserveReturnUrl(ReturnUrl))
        {
            return ReturnUrl;
        }

        return await ResolveDefaultReturnUrlAsync(user);
    }

    private async Task<string> ResolveDefaultReturnUrlAsync(AspNetCoreUser? user = null)
    {
        user ??= await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Url.Content("~/");
        }

        if (await userManager.IsInRoleAsync(user, "Desenvolvedor"))
        {
            return "/Admin/Auth/Usuarios";
        }

        return "/Admin";
    }

    private static bool ShouldPreserveReturnUrl(string returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        {
            return false;
        }

        return !string.Equals(returnUrl, "/", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(returnUrl, "/Admin", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(returnUrl, "/Admin/", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(returnUrl, "/Login", StringComparison.OrdinalIgnoreCase);
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail valido.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a senha.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Lembrar acesso")]
        public bool RememberMe { get; set; }
    }
}
