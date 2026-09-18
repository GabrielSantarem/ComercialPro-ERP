using System.Threading.Tasks;

namespace GetStartedApp.Services.Fiscal;

public record ResumoFechamentoFiscalDto(
    int TotalAutorizadas,
    int TotalCanceladas,
    decimal FaturamentoTotal,
    decimal TotalImpostosAproximados,
    string CaminhoArquivoZip);

public interface IFechamentoFiscalService
{
    Task<ResumoFechamentoFiscalDto> GerarPacoteMensalAsync(int ano, int mes, string diretorioDestino);
}
