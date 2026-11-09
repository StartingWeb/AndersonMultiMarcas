using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Xunit;

namespace Project.Tests.Database;

public sealed class ProductionMigrationTests
{
    [SqlServerFact]
    public async Task ProductionCandidatesRequireApprovalsAndPreserveRowsAcrossABCAndReplay()
    {
        await WithFixture(async connection =>
        {
            var denied = await Assert.ThrowsAsync<SqlException>(() => Apply(connection,"010_schema.sql"));
            Assert.Equal(52000,denied.Number);
            await Context(connection,"schema-A");await Apply(connection,"010_schema.sql");await Apply(connection,"010_schema.sql");
            Assert.Equal(0,await Number(connection,"SELECT CONVERT(int,is_nullable) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Marca') AND name='LogoUrl'"));
            Assert.Equal(8000,await Number(connection,"SELECT max_length FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Veiculo') AND name='Descricao'"));
            await Context(connection,"schema-C");var missingB=await Assert.ThrowsAsync<SqlException>(()=>Apply(connection,"030_index.sql"));
            Assert.Equal(52030,missingB.Number);await Execute(connection,"IF @@TRANCOUNT>0 ROLLBACK;");
            await Context(connection,"schema-B");var noApproval=await Assert.ThrowsAsync<SqlException>(()=>Apply(connection,"020_marca.sql"));
            Assert.Equal(52020,noApproval.Number);await Execute(connection,"IF @@TRANCOUNT>0 ROLLBACK;");
            Assert.Equal(2,await Number(connection,"SELECT COUNT(*) FROM dbo.Marca WHERE Nome=N'Synthetic brand'"));
            await Context(connection,"schema-B",true);await Apply(connection,"020_marca.sql");await Apply(connection,"020_marca.sql");
            Assert.Equal(1,await Number(connection,"SELECT COUNT(*) FROM dbo.Marca WHERE Id=1 AND Nome=N'Synthetic brand'"));
            Assert.Equal(1,await Number(connection,"SELECT COUNT(*) FROM dbo.Marca WHERE Id=4 AND Nome=N'Synthetic brand (additional)'"));
            Assert.Equal(1,await Number(connection,"SELECT COUNT(*) FROM dbo.Veiculo WHERE MarcaId=1"));
            await Context(connection,"schema-C");await Apply(connection,"030_index.sql");await Apply(connection,"030_index.sql");
            Assert.Equal(1,await Number(connection,"SELECT CONVERT(int,is_unique) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Marca') AND name='IX_Marca_Nome'"));
            Assert.Equal(3,await Number(connection,"SELECT COUNT(*) FROM dbo.AndersonProductionMigrationJournal"));
            await Execute(connection,"EXEC sys.sp_set_session_context @key=N'Anderson.ExpectedDatabaseGuid',@value=N'00000000-0000-0000-0000-000000000001';");
            var wrongTarget=await Assert.ThrowsAsync<SqlException>(()=>Apply(connection,"030_index.sql"));Assert.Equal(52000,wrongTarget.Number);
        });
    }

    [SqlServerFact]
    public async Task LogoNullabilityRequiresAnAdditionalExplicitAuthorization()
    {
        await WithFixture(async connection =>
        {
            await Context(connection,"schema-A");
            await Execute(connection,"EXEC sys.sp_set_session_context @key=N'Anderson.AllowLogoNullability',@value=N'approved';");
            var denied=await Assert.ThrowsAsync<SqlException>(()=>Apply(connection,"010_schema.sql"));
            Assert.Equal(52010,denied.Number);await Execute(connection,"IF @@TRANCOUNT>0 ROLLBACK;");
            await Execute(connection,"EXEC sys.sp_set_session_context @key=N'Anderson.LogoNullabilityAuthorization',@value=N'isolated-test-only';");
            await Apply(connection,"010_schema.sql");await Apply(connection,"010_schema.sql");
            Assert.Equal(1,await Number(connection,"SELECT CONVERT(int,is_nullable) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Marca') AND name='LogoUrl'"));
        });
    }

    private static async Task Execute(SqlConnection connection,string text)
    {await using var cmd=new SqlCommand(text,connection);await cmd.ExecuteNonQueryAsync();}
    private static async Task<int> Number(SqlConnection connection,string text)
    {await using var cmd=new SqlCommand(text,connection);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
    private static async Task Context(SqlConnection connection,string scope,bool approveName=false)
    {
        await using var identity=new SqlCommand("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),CONVERT(nvarchar(36),database_guid) FROM sys.database_recovery_status WHERE database_id=DB_ID()",connection);
        await using var reader=await identity.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());
        var server=reader.GetString(0);var guid=reader.GetString(1);await reader.DisposeAsync();
        foreach(var (key,value) in new Dictionary<string,string?>{
            ["Anderson.ExpectedServer"]=server,["Anderson.ExpectedDatabaseGuid"]=guid,["Anderson.Scope"]=scope,
            ["Anderson.Authorization"]="isolated-test-only",["Anderson.SchemaExecutionApproved"]="approved",
            ["Anderson.ExpectedMarcaName"]="Synthetic brand",["Anderson.ApprovedMarcaName"]=approveName?"Synthetic brand (additional)":null,
            ["Anderson.MarcaNameApproval"]=approveName?"isolated-test-only":null})
        {
            await using var command=new SqlCommand("EXEC sys.sp_set_session_context @key=@k,@value=@v",connection);
            command.Parameters.AddWithValue("@k",key);command.Parameters.AddWithValue("@v",(object?)value??DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }
    }
    private static async Task Apply(SqlConnection connection,string filename)
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"deploy/production/migrations",filename)))root=root.Parent;
        Assert.NotNull(root);var bytes=await File.ReadAllBytesAsync(Path.Combine(root.FullName,"deploy/production/migrations",filename));
        await using var digest=new SqlCommand("EXEC sys.sp_set_session_context @key=N'Anderson.ScriptSha256',@value=@v",connection);
        digest.Parameters.AddWithValue("@v",Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());await digest.ExecuteNonQueryAsync();
        await Execute(connection,System.Text.Encoding.UTF8.GetString(bytes));
    }
    private static async Task WithFixture(Func<SqlConnection,Task> body)
    {
        var builder=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL")){InitialCatalog="master",Pooling=false};
        await using var connection=new SqlConnection(builder.ConnectionString);await connection.OpenAsync();var created=false;
        try
        {
            await Execute(connection,"CREATE DATABASE AndersonMultiMarcas");created=true;
            var database=new SqlConnectionStringBuilder(builder.ConnectionString){InitialCatalog="AndersonMultiMarcas"};
            await using(var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(database.ConnectionString).Options))
            {
                await db.Database.MigrateAsync();
                var store=new Loja("Synthetic store","Synthetic company",null,new Email("test@example.invalid"),new Telefone("16999990000"),new Endereco("Test street","1",null,"Test","Test",Uf.SP,"15900000"));
                var brand=new Marca("Synthetic brand","/img/default.png");db.AddRange(store,brand);await db.SaveChangesAsync();
                db.Add(new Veiculo(store.Id,brand.Id,"Synthetic vehicle","Synthetic model",2026,new Dinheiro(10000)));await db.SaveChangesAsync();
            }
            await connection.ChangeDatabaseAsync("AndersonMultiMarcas");
            await Execute(connection,"DROP INDEX IX_Marca_Nome ON dbo.Marca; CREATE INDEX IX_Marca_Nome ON dbo.Marca(Nome); SET IDENTITY_INSERT dbo.Marca ON; INSERT dbo.Marca(Id,Nome,LogoUrl,DataCadastro,Ativo) VALUES(4,N'Synthetic brand',N'/img/default.png',SYSUTCDATETIME(),1); SET IDENTITY_INSERT dbo.Marca OFF;");
            await Execute(connection,"DROP INDEX IX_Veiculo_MarcaId_Modelo_AnoModelo ON dbo.Veiculo;");
            foreach(var (table,column,size,nullable) in new (string,string,int,bool)[]{("Loja","Email",150,true),("Marca","LogoUrl",255,false),("Veiculo","Cor",30,true),("Veiculo","Descricao",1000,true),("Veiculo","Modelo",100,true),("Veiculo","ObservacoesInternas",1000,true),("Veiculo","Titulo",150,false),("Veiculo","UrlVideo",255,true),("Veiculo","Versao",100,true),("Vendedor","Cargo",100,true),("Vendedor","Email",150,true),("Vendedor","FotoUrl",255,true)})
                await Execute(connection,$"ALTER TABLE dbo.[{table}] ALTER COLUMN [{column}] nvarchar({size}) {(nullable?"NULL":"NOT NULL")}");
            await Execute(connection,"CREATE INDEX IX_Veiculo_MarcaId_Modelo_AnoModelo ON dbo.Veiculo(MarcaId,Modelo,AnoModelo);");
            await body(connection);
        }
        finally
        {
            await Execute(connection,"IF @@TRANCOUNT>0 ROLLBACK;");await connection.ChangeDatabaseAsync("master");
            if(created)await Execute(connection,"ALTER DATABASE AndersonMultiMarcas SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE AndersonMultiMarcas;");
        }
    }
}
