using System;
using System.Threading.Tasks;
using GetStartedApp.Models.Fiscal;

namespace GetStartedApp.Services.Fiscal;

public class RetornoCancelamentoNfce
{
    public bool Sucesso { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public int CodigoStatusSefaz { get; set; }
    public string? ProtocoloCancelamento { get; set; }
    public string? ProtocoloEvento { get => ProtocoloCancelamento; set => ProtocoloCancelamento = value; }
    public string? ProtocoloHomologacao { get => ProtocoloCancelamento; set => ProtocoloCancelamento = value; }
    public DateTime? DataHoraCancelamento { get; set; }
    public DateTime? DataHoraEvento { get => DataHoraCancelamento; set => DataHoraCancelamento = value; }
    public string? XmlCancelamento { get; set; }
    public string? XmlEventoAssinado { get => XmlCancelamento; set => XmlCancelamento = value; }
}

public interface INfceCancelamentoService
{
    Task<RetornoCancelamentoNfce> CancelarNfceAsync(
        int vendaId,
        string justificativa,
        string senhaSupervisor);

    Task<RetornoCancelamentoNfce> CancelarUltimaNfceAsync(
        string justificativa,
        string senhaSupervisor,
        ConfiguracaoFiscalEmpresa? fiscalConfig = null);

    Task<RetornoCancelamentoNfce> CancelarVendaAsync(
        int vendaId,
        string justificativa,
        string senhaSupervisor,
        ConfiguracaoFiscalEmpresa? fiscalConfig = null);
}
