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

public partial class PdvViewModel : ViewModelBase
{
    private readonly PdvService _pdvService;
    private readonly ILogger<PdvViewModel> _logger;

    [ObservableProperty]
    private bool _isModalAberto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Troco))]
    [NotifyPropertyChangedFor(nameof(PodeConfirmarPagamento))]
    private decimal? _valorRecebido;

    public decimal Troco => (ValorRecebido ?? 0) - TotalVenda;
    public bool PodeConfirmarPagamento => (ValorRecebido ?? 0) >= TotalVenda && TotalVenda > 0;


    [ObservableProperty]
    private string _textoPesquisa = string.Empty;

    [ObservableProperty]
    private Vendedor? _vendedorSelecionado;

    public ObservableCollection<Vendedor> Vendedores { get; } = new();
    public ObservableCollection<Produto> ResultadosPesquisa { get; } = new();
    public ObservableCollection<ProdutoItem> Carrinho { get; } = new();
    public decimal TotalVenda => Carrinho.Sum(x => x.Total);

    public PdvViewModel(PdvService pdvService, ILogger<PdvViewModel> logger)
    {
        _pdvService = pdvService;
        _logger = logger;
    }

    public async Task InicializarAsync()
    {
        await _pdvService.InicializarBancoDadosAsync();

        if(Vendedores.Count > 0) return; // Ja iniciou antes
        var vendedoresDb = await _pdvService.ObterVendedoresAsync();
        Vendedores.Clear();
        foreach (var v in vendedoresDb) Vendedores.Add(v);

        VendedorSelecionado = Vendedores.FirstOrDefault();
    }

    async partial void OnTextoPesquisaChanged(string value)
    {
        ResultadosPesquisa.Clear();
        var filtrados = await _pdvService.PesquisarProdutosAsync(value);
        foreach(var f in filtrados)
        {
            ResultadosPesquisa.Add(f);
        }
    }

    [RelayCommand]
    private void AdicionarAoCarrinho(Produto produto)
    {
        if (produto == null) return;
        var existente = Carrinho.FirstOrDefault(x => x.Produto.Id == produto.Id);
        if (existente != null) existente.Quantidade++;
        else Carrinho.Add(new ProdutoItem { Produto = produto, Quantidade = 1 });
        AtualizarTotal();
    }

    [RelayCommand]
    private void AumentarQtd(ProdutoItem item)
    {
        if (item != null) { item.Quantidade++; AtualizarTotal(); }
    }

    [RelayCommand]
    private void DiminuirQtd(ProdutoItem item)
    {
        _logger.LogInformation("Diminuindo quantidade de {Produto}", item.Produto.Nome);
        if (item != null)
        {
            item.Quantidade--;
            if (item.Quantidade <= 0) Carrinho.Remove(item);
            AtualizarTotal();
        }
    }

    
    [RelayCommand]
    private void AbrirModalPagamento()
    {
        if (Carrinho.Count == 0) 
        {
            _logger.LogWarning("Tentativa de finalizar venda com carrinho vazio.");
            return;
        }
        if (VendedorSelecionado == null) 
        {
            _logger.LogWarning("Tentativa de finalizar sem selecionar vendedor.");
            return;
        }
        
        ValorRecebido = TotalVenda; // Default para facilitar a vida do caixa
        IsModalAberto = true;
    }

    [RelayCommand]
    private void FecharModalPagamento()
    {
        IsModalAberto = false;
        ValorRecebido = null;
    }

    [RelayCommand]
    private async Task ConfirmarPagamentoAsync()
    {
        if (!PodeConfirmarPagamento) return;
        
        _logger.LogInformation("Confirmando Checkout do Carrinho. Itens: {Qtd}", Carrinho.Count);
        var itens = Carrinho.Select(i => (i.Produto, i.Quantidade)).ToList();
        
        await _pdvService.SalvarPedidoAsync(VendedorSelecionado.Id, itens);
        
        Carrinho.Clear();
        TextoPesquisa = string.Empty;
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

    private void AtualizarTotal() => OnPropertyChanged(nameof(TotalVenda));
}
