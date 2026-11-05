using Data;
using Core.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Project.Features.Storage.Legacy;

namespace Project.Tests.Security;

public sealed class SecurityWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnectionDev", "Server=localhost;Database=SecurityTests;Integrated Security=true;TrustServerCertificate=true");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ApplyMigrationsOnStartup"] = "false",
            ["DevelopmentBootstrap:Enabled"] = "false"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService, MemoryStorageService>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase($"security-{GetHashCode()}"));
            var worker = services.SingleOrDefault(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(LegacyImageImportWorker));
            if (worker is not null) services.Remove(worker);
        });
    }
}
