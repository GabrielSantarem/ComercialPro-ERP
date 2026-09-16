using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class ProdutoItem : ObservableObject
{
    public Produto Produto { get; set; } = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Total))]
    public partial int Quantidade { get; set; }

    public decimal Total => Quantidade * Produto.Preco;
}

public partial class PdvViewModel(PdvService pdvService, ILogger<PdvViewModel> logger) : ViewModelBase
{
    private readonly PdvService _pdvService = pdvService;
    private readonly ILogger<PdvViewModel> _logger = logger;

    async partial void OnFormaPagamentoSelecionadaChanged(string value)
    {
        OnPropertyChanged(nameof(Acrescimo));
        OnPropertyChanged(nameof(TotalComTaxa));
        OnPropertyChanged(nameof(Troco));
        OnPropertyChanged(nameof(PodeConfirmarPagamento));
        ValorRecebido = TotalComTaxa;
    }

    [ObservableProperty]
    public partial bool IsModalAberto { get; set; }

    [ObservableProperty]
    public partial string ClienteIdentificacao { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Acrescimo))]
    [NotifyPropertyChangedFor(nameof(TotalComTaxa))]
    [NotifyPropertyChangedFor(nameof(Troco))]
    [NotifyPropertyChangedFor(nameof(PodeConfirmarPagamento))]
    public partial string FormaPagamentoSelecionada { get; set; } = "Dinheiro";
    public ObservableCollection<string> FormasPagamento { get; } = ["Dinheiro", "PIX", "Débito", "Crédito (+2%)"];

    public decimal Acrescimo => FormaPagamentoSelecionada == "Crédito (+2%)" ? TotalVenda * 0.02m : 0m;
    public decimal TotalComTaxa => TotalVenda + Acrescimo;

    [ObservableProperty]
    private ProdutoItem? _itemSelecionado;

    [ObservableProperty]
    private Produto? _produtoPesquisaSelecionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Troco))]
    [NotifyPropertyChangedFor(nameof(PodeConfirmarPagamento))]
    public partial decimal? ValorRecebido { get; set; }

    public decimal Troco => (ValorRecebido ?? 0) - TotalComTaxa;
    public bool PodeConfirmarPagamento => (ValorRecebido ?? 0) >= TotalComTaxa && TotalComTaxa > 0;

    [ObservableProperty]
    public partial string TextoPesquisa { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Vendedor? VendedorSelecionado { get; set; }
    public ObservableCollection<Vendedor> Vendedores { get; } = new();
    public ObservableCollection<Produto> ResultadosPesquisa { get; } = new();
    public ObservableCollection<ProdutoItem> Carrinho { get; } = new();
    public decimal TotalVenda => Carrinho.Sum(x => x.Total);

    public async Task InicializarAsync()
    {
        await _pdvService.InicializarBancoDadosAsync();

        if (Vendedores.Count > 0) return;
        var vendedoresDb = await _pdvService.ObterVendedoresAsync();
        Vendedores.Clear();
        foreach (var v in vendedoresDb) Vendedores.Add(v);

        VendedorSelecionado = Vendedores.FirstOrDefault();
    }

    async partial void OnTextoPesquisaChanged(string value)
    {
        ResultadosPesquisa.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            ProdutoPesquisaSelecionado = null;
            return;
        }

        // Se houver multiplicador (ex: "5*sacola"), busca pelo termo após o '*'
        var termo = ExtrairTermoBusca(value);
        var filtrados = await _pdvService.PesquisarProdutosAsync(termo);
        foreach (var f in filtrados)
        {
            ResultadosPesquisa.Add(f);
        }

        ProdutoPesquisaSelecionado = ResultadosPesquisa.FirstOrDefault();
    }

    private static (int Quantidade, string Termo) ProcessarMultiplicador(string input)
    {
        input = input.Trim();
        var asteriscoIdx = input.IndexOf('*');
        if (asteriscoIdx > 0 && asteriscoIdx < input.Length - 1)
        {
            var prefixo = input[..asteriscoIdx].Trim();
            var resto = input[(asteriscoIdx + 1)..].Trim();
            if (int.TryParse(prefixo, out int qtd) && qtd > 0)
            {
                return (qtd, resto);
            }
        }
        return (1, input);
    }

    private static string ExtrairTermoBusca(string input)
    {
        var (_, termo) = ProcessarMultiplicador(input);
        return termo;
    }

    [RelayCommand]
    public async Task LancarProdutoAsync()
    {
        if (string.IsNullOrWhiteSpace(TextoPesquisa)) return;

        var (quantidade, termo) = ProcessarMultiplicador(TextoPesquisa);

        // 1. Se o operador navegou e selecionou um item na lista com setas, usa ele
        Produto? produto = ProdutoPesquisaSelecionado;

        // 2. Se não selecionou explicitamente, busca no banco pelo termo
        if (produto == null || !produto.Nome.Contains(termo, System.StringComparison.OrdinalIgnoreCase))
        {
            var produtos = await _pdvService.PesquisarProdutosAsync(termo);
            produto = produtos.FirstOrDefault();
        }

        if (produto != null)
        {
            AdicionarAoCarrinhoComQtd(produto, quantidade);
            TextoPesquisa = string.Empty;
            ResultadosPesquisa.Clear();
            ProdutoPesquisaSelecionado = null;
        }
    }

    [RelayCommand]
    private void RemoverItemSelecionado()
    {
        if (ItemSelecionado != null)
        {
            Carrinho.Remove(ItemSelecionado);
            ItemSelecionado = null;
            AtualizarTotal();
        }
    }

    [RelayCommand]
    public void AdicionarAoCarrinho(Produto produto)
    {
        AdicionarAoCarrinhoComQtd(produto, 1);
    }

    public void AdicionarAoCarrinhoComQtd(Produto produto, int quantidade)
    {
        if (produto == null || quantidade <= 0) return;
        var existente = Carrinho.FirstOrDefault(x => x.Produto.Id == produto.Id);
        if (existente != null)
        {
            existente.Quantidade += quantidade;
        }
        else
        {
            Carrinho.Add(new ProdutoItem { Produto = produto, Quantidade = quantidade });
        }
        AtualizarTotal();
    }

    [RelayCommand]
    private void AumentarQtd(ProdutoItem item)
    {
        item.Quantidade++;
        AtualizarTotal();
    }

    [RelayCommand]
    private void DiminuirQtd(ProdutoItem item)
    {
        if (item.Quantidade > 1)
        {
            item.Quantidade--;
        }
        else
        {
            Carrinho.Remove(item);
        }
        AtualizarTotal();
    }

    [RelayCommand]
    private void AbrirModalPagamento()
    {
        if (Carrinho.Count == 0) return;
        ValorRecebido = TotalComTaxa;
        IsModalAberto = true;
    }

    [RelayCommand]
    private void FecharModalPagamento()
    {
        IsModalAberto = false;
    }

    [RelayCommand]
    private async Task ConfirmarPagamentoAsync()
    {
        if (!PodeConfirmarPagamento) return;

        _logger.LogInformation("Confirmando Checkout do Carrinho. Itens: {Qtd}, Cliente: {Cliente}", Carrinho.Count, ClienteIdentificacao);
        var itens = Carrinho.Select(i => (i.Produto, i.Quantidade)).ToList();

        await _pdvService.SalvarPedidoAsync(VendedorSelecionado!.Id, itens);

        Carrinho.Clear();
        TextoPesquisa = string.Empty;
        ClienteIdentificacao = string.Empty;
        ResultadosPesquisa.Clear();
        AtualizarTotal();
        IsModalAberto = false;
        _logger.LogInformation("Venda processada com sucesso. Modal fechado.");
    }

    [RelayCommand]
    private void LimparCarrinho()
    {
        Carrinho.Clear();
        AtualizarTotal();
    }

    private void AtualizarTotal()
    {
        OnPropertyChanged(nameof(TotalVenda));
        OnPropertyChanged(nameof(Acrescimo));
        OnPropertyChanged(nameof(TotalComTaxa));
        OnPropertyChanged(nameof(Troco));
        OnPropertyChanged(nameof(PodeConfirmarPagamento));
        ValorRecebido = TotalComTaxa;
    }
}
