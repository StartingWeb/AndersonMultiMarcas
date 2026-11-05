using Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using System.Text.RegularExpressions;

namespace Project.Tests.Database;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL")))
            Skip = "Set ANDERSON_TEST_SQL to an isolated disposable SQL Server to run database integration tests.";
    }
}

public sealed class SqlServerMigrationTests
{
    [SqlServerFact]
    public async Task FreshDatabaseMigratesAndSecondRunIsIdempotent()
    {
        await WithDatabase(async db =>
        {
            await db.Database.MigrateAsync();
            var first = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.True(first.Length >= 10);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            await db.Database.MigrateAsync();
            Assert.Equal(first, await db.Database.GetAppliedMigrationsAsync());
            Assert.Equal(0, await db.Veiculos.CountAsync());
            Assert.Equal(0, await db.Lojas.CountAsync());
            Assert.Equal(0, await db.Users.CountAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        });
    }

    [SqlServerFact]
    public async Task LegacyIdentityGuidKeysAndRoleRelationshipsArePreserved()
    {
        await WithDatabase(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260307182118_InitialMigration");
            var user = Guid.NewGuid(); var role = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT AspNetUsers (Id, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount) VALUES ({user.ToString()},0,0,0,0,0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT AspNetRoles (Id, Name) VALUES ({role.ToString()}, N'TestRole')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT AspNetUserRoles (UserId, RoleId) VALUES ({user.ToString()}, {role.ToString()})");
            await migrator.MigrateAsync();
            Assert.Equal(user, (await db.Users.SingleAsync()).Id);
            Assert.Equal(role, (await db.Roles.SingleAsync()).Id);
            var assigned = await db.UserRoles.SingleAsync();
            Assert.Equal(user, assigned.UserId); Assert.Equal(role, assigned.RoleId);
        });
    }

    [SqlServerFact]
    public async Task NonGuidLegacyIdentityIsRefusedWithoutChangingKeysOrHistory()
    {
        await WithDatabase(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260307182118_InitialMigration");
            await db.Database.ExecuteSqlRawAsync("INSERT AspNetUsers (Id, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount) VALUES (N'legacy-invalid-key',0,0,0,0,0)");
            var error = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync());
            Assert.Equal(51000, error.Number);
            Assert.Single(await db.Database.GetAppliedMigrationsAsync());
            var key = await db.Database.SqlQueryRaw<string>("SELECT Id AS [Value] FROM AspNetUsers").SingleAsync();
            Assert.Equal("legacy-invalid-key", key);
        });
    }

    [SqlServerFact]
    public async Task ReviewableIdempotentSqlCreatesDatabaseSchemaAndCanBeReplayed()
    {
        await WithDatabase(async db =>
        {
            // Database creation is separate from schema application, just as for reviewed deployment SQL.
            await db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>().CreateAsync();
            await db.Database.OpenConnectionAsync();
            var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                    if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
            }
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(0, await db.Veiculos.CountAsync());
        });
    }

    [SqlServerFact]
    public async Task GuidWithSuffixIsRejectedInsteadOfSilentlyTruncated()
    {
        await WithDatabase(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260307182118_InitialMigration");
            var key = Guid.NewGuid().ToString() + "-suffix";
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT AspNetUsers (Id, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount) VALUES ({key},0,0,0,0,0)");
            var error = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync());
            Assert.Equal(51000, error.Number);
            Assert.Equal(key, await db.Database.SqlQueryRaw<string>("SELECT Id AS [Value] FROM AspNetUsers").SingleAsync());
        });
    }

    [SqlServerFact]
    public async Task IdentitySchemaDowngradeIsExplicitlyBlockedWithoutLosingHistory()
    {
        await WithDatabase(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260528140115_AlignIdentityToGuid");
            var error = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync("20260307182118_InitialMigration"));
            Assert.Equal(51002, error.Number);
            Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
        });
    }

    private static async Task WithDatabase(Func<ApplicationDbContext, Task> action)
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL"))
        {
            InitialCatalog = "AndersonTest_" + Guid.NewGuid().ToString("N")
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try { await action(db); }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
