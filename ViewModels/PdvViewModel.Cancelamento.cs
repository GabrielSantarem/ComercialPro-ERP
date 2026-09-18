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
    private readonly INfceCancelamentoService? _cancelamentoService;

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
        MensagemCancelamentoStatus = string.Empty;
        JustificativaCancelamento = string.Empty;
        SenhaSupervisorCancelamento = string.Empty;

        var ultimaVenda = await _pdvService.ObterUltimaVendaAsync();
        if (ultimaVenda == null)
        {
            MensagemCancelamentoStatus = "Nenhuma venda foi encontrada no sistema.";
            return;
        }

        VendaCancelamento = ultimaVenda;
        ModalCancelamentoAberto = true;
        _logger.LogInformation("Modal de cancelamento aberto para a venda #{Id}", ultimaVenda.Id);
    }

    [RelayCommand]
    public void FecharModalCancelamento()
    {
        ModalCancelamentoAberto = false;
        VendaCancelamento = null;
        JustificativaCancelamento = string.Empty;
        SenhaSupervisorCancelamento = string.Empty;
        MensagemCancelamentoStatus = string.Empty;
        IsCancelandoProcessando = false;
    }

    [RelayCommand]
    public async Task ConfirmarCancelamentoNfceAsync()
    {
        if (VendaCancelamento == null) return;

        if (_cancelamentoService == null)
        {
            MensagemCancelamentoStatus = "Serviço fiscal de cancelamento não configurado.";
            return;
        }

        IsCancelandoProcessando = true;
        MensagemCancelamentoStatus = "Comunicando com SEFAZ... Aguarde...";

        try
        {
            var retorno = await _cancelamentoService.CancelarNfceAsync(
                VendaCancelamento.Id,
                JustificativaCancelamento,
                SenhaSupervisorCancelamento);

            if (retorno.Sucesso)
            {
                MensagemCancelamentoStatus = $"✅ {retorno.Mensagem}";
                await AtualizarEstadoTurnoAsync();
                _logger.LogInformation("Venda #{Id} cancelada com sucesso via PDV.", VendaCancelamento.Id);
            }
            else
            {
                MensagemCancelamentoStatus = $"❌ {retorno.Mensagem}";
                _logger.LogWarning("Cancelamento da venda #{Id} rejeitado: {Msg}", VendaCancelamento.Id, retorno.Mensagem);
            }
        }
        catch (Exception ex)
        {
            MensagemCancelamentoStatus = $"❌ Falha: {ex.Message}";
            _logger.LogError(ex, "Exceção ao cancelar venda #{Id}", VendaCancelamento.Id);
        }
        finally
        {
            IsCancelandoProcessando = false;
        }
    }
}
