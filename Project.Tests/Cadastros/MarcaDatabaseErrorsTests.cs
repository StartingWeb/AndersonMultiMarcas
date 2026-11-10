using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Project.Pages.Admin.Cadastros.Marcas;
using Xunit;
using static Project.Tests.Cadastros.MarcaTestSupport;

namespace Project.Tests.Cadastros;

public sealed class MarcaDatabaseErrorsTests
{
    [Theory]
    [InlineData(2601, "IX_Marca_Nome", "dbo.Marca", true)]
    [InlineData(2627, "IX_Marca_Nome", "dbo.Marca", true)]
    [InlineData(2601, "ix_marca_nome", "Marca", true)]
    [InlineData(2601, "PK_Marca", "dbo.Marca", false)]
    [InlineData(2627, "PK_Marca", "dbo.Marca", false)]
    [InlineData(2601, "IX_Marca_Nome_Other", "dbo.Marca", false)]
    [InlineData(2627, "IX_Marca_Nome", "dbo.OutraTabela", false)]
    [InlineData(515, "IX_Marca_Nome", "dbo.Marca", false)]
    [InlineData(547, "IX_Marca_Nome", "dbo.Marca", false)]
    public void OnlyNameUniquenessOnMarcaIsRecognized(int number, string index, string table, bool expected)
    {
        var sql = SqlFailure((number, SqlMessage(number, index, table)));
        var original = new DbUpdateException("Synthetic save failure", sql);
        Assert.Equal(expected, MarcaDatabaseErrors.IsNameDuplicate(original));
        Assert.Same(sql, original.InnerException);
    }

    [Theory]
    [InlineData(2601)]
    [InlineData(2627)]
    public void DuplicateValueCannotImpersonateIndexOrTable(int number)
    {
        var message = SqlMessage(number, "PK_Marca").Replace("(ZEEKR)", "('IX_Marca_Nome' 'dbo.Marca')");
        Assert.False(MarcaDatabaseErrors.IsNameDuplicate(SqlFailure((number, message))));
        Assert.False(MarcaDatabaseErrors.IsNameDuplicateSqlError(number, "IX_Marca_Nome"));
    }

    [Fact]
    public void AllSqlErrorsAreCheckedAndNestedWrappersPreserveClassification()
    {
        var sql = SqlFailure((3621, "The statement has been terminated."), (2601, SqlMessage(2601)));
        Assert.True(MarcaDatabaseErrors.IsNameDuplicate(new DbUpdateException("Save failed", new Exception("Wrapper", sql))));
        Assert.False(MarcaDatabaseErrors.IsNameDuplicate(new DbUpdateException("Unknown persistence failure", new Win32Exception())));
    }
}
