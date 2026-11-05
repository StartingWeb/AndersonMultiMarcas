using System.Globalization;
using System.Text;
using Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Project.Features.Storage.Legacy;

public sealed class LegacyImageImportJobManager(
    ApplicationDbContext db,
    LegacyImageImportQueue queue,
    LegacyImportCancellationRegistry cancellationRegistry,
    IOptions<LegacyImageImportOptions> options)
{
    public async Task<ImportJob> StartAsync(
        LegacyImageImportRequest request,
        string? userId,
        string? userName,
        CancellationToken ct)
    {
        var normalized = Normalize(request);
        EnsureBaseUrlAllowed(normalized.BaseUrl, options.Value.AllowedHosts);

        var hasActiveJob = await db.ImportJobs
            .AnyAsync(x => LegacyImageImportJobStatus.Active.Contains(x.Status), ct);
        if (hasActiveJob)
        {
            throw new InvalidOperationException("Ja existe uma importacao em andamento.");
        }

        var job = new ImportJob(
            normalized.BaseUrl,
            normalized.DryRun,
            normalized.OnlyWithoutBlobName,
            normalized.OverwriteExisting,
            normalized.StartId,
            normalized.MaxVehicles,
            userId,
            userName);

        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(ct);

        AddLog(job.Id, null, null, null, "Inicio", "Pendente", "Job criado e enfileirado.");
        AddHistory(job.Id, "Iniciado", userId, userName, normalized.MaxVehicles, null, "Pendente", "Job criado e enfileirado.");
        await db.SaveChangesAsync(ct);
        await queue.QueueAsync(job.Id, ct);

        return job;
    }

    public async Task<IReadOnlyList<LegacyImageImportJobListItem>> ListJobsAsync(CancellationToken ct)
        => await ListJobsAsync(new LegacyImageImportFilters(), ct);

    public async Task<IReadOnlyList<LegacyImageImportJobListItem>> ListJobsAsync(LegacyImageImportFilters filters, CancellationToken ct)
    {
        var query = db.ImportJobs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filters.Status))
        {
            query = query.Where(x => x.Status == filters.Status);
        }

        if (!string.IsNullOrWhiteSpace(filters.User))
        {
            var user = filters.User.Trim();
            query = query.Where(x => x.UsuarioNome != null && x.UsuarioNome.Contains(user));
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.Trim();
            query = query.Where(x => x.Id.ToString().Contains(search)
                || (x.UltimaMensagem != null && x.UltimaMensagem.Contains(search)));
        }

        if (filters.PeriodStart.HasValue)
        {
            query = query.Where(x => x.CriadoEm >= filters.PeriodStart.Value.Date);
        }

        if (filters.PeriodEnd.HasValue)
        {
            query = query.Where(x => x.CriadoEm < filters.PeriodEnd.Value.Date.AddDays(1));
        }

        if (filters.OnlyErrors)
        {
            query = query.Where(x => x.Status == LegacyImageImportJobStatus.Failed
                || x.Status == LegacyImageImportJobStatus.CompletedWithFailures);
        }

        if (filters.OnlyPending)
        {
            query = query.Where(x => x.Status == LegacyImageImportJobStatus.Pending
                || x.Status == LegacyImageImportJobStatus.Running
                || x.Status == LegacyImageImportJobStatus.Cancelling);
        }

        if (filters.OnlyCompleted)
        {
            query = query.Where(x => x.Status == LegacyImageImportJobStatus.Completed);
        }

        var jobs = await query.OrderByDescending(x => x.Id).Take(100).ToListAsync(ct);

        return jobs.Select(x => new LegacyImageImportJobListItem(
                x.Id,
                x.Status,
                new DateTimeOffset(DateTime.SpecifyKind(x.CriadoEm, DateTimeKind.Utc)),
                ToOffset(x.IniciadoEm),
                ToOffset(x.FinalizadoEm),
                x.UsuarioNome,
                x.DryRun,
                x.SomenteSemBlobName,
                x.Sobrescrever,
                x.TotalVeiculos,
                x.VeiculosProcessados,
                x.TotalImagens,
                x.ImagensImportadas,
                x.ImagensIgnoradas,
                x.ImagensComErro,
                x.UltimaMensagem))
            .ToList();
    }

    public async Task<LegacyImageImportSnapshot?> GetSnapshotAsync(int jobId, int? afterLogIndex, CancellationToken ct)
    {
        var job = await db.ImportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (job is null)
        {
            return null;
        }

        var totalImages = await db.ImportJobItems.CountAsync(x => x.ImportJobId == jobId, ct);
        var imagesImported = await db.ImportJobItems.CountAsync(x => x.ImportJobId == jobId && x.Status == LegacyImageImportItemStatus.Completed, ct);
        var imagesSkipped = await db.ImportJobItems.CountAsync(x => x.ImportJobId == jobId && x.Status == LegacyImageImportItemStatus.Ignored, ct);
        var imagesWithError = await db.ImportJobItems.CountAsync(x => x.ImportJobId == jobId
            && (x.Status == LegacyImageImportItemStatus.Failed || x.Status == LegacyImageImportItemStatus.Review), ct);
        var imagesPending = await db.ImportJobItems.CountAsync(x => x.ImportJobId == jobId
            && (x.Status == LegacyImageImportItemStatus.Pending || x.Status == LegacyImageImportItemStatus.Running), ct);
        var vehiclesProcessed = await CountProcessedVehiclesAsync(jobId, ct);
        var vehiclesWithImportedImages = await CountVehiclesByAnyItemStatusAsync(jobId, LegacyImageImportItemStatus.Completed, ct);
        var vehiclesSkipped = await CountVehiclesByAnyItemStatusesAsync(
            jobId,
            [LegacyImageImportItemStatus.Ignored],
            ct);
        var vehiclesWithError = await CountVehiclesByAnyItemStatusesAsync(
            jobId,
            [LegacyImageImportItemStatus.Failed, LegacyImageImportItemStatus.Review],
            ct);

        totalImages = Math.Max(totalImages, job.TotalImagens);
        vehiclesProcessed = Math.Max(vehiclesProcessed, job.VeiculosProcessados);

        var logsQuery = db.ImportJobLogs
            .AsNoTracking()
            .Where(x => x.ImportJobId == jobId);

        if (afterLogIndex.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.Id > afterLogIndex.Value);
        }

        var logs = await logsQuery
            .OrderByDescending(x => x.Id)
            .Take(300)
            .OrderBy(x => x.Id)
            .Select(x => new LegacyImageImportLogEntry(
                x.Id,
                new DateTimeOffset(DateTime.SpecifyKind(x.CriadoEm, DateTimeKind.Utc)),
                x.VeiculoId,
                x.ImagemOrdem,
                x.Etapa,
                x.Status,
                x.Mensagem,
                x.UrlLegada))
            .ToListAsync(ct);

        var started = job.IniciadoEm ?? job.CriadoEm;
        var end = job.FinalizadoEm ?? DateTime.UtcNow;
        var elapsed = end - started;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var remainingVehicles = Math.Max(0, job.TotalVeiculos - vehiclesProcessed);
        var eta = vehiclesProcessed > 0 && job.Status == LegacyImageImportJobStatus.Running
            ? TimeSpan.FromTicks(elapsed.Ticks / Math.Max(1, vehiclesProcessed) * remainingVehicles)
            : TimeSpan.Zero;

        return new LegacyImageImportSnapshot(
            job.Id,
            job.Status,
            job.VeiculoAtualId,
            job.TotalVeiculos,
            vehiclesProcessed,
            remainingVehicles,
            vehiclesWithImportedImages,
            vehiclesSkipped,
            vehiclesWithError,
            imagesImported,
            imagesImported,
            imagesSkipped,
            imagesWithError,
            elapsed,
            eta,
            elapsed.TotalMinutes <= 0 ? 0 : imagesImported / elapsed.TotalMinutes,
            logs,
            totalImages,
            imagesPending);
    }

    public async Task<bool> CancelAsync(int jobId, CancellationToken ct)
        => await CancelAsync(jobId, null, null, ct);

    public async Task<bool> CancelAsync(int jobId, string? userId, string? userName, CancellationToken ct)
    {
        var job = await db.ImportJobs.FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (job is null || LegacyImageImportJobStatus.IsTerminal(job.Status))
        {
            return false;
        }

        job.RequestCancellation();
        AddLog(job.Id, null, null, null, "Sistema", "Cancelamento", "Cancelamento solicitado pelo usuario.");
        AddHistory(job.Id, "Cancelado", userId, userName, null, null, "CancelamentoSolicitado", "Cancelamento solicitado pelo usuario.");
        await db.SaveChangesAsync(ct);
        cancellationRegistry.Cancel(jobId);
        return true;
    }

    public async Task<bool> ResumeAsync(int jobId, CancellationToken ct)
        => await ResumeAsync(jobId, null, null, ct);

    public async Task<bool> ResumeAsync(int jobId, string? userId, string? userName, CancellationToken ct)
        => await QueueExistingJobAsync(jobId, retryFailures: true, "Retomado", userId, userName, "Retomada solicitada pelo usuario.", ct);

    public async Task<bool> RetryFailuresAsync(int jobId, CancellationToken ct)
        => await RetryFailuresAsync(jobId, null, null, ct);

    public async Task<bool> RetryFailuresAsync(int jobId, string? userId, string? userName, CancellationToken ct)
        => await QueueExistingJobAsync(jobId, retryFailures: true, "Reprocessado", userId, userName, "Reprocessamento de falhas solicitado pelo usuario.", ct);

    public async Task<byte[]?> CsvBytesAsync(int jobId, CancellationToken ct)
    {
        var exists = await db.ImportJobs.AnyAsync(x => x.Id == jobId, ct);
        if (!exists)
        {
            return null;
        }

        var logs = await db.ImportJobLogs
            .AsNoTracking()
            .Where(x => x.ImportJobId == jobId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Id;TimestampUtc;Veiculo;Imagem;Etapa;Status;Mensagem;Url");
        foreach (var log in logs)
        {
            sb.Append(log.Id.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Escape(log.CriadoEm.ToString("O", CultureInfo.InvariantCulture))).Append(';')
                .Append(log.VeiculoId?.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(log.ImagemOrdem?.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Escape(log.Etapa)).Append(';')
                .Append(Escape(log.Status)).Append(';')
                .Append(Escape(log.Mensagem)).Append(';')
                .Append(Escape(log.UrlLegada))
                .AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task<bool> QueueExistingJobAsync(
        int jobId,
        bool retryFailures,
        string historyType,
        string? userId,
        string? userName,
        string message,
        CancellationToken ct)
    {
        var job = await db.ImportJobs
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (job is null)
        {
            return false;
        }

        if (await db.ImportJobs.AnyAsync(x => x.Id != jobId && LegacyImageImportJobStatus.Active.Contains(x.Status), ct))
        {
            throw new InvalidOperationException("Ja existe outra importacao em andamento.");
        }

        var quantity = 0;
        foreach (var item in job.Items.Where(x => x.Status == LegacyImageImportItemStatus.Running))
        {
            item.MarkPending("Item retomado.");
            quantity++;
        }

        if (retryFailures)
        {
            foreach (var item in job.Items.Where(x => x.Status == LegacyImageImportItemStatus.Failed))
            {
                item.RequestRetry(options.Value.SafeMaxAttempts);
                quantity++;
            }
        }

        job.MarkQueued(message);
        AddLog(job.Id, null, null, null, "Sistema", "Pendente", message);
        AddHistory(job.Id, historyType, userId, userName, quantity, null, "Pendente", message);
        await db.SaveChangesAsync(ct);
        await queue.QueueAsync(job.Id, ct);
        return true;
    }

    private void AddLog(int jobId, int? itemId, int? vehicleId, int? imageIndex, string stage, string status, string message, string? imageUrl = null)
        => db.ImportJobLogs.Add(new ImportJobLog(jobId, itemId, vehicleId, imageIndex, imageUrl, stage, status, message));

    private void AddHistory(
        int jobId,
        string type,
        string? userId,
        string? userName,
        int? quantity,
        TimeSpan? duration,
        string? result,
        string? message)
        => db.ImportJobHistory.Add(new ImportJobHistory(jobId, type, userId, userName, quantity, duration, result, message));

    private async Task<int> CountProcessedVehiclesAsync(int jobId, CancellationToken ct)
    {
        var vehiclesWithItems = await db.ImportJobItems
            .Where(x => x.ImportJobId == jobId)
            .Select(x => x.VeiculoId)
            .Distinct()
            .ToListAsync(ct);

        if (vehiclesWithItems.Count == 0)
        {
            return 0;
        }

        var vehiclesWithOpenItems = await db.ImportJobItems
            .Where(x => x.ImportJobId == jobId && !LegacyImageImportItemStatus.Terminal.Contains(x.Status))
            .Select(x => x.VeiculoId)
            .Distinct()
            .ToListAsync(ct);

        return vehiclesWithItems.Except(vehiclesWithOpenItems).Count();
    }

    private async Task<int> CountVehiclesByAnyItemStatusAsync(int jobId, string status, CancellationToken ct)
        => await CountVehiclesByAnyItemStatusesAsync(jobId, [status], ct);

    private async Task<int> CountVehiclesByAnyItemStatusesAsync(int jobId, string[] statuses, CancellationToken ct)
        => await db.ImportJobItems
            .Where(x => x.ImportJobId == jobId && statuses.Contains(x.Status))
            .Select(x => x.VeiculoId)
            .Distinct()
            .CountAsync(ct);

    private static LegacyImageImportRequest Normalize(LegacyImageImportRequest request)
        => new()
        {
            BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl)
                ? "https://andersonmultimarcas.com.br"
                : request.BaseUrl.Trim().TrimEnd('/'),
            OnlyWithoutBlobName = request.OnlyWithoutBlobName,
            OverwriteExisting = request.OverwriteExisting,
            DryRun = request.DryRun,
            MaxVehicles = request.MaxVehicles is > 0 ? request.MaxVehicles : null,
            StartId = request.StartId is > 0 ? request.StartId : null
        };

    private static void EnsureBaseUrlAllowed(string baseUrl, string[] allowedHosts)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("URL base do site legado deve ser HTTPS absoluta.");
        }

        if (!LegacySourceUrlGuard.IsAllowedHost(uri.Host, allowedHosts))
        {
            throw new InvalidOperationException("URL base deve pertencer ao dominio andersonmultimarcas.com.br.");
        }
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
