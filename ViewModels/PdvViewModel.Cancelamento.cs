using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GetStartedApp.Models;
using GetStartedApp.Services.Fiscal;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class PdvViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    private bool _modalCancelamentoAberto;

    [ObservableProperty]
    private Venda? _vendaCancelamento;

    [ObservableProperty]
    private string _justificativaCancelamento = string.Empty;

    [ObservableProperty]
    private string _senhaSupervisorCancelamento = string.Empty;

    [ObservableProperty]
    private string _mensagemCancelamentoStatus = string.Empty;

    [ObservableProperty]
    private bool _isCancelandoProcessando;

    [RelayCommand]
    public async Task AbrirModalCancelamentoAsync()
    {
        _logger.LogInformation("Abrindo modal de cancelamento de NFC-e...");
        
        // Pega a última venda cadastrada no sistema
        var ultimaVenda = await _pdvService.ObterUltimaVendaAsync();
        if (ultimaVenda == null)
        {
            _logger.LogWarning("Nenhuma venda encontrada para cancelar.");
            return;
        }

        if (ultimaVenda.Status == "CANCELADA")
        {
            MensagemCancelamentoStatus = "A última venda já se encontra cancelada.";
        }
        else
        {
            MensagemCancelamentoStatus = string.Empty;
        }

        VendaCancelamento = ultimaVenda;
        JustificativaCancelamento = string.Empty;
        SenhaSupervisorCancelamento = string.Empty;
        ModalCancelamentoAberto = true;
    }

    [RelayCommand]
    public void FecharModalCancelamento()
    {
        ModalCancelamentoAberto = false;
        VendaCancelamento = null;
        MensagemCancelamentoStatus = string.Empty;
        SenhaSupervisorCancelamento = string.Empty;
        JustificativaCancelamento = string.Empty;
    }

    [RelayCommand]
    public async Task ConfirmarCancelamentoNfceAsync()
    {
        if (VendaCancelamento == null) return;

        if (_cancelamentoService == null)
        {
            MensagemCancelamentoStatus = "Serviço de cancelamento não configurado.";
            return;
        }

        IsCancelandoProcessando = true;
        MensagemCancelamentoStatus = "Transmitindo Evento de Cancelamento (110111) para a SEFAZ...";

        try
        {
            var retorno = await _cancelamentoService.CancelarNfceAsync(
                VendaCancelamento.Id,
                JustificativaCancelamento,
                SenhaSupervisorCancelamento);

            if (retorno.Sucesso)
            {
                MensagemCancelamentoStatus = $"✅ SUCESSO: {retorno.Mensagem} (Protocolo: {retorno.ProtocoloHomologacao})";
                _logger.LogInformation("Cancelamento homologado. Protocolo: {Prot}", retorno.ProtocoloHomologacao);
                
                // Recarrega estado da tela
                await AtualizarEstadoTurnoAsync();
            }
            else
            {
                MensagemCancelamentoStatus = $"❌ REJEIÇÃO: {retorno.Mensagem}";
                _logger.LogWarning("Falha no cancelamento: {Msg}", retorno.Mensagem);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao cancelar NFC-e.");
            MensagemCancelamentoStatus = $"Erro interno: {ex.Message}";
        }
        finally
        {
            IsCancelandoProcessando = false;
        }
    }
}
