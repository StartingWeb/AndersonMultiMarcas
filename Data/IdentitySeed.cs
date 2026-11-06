using Domain.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Data;

public static class IdentitySeed
{
    // Called only by the Development bootstrap. No existing identity is changed.
    public static async Task EnsureDeveloperUserAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        if (!string.Equals(configuration["DevelopmentBootstrap:Enabled"], "true", StringComparison.OrdinalIgnoreCase)) return;

        var email = Required(configuration, "Email");
        var userName = Required(configuration, "UserName");
        var password = Required(configuration, "Password");
        var userManager = services.GetRequiredService<UserManager<AspNetCoreUser>>();
        if (await userManager.FindByEmailAsync(email) is not null || await userManager.FindByNameAsync(userName) is not null)
            return;

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Desenvolvedor", "Administrador", "AdminConcessionaria" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                EnsureSuccess(await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() }), "criar perfil");
        }

        var user = new AspNetCoreUser
        {
            Id = Guid.NewGuid(), UserName = userName, Email = email,
            EmailConfirmed = true, LockoutEnabled = true,
            NomeCompleto = configuration["DevelopmentBootstrap:NomeCompleto"] ?? "Desenvolvedor local"
        };
        EnsureSuccess(await userManager.CreateAsync(user, password), "criar usuario local");
        EnsureSuccess(await userManager.AddToRoleAsync(user, "Desenvolvedor"), "vincular perfil local");
    }

    private static string Required(IConfiguration configuration, string key)
        => !string.IsNullOrWhiteSpace(configuration[$"DevelopmentBootstrap:{key}"])
            ? configuration[$"DevelopmentBootstrap:{key}"]!
            : throw new InvalidOperationException($"Configure DevelopmentBootstrap:{key} externamente para o bootstrap local.");

    private static void EnsureSuccess(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Erro ao {operation}: {string.Join(", ", result.Errors.Select(x => x.Description))}");
    }
}
