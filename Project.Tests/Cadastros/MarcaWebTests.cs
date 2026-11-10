using System.Net;
using System.Text.RegularExpressions;
using Data;
using Domain.Application;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Project.Tests.Security;
using Xunit;

namespace Project.Tests.Cadastros;

public sealed class MarcaWebTests(SecurityWebApplicationFactory factory) : IClassFixture<SecurityWebApplicationFactory>
{
    [Fact]
    public async Task AuthorizedListingRendersCountsAccentsAndOnlyTheContentTitle()
    {
        using var client = await LoginAsync(authorized: true);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var prefix = $"Interface-{Guid.NewGuid():N}";
        var empty = new Marca(prefix + " zero", null);
        var populated = new Marca(prefix + " vários", null);
        db.AddRange(empty, populated);
        await db.SaveChangesAsync();
        var sold = MarcaIndexTests.Vehicle(populated.Id);
        sold.MarcarComoVendido(DateTime.UtcNow);
        var deleted = MarcaIndexTests.Vehicle(populated.Id);
        deleted.Desativar();
        db.AddRange(MarcaIndexTests.Vehicle(populated.Id), sold, deleted);
        await db.SaveChangesAsync();

        using var response = await client.GetAsync("/Admin/Cadastros/Marcas?Filtro=" + prefix);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("utf-8", response.Content.Headers.ContentType!.CharSet);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("<title>Marcas - Anderson Multimarcas</title>", html);
        Assert.Single(Regex.Matches(html, "<h1[^>]*>.*?</h1>", RegexOptions.Singleline).Cast<Match>());
        var header = Regex.Match(html, "<header class=\"admin-topbar\">.*?</header>", RegexOptions.Singleline).Value;
        Assert.DoesNotContain("<strong>Marcas</strong>", header);
        Assert.Contains("Anderson Multimarcas", header);
        Assert.Contains("data-admin-toggle", header);
        Assert.Contains("bi-tags-fill me-2\"></i>Marcas</h1>", html);
        foreach (var text in new[] { "Lista e gerenciamento das marcas cadastradas", "Nova marca", "Veículos", "Ações", "Código", "Confirmar exclusão" })
            Assert.Contains(text, html);
        var rows = Regex.Matches(html, "<tr>.*?</tr>", RegexOptions.Singleline).Cast<Match>().Select(x => x.Value).ToArray();
        Assert.Contains("class=\"text-end vehicle-count\">0</td>", rows.Single(x => x.Contains(empty.Nome)));
        Assert.Contains("class=\"text-end vehicle-count\">2</td>", rows.Single(x => x.Contains(populated.Nome)));
        Assert.Contains("class=\"table-responsive\"", html);
        Assert.Contains($"/Admin/Cadastros/Marcas/Upsert/{populated.Id}", html);
        Assert.DoesNotContain('\ufffd', html);

        var otherPage = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Cadastros/Vendedores"));
        Assert.Contains("<strong>Vendedores</strong>", otherPage);
    }

    [Fact]
    public async Task EmptyListingKeepsItsHelpfulMessageAndSingleContentTitle()
    {
        using var client = await LoginAsync(authorized: true);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Cadastros/Marcas?Filtro=" + Guid.NewGuid()));
        Assert.Contains("Nenhuma marca encontrada", html);
        Assert.Contains("Ajuste os filtros e tente novamente.", html);
        Assert.DoesNotContain("<strong>Marcas</strong>", html);
    }

    [Fact]
    public async Task CreateAndEditRenderAccentedHelpLabelsAndValidation()
    {
        using var client = await LoginAsync(authorized: true);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = new Marca("Edição-" + Guid.NewGuid(), null);
        db.Add(brand);
        await db.SaveChangesAsync();
        foreach (var path in new[] { "/Admin/Cadastros/Marcas/Upsert", $"/Admin/Cadastros/Marcas/Upsert/{brand.Id}" })
        {
            var html = WebUtility.HtmlDecode(await client.GetStringAsync(path));
            Assert.Contains("Preencha as informações principais para exibição e filtros.", html);
            Assert.Contains("O nome deve ter no máximo 100 caracteres.", html);
            Assert.Contains("A URL deve ter no máximo 400 caracteres.", html);
            if (path.EndsWith($"/{brand.Id}")) Assert.Contains("Salvar alterações", html);
        }
        var form = await client.GetStringAsync($"/Admin/Cadastros/Marcas/Upsert/{brand.Id}");
        using var response = await client.PostAsync($"/Admin/Cadastros/Marcas/Upsert/{brand.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Marca.Id"] = brand.Id.ToString(), ["Marca.Nome"] = brand.Nome,
            ["Marca.LogoUrl"] = new string('x', 401), ["__RequestVerificationToken"] = Token(form)
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("A URL deve ter no máximo 400 caracteres.", result);
        Assert.Contains("Salvar alterações", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("over-limit")]
    public async Task RazorValidationRejectsInvalidNamesWithoutInserting(string input)
    {
        using var client = await LoginAsync(authorized: true);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await db.Marcas.CountAsync();
        var html = await client.GetStringAsync("/Admin/Cadastros/Marcas/Upsert");
        using var response = await client.PostAsync("/Admin/Cadastros/Marcas/Upsert", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Marca.Id"] = "0", ["Marca.Nome"] = input == "over-limit" ? new string('Z', 101) : input,
            ["Marca.LogoUrl"] = "", ["__RequestVerificationToken"] = Token(html)
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(input == "over-limit" ? "O nome deve ter no máximo 100 caracteres." : "Informe o nome da marca.", result);
        Assert.DoesNotContain("Marca cadastrada com sucesso.", result);
        Assert.Equal(count, await db.Marcas.CountAsync());
    }

    [Theory]
    [InlineData("/Admin/Cadastros/Marcas")]
    [InlineData("/Admin/Cadastros/Marcas/Upsert")]
    [InlineData("/Admin/Cadastros/Marcas/Upsert/1")]
    public async Task AnonymousUsersCannotOpenBrandRoutes(string path)
    {
        using var client = Client();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/Login", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/Admin/Cadastros/Marcas")]
    [InlineData("/Admin/Cadastros/Marcas/Upsert")]
    [InlineData("/Admin/Cadastros/Marcas/Upsert/1")]
    public async Task ProfileWithoutBrandPermissionCannotOpenOrPostBrandRoutes(string path)
    {
        using var client = await LoginAsync(authorized: false);
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
        {
            using var response = await client.SendAsync(new HttpRequestMessage(method, path));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("https://localhost/AccessDenied", response.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task AuthorizedPostStillRequiresAntiforgeryToken()
    {
        using var client = await LoginAsync(authorized: true);
        using var response = await client.PostAsync("/Admin/Cadastros/Marcas/Upsert", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Marca.Id"] = "0", ["Marca.Nome"] = "ZEEKR"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
    });

    private async Task<HttpClient> LoginAsync(bool authorized)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetCoreUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var user = new AspNetCoreUser
        {
            Id = Guid.NewGuid(), UserName = $"marca-{Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@example.test", EmailConfirmed = true
        };
        var password = $"Aa1!{Guid.NewGuid():N}";
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        var role = authorized ? "Administrador" : $"SemMarcas-{Guid.NewGuid():N}";
        if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        var client = Client();
        var html = await client.GetStringAsync("/Login");
        using var response = await client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = user.Email!, ["Input.Password"] = password, ["__RequestVerificationToken"] = Token(html)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static string Token(string html)
    {
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.False(string.IsNullOrEmpty(token));
        return token;
    }
}
