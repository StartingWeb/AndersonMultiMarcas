using System.Net;
using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.SqlClient;
using Project.Tests.Database;
using Project.Features.Veiculos.DTOs;
using Project.Shared;
using Project.Tests.Security;
using Xunit;

namespace Project.Tests.Shared;

public sealed class VehicleTitleTests(SecurityWebApplicationFactory factory) : IClassFixture<SecurityWebApplicationFactory>
{
    [Theory]
    [InlineData("Volkswagen", "T-Cross", "Volkswagen T-Cross")]
    [InlineData("Renault", "Kwid", "Renault Kwid")]
    [InlineData("Volkswagen", null, "Volkswagen")]
    [InlineData("Volkswagen", "", "Volkswagen")]
    [InlineData(" Volkswagen ", "  \t ", "Volkswagen")]
    [InlineData(" Volkswagen ", " T-Cross  ", "Volkswagen T-Cross")]
    [InlineData("Land   Rover", " Range   Rover ", "Land Rover Range Rover")]
    public async Task CatalogCardRendersBrandAndModelWithLegacyFallback(string brand, string? model, string expected)
    {
        // Titulo deliberately differs from MarcaNome: the brand relationship is authoritative.
        var item = new VeiculoListItemDto
        {
            Id = 123, Titulo = "Titulo legado", MarcaNome = brand, Modelo = model!,
            Cor = "Branco", AnoFabricacao = 2023, AnoModelo = 2024,
            Cambio = Cambio.Automatico, Combustivel = Combustivel.Flex,
            PrecoVenda = 105900, Seminovo = true, EstaDisponivel = true,
            MidiaCapaUrl = "/img/carroDefault.png"
        };
        var html = WebUtility.HtmlDecode(await RenderCard(item));
        Assert.Contains($"<div class=\"vehicle-name\">{expected}</div>", html);
        Assert.Contains($"alt=\"{expected}\"", html);
        Assert.Contains($"aria-label=\"Abrir detalhes de {expected}\"", html);
        Assert.Contains($"data-vehicle-title=\"{expected}\"", html);
        Assert.Contains("href=\"/veiculo/123/\"", html);
        Assert.Contains("src=\"/img/carroDefault.png\"", html);
        Assert.Contains("Branco - 2023 / 2024", html);
        Assert.Contains("Automatico", html);
        Assert.Contains("Flex", html);
        Assert.Contains("105.900,00", html);
        Assert.Contains("Disponível", html);
        Assert.Contains("data-whatsapp-modal=\"true\"", html);
        Assert.Contains("Tenho interesse", html);
        Assert.DoesNotContain("null", html);
        Assert.DoesNotContain("Titulo legado", html);
    }

    [Fact]
    public void HomeTitleKeepsExistingVersionAndNormalizesWhitespace()
        => Assert.Equal("Volkswagen T-Cross Comfortline 1.0",
            VehicleTitleHelper.Format(" Volkswagen ", " T-Cross ", " Comfortline   1.0 "));

    [SqlServerFact]
    public async Task PublicPagesCarryModelFromDatabaseThroughFiltersAndAllHomeSections()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL"))
        {
            InitialCatalog = "AndersonTitles_" + Guid.NewGuid().ToString("N")
        };
        await using var sqlFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection.ConnectionString));
        }));
        using var databaseScope = sqlFactory.Services.CreateScope();
        var database = databaseScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            await database.Database.EnsureCreatedAsync();
            using (var scope = sqlFactory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var store = new Loja("Loja teste", "Empresa teste", null, new Email("loja@example.test"), new Telefone("16999990000"),
                    new Endereco("Rua teste", "1", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));
                var volkswagen = new Marca("Volkswagen", null);
                var renault = new Marca("Renault", null);
                db.AddRange(store, volkswagen, renault);
                await db.SaveChangesAsync();
                var first = new Veiculo(store.Id, volkswagen.Id, "Volkswagen", "T-Cross", 2024, new Dinheiro(105900));
                first.AtualizarComercial(true, true, true, false, null, null, null);
                first.Update("Volkswagen", "T-Cross", "Comfortline", 2023, 2024, Combustivel.Eletrico, Cambio.Automatico, 0, null, "Branco", null);
                // Cover the separate electric motorcycle projection as well.
                db.Entry(first).Property(v => v.MotoEletrica).CurrentValue = true;
                db.AddRange(first, new Veiculo(store.Id, renault.Id, "Renault", "Kwid", 2027, new Dinheiro(79900)));
                await db.SaveChangesAsync();
            }
            using var client = sqlFactory.CreateClient();
            foreach (var path in new[] { "/veiculos", "/veiculos?Busca=Kwid", "/veiculos?Marca=Renault", "/veiculos/zero-km" })
            {
                var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
                Assert.Contains("<div class=\"vehicle-name\">Renault Kwid</div>", html);
                if (path.Contains('?')) Assert.DoesNotContain("<div class=\"vehicle-name\">Volkswagen", html);
            }
            var home = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
            Assert.Equal(4, home.Split("<div class=\"vehicle-name\">Volkswagen T-Cross Comfortline</div>").Length - 1);
        }
        finally { await database.Database.EnsureDeletedAsync(); }
    }

    private async Task<string> RenderCard(VeiculoListItemDto model)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var http = new DefaultHttpContext { RequestServices = services };
        var action = new ActionContext(http, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var view = services.GetRequiredService<IRazorViewEngine>().GetView(null, "/Pages/Veiculos/Partials/_CatalogVehicleCard.cshtml", false);
        Assert.True(view.Success);
        var data = new ViewDataDictionary<VeiculoListItemDto>(services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
        using var output = new StringWriter();
        await view.View.RenderAsync(new ViewContext(action, view.View, data,
            new TempDataDictionary(http, services.GetRequiredService<ITempDataProvider>()), output, new HtmlHelperOptions()));
        return output.ToString();
    }
}
