using Data;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Deployment;
using Xunit;

namespace Project.Tests.Deployment;

public sealed class DeploymentSafetyTests
{
    [Theory]
    [InlineData(true, null, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, null, false)]
    [InlineData(false, false, false)]
    public void StartupMigrations_AreDisabledOutsideDevelopment(bool development, bool? setting, bool expected)
        => Assert.Equal(expected, StartupDatabasePolicy.ShouldApplyMigrations(development, setting));

    [Fact]
    public void StartupMigrations_CannotBeEnabledInProductionOrStaging()
        => Assert.Throws<InvalidOperationException>(() => StartupDatabasePolicy.ShouldApplyMigrations(false, true));

    [Fact]
    public void DesignTimeFactory_DoesNotCarryAConnectionOrRunApplicationStartup()
    {
        using var db = new DesignTimeDbContextFactory().CreateDbContext([]);
        Assert.Null(db.Database.GetConnectionString());
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);
        Assert.NotEmpty(db.Database.GetMigrations());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DevelopmentWithoutReleaseMetadata_IsSupported(string? value)
        => Assert.Null(DeploymentRelease.ReadCommit(value));

    [Fact]
    public void ReleaseMetadata_AcceptsOnlyFullCommitHashes()
    {
        var commit = new string('a', 40);
        Assert.Equal(commit, DeploymentRelease.ReadCommit(commit));
        Assert.Throws<InvalidOperationException>(() => DeploymentRelease.ReadCommit("main"));
        Assert.Throws<InvalidOperationException>(() => DeploymentRelease.ReadCommit(commit + "\n"));
    }
}
