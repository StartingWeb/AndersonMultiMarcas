using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Data;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Project.Pages.Admin.Cadastros.Marcas;
using Xunit;
using static Project.Tests.Cadastros.MarcaTestSupport;

namespace Project.Tests.Cadastros;

public sealed class MarcaUpsertTests
{
    [Theory]
    [InlineData("ZEEKR", "ZEEKR")]
    [InlineData("TESTE-MARCA-AUDITORIA-20261110", "TESTE-MARCA-AUDITORIA-20261110")]
    [InlineData(" \u00a0ZEEKR\t ", "ZEEKR")]
    public async Task NewBrandPersistsNormalizedActiveNameWithoutLogo(string input, string expected)
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        var page = Page(db, input);
        db.BeforeSave = () => Assert.False(page.TempData.ContainsKey("SuccessMessage"));
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(default));
        db.ChangeTracker.Clear();
        var brand = await db.Marcas.SingleAsync();
        Assert.Equal(expected, brand.Nome);
        Assert.True(brand.Ativo);
        Assert.Null(brand.LogoUrl);
        Assert.Equal(1, db.SaveCalls);
        Assert.Equal("Marca cadastrada com sucesso.", page.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("over-limit")]
    public async Task InputAnnotationsPreventInvalidNameFromReachingSave(string input)
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        var page = Page(db, input == "over-limit" ? new string('Z', 101) : input);
        page.TempData["SuccessMessage"] = "Previous request";
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(page.Marca, new ValidationContext(page.Marca), errors, validateAllProperties: true));
        foreach (var error in errors)
            foreach (var member in error.MemberNames)
                page.ModelState.AddModelError($"Marca.{member}", error.ErrorMessage!);

        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.False(page.ModelState.IsValid);
        Assert.Equal(0, db.SaveCalls);
        Assert.Empty(await db.Marcas.ToListAsync());
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditWithoutUploadPreservesLogoAndOnlyAnnouncesSuccessAfterSave(bool inactive)
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        var brand = new Marca("Nome anterior", "/old.png");
        if (inactive) brand.Desativar();
        db.Marcas.Add(brand); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var page = Page(db, " ZEEKR ", brand.Id);
        db.BeforeSave = () => Assert.False(page.TempData.ContainsKey("SuccessMessage"));
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(default));
        db.ChangeTracker.Clear();
        var saved = await db.Marcas.SingleAsync();
        Assert.Equal("ZEEKR", saved.Nome);
        Assert.Equal("/old.png", saved.LogoUrl);
        Assert.Equal(!inactive, saved.Ativo);
        Assert.Equal("Marca atualizada com sucesso.", page.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData(2601, true, false)]
    [InlineData(2627, true, false)]
    [InlineData(2601, false, false)]
    [InlineData(2627, false, false)]
    [InlineData(2601, true, true)]
    [InlineData(2627, false, true)]
    public async Task RealNameViolationReportsVisibleActiveOrInactiveConflict(int number, bool active, bool editing)
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        var existing = new Marca("ZEEKR", null);
        if (!active) existing.Desativar();
        db.Add(existing);
        var edited = new Marca("Original", null);
        if (editing) db.Add(edited);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var exception = new DbUpdateException("Synthetic SQL save failure", SqlFailure((number, SqlMessage(number))));
        db.Failure = exception;
        var logger = new TestLogger();
        var page = Page(db, " ZEEKR ", editing ? edited.Id : 0, logger);
        page.TempData["SuccessMessage"] = "Previous request";
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal(active ? DuplicateMessage : InactiveMessage, Error(page));
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
        if (editing) Assert.Equal("Editar marca", page.PageTitle);
        db.ChangeTracker.Clear();
        Assert.Equal(editing ? 2 : 1, await db.Marcas.CountAsync());
        Assert.Equal(active, (await db.Marcas.SingleAsync(x => x.Id == existing.Id)).Ativo);
        if (editing) Assert.Equal("Original", (await db.Marcas.SingleAsync(x => x.Id == edited.Id)).Nome);
        Assert.Same(exception, Assert.Single(logger.Entries).Exception);
    }

    [Theory]
    [InlineData(515, false)]
    [InlineData(547, false)]
    [InlineData(2601, false)]
    [InlineData(2627, false)]
    [InlineData(0, false)]
    [InlineData(515, true)]
    [InlineData(547, true)]
    [InlineData(2601, true)]
    [InlineData(2627, true)]
    [InlineData(0, true)]
    public async Task OtherPersistenceFailuresStayGenericPreserveExceptionAndNeverAnnounceSuccess(int number, bool editing)
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        var existing = new Marca("Original", null);
        if (editing) { db.Add(existing); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
        var sqlMessage = number switch
        {
            515 => "Cannot insert the value NULL into column 'LogoUrl', table 'Disposable.dbo.Marca'; column does not allow nulls. INSERT fails.",
            547 => "The INSERT statement conflicted with the FOREIGN KEY constraint 'FK_Synthetic'. The conflict occurred in database 'Disposable'.",
            _ => SqlMessage(number, number == 2601 ? "IX_Marca_Other" : "PK_Marca")
        };
        Exception? inner = number == 0 ? null : SqlFailure((number, sqlMessage));
        var exception = new DbUpdateException("Synthetic internal details", inner);
        db.Failure = exception;
        var logger = new TestLogger();
        var page = Page(db, "ZEEKR", editing ? existing.Id : 0, logger);
        page.TempData["SuccessMessage"] = "Previous request";
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal(GenericMessage, Error(page));
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
        var log = Assert.Single(logger.Entries);
        Assert.Same(exception, log.Exception);
        Assert.DoesNotContain("ZEEKR", log.Message);
        Assert.DoesNotContain("Synthetic internal details", Error(page));
        db.ChangeTracker.Clear();
        Assert.Equal(editing ? 1 : 0, await db.Marcas.CountAsync());
        if (editing) Assert.Equal("Original", (await db.Marcas.SingleAsync()).Nome);
    }

    [Fact]
    public async Task UnwrappedSqlFailureIsAlsoLoggedAndHiddenFromUser()
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        db.Failure = SqlFailure((515, "Synthetic NOT NULL failure"));
        var logger = new TestLogger();
        var page = Page(db, logger: logger);
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal(GenericMessage, Error(page));
        Assert.Same(db.Failure, Assert.Single(logger.Entries).Exception);
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
    }

    [Fact]
    public async Task MissingEditTargetDoesNotCreateBrandOrAnnounceSuccess()
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        var page = Page(db, id: 123);
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(default));
        Assert.Equal(0, db.SaveCalls);
        Assert.Empty(await db.Marcas.ToListAsync());
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
    }

    [Fact]
    public async Task ConflictOutsideQueryFilterIsNotDisclosed()
    {
        await using var db = new FilteredFailureContext(MemoryOptions());
        var hidden = new Marca("ZEEKR", null); hidden.Desativar();
        db.Add(hidden); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.Fail = true;
        var page = Page(db);
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal(GenericMessage, Error(page));
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
        Assert.False((await db.Marcas.IgnoreQueryFilters().SingleAsync()).Ativo);
    }

    [Fact]
    public async Task ConflictLookupFailureKeepsOriginalExceptionAndReturnsGenericMessage()
    {
        var lookupFailure = SqlFailure((53, "Synthetic lookup unavailable"));
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new FailedLookupInterceptor(lookupFailure)).Options;
        await using var db = new SaveProbeContext(options);
        db.Failure = new DbUpdateException("Original save failure", SqlFailure((2601, SqlMessage(2601))));
        var logger = new TestLogger();
        var page = Page(db, logger: logger);
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal(GenericMessage, Error(page));
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
        Assert.Equal(2, logger.Entries.Count);
        Assert.Same(db.Failure, logger.Entries[0].Exception);
        Assert.Same(lookupFailure, logger.Entries[1].Exception);
    }

    private sealed class FailedLookupInterceptor(Exception failure) : IQueryExpressionInterceptor
    {
        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData) => throw failure;
    }

    private sealed class FilteredFailureContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        internal bool Fail { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Marca>().HasQueryFilter(x => x.Ativo);
        }
        public override Task<int> SaveChangesAsync(CancellationToken ct = default) => Fail
            ? throw new DbUpdateException("Synthetic hidden conflict", SqlFailure((2601, SqlMessage(2601))))
            : base.SaveChangesAsync(ct);
    }
}
