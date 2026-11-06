namespace Project.Infrastructure.Deployment;

public static class StartupDatabasePolicy
{
    public static bool ShouldApplyMigrations(bool isDevelopment, bool? configured)
    {
        if (!isDevelopment && configured == true)
        {
            throw new InvalidOperationException(
                "Migrations no startup sao permitidas somente em Development. " +
                "Em outros ambientes, revisar e aplicar o script SQL por processo separado e autorizado.");
        }

        return isDevelopment && configured != false;
    }
}
