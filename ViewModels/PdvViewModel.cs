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

public partial class PdvViewModel : ViewModelBase
{
    private readonly PdvService _pdvService;
    private readonly ILogger<PdvViewModel> _logger;

    public ObservableCollection<ProdutoItem> Carrinho { get; } = [];
    public ObservableCollection<Produto> ResultadosPesquisa { get; } = [];
    public ObservableCollection<Vendedor> Vendedores { get; } = [];

    [ObservableProperty]
    private decimal _totalVenda;

    [ObservableProperty]
    private ProdutoItem? _itemSelecionado;

    [ObservableProperty]
    private Produto? _produtoPesquisaSelecionado;

    [ObservableProperty]
    private Vendedor? _vendedorSelecionado;

    [ObservableProperty]
    private string _textoPesquisa = string.Empty;

    public bool IsBloqueadoPorModal => IsModalAberto || ModalCaixaAberto || ModalFilaBalcaoAberto;

    public PdvViewModel(PdvService pdvService, ILogger<PdvViewModel> logger)
    {
        _pdvService = pdvService;
        _logger = logger;
    }

    public async Task InicializarAsync()
    {
        _logger.LogInformation("Inicializando PdvViewModel e carregando vendedores...");
        Vendedores.Clear();
        var vendedoresDb = await _pdvService.ObterVendedoresAsync();
        foreach (var v in vendedoresDb) Vendedores.Add(v);

        VendedorSelecionado = Vendedores.FirstOrDefault();
        await AtualizarEstadoTurnoAsync();
        await AtualizarFilaPedidosAsync();
    }

    private void AtualizarTotal()
    {
        TotalVenda = Carrinho.Sum(x => x.Produto.Preco * x.Quantidade);
        OnPropertyChanged(nameof(Acrescimo));
        OnPropertyChanged(nameof(TotalComTaxa));
        OnPropertyChanged(nameof(Troco));
        OnPropertyChanged(nameof(PodeConfirmarPagamento));
        ValorRecebido = TotalComTaxa;
    }

    async partial void OnTextoPesquisaChanged(string value)
    {
        ResultadosPesquisa.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            ProdutoPesquisaSelecionado = null;
            return;
        }

        var termo = ExtrairTermoBusca(value);
        var filtrados = await _pdvService.PesquisarProdutosAsync(termo);
        foreach (var f in filtrados)
        {
            ResultadosPesquisa.Add(f);
        }

        ProdutoPesquisaSelecionado = ResultadosPesquisa.FirstOrDefault();
    }

    public static (int Quantidade, string Termo) ProcessarMultiplicador(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return (1, string.Empty);

        var match = Regex.Match(input.Trim(), @"^(\d+)\s*\*\s*(.*)$");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int qtd) && qtd > 0)
            {
                return (qtd, match.Groups[2].Value.Trim());
            }
        }

        return (1, input.Trim());
    }

    public static string ExtrairTermoBusca(string input)
    {
        var (_, termo) = ProcessarMultiplicador(input);
        return termo;
    }

    [RelayCommand]
    private void LancarProduto()
    {
        if (string.IsNullOrWhiteSpace(TextoPesquisa)) return;

        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ O Caixa está FECHADO! Abra o caixa antes de iniciar vendas.";
            return;
        }

        var (quantidade, termo) = ProcessarMultiplicador(TextoPesquisa);
        var produtoAlvo = ProdutoPesquisaSelecionado ?? ResultadosPesquisa.FirstOrDefault();

        if (produtoAlvo != null)
        {
            _logger.LogInformation("Lançando produto no cupom: '{Nome}' x {Qtd}", produtoAlvo.Nome, quantidade);
            AdicionarAoCarrinho(produtoAlvo, quantidade);
            TextoPesquisa = string.Empty;
            ResultadosPesquisa.Clear();
            ProdutoPesquisaSelecionado = null;
        }
        else
        {
            _logger.LogWarning("Nenhum produto encontrado para o termo '{Termo}' com quantidade {Qtd}", termo, quantidade);
        }
    }

    [RelayCommand]
    private void AdicionarItem(Produto produto)
    {
        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ O Caixa está FECHADO! Abra o caixa antes de iniciar vendas.";
            return;
        }
        AdicionarAoCarrinho(produto, 1);
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
            _logger.LogInformation("Cancelando item do cupom: {Nome}", ItemSelecionado.Produto.Nome);
            Carrinho.Remove(ItemSelecionado);
            AtualizarTotal();
        }
    }

    [RelayCommand]
    private void LimparCarrinho()
    {
        _logger.LogInformation("Limpando todo o cupom (carrinho limpo).");
        Carrinho.Clear();
        ClienteIdentificacao = string.Empty;
        PedidoBalcaoEmAtendimento = null;
        AtualizarTotal();
    }
}
