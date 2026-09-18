using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GetStartedApp.Services;

public record InformacaoBackupDto
{
    public string NomeArquivo { get; init; } = string.Empty;
    public string CaminhoCompleto { get; init; } = string.Empty;
    public DateTime DataHoraCriacao { get; init; }
    public long TamanhoBytes { get; init; }

    public InformacaoBackupDto() { }
    public InformacaoBackupDto(string nomeArquivo, string caminhoCompleto, DateTime dataHoraCriacao, long tamanhoBytes)
    {
        NomeArquivo = nomeArquivo;
        CaminhoCompleto = caminhoCompleto;
        DataHoraCriacao = dataHoraCriacao;
        TamanhoBytes = tamanhoBytes;
    }
}

public interface IBackupDatabaseService
{
    Task<InformacaoBackupDto> ExecutarBackupAsync(string? motivo = "Manual");
    Task<List<InformacaoBackupDto>> ListarBackupsExistentesAsync();
    Task<int> LimparBackupsAntigosAsync(int diasRetencao = 30);
    Task<bool> RestaurarBackupAsync(string caminhoArquivoZip);
}
