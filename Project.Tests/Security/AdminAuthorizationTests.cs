using System.Security.Claims;
using Project.Infrastructure.Security;
using Project.Shared;
using Xunit;

namespace Project.Tests.Security;

public sealed class AdminAuthorizationTests
{
    [Theory]
    [InlineData("Administrador", "veiculos", true)]
    [InlineData("AdminConcessionaria", "veiculos", true)]
    [InlineData("Administrador", "storage-importar-imagens", false)]
    [InlineData("Desenvolvedor", "storage-importar-imagens", true)]
    [InlineData("Visitante", "veiculos", false)]
    public void ProfilesEnforceSensitiveMenus(string role, string menu, bool expected)
        => Assert.Equal(expected, AdminAuthorization.CanAccess(User(new Claim(ClaimTypes.Role, role)), Item(menu)));

    [Fact]
    public void ConfiguredPermissionsRestrictAdministrators()
    {
        var user = User(new Claim(ClaimTypes.Role, "Administrador"), new Claim(AdminMenuCatalog.ClaimType, "marcas"));
        Assert.False(AdminAuthorization.CanAccess(user, Item("veiculos")));
        Assert.True(AdminAuthorization.CanAccess(user, Item("marcas")));
    }

    [Fact]
    public void CustomRoleNeedsExplicitPermission()
    {
        var user = User(new Claim(ClaimTypes.Role, "Estoque"), new Claim(AdminMenuCatalog.ClaimType, "veiculos"));
        Assert.True(AdminAuthorization.CanAccess(user, Item("veiculos")));
        Assert.False(AdminAuthorization.CanAccess(user, Item("vendas-veiculos")));
    }

    private static ClaimsPrincipal User(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));
    private static AdminMenuItem Item(string id) => AdminMenuCatalog.AllItems.Single(x => x.Id == id);
}
