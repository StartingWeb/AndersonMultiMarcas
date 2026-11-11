using Core.Storage;
using Data;
using Domain.Entities;
using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project.Pages.Admin.Cadastros.Marcas;
using Project.Tests.Security;
using Xunit;
using static Project.Tests.Cadastros.MarcaTestSupport;

namespace Project.Tests.Cadastros;

public sealed class MarcaLogoUploadTests
{
    [Theory]
    [InlineData("logo.jpg")]
    [InlineData("logo.png")]
    [InlineData("logo.webp")]
    [InlineData("../../untrusted.png")]
    public async Task UploadDecodesRasterAndPersistsUniqueResizedWebp(string filename)
    {
        await using var db = new ApplicationDbContext(MemoryOptions());
        var storage = new ProbeStorage();
        var page = Page(db, storage: storage);
        page.LogoArquivo = LogoFile(filename);

        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(default));
        var brand = await db.Marcas.AsNoTracking().SingleAsync();
        var key = Assert.Single(storage.Attempts);
        Assert.Matches(@"^uploads/marcas/[a-f0-9]{32}\.webp$", key);
        Assert.Equal(storage.GetPublicUrl(key), brand.LogoUrl);
        Assert.Empty(storage.Deleted);
        Assert.Equal("image/webp", (await storage.GetMetadataAsync(key, default))!.ContentType);
        await using var bytes = await storage.OpenReadAsync(key, default);
        using var image = new MagickImage(bytes!);
        Assert.Equal(MagickFormat.WebP, image.Format);
        Assert.Equal(512u, image.Width);
        Assert.Equal(128u, image.Height);
        Assert.Equal("Marca cadastrada com sucesso.", page.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("large")]
    [InlineData("svg")]
    [InlineData("corrupt")]
    public async Task InvalidFilesNeverReachStorageOrDatabase(string kind)
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        var storage = new ProbeStorage();
        var page = Page(db, storage: storage);
        page.LogoArquivo = kind switch
        {
            "empty" => LogoFile(bytes: []),
            "large" => new FormFile(new MemoryStream(), 0, UpsertModel.MaxLogoBytes + 1, "LogoArquivo", "large.png"),
            "svg" => LogoFile("logo.svg", "<svg/>"u8.ToArray()),
            _ => LogoFile(bytes: "not an image"u8.ToArray())
        };
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.False(page.ModelState.IsValid);
        Assert.True(page.ModelState.ContainsKey(nameof(page.LogoArquivo)));
        Assert.Empty(storage.Attempts);
        Assert.Equal(0, db.SaveCalls);
        Assert.Empty(await db.Marcas.ToListAsync());
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
    }

    [Fact]
    public async Task InvalidFormDoesNotUploadAndRetainsExistingPreview()
    {
        await using var db = new ApplicationDbContext(MemoryOptions());
        var brand = new Marca("Original", "/old.png");
        db.Add(brand); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var storage = new ProbeStorage();
        var page = Page(db, id: brand.Id, storage: storage);
        page.LogoArquivo = LogoFile();
        page.ModelState.AddModelError("Marca.Nome", "Informe o nome da marca.");
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Empty(storage.Attempts);
        Assert.Equal("/old.png", page.CurrentLogoUrl);
        Assert.Equal("Editar marca", page.PageTitle);
    }

    [Fact]
    public async Task FailedDatabaseSaveRemovesOnlyNewUploadAndKeepsOldLogo()
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        var brand = new Marca("Original", "/old.png");
        db.Add(brand); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.Failure = new DbUpdateException("Synthetic database failure");
        var storage = new ProbeStorage();
        var page = Page(db, id: brand.Id, storage: storage);
        page.LogoArquivo = LogoFile();
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal(GenericMessage, Error(page));
        Assert.Equal(storage.Attempts, storage.Deleted);
        Assert.False(await storage.ExistsAsync(Assert.Single(storage.Attempts), default));
        db.ChangeTracker.Clear();
        var saved = await db.Marcas.SingleAsync();
        Assert.Equal("Original", saved.Nome);
        Assert.Equal("/old.png", saved.LogoUrl);
        Assert.Equal("/old.png", page.CurrentLogoUrl);
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
    }

    [Fact]
    public async Task StorageFailureDoesNotSaveBrandOrAnnounceSuccess()
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        var storage = new ProbeStorage { SaveFailure = new IOException("Synthetic storage details") };
        var page = Page(db, storage: storage);
        page.LogoArquivo = LogoFile();
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal("Não foi possível enviar a logo. Tente novamente.", Error(page));
        Assert.Equal(0, db.SaveCalls);
        Assert.Empty(await db.Marcas.ToListAsync());
        Assert.False(await storage.ExistsAsync(Assert.Single(storage.Attempts), default));
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
    }

    [Fact]
    public async Task CleanupFailureDoesNotHideDatabaseError()
    {
        await using var db = new SaveProbeContext(MemoryOptions()) { Failure = new DbUpdateException("Synthetic") };
        var storage = new ProbeStorage { DeleteFailure = new IOException("Synthetic cleanup failure") };
        var logger = new TestLogger();
        var page = Page(db, logger: logger, storage: storage);
        page.LogoArquivo = LogoFile();
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal(GenericMessage, Error(page));
        Assert.Contains(logger.Entries, x => x.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
    }

    [Fact]
    public async Task UnacknowledgedCommitNeverDeletesAnAlreadyReferencedUpload()
    {
        await using var db = new CommitThenFailContext(MemoryOptions());
        var storage = new ProbeStorage();
        var page = Page(db, storage: storage);
        page.LogoArquivo = LogoFile();
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal(GenericMessage, Error(page));
        var key = Assert.Single(storage.Attempts);
        Assert.Empty(storage.Deleted);
        Assert.True(await storage.ExistsAsync(key, default));
        Assert.Empty(await db.Marcas.ToListAsync());
        Assert.Equal(storage.GetPublicUrl(key), (await db.Marcas.IgnoreQueryFilters().AsNoTracking().SingleAsync()).LogoUrl);
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
    }

    [Fact]
    public async Task MissingEditTargetNeverUploads()
    {
        await using var db = new ApplicationDbContext(MemoryOptions());
        var storage = new ProbeStorage();
        var page = Page(db, id: 9999, storage: storage);
        page.LogoArquivo = LogoFile();
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(default));
        Assert.Empty(storage.Attempts);
        Assert.Empty(await db.Marcas.ToListAsync());
    }

    [Fact]
    public async Task InvalidPublicUrlDoesNotReachTheDatabase()
    {
        await using var db = new SaveProbeContext(MemoryOptions());
        var storage = new ProbeStorage { ReturnedUrl = "https://example.test/" + new string('x', 400) };
        var page = Page(db, storage: storage);
        page.LogoArquivo = LogoFile();
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal(0, db.SaveCalls);
        Assert.Equal(storage.Attempts, storage.Deleted);
        Assert.False(page.ModelState.IsValid);
    }

    private sealed class CommitThenFailContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Marca>().HasQueryFilter(x => x.Nome != "ZEEKR");
        }

        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            await base.SaveChangesAsync(ct);
            throw new DbUpdateException("Synthetic lost commit acknowledgement");
        }
    }

    private sealed class ProbeStorage : IStorageService
    {
        private readonly MemoryStorageService memory = new();
        internal List<string> Attempts { get; } = [];
        internal List<string> Deleted { get; } = [];
        internal Exception? SaveFailure { get; init; }
        internal Exception? DeleteFailure { get; init; }
        internal string? ReturnedUrl { get; init; }
        public async Task<StoredFile> SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
        {
            Attempts.Add(key);
            var result = await memory.SaveAsync(key, content, contentType, ct);
            if (SaveFailure is not null) throw SaveFailure;
            return ReturnedUrl is null ? result : result with { Url = ReturnedUrl };
        }
        public async Task DeleteAsync(string key, CancellationToken ct)
        {
            Deleted.Add(key);
            if (DeleteFailure is not null) throw DeleteFailure;
            await memory.DeleteAsync(key, ct);
        }
        public Task<bool> ExistsAsync(string key, CancellationToken ct) => memory.ExistsAsync(key, ct);
        public Task<StorageObjectMetadata?> GetMetadataAsync(string key, CancellationToken ct) => memory.GetMetadataAsync(key, ct);
        public IAsyncEnumerable<StorageObjectMetadata> ListAsync(string prefix, CancellationToken ct) => memory.ListAsync(prefix, ct);
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => memory.OpenReadAsync(key, ct);
        public string GetPublicUrl(string key) => memory.GetPublicUrl(key);
    }
}
