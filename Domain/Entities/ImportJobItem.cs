using Domain.Common;

namespace Domain.Entities;

public sealed class ImportJobItem : BaseEntity
{
    public int ImportJobId { get; private set; }
    public int VeiculoId { get; private set; }
    public int? VeiculoMidiaId { get; private set; }
    public int Ordem { get; private set; }
    public bool Capa { get; private set; }
    public string UrlLegada { get; private set; } = null!;
    public string NomeArquivoDestino { get; private set; } = null!;
    public string BlobNameDestino { get; private set; } = null!;
    public string? ContainerDestino { get; private set; }
    public string? UrlDestino { get; private set; }
    public string Status { get; private set; } = "Pendente";
    public int Tentativas { get; private set; }
    public int MaxTentativas { get; private set; } = 3;
    public string? ContentType { get; private set; }
    public long? TamanhoBytes { get; private set; }
    public string? Erro { get; private set; }
    public DateTime? IniciadoEm { get; private set; }
    public DateTime? FinalizadoEm { get; private set; }
    public string? LockId { get; private set; }
    public DateTime? LockExpiraEm { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public ImportJob ImportJob { get; private set; } = null!;
    public Veiculo Veiculo { get; private set; } = null!;
    public VeiculoMidia? VeiculoMidia { get; private set; }
    public ICollection<ImportJobLog> Logs { get; private set; } = [];

    private ImportJobItem() { }

    public ImportJobItem(
        int importJobId,
        int veiculoId,
        int? veiculoMidiaId,
        int ordem,
        bool capa,
        string urlLegada,
        string nomeArquivoDestino,
        string blobNameDestino,
        int maxTentativas)
    {
        ImportJobId = importJobId;
        VeiculoId = veiculoId;
        VeiculoMidiaId = veiculoMidiaId;
        Ordem = ordem;
        Capa = capa;
        UrlLegada = string.IsNullOrWhiteSpace(urlLegada) ? throw new ArgumentException("URL legada obrigatoria.") : urlLegada.Trim();
        NomeArquivoDestino = string.IsNullOrWhiteSpace(nomeArquivoDestino) ? throw new ArgumentException("Nome do arquivo obrigatorio.") : nomeArquivoDestino.Trim();
        BlobNameDestino = string.IsNullOrWhiteSpace(blobNameDestino) ? throw new ArgumentException("BlobName obrigatorio.") : blobNameDestino.Trim();
        MaxTentativas = Math.Max(1, maxTentativas);
    }

    public bool IsTerminal =>
        Status is "Concluido" or "Ignorado" or "PendenteRevisao"
        || (Status == "Erro" && Tentativas >= MaxTentativas);

    public void AttachMedia(int? veiculoMidiaId)
    {
        VeiculoMidiaId = veiculoMidiaId;
    }

    public void MarkPending(string? message = null)
    {
        Status = "Pendente";
        Erro = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        FinalizadoEm = null;
        ClearLock();
    }

    public void RequestRetry(int maxAttempts)
    {
        MaxTentativas = checked(Tentativas + Math.Clamp(maxAttempts, 1, 10));
        MarkPending("Falha reenfileirada por solicitacao do usuario.");
    }

    public void MarkRunning(string lockId, DateTime lockExpiresAt)
    {
        Status = "EmExecucao";
        IniciadoEm = DateTime.UtcNow;
        FinalizadoEm = null;
        LockId = lockId;
        LockExpiraEm = lockExpiresAt;
    }

    public void IncrementAttempt()
    {
        Tentativas++;
    }

    public void UpdateDestination(string? container, string? url, string? contentType, long? sizeBytes)
    {
        ContainerDestino = string.IsNullOrWhiteSpace(container) ? null : container.Trim();
        UrlDestino = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim();
        TamanhoBytes = sizeBytes;
    }

    public void MarkSucceeded()
    {
        Status = "Concluido";
        Erro = null;
        FinalizadoEm = DateTime.UtcNow;
        ClearLock();
    }

    public void MarkIgnored(string message)
    {
        Status = "Ignorado";
        Erro = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        FinalizadoEm = DateTime.UtcNow;
        ClearLock();
    }

    public void MarkFailed(string message)
    {
        Status = "Erro";
        Erro = string.IsNullOrWhiteSpace(message) ? "Falha desconhecida." : message.Trim();
        FinalizadoEm = DateTime.UtcNow;
        ClearLock();
    }

    public void RequestReview(string message)
    {
        Status = "PendenteRevisao";
        Erro = string.IsNullOrWhiteSpace(message) ? "Associacao deterministica nao encontrada." : message.Trim();
        FinalizadoEm = DateTime.UtcNow;
        ClearLock();
    }

    public void ClearLock()
    {
        LockId = null;
        LockExpiraEm = null;
    }
}
