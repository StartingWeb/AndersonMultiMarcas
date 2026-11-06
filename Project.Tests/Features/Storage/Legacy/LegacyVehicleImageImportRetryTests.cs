using Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Project.Features.Storage.Legacy;
using Project.Tests.Security;
using Xunit;

namespace Project.Tests.Features.Storage.Legacy;

public sealed class LegacyVehicleImageImportRetryTests
{
    [Fact]
    public async Task FailedItemBelowLimitCanProgressOnNextAttempt()
    {
        await using var db = CreateDb();
        var (job, item) = await SeedAsync(db, true);
        item.IncrementAttempt();
        item.MarkFailed("Falha transitoria");
        await db.SaveChangesAsync();
        Assert.False(item.IsTerminal);
        using var semaphore = new SemaphoreSlim(1);
        await Processor(db).ProcessAsync(job.Id, item.Id, "synthetic-worker", semaphore, CancellationToken.None);
        Assert.Equal(2, item.Tentativas);
        Assert.Equal(LegacyImageImportItemStatus.Ignored, item.Status);
        Assert.True(item.IsTerminal);
    }

    [Fact]
    public async Task PermanentFailureStopsExactlyAtConfiguredLimit()
    {
        await using var db = CreateDb();
        var (job, item) = await SeedAsync(db, false);
        using var semaphore = new SemaphoreSlim(1);
        var processor = Processor(db);
        for (var i = 0; i < 5; i++)
            await processor.ProcessAsync(job.Id, item.Id, "synthetic-worker", semaphore, CancellationToken.None);
        Assert.Equal(3, item.Tentativas);
        Assert.Equal(LegacyImageImportItemStatus.Failed, item.Status);
        Assert.True(item.IsTerminal);
    }

    [Fact]
    public async Task ExplicitRetryAddsNewBoundedBudgetAndPreservesHistory()
    {
        await using var db = CreateDb();
        var (job, item) = await SeedAsync(db, false);
        for (var i = 0; i < 3; i++) item.IncrementAttempt();
        item.MarkFailed("Falha permanente");
        job.MarkCompleted();
        await db.SaveChangesAsync();
        var options = Options.Create(new LegacyImageImportOptions { MaxAttempts = 2 });
        var manager = new LegacyImageImportJobManager(db, new LegacyImageImportQueue(), new LegacyImportCancellationRegistry(), options);
        Assert.True(await manager.RetryFailuresAsync(job.Id, "synthetic", "Operador", CancellationToken.None));
        Assert.Equal(3, item.Tentativas);
        Assert.Equal(5, item.MaxTentativas);
        using var semaphore = new SemaphoreSlim(1);
        var processor = Processor(db);
        for (var i = 0; i < 4; i++)
            await processor.ProcessAsync(job.Id, item.Id, "synthetic-worker", semaphore, CancellationToken.None);
        Assert.Equal(5, item.Tentativas);
        Assert.True(item.IsTerminal);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private static async Task<(ImportJob Job, ImportJobItem Item)> SeedAsync(ApplicationDbContext db, bool dryRun)
    {
        var job = new ImportJob("https://example.test", dryRun, false, false, null, null, "synthetic", "Operador");
        db.Add(job);
        await db.SaveChangesAsync();
        // This address must be rejected by the URL guard before any network access.
        var item = new ImportJobItem(job.Id, 1, null, 0, true, "https://127.0.0.1/synthetic.png", "synthetic.png", "synthetic/image.png", 3);
        db.Add(item);
        await db.SaveChangesAsync();
        return (job, item);
    }
    private static LegacyVehicleImageImportItemProcessor Processor(ApplicationDbContext db) => new(db, new MemoryStorageService(),
        Options.Create(new LegacyImageImportOptions { AllowedHosts = ["example.test"] }), new NeverHttpClientFactory(), NullLogger<LegacyVehicleImageImportItemProcessor>.Instance);
    private sealed class NeverHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("No network access is permitted by this test.");
    }
}
