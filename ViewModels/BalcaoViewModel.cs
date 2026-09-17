using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class BalcaoViewModel : ViewModelBase
{
    private readonly PdvService _service;
    private readonly ILogger<BalcaoViewModel> _logger;

    public ObservableCollection<ProdutoItem> Carrinho { get; } = [];
    public ObservableCollection<Produto> ResultadosPesquisa { get; } = [];
    public ObservableCollection<Vendedor> Vendedores { get; } = [];

    [ObservableProperty] public partial Vendedor? VendedorSelecionado { get; set; }

    // === MODAL DE IDENTIFICAÇÃO RÁPIDA (OPCIONAL AO FECHAR) ===
    [ObservableProperty] public partial bool ModalIdentificacaoAberto { get; set; }
    [ObservableProperty] public partial string ClienteNome { get; set; } = "Cliente Balcão";
    [ObservableProperty] public partial string ClienteCpf { get; set; } = string.Empty;

    [ObservableProperty] private decimal _totalVenda;
    [ObservableProperty] private ProdutoItem? _itemSelecionado;
    [ObservableProperty] private Produto? _produtoPesquisaSelecionado;
    [ObservableProperty] private string _textoPesquisa = string.Empty;

    // === MODAL DE SUCESSO / COMANDA GERADA ===
    [ObservableProperty] public partial bool IsModalConfirmacaoAberto { get; set; }
    [ObservableProperty] public partial string MensagemConfirmacao { get; set; } = string.Empty;
    [ObservableProperty] public partial string NumeroComandaGerada { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal ValorComandaGerada { get; set; }

    public bool IsBloqueadoPorModal => ModalIdentificacaoAberto || IsModalConfirmacaoAberto;

    public BalcaoViewModel(PdvService service, ILogger<BalcaoViewModel> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task InicializarAsync()
    {
        _logger.LogInformation("Inicializando Terminal de Balcão (Pré-Venda)...");
        Vendedores.Clear();
        var lista = await _service.ObterVendedoresAsync();
        foreach (var v in lista) Vendedores.Add(v);
        VendedorSelecionado = Vendedores.FirstOrDefault();
        ClienteNome = "Cliente Balcão";
        ClienteCpf = string.Empty;
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
        var idx = VendedorSelecionado != null ? Vendedores.IndexOf(VendedorSelecionado) : -1;
        var proximo = (idx + 1) % Vendedores.Count;
        VendedorSelecionado = Vendedores[proximo];
    }

    [RelayCommand]
    public void SolicitarEnvioAoCaixa()
    {
        if (Carrinho.Count == 0) return;

        // Se ainda não definiu nome, sugere o padrão "Cliente Balcão"
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

        NumeroComandaGerada = pedido.NumeroComanda;
        ValorComandaGerada = pedido.ValorTotal;
        MensagemConfirmacao = $"Pedido {pedido.NumeroComanda} gerado com sucesso!\nCliente: {pedido.ClienteNome} | Vendedor: {VendedorSelecionado.Nome}\n\nOriente o cliente a apresentar o número no Caixa Central.";

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
    private void FecharModalConfirmacao()
    {
        IsModalConfirmacaoAberto = false;
    }
}
