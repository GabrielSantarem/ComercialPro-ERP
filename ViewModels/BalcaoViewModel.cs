using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.Services.Impressao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GetStartedApp.ViewModels;

public partial class BalcaoViewModel : ViewModelBase
{
    private readonly PdvService _service;
    private readonly CupomTermicoService _cupomService;
    private readonly ILogger<BalcaoViewModel> _logger;

    public ObservableCollection<ProdutoItem> Carrinho { get; } = [];
    public ObservableCollection<Produto> ResultadosPesquisa { get; } = [];
    public ObservableCollection<Vendedor> Vendedores { get; } = [];

    [ObservableProperty] public partial Vendedor? VendedorSelecionado { get; set; }
    [ObservableProperty] public partial int VendedorIndex { get; set; } = 0;

    partial void OnVendedorIndexChanged(int value)
    {
        if (value >= 0 && value < Vendedores.Count)
        {
            VendedorSelecionado = Vendedores[value];
        }
    }

    partial void OnVendedorSelecionadoChanged(Vendedor? value)
    {
        if (value != null)
        {
            var idx = Vendedores.IndexOf(value);
            if (idx >= 0 && idx != VendedorIndex)
            {
                VendedorIndex = idx;
            }
        }
    }

    // === MODAL DE IDENTIFICAÇÃO RÁPIDA (OPCIONAL AO FECHAR) ===
    [ObservableProperty] public partial bool ModalIdentificacaoAberto { get; set; }
    [ObservableProperty] public partial string ClienteNome { get; set; } = "Cliente Balcão";
    [ObservableProperty] public partial string ClienteCpf { get; set; } = string.Empty;

    [ObservableProperty] private decimal _totalVenda;
    [ObservableProperty] private ProdutoItem? _itemSelecionado;
    [ObservableProperty] private Produto? _produtoPesquisaSelecionado;
    [ObservableProperty] private string _textoPesquisa = string.Empty;

    // === MODAL DE COMANDA GERADA COM CUPOM TÉRMICO (PROTÓTIPO ESC/POS) ===
    [ObservableProperty] public partial bool IsModalConfirmacaoAberto { get; set; }
    [ObservableProperty] public partial string MensagemConfirmacao { get; set; } = string.Empty;
    [ObservableProperty] public partial string NumeroComandaGerada { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal ValorComandaGerada { get; set; }

    [ObservableProperty] public partial PedidoBalcao? UltimoPedidoGerado { get; set; }
    [ObservableProperty] public partial string TextoCupomTermico { get; set; } = string.Empty;

    [ObservableProperty] public partial string LarguraCupomSelecionada { get; set; } = "80mm";
    public ObservableCollection<string> LargurasDisponiveis { get; } = ["80mm", "58mm"];

    [ObservableProperty] public partial string StatusImpressaoFeedback { get; set; } = string.Empty;

    public bool IsBloqueadoPorModal => ModalIdentificacaoAberto || IsModalConfirmacaoAberto;

    public BalcaoViewModel(
        PdvService service, 
        CupomTermicoService? cupomService = null, 
        ILogger<BalcaoViewModel>? logger = null)
    {
        _service = service;
        _cupomService = cupomService ?? new CupomTermicoService();
        _logger = logger ?? NullLogger<BalcaoViewModel>.Instance;
    }

    public BalcaoViewModel(PdvService service, ILogger<BalcaoViewModel> logger)
        : this(service, null, logger)
    {
    }

    public async Task InicializarAsync()
    {
        _logger.LogInformation("Inicializando Terminal de Balcão (Pré-Venda)...");
        Vendedores.Clear();
        var lista = await _service.ObterVendedoresAsync();
        foreach (var v in lista) Vendedores.Add(v);

        VendedorIndex = 0;
        VendedorSelecionado = Vendedores.FirstOrDefault();
        ClienteNome = "Cliente Balcão";
        ClienteCpf = string.Empty;
        _logger.LogInformation("[BALCÃO] Vendedor inicializado: #{Id} - '{Nome}'", VendedorSelecionado?.Id, VendedorSelecionado?.Nome);
    }

    partial void OnLarguraCupomSelecionadaChanged(string value)
    {
        if (UltimoPedidoGerado != null)
        {
            TextoCupomTermico = _cupomService.GerarCupomComandaTexto(UltimoPedidoGerado, largura: value);
        }
    }

    private void AtualizarTotal()
    {
        TotalVenda = Carrinho.Sum(x => x.Total);
    }

    async partial void OnTextoPesquisaChanged(string value)
    {
        ResultadosPesquisa.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            ProdutoPesquisaSelecionado = null;
            return;
        }

        var (_, termo) = PdvViewModel.ProcessarMultiplicador(value);
        var filtrados = await _service.PesquisarProdutosAsync(termo);
        foreach (var f in filtrados)
        {
            ResultadosPesquisa.Add(f);
        }

        ProdutoPesquisaSelecionado = ResultadosPesquisa.FirstOrDefault();
    }

    [RelayCommand]
    private void LancarProduto()
    {
        if (string.IsNullOrWhiteSpace(TextoPesquisa)) return;

        var (quantidade, termo) = PdvViewModel.ProcessarMultiplicador(TextoPesquisa);
        var produtoAlvo = ProdutoPesquisaSelecionado ?? ResultadosPesquisa.FirstOrDefault();

        if (produtoAlvo != null)
        {
            _logger.LogInformation("[BALCÃO] Lançando item: {Nome} x {Qtd}", produtoAlvo.Nome, quantidade);
            AdicionarAoCarrinho(produtoAlvo, quantidade);
            TextoPesquisa = string.Empty;
            ResultadosPesquisa.Clear();
            ProdutoPesquisaSelecionado = null;
        }
    }

    private void AdicionarAoCarrinho(Produto produto, int quantidade)
    {
        var itemExistente = Carrinho.FirstOrDefault(x => x.Produto.Id == produto.Id);
        if (itemExistente != null)
        {
            itemExistente.Quantidade += quantidade;
            var index = Carrinho.IndexOf(itemExistente);
            Carrinho[index] = new ProdutoItem { Produto = produto, Quantidade = itemExistente.Quantidade };
        }
        else
        {
            Carrinho.Add(new ProdutoItem { Produto = produto, Quantidade = quantidade });
        }

        AtualizarTotal();
    }

    [RelayCommand]
    private void CancelarItemSelecionado()
    {
        if (ItemSelecionado != null)
        {
            Carrinho.Remove(ItemSelecionado);
            AtualizarTotal();
        }
    }

    [RelayCommand]
    private void LimparCarrinho()
    {
        Carrinho.Clear();
        ClienteNome = "Cliente Balcão";
        ClienteCpf = string.Empty;
        ModalIdentificacaoAberto = false;
        AtualizarTotal();
    }

    [RelayCommand]
    public void TrocarVendedorProximo()
    {
        if (Vendedores.Count == 0) return;

        var proximo = (VendedorIndex + 1) % Vendedores.Count;
        VendedorIndex = proximo;
        VendedorSelecionado = Vendedores[proximo];

        _logger.LogInformation("[BALCÃO] TrocarVendedorProximo: Vendedor alternado para #{Id} - '{Nome}' (Index {Idx})", 
            VendedorSelecionado.Id, VendedorSelecionado.Nome, VendedorIndex);
    }

    [RelayCommand]
    public void SolicitarEnvioAoCaixa()
    {
        if (Carrinho.Count == 0) return;

        if (string.IsNullOrWhiteSpace(ClienteNome))
        {
            ClienteNome = "Cliente Balcão";
        }

        ModalIdentificacaoAberto = true;
    }

    [RelayCommand]
    public void CancelarIdentificacao()
    {
        ModalIdentificacaoAberto = false;
    }

    [RelayCommand]
    public async Task ConfirmarEnvioAoCaixaAsync()
    {
        if (Carrinho.Count == 0) return;
        if (VendedorSelecionado == null) return;

        var itens = Carrinho.Select(i => (i.Produto, i.Quantidade)).ToList();

        var nomeFinal = string.IsNullOrWhiteSpace(ClienteNome) ? "Cliente Balcão" : ClienteNome.Trim();

        var pedido = await _service.CriarPedidoBalcaoAsync(
            VendedorSelecionado.Id,
            nomeFinal,
            ClienteCpf,
            itens);

        UltimoPedidoGerado = pedido;
        NumeroComandaGerada = pedido.NumeroComanda;
        ValorComandaGerada = pedido.ValorTotal;
        StatusImpressaoFeedback = string.Empty;

        TextoCupomTermico = _cupomService.GerarCupomComandaTexto(pedido, largura: LarguraCupomSelecionada);

        MensagemConfirmacao = $"Pedido {pedido.NumeroComanda} gerado com sucesso!\nCliente: {pedido.ClienteNome} | Vendedor: {VendedorSelecionado.Nome}\n\nOriente o cliente a apresentar esta comanda no Caixa Central.";

        Carrinho.Clear();
        ClienteNome = "Cliente Balcão";
        ClienteCpf = string.Empty;
        TextoPesquisa = string.Empty;
        ResultadosPesquisa.Clear();
        AtualizarTotal();

        ModalIdentificacaoAberto = false;
        IsModalConfirmacaoAberto = true;
    }

    [RelayCommand]
    private void SimularImpressaoEscPos()
    {
        StatusImpressaoFeedback = $"🖨️ Comando ESC/POS enviado para bobina térmica ({LarguraCupomSelecionada}) com guilhotina!";
        _logger.LogInformation("Comanda {Comanda} impressa com sucesso no formato {Largura}", NumeroComandaGerada, LarguraCupomSelecionada);
    }

    [RelayCommand]
    private void FecharModalConfirmacao()
    {
        IsModalConfirmacaoAberto = false;
        UltimoPedidoGerado = null;
        StatusImpressaoFeedback = string.Empty;
    }
}
