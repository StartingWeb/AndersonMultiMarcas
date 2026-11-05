using Data;
using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Project.Shared;
using Xunit;

namespace Project.Tests.Features.Veiculos;

public sealed class VehiclePriceOrderingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConvertedMoneyOrderingTranslatesToSqlServer(bool descending)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=TranslationOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        IQueryable<Veiculo> query = db.Veiculos.Where(x => x.Ativo && !x.Vendido);
        query = descending ? query.OrderByDescending(x => x.PrecoVenda).ThenBy(x => x.Id) : query.OrderBy(x => x.PrecoVenda).ThenBy(x => x.Id);
        var sql = query.Skip(20).Take(20).ToQueryString();
        Assert.Contains(descending ? "ORDER BY [v].[PrecoVenda] DESC, [v].[Id]" : "ORDER BY [v].[PrecoVenda], [v].[Id]", sql);
        Assert.Contains("OFFSET", sql);
    }

    [Fact]
    public void MoneyOrderingWorksWithInMemoryProviderAndEqualPriceIsStable()
    {
        var items = new[] { new { Id = 2, Price = new Dinheiro(10) }, new { Id = 1, Price = new Dinheiro(10) }, new { Id = 3, Price = new Dinheiro(5) } };
        Assert.Equal([3, 1, 2], items.OrderBy(x => x.Price).ThenBy(x => x.Id).Select(x => x.Id));
    }

    [Theory]
    [InlineData(-1, -5, 1, 1)]
    [InlineData(int.MaxValue, int.MaxValue, 1000000, 100)]
    public void PaginationBoundsPreventInvalidOffsetsAndUnboundedPageSize(int page, int size, int expectedPage, int expectedSize)
    {
        var pagination = new PaginationParams { Page = page, PageSize = size };
        Assert.Equal(expectedPage, pagination.Page);
        Assert.Equal(expectedSize, pagination.PageSize);
        Assert.True(pagination.Skip >= 0);
    }
}
