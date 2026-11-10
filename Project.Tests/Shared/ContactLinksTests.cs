using Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Project.Infrastructure.Storage;
using Project.Pages;
using Project.Pages.ViewModels;
using Project.Shared;
using Project.Tests.Security;
using Project.ViewComponents;
using Xunit;

namespace Project.Tests.Shared;

public sealed class ContactLinksTests(SecurityWebApplicationFactory factory) : IClassFixture<SecurityWebApplicationFactory>
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("(16) 99999-0000", "5516999990000")]
    [InlineData("1633330000", "551633330000")]
    [InlineData("+55 (16) 99999-0000", "5516999990000")]
    [InlineData("551633330000", "551633330000")]
    [InlineData("55999990000", "5555999990000")]
    public void InternationalLinksNormalizeBrazilianNumbersWithoutDuplicatingCountryCode(string? input, string expected)
        => Assert.Equal(expected, PhoneLinkHelper.ToInternational(input));

    [Fact]
    public async Task RenderedSellerModalUsesBrazilianInternationalNumberForLinkAndScript()
    {
        var model = new[] { new SellerContactModalStoreGroupViewModel("Loja teste",
            [new SellerContactModalSellerViewModel("Ana", "16999990000", null)]) };
        var html = await Render("/Pages/Shared/Components/SellerContactModal/Default.cshtml", model);
        Assert.Contains("href=\"https://wa.me/5516999990000\"", html);
        Assert.Contains("data-seller-phone=\"5516999990000\"", html);
    }

    [Fact]
    public async Task RenderedContactPageHasDialableStoreSellerAndWhatsAppLinks()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        using var scope = factory.Services.CreateScope();
        var model = new ContatoModel(db, scope.ServiceProvider.GetRequiredService<IStorageImageResolver>());
        typeof(ContatoModel).GetProperty(nameof(ContatoModel.Lojas))!.SetValue(model,
            new[] { new ContactStoreViewModel { Nome = "Loja teste", Telefone = "1633330000",
                Vendedores = [new ContactSellerViewModel { Nome = "Ana", Telefone = "16999990000" }] } });
        var html = await Render("/Pages/Contato.cshtml", model);
        Assert.Contains("href=\"tel:+551633330000\"", html);
        Assert.Contains("href=\"tel:+5516999990000\"", html);
        Assert.Contains("href=\"https://wa.me/5516999990000?text=", html);
        Assert.DoesNotContain("@loja.Telefone", html);
    }

    private async Task<string> Render<TModel>(string path, TModel model)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var http = new DefaultHttpContext { RequestServices = services };
        var action = new ActionContext(http, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var view = services.GetRequiredService<IRazorViewEngine>().GetView(null, path, false);
        Assert.True(view.Success, string.Join(", ", view.SearchedLocations ?? []));
        var data = new ViewDataDictionary<TModel>(services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
        if (view.View is RazorView { RazorPage: Microsoft.AspNetCore.Mvc.RazorPages.Page page })
            page.PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext(action) { ViewData = data };
        using var output = new StringWriter();
        await view.View.RenderAsync(new ViewContext(action, view.View, data,
            new TempDataDictionary(http, services.GetRequiredService<ITempDataProvider>()), output, new HtmlHelperOptions()));
        return output.ToString();
    }
}
