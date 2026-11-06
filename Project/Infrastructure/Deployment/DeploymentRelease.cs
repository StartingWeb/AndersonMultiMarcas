using System.Text.RegularExpressions;

namespace Project.Infrastructure.Deployment;

public static partial class DeploymentRelease
{
    public static string? ReadCommit(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return null;
        if (!CommitPattern().IsMatch(configured))
            throw new InvalidOperationException("Deployment:Commit deve conter o SHA Git completo em hexadecimal minusculo.");
        return configured;
    }

    [GeneratedRegex("\\A[0-9a-f]{40}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CommitPattern();
}
