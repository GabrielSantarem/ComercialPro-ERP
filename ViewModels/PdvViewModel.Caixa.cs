using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using GetStartedApp.Models;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class PdvViewModel
{
    // === CONTROLE DE TURNOS DE CAIXA (ABERTURA, SANGRIA, FECHAMENTO) ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusCaixaTexto))]
    [NotifyPropertyChangedFor(nameof(StatusCaixaCor))]
    [NotifyPropertyChangedFor(nameof(SaldoCaixaDinheiroTexto))]
    public partial CaixaTurno? TurnoAtual { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusCaixaTexto))]
    [NotifyPropertyChangedFor(nameof(StatusCaixaCor))]
    [NotifyPropertyChangedFor(nameof(SaldoCaixaDinheiroTexto))]
    public partial bool IsCaixaAberto { get; set; }

    public string StatusCaixaTexto => IsCaixaAberto ? $"🟢 CAIXA ABERTO (TURNO #{TurnoAtual?.Id})" : "🔴 CAIXA FECHADO";
    public string StatusCaixaCor => IsCaixaAberto ? "#27AE60" : "#C0392B";
    public string SaldoCaixaDinheiroTexto => IsCaixaAberto ? $"Gaveta: R$ {TurnoAtual?.SaldoEsperadoEmDinheiro:N2}" : "Abra o Caixa";

    // === MODAL DE OPERAÇÕES DE CAIXA ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    public partial bool ModalCaixaAberto { get; set; }

    [ObservableProperty] public partial string TipoModalCaixa { get; set; } = "ABERTURA";
    [ObservableProperty] public partial string TituloModalCaixa { get; set; } = string.Empty;
    [ObservableProperty] public partial string DescricaoModalCaixa { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal ValorModalCaixa { get; set; }
    [ObservableProperty] public partial string MotivoModalCaixa { get; set; } = string.Empty;
    [ObservableProperty] public partial string MensagemCaixaErro { get; set; } = string.Empty;

    public async Task AtualizarEstadoTurnoAsync()
    {
        TurnoAtual = await _pdvService.ObterTurnoAtualAsync();
        IsCaixaAberto = TurnoAtual != null;
        OnPropertyChanged(nameof(TurnoAtual));
        OnPropertyChanged(nameof(IsCaixaAberto));
        OnPropertyChanged(nameof(StatusCaixaTexto));
        OnPropertyChanged(nameof(StatusCaixaCor));
        OnPropertyChanged(nameof(SaldoCaixaDinheiroTexto));
    }

    [RelayCommand]
    public void AbrirModalAberturaCaixa()
    {
        TipoModalCaixa = "ABERTURA";
        TituloModalCaixa = "🟢 ABERTURA DE TURNO DE CAIXA";
        DescricaoModalCaixa = "Informe o fundo de troco inicial em dinheiro colocado na gaveta:";
        ValorModalCaixa = 100.00m;
        MotivoModalCaixa = "Fundo de troco inicial";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void AbrirModalSuprimento()
    {
        TipoModalCaixa = "SUPRIMENTO";
        TituloModalCaixa = "➕ SUPRIMENTO DE CAIXA (ENTRADA DE TROCO)";
        DescricaoModalCaixa = "Informe o valor em dinheiro que está entrando na gaveta:";
        ValorModalCaixa = 50.00m;
        MotivoModalCaixa = "Troco extra em moedas/cédulas";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void AbrirModalSangria()
    {
        TipoModalCaixa = "SANGRIA";
        TituloModalCaixa = "➖ SANGRIA DE CAIXA (RETIRADA PARA COFRE)";
        DescricaoModalCaixa = $"Saldo disponível na gaveta: R$ {TurnoAtual?.SaldoEsperadoEmDinheiro:N2}. Digite o valor a retirar:";
        ValorModalCaixa = Math.Min(100.00m, TurnoAtual?.SaldoEsperadoEmDinheiro ?? 0m);
        MotivoModalCaixa = "Recolhimento para o cofre pelo gerente";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void AbrirModalFechamentoCaixa()
    {
        TipoModalCaixa = "FECHAMENTO";
        TituloModalCaixa = "🔒 FECHAMENTO CEGO DE TURNO";
        DescricaoModalCaixa = "Conte o dinheiro físico presente na gaveta e informe o valor total apurado:";
        ValorModalCaixa = 0m;
        MotivoModalCaixa = "Fechamento de expediente";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void FecharModalCaixa()
    {
        ModalCaixaAberto = false;
        MensagemCaixaErro = string.Empty;
    }

    [RelayCommand]
    public async Task ConfirmarAcaoCaixaAsync()
    {
        try
        {
            MensagemCaixaErro = string.Empty;
            var vendedorId = VendedorSelecionado?.Id ?? 1;

            switch (TipoModalCaixa)
            {
                case "ABERTURA":
                    await _pdvService.AbrirCaixaAsync(vendedorId, ValorModalCaixa, MotivoModalCaixa);
                    break;

                case "SUPRIMENTO":
                    if (TurnoAtual == null) throw new InvalidOperationException("Nenhum turno aberto.");
                    await _pdvService.RegistrarSuprimentoAsync(TurnoAtual.Id, ValorModalCaixa, MotivoModalCaixa);
                    break;

                case "SANGRIA":
                    if (TurnoAtual == null) throw new InvalidOperationException("Nenhum turno aberto.");
                    await _pdvService.RegistrarSangriaAsync(TurnoAtual.Id, ValorModalCaixa, MotivoModalCaixa);
                    break;

                case "FECHAMENTO":
                    if (TurnoAtual == null) throw new InvalidOperationException("Nenhum turno aberto.");
                    var turnoFechado = await _pdvService.FecharCaixaAsync(TurnoAtual.Id, ValorModalCaixa, MotivoModalCaixa);
                    _logger.LogInformation("Fechamento concluído. Quebra: R$ {Quebra}", turnoFechado.DiferencaQuebra);
                    break;
            }

            await AtualizarEstadoTurnoAsync();
            ModalCaixaAberto = false;
        }
        catch (Exception ex)
        {
            MensagemCaixaErro = $"❌ {ex.Message}";
            _logger.LogWarning(ex, "Erro na ação de caixa ({Tipo})", TipoModalCaixa);
        }
    }
}
