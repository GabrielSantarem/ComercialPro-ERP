using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services.Comercial;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public class ItemDevolucaoLinhaVm : ObservableObject
{
    public int ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = string.Empty;
    public int Quantidade { get; set; } = 1;
    public decimal PrecoUnitario { get; set; }
    public decimal Subtotal => Quantidade * PrecoUnitario;
    public bool DestinarAvaria { get; set; }
    public string Motivo { get; set; } = "Troca por desistência ou tamanho";
}

public partial class PdvViewModel
{
    // === CONTROLE DO MODAL DE TROCAS & VALE-CRÉDITO [F10] ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    public partial bool ModalTrocasAberto { get; set; }

    [ObservableProperty]
    public partial string TrocaVendaOrigemTexto { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TrocaClienteNome { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TrocaClienteCpf { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TrocaMotivoGeral { get; set; } = "Troca e Devolução no Balcão";

    [ObservableProperty]
    public partial string TrocaMensagemErro { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TrocaProcessando { get; set; }

    [ObservableProperty]
    public partial ValeCredito? ValeGeradoSucesso { get; set; }

    [ObservableProperty]
    public partial bool ExibirValeEmitidoModal { get; set; }

    public ObservableCollection<ItemDevolucaoLinhaVm> ItensParaDevolucao { get; } = [];

    public decimal TotalDevolucao => ItensParaDevolucao.Sum(i => i.Subtotal);

    [RelayCommand]
    public void AbrirModalTrocas()
    {
        TrocaVendaOrigemTexto = string.Empty;
        TrocaClienteNome = ClienteIdentificacao;
        TrocaClienteCpf = string.Empty;
        TrocaMotivoGeral = "Troca e Devolução no Balcão";
        TrocaMensagemErro = string.Empty;
        ValeGeradoSucesso = null;
        ExibirValeEmitidoModal = false;
        ItensParaDevolucao.Clear();

        // Se houver produtos no carrinho do PDV, copia para devolução como sugestão
        if (Carrinho.Count > 0)
        {
            foreach (var item in Carrinho)
            {
                ItensParaDevolucao.Add(new ItemDevolucaoLinhaVm
                {
                    ProdutoId = item.Produto.Id,
                    ProdutoNome = item.Produto.Nome,
                    Quantidade = (int)Math.Max(1, Math.Round(item.Quantidade)),
                    PrecoUnitario = item.Produto.Preco,
                    DestinarAvaria = false,
                    Motivo = "Troca de mercadoria"
                });
            }
        }

        OnPropertyChanged(nameof(TotalDevolucao));
        ModalTrocasAberto = true;
    }

    [RelayCommand]
    public void FecharModalTrocas()
    {
        ModalTrocasAberto = false;
        ExibirValeEmitidoModal = false;
        TrocaMensagemErro = string.Empty;
    }

    [RelayCommand]
    public void AdicionarItemDevolucaoManual(Produto? produto)
    {
        if (produto == null) return;

        var existente = ItensParaDevolucao.FirstOrDefault(i => i.ProdutoId == produto.Id);
        if (existente != null)
        {
            existente.Quantidade++;
        }
        else
        {
            ItensParaDevolucao.Add(new ItemDevolucaoLinhaVm
            {
                ProdutoId = produto.Id,
                ProdutoNome = produto.Nome,
                Quantidade = 1,
                PrecoUnitario = produto.Preco,
                DestinarAvaria = false,
                Motivo = "Troca de mercadoria"
            });
        }
        OnPropertyChanged(nameof(TotalDevolucao));
    }

    [RelayCommand]
    public void RemoverItemDevolucao(ItemDevolucaoLinhaVm? item)
    {
        if (item == null) return;
        ItensParaDevolucao.Remove(item);
        OnPropertyChanged(nameof(TotalDevolucao));
    }

    [RelayCommand]
    public async Task ConfirmarEmissaoValeAsync()
    {
        if (_trocaService == null)
        {
            TrocaMensagemErro = "Serviço de Trocas e Vales não disponível.";
            return;
        }

        if (ItensParaDevolucao.Count == 0)
        {
            TrocaMensagemErro = "Adicione ao menos um produto a ser devolvido.";
            return;
        }

        int? vendaOrigemId = null;
        if (!string.IsNullOrWhiteSpace(TrocaVendaOrigemTexto))
        {
            if (int.TryParse(TrocaVendaOrigemTexto.Trim(), out int vId))
            {
                vendaOrigemId = vId;
            }
            else
            {
                TrocaMensagemErro = "Número de venda de origem inválido.";
                return;
            }
        }

        TrocaProcessando = true;
        TrocaMensagemErro = string.Empty;

        try
        {
            var dtos = ItensParaDevolucao.Select(i => new ItemDevolucaoDto(
                ProdutoId: i.ProdutoId,
                Quantidade: i.Quantidade,
                PrecoUnitario: i.PrecoUnitario,
                DestinarAvaria: i.DestinarAvaria,
                Motivo: i.Motivo
            )).ToList();

            var vale = await _trocaService.EmitirValeTrocaAsync(
                dtos,
                motivoGeral: TrocaMotivoGeral,
                clienteNome: string.IsNullOrWhiteSpace(TrocaClienteNome) ? null : TrocaClienteNome.Trim(),
                clienteCpf: string.IsNullOrWhiteSpace(TrocaClienteCpf) ? null : TrocaClienteCpf.Trim(),
                vendaOrigemId: vendaOrigemId
            );

            ValeGeradoSucesso = vale;
            ExibirValeEmitidoModal = true;
            _logger.LogInformation("Vale-Crédito emitido com sucesso: {Codigo} R$ {Valor:N2}", vale.Codigo, vale.ValorOriginal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao emitir Vale-Crédito.");
            TrocaMensagemErro = $"Erro na emissão: {ex.Message}";
        }
        finally
        {
            TrocaProcessando = false;
        }
    }
}
