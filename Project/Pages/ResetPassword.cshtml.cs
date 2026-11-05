using System.ComponentModel.DataAnnotations;
using System.Text;
using Domain.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Project.Pages;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ResetPasswordModel(UserManager<AspNetCoreUser> userManager) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid UserId { get; set; }
    [BindProperty(SupportsGet = true)] public string Code { get; set; } = string.Empty;
    [BindProperty, Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [BindProperty, Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = string.Empty;
    public bool Completed { get; private set; }

    public void OnGet() => SetPrivacyHeaders();

    public async Task<IActionResult> OnPostAsync()
    {
        SetPrivacyHeaders();
        if (!ModelState.IsValid) return Page();
        var user = await userManager.FindByIdAsync(UserId.ToString());
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code)); }
        catch (FormatException) { ModelState.AddModelError(string.Empty, "Link invalido ou expirado."); return Page(); }
        if (user is null) { ModelState.AddModelError(string.Empty, "Link invalido ou expirado."); return Page(); }
        var result = await userManager.ResetPasswordAsync(user, token, Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Link invalido ou expirado, ou senha fora dos requisitos (12 caracteres, maiuscula, minuscula, numero e simbolo).");
            return Page();
        }
        Completed = true;
        return Page();
    }

    private void SetPrivacyHeaders()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        ViewData["Robots"] = "noindex,nofollow";
        ViewData["Title"] = "Escolher nova senha";
    }
}
