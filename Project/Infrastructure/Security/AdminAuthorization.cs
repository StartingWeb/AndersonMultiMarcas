using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Project.Shared;

namespace Project.Infrastructure.Security;

public static class AdminAuthorization
{
    public const string AccessPolicy = "AdminAccess";
    public const string MenuPolicyPrefix = "AdminMenu:";
    private static readonly string[] AdministratorRoles = ["Desenvolvedor", "Administrador", "AdminConcessionaria"];

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(AccessPolicy, policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasAdminAccess(context.User)));
        foreach (var item in AdminMenuCatalog.AllItems)
        {
            options.AddPolicy(MenuPolicyPrefix + item.Id, policy => policy.RequireAuthenticatedUser()
                .RequireAssertion(context => CanAccess(context.User, item)));
        }
    }

    public static bool HasAdminAccess(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        && (AdministratorRoles.Any(user.IsInRole)
            || (user.Claims.Any(x => x.Type == ClaimTypes.Role)
                && user.Claims.Any(x => x.Type == AdminMenuCatalog.ClaimType && AdminMenuCatalog.AllItems.Any(item => item.Id == x.Value))));

    public static bool CanAccess(ClaimsPrincipal user, AdminMenuItem item)
    {
        if (!HasAdminAccess(user)) return false;
        if (user.IsInRole("Desenvolvedor")) return true;
        if (item.DeveloperOnly) return false;
        if (item.RequiredRoles is { Count: > 0 } && !item.RequiredRoles.Any(user.IsInRole)) return false;
        var permissions = user.FindAll(AdminMenuCatalog.ClaimType).ToArray();
        return permissions.Length > 0
            ? permissions.Any(x => string.Equals(x.Value, item.Id, StringComparison.OrdinalIgnoreCase))
            : AdministratorRoles.Any(user.IsInRole);
    }
}
