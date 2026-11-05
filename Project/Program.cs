using Core.Storage;
using Data;
using Domain.Application;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Project.Features.Storage.Legacy;
using Project.Features.Storage.R2Sync;
using Project.Features.Storage.Validation;
using Project.Features.Veiculos.Services;
using Project.Infrastructure.Storage;
using Project.Infrastructure.Deployment;
using Project.Shared;
using Project.Infrastructure.Security;
using System.Globalization;
using System.Net;
using System.Text;

var brCulture = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = brCulture;
CultureInfo.DefaultThreadCurrentUICulture = brCulture;

var builder = WebApplication.CreateBuilder(args);
var applyStartupMigrations = StartupDatabasePolicy.ShouldApplyMigrations(
    builder.Environment.IsDevelopment(),
    builder.Configuration.GetValue<bool?>("Database:ApplyMigrationsOnStartup"));
var releaseCommit = DeploymentRelease.ReadCommit(builder.Configuration["Deployment:Commit"]);

// ==============================
// BANCO DE DADOS
// ==============================
var connectionStringName = builder.Environment.IsProduction()
    ? "DefaultConnectionProd"
    : "DefaultConnectionDev";
var connectionString = GetRequiredConnectionString(
    builder.Configuration,
    connectionStringName,
    builder.Environment.IsProduction());

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = null;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName(builder.Configuration["DataProtection:ApplicationName"] ?? "AndersonMultiMarcas");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath) && builder.Environment.IsProduction())
{
    dataProtectionKeysPath = "/home/app/.aspnet/DataProtection-Keys";
}

if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    Directory.CreateDirectory(dataProtectionKeysPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

// ==============================
// IDENTITY
// ==============================
builder.Services
    .AddIdentity<AspNetCoreUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 12;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ==============================
// RAZOR PAGES
// ==============================
builder.Services.AddAuthorization(AdminAuthorization.Configure);
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(1));
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", AdminAuthorization.AccessPolicy);
    foreach (var item in AdminMenuCatalog.AllItems)
    {
        var policy = AdminAuthorization.MenuPolicyPrefix + item.Id;
        if (item.ExactMatch) options.Conventions.AuthorizePage(item.Url == "/Admin" ? "/Admin/Index" : item.Url, policy);
        else if (item.Id.StartsWith("storage-", StringComparison.Ordinal) || item.Id.StartsWith("ferramentas-", StringComparison.Ordinal))
            options.Conventions.AuthorizePage(item.Url, policy);
        else options.Conventions.AuthorizeFolder(item.Url, policy);
    }
});
builder.Services.AddControllers();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(brCulture);
    options.SupportedCultures = [brCulture];
    options.SupportedUICultures = [brCulture];
});
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<LegacyImageImportOptions>(builder.Configuration.GetSection(LegacyImageImportOptions.SectionName));
builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddScoped<IVeiculoSlugService, VeiculoSlugService>();
builder.Services.AddScoped<IVeiculoMediaService, VeiculoMediaService>();
builder.Services.AddSingleton<R2StorageService>();
builder.Services.AddSingleton<LocalWebRootStorageService>();
builder.Services.AddScoped<IStorageService, ApplicationStorageService>();
builder.Services.AddScoped<IStorageImageResolver, StorageImageResolver>();
builder.Services.AddSingleton<LegacyImageImportQueue>();
builder.Services.AddSingleton<LegacyImportCancellationRegistry>();
builder.Services.AddScoped<LegacyImageImportJobManager>();
builder.Services.AddScoped<LegacyVehicleJsonLdParser>();
builder.Services.AddScoped<LegacyVehicleImageImportService>();
builder.Services.AddScoped<LegacyVehicleImageImportItemProcessor>();
builder.Services.AddScoped<LegacyImageImportReportService>();
builder.Services.AddScoped<StorageImportValidationService>();
builder.Services.AddSingleton<R2VehicleImageSyncJobManager>();
builder.Services.AddScoped<R2VehicleImageSyncService>();
builder.Services.AddHostedService<LegacyImageImportWorker>();
builder.Services.AddHttpClient(LegacyVehicleImageImportService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AndersonMultimarcasStorageImporter/1.0");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false
});
builder.Services.AddHttpClient(StorageImportValidationService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AndersonMultimarcasStorageValidator/1.0");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// ==============================
// COOKIE / AUTH
// ==============================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = StatusCodes.Status403Forbidden;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

var app = builder.Build();
var siteBaseUrl = builder.Configuration["Seo:BaseUrl"]?.TrimEnd('/');

// ==============================
// PIPELINE
// ==============================
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// ==============================
// MIGRATIONS AUTOMATICAS
// ==============================
if (applyStartupMigrations)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        await IdentitySeed.EnsureDeveloperUserAsync(scope.ServiceProvider);
    }
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(self)";
    context.Response.Headers.XFrameOptions = "SAMEORIGIN";
    await next();
});
app.UseResponseCompression();
app.UseResponseCaching();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        const int immutableDurationInSeconds = 60 * 60 * 24 * 365;
        const int defaultDurationInSeconds = 60 * 60 * 24 * 30;
        var path = context.File.PhysicalPath ?? string.Empty;
        var isVersionedAsset = path.Contains($"{Path.DirectorySeparatorChar}css{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}js{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}favicon{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

        context.Context.Response.Headers.CacheControl = isVersionedAsset
            ? $"public,max-age={immutableDurationInSeconds},immutable"
            : $"public,max-age={defaultDurationInSeconds}";
        context.Context.Response.Headers.Vary = "Accept-Encoding";
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("OK")).AllowAnonymous();
app.MapGet("/health/release", () => Results.Ok(new { commit = releaseCommit })).AllowAnonymous();

app.MapGet("/Admin/Login", (HttpContext context) =>
{
    var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
    return Results.Redirect($"/Login{queryString}");
});

app.MapGet("/Veiculo", (HttpContext context) =>
{
    var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
    return Results.Redirect($"/Admin/Veiculo{queryString}", permanent: true);
});

app.MapGet("/Veiculo/Upsert/{id:int?}", (int? id, HttpContext context) =>
{
    var target = id.HasValue ? $"/Admin/Veiculo/Upsert/{id.Value}" : "/Admin/Veiculo/Upsert";
    var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
    return Results.Redirect($"{target}{queryString}", permanent: true);
});

app.MapGet("/veiculo/{id:int}/{slug}", (int id) =>
{
    return Results.Redirect($"/veiculo/{id}/", permanent: true);
});

app.MapGet("/robots.txt", (HttpContext context) =>
{
    var baseUrl = siteBaseUrl ?? $"{context.Request.Scheme}://{context.Request.Host}";
    var sb = new StringBuilder();
    sb.AppendLine("User-agent: *");
    sb.AppendLine("Allow: /");
    sb.AppendLine("Disallow: /Admin");
    sb.AppendLine("Disallow: /Login");
    sb.AppendLine("Disallow: /Error");
    sb.AppendLine("Disallow: /*?handler=");
    sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
    return Results.Text(sb.ToString(), "text/plain");
});

app.MapGet("/sitemap.xml", async (HttpContext context) =>
{
    var baseUrl = siteBaseUrl ?? $"{context.Request.Scheme}://{context.Request.Host}";
    var now = DateTime.UtcNow.ToString("yyyy-MM-dd");
    var urls = new List<(string Url, string Priority, string ChangeFreq)>
    {
        ($"{baseUrl}/", "1.0", "daily"),
        ($"{baseUrl}/veiculos", "0.95", "daily"),
        ($"{baseUrl}/veiculos/zero-km", "0.90", "daily"),
        ($"{baseUrl}/veiculos/seminovos", "0.90", "daily"),
        ($"{baseUrl}/veiculos/hibridos", "0.85", "weekly"),
        ($"{baseUrl}/veiculos/eletricos", "0.85", "weekly"),
        ($"{baseUrl}/veiculos/motos-eletricas", "0.80", "weekly"),
        ($"{baseUrl}/veiculos/taquaritinga", "0.85", "weekly"),
        ($"{baseUrl}/veiculos/seminovos-taquaritinga", "0.88", "weekly"),
        ($"{baseUrl}/veiculos/carros-automaticos-taquaritinga", "0.86", "weekly"),
        ($"{baseUrl}/veiculos/suvs-seminovos-taquaritinga", "0.84", "weekly"),
        ($"{baseUrl}/veiculos/carros-ate-50-mil", "0.84", "weekly"),
        ($"{baseUrl}/veiculos/financiamento", "0.82", "weekly"),
        ($"{baseUrl}/veiculos/troca-de-veiculos", "0.82", "weekly"),
        ($"{baseUrl}/Empresa", "0.70", "monthly"),
        ($"{baseUrl}/Contato", "0.75", "monthly"),
        ($"{baseUrl}/Privacy", "0.30", "yearly")
    };

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var veiculos = await db.Veiculos
        .AsNoTracking()
        .Where(x => x.Ativo && !x.Vendido)
        .OrderByDescending(x => x.Id)
        .Select(x => new { x.Id })
        .ToListAsync();
    urls.AddRange(veiculos.Select(x => ($"{baseUrl}/veiculo/{x.Id}/", "0.80", "daily")));

    var sb = new StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
    foreach (var (url, priority, changeFreq) in urls.DistinctBy(x => x.Url))
    {
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{WebUtility.HtmlEncode(url)}</loc>");
        sb.AppendLine($"    <lastmod>{now}</lastmod>");
        sb.AppendLine($"    <changefreq>{changeFreq}</changefreq>");
        sb.AppendLine($"    <priority>{priority}</priority>");
        sb.AppendLine("  </url>");
    }
    sb.AppendLine("</urlset>");
    return Results.Text(sb.ToString(), "application/xml");
});

app.MapControllers();
app.MapRazorPages();

await app.RunAsync();

static string GetRequiredConnectionString(IConfiguration configuration, string name, bool production)
{
    var connectionString = configuration.GetConnectionString(name);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException($"ConnectionStrings:{name} deve ser configurada por variavel de ambiente.");
    }

    if (production && IsLocalOrWindowsSqlServer(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:DefaultConnectionProd deve apontar para o SQL Server de producao em Docker/Linux.");
    }

    return connectionString;
}

static bool IsLocalOrWindowsSqlServer(string connectionString)
{
    var builder = new SqlConnectionStringBuilder(connectionString);
    var dataSource = builder.DataSource.Trim();

    return dataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase)
        || dataSource.Contains("MSSQLLocalDB", StringComparison.OrdinalIgnoreCase)
        || dataSource.Contains("SQLEXPRESS", StringComparison.OrdinalIgnoreCase)
        || dataSource.Contains('\\')
        || dataSource.Equals(".", StringComparison.Ordinal)
        || dataSource.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || dataSource.StartsWith("127.", StringComparison.Ordinal);
}

public partial class Program { }
