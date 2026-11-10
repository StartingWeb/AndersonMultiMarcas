using System.Reflection;
using Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Project.Pages.Admin.Cadastros.Marcas;

namespace Project.Tests.Cadastros;

internal static class MarcaTestSupport
{
    internal const string DuplicateMessage = "Já existe uma marca cadastrada com este nome. Verifique a lista de marcas antes de continuar.";
    internal const string InactiveMessage = "Esta marca já está cadastrada, mas encontra-se inativa. Entre em contato com o administrador para reativá-la.";
    internal const string GenericMessage = "Não foi possível salvar a marca. Tente novamente ou entre em contato com o suporte.";

    internal static DbContextOptions<ApplicationDbContext> MemoryOptions() => new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    internal static UpsertModel Page(ApplicationDbContext db, string name = "ZEEKR", int id = 0, TestLogger? logger = null)
    {
        var http = new DefaultHttpContext();
        return new(db, logger ?? new TestLogger())
        {
            PageContext = new PageContext
            {
                HttpContext = http,
                ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                    new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
            },
            TempData = new TempDataDictionary(http, new EmptyTempDataProvider()),
            Marca = new UpsertModel.MarcaInputModel { Id = id, Nome = name }
        };
    }

    internal static string Error(UpsertModel page) => page.ModelState.Values.SelectMany(x => x.Errors).Single().ErrorMessage;

    internal static string SqlMessage(int number, string index = "IX_Marca_Nome", string table = "dbo.Marca") => number == 2601
        ? $"Cannot insert duplicate key row in object '{table}' with unique index '{index}'. The duplicate key value is (ZEEKR)."
        : $"Violation of UNIQUE KEY constraint '{index}'. Cannot insert duplicate key in object '{table}'. The duplicate key value is (ZEEKR).";

    // SqlClient exposes no public constructors for server errors. Keep this test-only
    // factory here; SQL integration tests also exercise genuine exceptions from the server.
    internal static SqlException SqlFailure(params (int Number, string Message)[] failures)
    {
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(x => x.GetParameters().Length == 9 && x.GetParameters()[7].ParameterType == typeof(int));
        foreach (var (number, message) in failures)
        {
            var error = constructor.Invoke([number, (byte)1, (byte)16, "synthetic-sql", message, "", 1, 0, null]);
            typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(errors, [error]);
        }
        return (SqlException)typeof(SqlException).GetMethod("CreateException", BindingFlags.NonPublic | BindingFlags.Static,
            null, [typeof(SqlErrorCollection), typeof(string)], null)!.Invoke(null, [errors, "16.0"])!;
    }

    internal sealed class TestLogger : ILogger<UpsertModel>
    {
        internal List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((level, exception, formatter(state, exception)));
    }

    internal sealed class SaveProbeContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        internal Exception? Failure { get; set; }
        internal Action? BeforeSave { get; set; }
        internal int SaveCalls { get; private set; }
        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;
            BeforeSave?.Invoke();
            if (Failure is not null) throw Failure;
            return await base.SaveChangesAsync(ct);
        }
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
