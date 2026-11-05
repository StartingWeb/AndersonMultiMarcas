using Data;
using Domain.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Project.Tests.Security;

public sealed class IdentitySecurityTests(SecurityWebApplicationFactory factory) : IClassFixture<SecurityWebApplicationFactory>
{
    [Fact]
    public async Task BootstrapIsDisabledByDefault()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetCoreUser>>();
        var before = users.Users.Count();
        await IdentitySeed.EnsureDeveloperUserAsync(scope.ServiceProvider);
        Assert.Equal(before, users.Users.Count());
    }

    [Fact]
    public async Task BootstrapDoesNotAlterExistingUserOrGrantRoles()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetCoreUser>>();
        var user = await NewUserAsync(users);
        var originalHash = user.PasswordHash;
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        config["DevelopmentBootstrap:Enabled"] = "true";
        config["DevelopmentBootstrap:Email"] = user.Email;
        config["DevelopmentBootstrap:UserName"] = user.UserName;
        config["DevelopmentBootstrap:Password"] = RandomPassword();
        try
        {
            await IdentitySeed.EnsureDeveloperUserAsync(scope.ServiceProvider);
            Assert.Equal(originalHash, user.PasswordHash);
            Assert.False(user.EmailConfirmed);
            Assert.Empty(await users.GetRolesAsync(user));
        }
        finally { config["DevelopmentBootstrap:Enabled"] = "false"; config["DevelopmentBootstrap:Password"] = null; }
    }

    [Fact]
    public async Task BootstrapRequiresExternalPasswordAndCreatesOnlyConfiguredLocalUser()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetCoreUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var email = $"{Guid.NewGuid():N}@example.test";
        var password = RandomPassword();
        config["DevelopmentBootstrap:Enabled"] = "true";
        config["DevelopmentBootstrap:Email"] = email;
        config["DevelopmentBootstrap:UserName"] = Guid.NewGuid().ToString("N");
        config["DevelopmentBootstrap:Password"] = null;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => IdentitySeed.EnsureDeveloperUserAsync(scope.ServiceProvider));
            Assert.Null(await users.FindByEmailAsync(email));
            config["DevelopmentBootstrap:Password"] = password;
            await IdentitySeed.EnsureDeveloperUserAsync(scope.ServiceProvider);
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.True(await users.CheckPasswordAsync(user, password));
            Assert.Equal(["Desenvolvedor"], await users.GetRolesAsync(user));
            var count = users.Users.Count();
            await IdentitySeed.EnsureDeveloperUserAsync(scope.ServiceProvider);
            Assert.Equal(count, users.Users.Count());
        }
        finally { config["DevelopmentBootstrap:Enabled"] = "false"; config["DevelopmentBootstrap:Password"] = null; }
    }

    [Fact]
    public async Task PasswordResetTokenIsIndividualAndSingleUse()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetCoreUser>>();
        var user = await NewUserAsync(users);
        var other = await NewUserAsync(users);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        Assert.False((await users.ResetPasswordAsync(other, token, RandomPassword())).Succeeded);
        var password = RandomPassword();
        Assert.True((await users.ResetPasswordAsync(user, token, password)).Succeeded);
        Assert.True(await users.CheckPasswordAsync(user, password));
        Assert.False((await users.ResetPasswordAsync(user, token, RandomPassword())).Succeeded);
    }

    [Fact]
    public async Task ExpiredPasswordResetTokenIsRejected()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetCoreUser>>();
        var user = await NewUserAsync(users);
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;
        Assert.Equal(TimeSpan.FromHours(1), options.TokenLifespan);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        options.TokenLifespan = TimeSpan.FromMinutes(-1);
        try { Assert.False((await users.ResetPasswordAsync(user, token, RandomPassword())).Succeeded); }
        finally { options.TokenLifespan = TimeSpan.FromHours(1); }
    }

    private static async Task<AspNetCoreUser> NewUserAsync(UserManager<AspNetCoreUser> users)
    {
        var user = new AspNetCoreUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N"), Email = $"{Guid.NewGuid():N}@example.test" };
        Assert.True((await users.CreateAsync(user, RandomPassword())).Succeeded);
        return user;
    }
    private static string RandomPassword() => $"Aa1!{Guid.NewGuid():N}";
}
