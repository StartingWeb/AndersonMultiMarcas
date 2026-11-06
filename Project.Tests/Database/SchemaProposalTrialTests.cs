using Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Xunit;

namespace Project.Tests.Database;

public sealed class SchemaProposalTrialTests
{
    [SqlServerFact]
    public async Task ProposalsPreserveDataRejectUnexpectedStateAndCanUndoOnlyTheOptionalRename()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL")) { InitialCatalog = "master", Pooling = false };
        await using var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync();
        var created = false;
        async Task Execute(string text) { await using var command = new SqlCommand(text, connection); await command.ExecuteNonQueryAsync(); }
        async Task<int> Number(string text) { await using var command = new SqlCommand(text, connection); return Convert.ToInt32(await command.ExecuteScalarAsync()); }
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs/schema/EXPAND_STORAGE_PROPOSAL.sql"))) directory = directory.Parent;
        Assert.NotNull(directory);
        async Task Apply(string name)
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "docs/schema", name));
            foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                if (!string.IsNullOrWhiteSpace(batch)) await Execute(batch);
        }
        try
        {
            await Execute("CREATE DATABASE AndersonSchemaTrial COLLATE SQL_Latin1_General_CP1_CI_AS"); created = true;
            var database = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "AndersonSchemaTrial" };
            await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(database.ConnectionString).Options))
                await db.Database.MigrateAsync();
            await connection.ChangeDatabaseAsync("AndersonSchemaTrial");
            await Execute("DROP INDEX IX_Marca_Nome ON dbo.Marca; CREATE INDEX IX_Marca_Nome ON dbo.Marca(Nome); ALTER TABLE dbo.Marca ALTER COLUMN LogoUrl nvarchar(255) NOT NULL; ALTER TABLE dbo.Veiculo ALTER COLUMN Descricao nvarchar(1000) NULL; SET IDENTITY_INSERT dbo.Marca ON; INSERT dbo.Marca(Id,Nome,LogoUrl,DataCadastro,Ativo) VALUES(1,N'Synthetic identical',N'',SYSUTCDATETIME(),1),(4,N'Synthetic identical',N'',SYSUTCDATETIME(),1); SET IDENTITY_INSERT dbo.Marca OFF;");
            await Apply("EXPAND_STORAGE_PROPOSAL.sql"); await Apply("EXPAND_STORAGE_PROPOSAL.sql");
            Assert.Equal(8000, await Number("SELECT max_length FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Veiculo') AND name='Descricao'"));
            var duplicate = await Assert.ThrowsAsync<SqlException>(() => Apply("MARCA_NOME_UNIQUE_PROPOSAL.sql"));
            Assert.Equal(51021, duplicate.Number); await Execute("IF @@TRANCOUNT>0 ROLLBACK;");
            Assert.Equal(0, await Number("SELECT CONVERT(int,is_unique) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Marca') AND name='IX_Marca_Nome'"));
            await Apply("RENAME_UNREFERENCED_MARCA_PROPOSAL.sql"); await Apply("RENAME_UNREFERENCED_MARCA_PROPOSAL.sql");
            await Apply("MARCA_NOME_UNIQUE_PROPOSAL.sql"); await Apply("MARCA_NOME_UNIQUE_PROPOSAL.sql");
            Assert.Equal(2, await Number("SELECT COUNT(*) FROM dbo.Marca"));
            Assert.Equal(1, await Number("SELECT CONVERT(int,is_unique) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Marca') AND name='IX_Marca_Nome'"));
            await Execute("DROP INDEX IX_Marca_Nome ON dbo.Marca; CREATE UNIQUE INDEX IX_Marca_Nome ON dbo.Marca(Nome) WHERE Ativo=1;");
            var unexpectedIndex = await Assert.ThrowsAsync<SqlException>(() => Apply("UNDO_MARCA_RENAME_TRIAL.sql"));
            Assert.Equal(51038, unexpectedIndex.Number); await Execute("IF @@TRANCOUNT>0 ROLLBACK;");
            var creationGuard = await Assert.ThrowsAsync<SqlException>(() => Apply("MARCA_NOME_UNIQUE_PROPOSAL.sql"));
            Assert.Equal(51022, creationGuard.Number); await Execute("IF @@TRANCOUNT>0 ROLLBACK;");
            await Execute("DROP INDEX IX_Marca_Nome ON dbo.Marca; CREATE UNIQUE INDEX IX_Marca_Nome ON dbo.Marca(Nome);");
            await Apply("UNDO_MARCA_RENAME_TRIAL.sql"); await Apply("UNDO_MARCA_RENAME_TRIAL.sql");
            Assert.Equal(2, await Number("SELECT COUNT(*) FROM dbo.Marca WHERE Nome=N'Synthetic identical'"));
            Assert.Equal(8000, await Number("SELECT max_length FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Veiculo') AND name='Descricao'"));
            await Execute("SET IDENTITY_INSERT dbo.Marca ON; INSERT dbo.Marca(Id,Nome,LogoUrl,DataCadastro,Ativo) VALUES(7,N'Synthetic identical',N'',SYSUTCDATETIME(),1); SET IDENTITY_INSERT dbo.Marca OFF;");
            var changed = await Assert.ThrowsAsync<SqlException>(() => Apply("RENAME_UNREFERENCED_MARCA_PROPOSAL.sql"));
            Assert.Equal(51033, changed.Number); await Execute("IF @@TRANCOUNT>0 ROLLBACK;");
            Assert.Equal(3, await Number("SELECT COUNT(*) FROM dbo.Marca WHERE Nome=N'Synthetic identical'"));
            await Execute("DELETE FROM dbo.Marca WHERE Id=7; UPDATE dbo.Marca SET Nome=REPLICATE(N'x',80)+REPLICATE(N' ',20);");
            var trailingSpaces = await Assert.ThrowsAsync<SqlException>(() => Apply("RENAME_UNREFERENCED_MARCA_PROPOSAL.sql"));
            Assert.Equal(51034, trailingSpaces.Number); await Execute("IF @@TRANCOUNT>0 ROLLBACK;");
            Assert.Equal(2, await Number("SELECT COUNT(*) FROM dbo.Marca WHERE DATALENGTH(Nome)=200"));
            await connection.ChangeDatabaseAsync("master");
            var wrongDatabase = await Assert.ThrowsAsync<SqlException>(() => Apply("EXPAND_STORAGE_PROPOSAL.sql"));
            Assert.Equal(51090, wrongDatabase.Number);
        }
        finally
        {
            await connection.ChangeDatabaseAsync("master");
            if (created) await Execute("ALTER DATABASE AndersonSchemaTrial SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE AndersonSchemaTrial;");
        }
    }
}
