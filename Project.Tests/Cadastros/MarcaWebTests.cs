using System.Net;
using System.Text.RegularExpressions;
using Core.Storage;
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
    public async Task MultipartUploadPersistsLogoAndRendersItsPublicUrlInListing()
    {
        using var client = await LoginAsync(authorized: true);
        var name = "Upload-" + Guid.NewGuid().ToString("N");
        var form = await client.GetStringAsync("/Admin/Cadastros/Marcas/Upsert");
        var file = MarcaTestSupport.LogoFile();
        await using var bytes = new MemoryStream();
        await file.CopyToAsync(bytes);
        using var multipart = new MultipartFormDataContent
        {
            { new StringContent("0"), "Marca.Id" },
            { new StringContent(name), "Marca.Nome" },
            { new StringContent(Token(form)), "__RequestVerificationToken" },
            { new ByteArrayContent(bytes.ToArray()), "LogoArquivo", "logo.png" }
        };
        using var response = await client.PostAsync("/Admin/Cadastros/Marcas/Upsert", multipart);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = await db.Marcas.AsNoTracking().SingleAsync(x => x.Nome == name);
        Assert.StartsWith("https://storage.example.test/uploads/marcas/", brand.LogoUrl);
        var key = new Uri(brand.LogoUrl!).AbsolutePath.TrimStart('/');
        Assert.True(await scope.ServiceProvider.GetRequiredService<IStorageService>().ExistsAsync(key, default));
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Cadastros/Marcas?Filtro=" + name));
        Assert.Contains($"<img src=\"{brand.LogoUrl}\"", html);
    }

    [Fact]
    public async Task EditWithoutFileKeepsStoredLogoAndIgnoresForgedUrlFields()
    {
        using var client = await LoginAsync(authorized: true);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = new Marca("Preservar-" + Guid.NewGuid().ToString("N"), "/img/logo.png");
        db.Add(brand); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var path = $"/Admin/Cadastros/Marcas/Upsert/{brand.Id}";
        var html = await client.GetStringAsync(path);
        Assert.Contains("src=\"/img/logo.png\"", html);
        using var multipart = new MultipartFormDataContent
        {
            { new StringContent(brand.Id.ToString()), "Marca.Id" },
            { new StringContent(brand.Nome + " editada"), "Marca.Nome" },
            { new StringContent("https://untrusted.example.test/forged.png"), "Marca.LogoUrl" },
            { new StringContent(Token(html)), "__RequestVerificationToken" }
        };
        using var response = await client.PostAsync(path, multipart);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await db.Marcas.AsNoTracking().SingleAsync(x => x.Id == brand.Id);
        Assert.Equal("/img/logo.png", saved.LogoUrl);
        Assert.Equal(brand.Nome + " editada", saved.Nome);
    }

    [Theory]
    [InlineData("/uploads/marcas/synthetic-logo.png")]
    [InlineData("https://images.example.test/logo.png")]
    [InlineData(null)]
    public async Task ListingRendersLogoAsImageAndHandlesMissingLogo(string? logo)
    {
        using var client = await LoginAsync(authorized: true);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = new Marca("Logo-" + Guid.NewGuid().ToString("N"), logo);
        db.Add(brand);
        await db.SaveChangesAsync();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Cadastros/Marcas?Filtro=" + brand.Nome));
        var row = Regex.Matches(html, "<tr>.*?</tr>", RegexOptions.Singleline).Cast<Match>()
            .Single(x => x.Value.Contains(brand.Nome)).Value;
        Assert.Contains("Sem logo", row);
        if (logo is null)
        {
            Assert.DoesNotContain("<img", row);
        }
        else
        {
            Assert.Contains($"<img src=\"{logo}\"", row);
            Assert.Contains($"alt=\"Logo da marca {brand.Nome}\"", row);
            Assert.Contains("class=\"brand-logo\"", row);
            Assert.Contains("width=\"48\" height=\"48\"", row);
            Assert.Contains("loading=\"lazy\"", row);
            Assert.Contains("onerror=\"this.hidden=true;this.nextElementSibling.hidden=false;\"", row);
            Assert.DoesNotContain($">{logo}</", row);
        }
    }

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
            Assert.Contains("enctype=\"multipart/form-data\"", html);
            Assert.Contains("type=\"file\"", html);
            Assert.Contains("name=\"LogoArquivo\"", html);
            Assert.Contains("JPG, PNG ou WebP, até 5 MB.", html);
            Assert.DoesNotContain("name=\"Marca.LogoUrl\"", html);
            if (path.EndsWith($"/{brand.Id}")) Assert.Contains("Salvar alterações", html);
        }
        var form = await client.GetStringAsync($"/Admin/Cadastros/Marcas/Upsert/{brand.Id}");
        using var multipart = new MultipartFormDataContent
        {
            { new StringContent(brand.Id.ToString()), "Marca.Id" },
            { new StringContent(brand.Nome), "Marca.Nome" },
            { new StringContent(Token(form)), "__RequestVerificationToken" },
            { new ByteArrayContent("invalid image"u8.ToArray()), "LogoArquivo", "fake.png" }
        };
        using var response = await client.PostAsync($"/Admin/Cadastros/Marcas/Upsert/{brand.Id}", multipart);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("O arquivo não é uma imagem válida. Use JPG, PNG ou WebP.", result);
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
