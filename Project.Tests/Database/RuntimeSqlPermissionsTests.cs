using Data;
using Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Project.Tests.Database;

public sealed class RuntimeSqlPermissionsTests
{
    [SqlServerFact]
    public async Task DedicatedRuntimePerformsCrudWithoutDdlOrAdministrativePermissions()
    {
        var adminBuilder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL"))
            { InitialCatalog = "master", Pooling = false };
        await using var admin = new SqlConnection(adminBuilder.ConnectionString); await admin.OpenAsync();
        var createdDatabase = false; var createdLogin = false;
        async Task Execute(string text) { await using var command = new SqlCommand(text, admin); await command.ExecuteNonQueryAsync(); }
        var password = "Aa9!" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        try
        {
            // Refuse adoption of existing resources; this fixture owns only what it creates.
            await Execute("CREATE DATABASE AndersonRuntimeTrial"); createdDatabase = true;
            var database = new SqlConnectionStringBuilder(adminBuilder.ConnectionString) { InitialCatalog = "AndersonRuntimeTrial" };
            await using (var setup = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(database.ConnectionString).Options))
                await setup.Database.MigrateAsync();
            await Execute("CREATE LOGIN anderson_runtime WITH PASSWORD=N'" + password + "'; DENY CREATE ANY DATABASE,ALTER ANY LOGIN,ALTER SERVER STATE TO anderson_runtime;");
            createdLogin = true;
            await admin.ChangeDatabaseAsync("AndersonRuntimeTrial");
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "deploy/sql/runtime_permissions_trial.sql"))) directory = directory.Parent;
            Assert.NotNull(directory);
            var grants = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "deploy/sql/runtime_permissions_trial.sql"));
            await Execute(grants); await Execute(grants);
            var runtime = new SqlConnectionStringBuilder(database.ConnectionString) { UserID = "anderson_runtime", Password = password, IntegratedSecurity = false };
            await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(runtime.ConnectionString).Options))
            {
                var brand = new Marca("Synthetic runtime", null); db.Marcas.Add(brand); await db.SaveChangesAsync();
                db.ChangeTracker.Clear(); var stored = await db.Marcas.SingleAsync();
                stored.Update("Synthetic edited", "/img/test.png"); await db.SaveChangesAsync();
                db.Marcas.Remove(stored); await db.SaveChangesAsync(); Assert.Empty(await db.Marcas.ToListAsync());
            }
            await using var restricted = new SqlConnection(runtime.ConnectionString); await restricted.OpenAsync();
            foreach (var table in new[] { "Loja", "Marca", "Veiculo", "VeiculoCaracteristica", "VeiculoMidia", "Vendedor",
                "ImportJob", "ImportJobItem", "ImportJobLog", "ImportJobHistory", "AspNetUsers", "AspNetRoles", "AspNetUserRoles",
                "AspNetRoleClaims" })
            {
                await using var grantsCommand = new SqlCommand($"SELECT COUNT(*) FROM sys.fn_my_permissions('dbo.{table}','OBJECT') WHERE subentity_name=N'' AND permission_name IN ('SELECT','INSERT','UPDATE','DELETE')", restricted);
                Assert.Equal(4, Convert.ToInt32(await grantsCommand.ExecuteScalarAsync()));
            }
            foreach (var table in new[] { "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens" })
            {
                await using var read = new SqlCommand($"SELECT COUNT(*) FROM sys.fn_my_permissions('dbo.{table}','OBJECT') WHERE subentity_name=N'' AND permission_name IN ('SELECT','INSERT','UPDATE','DELETE')", restricted);
                Assert.Equal(1, Convert.ToInt32(await read.ExecuteScalarAsync()));
                await using var write = new SqlCommand($"DELETE FROM dbo.{table} WHERE 1=0", restricted);
                await Assert.ThrowsAsync<SqlException>(() => write.ExecuteNonQueryAsync());
            }
            foreach (var query in new[] { "CREATE TABLE dbo.Forbidden(Id int)", "ALTER TABLE dbo.Marca ADD Forbidden int NULL",
                "DROP TABLE dbo.Marca", "CREATE USER Forbidden WITHOUT LOGIN", "GRANT SELECT ON dbo.Marca TO public",
                "CREATE DATABASE Forbidden", "CREATE LOGIN Forbidden WITH PASSWORD=N'" + password + "'",
                "ALTER SERVER ROLE sysadmin ADD MEMBER anderson_runtime", "DBCC FREEPROCCACHE" })
            {
                await using var denied = new SqlCommand(query, restricted);
                await Assert.ThrowsAsync<SqlException>(() => denied.ExecuteNonQueryAsync());
            }
            await Execute("GRANT VIEW DEFINITION TO anderson_runtime;");
            var unexpected = await Assert.ThrowsAsync<SqlException>(() => Execute(grants));
            Assert.Equal(51106, unexpected.Number);
            await Execute("IF @@TRANCOUNT>0 ROLLBACK; REVOKE VIEW DEFINITION TO anderson_runtime;");
            await Execute("ALTER SERVER ROLE securityadmin ADD MEMBER anderson_runtime;");
            var serverPrivilege = await Assert.ThrowsAsync<SqlException>(() => Execute(grants));
            Assert.Equal(51105, serverPrivilege.Number);
            await Execute("ALTER SERVER ROLE securityadmin DROP MEMBER anderson_runtime;");

        }
        finally
        {
            await admin.ChangeDatabaseAsync("master");
            if (createdDatabase) await Execute("ALTER DATABASE AndersonRuntimeTrial SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE AndersonRuntimeTrial;");
            if (createdLogin) await Execute("DROP LOGIN anderson_runtime;");
        }
    }
}
