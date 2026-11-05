using System.Data.Common;
using Core.Storage;
using Data;
using Domain.Entities;
using Domain.Enums;
using ImageMagick;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Project.Features.Veiculos.Commands;
using Project.Features.Veiculos.Services;
using Project.Shared;

namespace Project.Features.Veiculos.Handlers;

public sealed class UploadMidiaVeiculoCommandHandler(
    ApplicationDbContext db,
    IVeiculoMediaService mediaService,
    ILogger<UploadMidiaVeiculoCommandHandler>? logger = null) : IRequestHandler<UploadMidiaVeiculoCommand, Result>
{
    public async Task<Result> Handle(UploadMidiaVeiculoCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        var veiculo = await db.Veiculos.Include(x => x.Midias).FirstOrDefaultAsync(x => x.Id == dto.VeiculoId, cancellationToken);
        if (veiculo is null) return Result.Failure("Veiculo nao encontrado.");

        IReadOnlyCollection<VeiculoMediaUploadResult> processados;
        try { processados = await mediaService.ProcessarUploadAsync(dto.VeiculoId, request.Arquivos, cancellationToken); }
        catch (Exception ex) when (ex is InvalidDataException or MagickException)
        { return Result.Failure("Imagem invalida ou fora dos limites permitidos."); }
        if (processados.Count == 0) return Result.Failure("Nenhuma imagem em formato suportado foi enviada.");

        IDbContextTransaction? transaction = null;
        try
        {
            if (db.Database.IsRelational()) transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var ordemBase = veiculo.Midias.Count == 0 ? 0 : veiculo.Midias.Max(x => x.Ordem) + 1;
            var index = 0;
            foreach (var item in processados)
            {
                var midia = new VeiculoMidia(dto.VeiculoId, item.NomeArquivo, item.Url, TipoMidia.Imagem, ordemBase + index++);
                midia.UpdateStorage(item.BlobName, item.Container, item.ContentType, item.TamanhoBytes);
                db.VeiculoMidias.Add(midia);
            }

            // Persist cleared covers before selecting the first image, within one transaction.
            // SQL Server's filtered unique index is checked on each UPDATE, not at commit.
            if (dto.DefinirPrimeiraComoCapa)
                foreach (var media in veiculo.Midias.Where(x => x.Capa))
                    db.Entry(media).Property(nameof(VeiculoMidia.Capa)).CurrentValue = false;
            await db.SaveChangesAsync(cancellationToken);

            if (dto.DefinirPrimeiraComoCapa)
            {
                var cover = await db.VeiculoMidias.Where(x => x.VeiculoId == dto.VeiculoId && x.Ativo && x.Tipo == TipoMidia.Imagem)
                    .OrderBy(x => x.Ordem).ThenBy(x => x.Id).FirstAsync(cancellationToken);
                cover.DefinirComoCapa();
                await db.SaveChangesAsync(cancellationToken);
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException or OperationCanceledException)
        {
            if (transaction is not null)
            {
                try { await transaction.RollbackAsync(CancellationToken.None); }
                catch (Exception rollbackError) { logger?.LogWarning(rollbackError, "Falha ao desfazer persistencia das midias do veiculo {VehicleId}.", dto.VeiculoId); }
                await transaction.DisposeAsync();
                transaction = null;
            }
            db.ChangeTracker.Clear();
            await RemoveUnreferencedUploadsAsync(processados);
            if (ex is OperationCanceledException) throw;
            logger?.LogWarning(ex, "Nao foi possivel persistir midias do veiculo {VehicleId}.", dto.VeiculoId);
            return Result.Failure("Nao foi possivel salvar as imagens. Tente novamente.");
        }
        finally { if (transaction is not null) await transaction.DisposeAsync(); }
    }

    private async Task RemoveUnreferencedUploadsAsync(IEnumerable<VeiculoMediaUploadResult> uploads)
    {
        foreach (var upload in uploads.DistinctBy(x => x.BlobName))
        {
            try
            {
                // An uncertain commit or a reused key must never remove a referenced object.
                if (await db.VeiculoMidias.AsNoTracking().AnyAsync(x => x.BlobName == upload.BlobName || x.Url == upload.Url, CancellationToken.None)) continue;
                await mediaService.RemoverArquivoAsync(new StorageImageReference(upload.Url, upload.BlobName, upload.Container), CancellationToken.None);
            }
            catch (Exception cleanupError)
            {
                logger?.LogWarning(cleanupError, "Upload sem persistencia requer revisao de limpeza: {StorageKey}.", upload.BlobName);
            }
        }
    }
}
