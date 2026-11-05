using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Data;
using Domain.Application;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Project.Features.Veiculos.DTOs;
using Xunit;

namespace Project.Tests.Security;

public sealed class VehicleApiAuthorizationTests : IClassFixture<SecurityWebApplicationFactory>
{
    private readonly SecurityWebApplicationFactory factory;
    public VehicleApiAuthorizationTests(SecurityWebApplicationFactory factory) => this.factory = factory;

    [Theory]
    [InlineData("POST", "/api/veiculos")]
    [InlineData("PUT", "/api/veiculos/1")]
    [InlineData("PATCH", "/api/veiculos/1/vender")]
    [InlineData("POST", "/api/veiculos/1/midias")]
    public async Task AnonymousWritesReturn401(string method, string path)
    {
        using var client = CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/veiculos")]
    [InlineData("PUT", "/api/veiculos/1")]
    [InlineData("PATCH", "/api/veiculos/1/vender")]
    [InlineData("POST", "/api/veiculos/1/midias")]
    public async Task AuthenticatedWithoutProfileReturns403(string method, string path)
    {
        using var client = await LoginAsync();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/veiculos")]
    [InlineData("PUT", "/api/veiculos/1")]
    [InlineData("PATCH", "/api/veiculos/1/vender")]
    [InlineData("POST", "/api/veiculos/1/midias")]
    public async Task AuthorizedCookieWithoutCsrfReturns400(string method, string path)
    {
        using var client = await LoginAsync("Administrador");
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PublicReadsRemainAnonymous()
    {
        using var client = CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/veiculos")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/veiculos/inexistente")).StatusCode);
    }

    [Fact]
    public async Task AuthorizedUserCanCreateEditAndSellWithCsrf()
    {
        using var client = await LoginAsync("Administrador");
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/security/antiforgery");
        client.DefaultRequestHeaders.Add(csrf!.HeaderName, csrf.Token);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var loja = new Loja("Loja sintética", "Teste", null, new Email("loja@example.test"), new Telefone("16999990000"), new Endereco("Rua A", "1", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));
        var marca = new Marca("Marca sintética", null);
        db.AddRange(loja, marca);
        await db.SaveChangesAsync();
        using var create = await client.PostAsJsonAsync("/api/veiculos", new VeiculoCreateDto
        {
            LojaId = loja.Id, MarcaId = marca.Id, Modelo = "Modelo teste", Titulo = "Veículo sintético", AnoModelo = 2026, PrecoVenda = 10000
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CreatedResponse>();
        using var update = await client.PutAsJsonAsync($"/api/veiculos/{created!.Id}", new VeiculoUpdateDto
        {
            Id = created.Id, LojaId = loja.Id, MarcaId = marca.Id, Modelo = "Modelo atualizado", Titulo = "Veículo sintético", AnoModelo = 2026, PrecoVenda = 12000
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        using var invalidMultipart = new MultipartFormDataContent();
        using var invalidImage = new ByteArrayContent([1, 2, 3, 4]);
        invalidImage.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        invalidMultipart.Add(invalidImage, "arquivos", "invalid.png");
        using var invalidUpload = await client.PostAsync($"/api/veiculos/{created.Id}/midias", invalidMultipart);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpload.StatusCode);
        Assert.False(await db.VeiculoMidias.AnyAsync(x => x.VeiculoId == created.Id));
        using var multipart = new MultipartFormDataContent();
        using var image = new ByteArrayContent(new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 1, 1).ToByteArray(ImageMagick.MagickFormat.Png));
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        multipart.Add(image, "arquivos", "synthetic.png");
        using var upload = await client.PostAsync($"/api/veiculos/{created.Id}/midias", multipart);
        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
        using var sell = await client.PatchAsync($"/api/veiculos/{created.Id}/vender", null);
        Assert.Equal(HttpStatusCode.NoContent, sell.StatusCode);
        db.ChangeTracker.Clear();
        var vehicle = await db.Veiculos.FindAsync(created.Id);
        Assert.Equal("Modelo atualizado", vehicle!.Modelo);
        Assert.True(vehicle.Vendido);
    }

    [Fact]
    public async Task DirectAdminRoutesDenyUsersWithoutProfile()
    {
        using var client = await LoginAsync();
        using var response = await client.GetAsync("/Admin/Veiculo/Upsert");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/AccessDenied", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task LoginLocksAfterFiveFailures()
    {
        using var client = await LoginAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetCoreUser>>();
        var user = await users.FindByEmailAsync(client.DefaultRequestHeaders.GetValues("X-Test-Email").Single());
        // A separate browser avoids the existing session and exercises the real login handler.
        using var anonymous = CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var token = await LoginTokenAsync(anonymous);
            await anonymous.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = user!.Email!, ["Input.Password"] = Guid.NewGuid().ToString(), ["__RequestVerificationToken"] = token
            }));
        }
        var updated = await users.FindByIdAsync(user!.Id.ToString());
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Entry(updated!).ReloadAsync();
        Assert.True(await users.IsLockedOutAsync(updated!));
    }

    [Fact]
    public async Task RestrictedProfileCannotWriteVehiclesOrEnterDeveloperStorage()
    {
        using var client = await LoginAsync($"Estoque-{Guid.NewGuid():N}", "marcas");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/veiculos", new { })).StatusCode);
        using var admin = await LoginAsync("Administrador");
        var response = await admin.GetAsync("/Admin/Storage/ImportarImagens");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task PasswordResetPageValidatesTokenBeforeChangingPassword()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetCoreUser>>();
        var password = $"Aa1!{Guid.NewGuid():N}";
        var user = new AspNetCoreUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N"), Email = $"{Guid.NewGuid():N}@example.test" };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var code = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token));
        using var client = CreateClient();
        var html = await client.GetStringAsync($"/ResetPassword?userId={user.Id}&code={code}");
        Assert.True(await users.CheckPasswordAsync(user, password));
        var csrf = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var replacement = $"Aa1!{Guid.NewGuid():N}";
        using var invalid = await client.PostAsync("/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = user.Id.ToString(), ["Code"] = "invalid", ["Password"] = replacement,
            ["ConfirmPassword"] = replacement, ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Entry(user).ReloadAsync();
        Assert.True(await users.CheckPasswordAsync(user, password));
        using var valid = await client.PostAsync("/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = user.Id.ToString(), ["Code"] = code, ["Password"] = replacement,
            ["ConfirmPassword"] = replacement, ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.Contains("Senha atualizada", await valid.Content.ReadAsStringAsync());
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Entry(user).ReloadAsync();
        Assert.True(await users.CheckPasswordAsync(user, replacement));
    }

    [Fact]
    public async Task CoverReviewJsonSaveRequiresConfiguredAntiforgeryHeader()
    {
        using var client = await LoginAsync("Administrador");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var store = new Loja("Loja capa sintética", "Teste", null, new Email("cover@example.test"), new Telefone("16999990000"),
            new Endereco("Rua A", "1", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));
        var brand = new Marca("Marca capa sintética", null);
        db.AddRange(store, brand); await db.SaveChangesAsync();
        var vehicle = new Veiculo(store.Id, brand.Id, "Teste capa", "Modelo", 2026, new Dinheiro(10000));
        db.Add(vehicle); await db.SaveChangesAsync();
        var media = new VeiculoMidia(vehicle.Id, "cover.webp", "/uploads/veiculos/synthetic-cover.webp", TipoMidia.Imagem, 1);
        db.Add(media); await db.SaveChangesAsync();
        var html = await client.GetStringAsync($"/Admin/Ferramentas/RevisarCapasVeiculos?Mode=Todos&Busca={vehicle.Id}");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.False(string.IsNullOrEmpty(token));
        const string endpoint = "/Admin/Ferramentas/RevisarCapasVeiculos?handler=SaveCover";
        var payload = new { vehicleId = vehicle.Id, mediaId = media.Id, position = 1, mode = "Todos", busca = vehicle.Id.ToString() };
        client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
        using var wrongHeader = await client.PostAsJsonAsync(endpoint, payload);
        Assert.Equal(HttpStatusCode.BadRequest, wrongHeader.StatusCode);
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        using var saved = await client.PostAsJsonAsync(endpoint, payload);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var result = System.Text.Json.JsonDocument.Parse(await saved.Content.ReadAsStringAsync());
        Assert.True(result.RootElement.GetProperty("success").GetBoolean());
        db.ChangeTracker.Clear();
        Assert.True((await db.VeiculoMidias.SingleAsync(x => x.Id == media.Id)).Capa);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    private async Task<HttpClient> LoginAsync(string? role = null, string? permission = null)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetCoreUser>>();
        var user = new AspNetCoreUser { Id = Guid.NewGuid(), UserName = $"synthetic-{Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@example.test", EmailConfirmed = true, LockoutEnabled = true };
        var password = $"Aa1!{Guid.NewGuid():N}";
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        if (role is not null)
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
            if (permission is not null)
            {
                var configuredRole = await roles.FindByNameAsync(role);
                Assert.True((await roles.AddClaimAsync(configuredRole!, new System.Security.Claims.Claim(Project.Shared.AdminMenuCatalog.ClaimType, permission))).Succeeded);
            }
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        var client = CreateClient();
        var token = await LoginTokenAsync(client);
        using var response = await client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = user.Email!, ["Input.Password"] = password, ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Email", user.Email);
        return client;
    }

    private static async Task<string> LoginTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/Login");
        return WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    }

    private sealed record CsrfResponse(string Token, string HeaderName);
    private sealed record CreatedResponse(int Id);
}
