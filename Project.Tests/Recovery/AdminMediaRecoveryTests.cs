using Core.Storage;
using Data;
using Domain.Entities;
using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Project.Features.Veiculos.Services;
using Project.Infrastructure.Storage;
using Project.Tests.Database;
using AdminPage = Project.Pages.Admin.Veiculo.UpsertModel;
using Xunit;

namespace Project.Tests.Recovery;

public sealed class AdminMediaRecoveryTests
{
    [SqlServerFact]
    public async Task InvalidAdminImageDoesNotDeleteExistingDatabaseRowsOrBlobs()
    {
        await WithDatabase(async db =>
        {
            var (vehicle, images) = await CoverReviewTests.Seed(db);
            var storage = new RecordingStorage();
            var page = Page(db, storage, vehicle.Id);
            page.RemoverMidiaIds = [images[0].Id];
            page.NovasMidias = [File([1, 2, 3], "invalid.png")];
            Assert.IsType<RedirectToPageResult>(await page.OnPostMidiasAsync(default));
            Assert.True(page.TempData.ContainsKey("ErrorMessage"));
            Assert.False(page.TempData.ContainsKey("SuccessMessage"));
            Assert.Equal(3, await db.VeiculoMidias.CountAsync());
            Assert.Empty(storage.Saved);
            Assert.Empty(storage.Deleted);
        });
    }

    [SqlServerFact]
    public async Task AdminDeletesOldBlobOnlyAfterNewImageAndCoverAreCommitted()
    {
        await WithDatabase(async db =>
        {
            var (vehicle, images) = await CoverReviewTests.Seed(db);
            images[0].DefinirComoCapa();
            await db.SaveChangesAsync();
            var storage = new RecordingStorage();
            storage.OnDelete = async key =>
            {
                Assert.Null(db.Database.CurrentTransaction);
                Assert.False(await db.VeiculoMidias.AnyAsync(x => x.Id == images[0].Id));
                Assert.Equal(1, await db.VeiculoMidias.CountAsync(x => x.Capa));
            };
            var page = Page(db, storage, vehicle.Id);
            page.RemoverMidiaIds = [images[0].Id];
            page.NovasMidias = [ValidImage()];
            Assert.IsType<RedirectToPageResult>(await page.OnPostMidiasAsync(default));
            Assert.True(page.TempData.ContainsKey("SuccessMessage"));
            Assert.False(page.TempData.ContainsKey("ErrorMessage"));
            Assert.Single(storage.Saved);
            Assert.Equal([$"uploads/veiculos/{vehicle.Id}/1.jpg"], storage.Deleted);
            Assert.Equal(3, await db.VeiculoMidias.CountAsync());
        });
    }

    [SqlServerFact]
    public async Task AdminSqlFailureRollsBackOldImageRemovalAndCleansOnlyNewUpload()
    {
        await WithDatabase(async db =>
        {
            var (vehicle, images) = await CoverReviewTests.Seed(db);
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectSyntheticNewImage ON VeiculoMidia AFTER INSERT AS BEGIN; THROW 51050, 'Synthetic media persistence failure', 1; END");
            var storage = new RecordingStorage();
            var page = Page(db, storage, vehicle.Id);
            page.RemoverMidiaIds = [images[0].Id];
            page.NovasMidias = [ValidImage()];
            Assert.IsType<RedirectToPageResult>(await page.OnPostMidiasAsync(default));
            Assert.True(page.TempData.ContainsKey("ErrorMessage"));
            Assert.False(page.TempData.ContainsKey("SuccessMessage"));
            Assert.Equal(3, await db.VeiculoMidias.CountAsync());
            Assert.True(await db.VeiculoMidias.AnyAsync(x => x.Id == images[0].Id));
            Assert.Single(storage.Saved);
            Assert.Equal(storage.Saved, storage.Deleted);
            Assert.DoesNotContain($"uploads/veiculos/{vehicle.Id}/1.jpg", storage.Deleted);
        });
    }

    private static AdminPage Page(ApplicationDbContext db, IStorageService storage, int vehicleId)
    {
        var options = Options.Create(new StorageOptions());
        var http = new DefaultHttpContext();
        return new(db, null!, new VeiculoMediaService(storage, options), new StorageImageResolver(options))
        {
            Veiculo = new AdminPage.VehicleInputModel { Id = vehicleId },
            TempData = new TempDataDictionary(http, new EmptyTempDataProvider())
        };
    }

    private static IFormFile ValidImage()
    {
        using var image = new MagickImage(MagickColors.Red, 2, 2);
        return File(image.ToByteArray(MagickFormat.Png), "valid.png");
    }

    private static FormFile File(byte[] data, string name) => new(new MemoryStream(data), 0, data.Length, "NovasMidias", name)
    { Headers = new HeaderDictionary(), ContentType = "image/png" };

    private sealed class RecordingStorage : IStorageService
    {
        public List<string> Saved { get; } = [];
        public List<string> Deleted { get; } = [];
        public Func<string, Task>? OnDelete { get; set; }
        public Task<StoredFile> SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
        {
            Saved.Add(key);
            return Task.FromResult(new StoredFile(key, "/" + key, Path.GetFileName(key), "local", contentType, content.Length));
        }
        public async Task DeleteAsync(string key, CancellationToken ct) { if (OnDelete is not null) await OnDelete(key); Deleted.Add(key); }
        public Task<bool> ExistsAsync(string key, CancellationToken ct) => Task.FromResult(true);
        public Task<StorageObjectMetadata?> GetMetadataAsync(string key, CancellationToken ct) => Task.FromResult<StorageObjectMetadata?>(null);
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream?>(null);
        public string GetPublicUrl(string key) => "/" + key;
        public IAsyncEnumerable<StorageObjectMetadata> ListAsync(string prefix, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static async Task WithDatabase(Func<ApplicationDbContext, Task> action)
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL"))
        { InitialCatalog = "AndersonAdminMedia_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try { await db.Database.MigrateAsync(); await action(db); }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
