using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;

namespace GetStartedApp.ViewModels;

public partial class ItemEntradaTemp : ObservableObject
{
    public Produto Produto { get; set; } = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustoTotal))]
    public partial int Quantidade { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustoTotal))]
    public partial decimal CustoUnitario { get; set; }

    public decimal CustoTotal => Quantidade * CustoUnitario;
}

public partial class EstoqueViewModel : ViewModelBase
{
    private readonly PdvService _service;

    [ObservableProperty]
    public partial DateTime? DataFiltro { get; set; } = DateTime.Today;

    public ObservableCollection<Produto> ProdutosLista { get; } = [];
    public ObservableCollection<RelatorioInventarioDto> Relatorio { get; } = [];

    // === ABA 3: ENTRADA MANUAL DE NOTAS / MERCADORIAS ===
    [ObservableProperty] public partial string EntradaNumeroNota { get; set; } = string.Empty;
    [ObservableProperty] public partial string EntradaFornecedor { get; set; } = string.Empty;
    [ObservableProperty] public partial string EntradaObservacao { get; set; } = string.Empty;

    [ObservableProperty] public partial Produto? EntradaProdutoSelecionado { get; set; }
    [ObservableProperty] public partial int EntradaQtdItem { get; set; } = 1;
    [ObservableProperty] public partial decimal EntradaCustoItem { get; set; } = 0m;

    public ObservableCollection<ItemEntradaTemp> ItensEntrada { get; } = [];
    public ObservableCollection<EntradaMercadoria> HistoricoEntradas { get; } = [];

    public decimal TotalNotaEntrada => ItensEntrada.Sum(x => x.CustoTotal);

    [ObservableProperty]
    private string _mensagemAviso = string.Empty;

    // Campos form novo Cadastro rápido
    [ObservableProperty] private string _novoNome = string.Empty;
    [ObservableProperty] private decimal _novoPreco;
    [ObservableProperty] private int _novoEstoque;

    public EstoqueViewModel(PdvService service)
    {
        _service = service;
        _ = CarregarProdutosAsync();
        _ = BuscarGiroEstoqueAsync();
        _ = CarregarHistoricoEntradasAsync();
    }

    [RelayCommand]
    private async Task CarregarProdutosAsync()
    {
        ProdutosLista.Clear();
        var lista = await _service.ObterTodosProdutosAsync();
        foreach (var p in lista) ProdutosLista.Add(p);
    }

    [RelayCommand]
    private async Task CarregarHistoricoEntradasAsync()
    {
        HistoricoEntradas.Clear();
        var lista = await _service.ObterHistoricoEntradasAsync();
        foreach (var e in lista) HistoricoEntradas.Add(e);
    }

    [RelayCommand]
    private async Task SalvarNovoProdutoAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoNome)) return;

        var pro = new Produto
        {
            Nome = NovoNome,
            Preco = NovoPreco,
            Estoque = NovoEstoque
        };
        await _service.SalvarProdutoAsync(pro);

        NovoNome = string.Empty;
        NovoPreco = 0;
        NovoEstoque = 0;

        await CarregarProdutosAsync();
        MensagemAviso = "Produto salvo com sucesso!";
        _ = LimparAvisoDepoisAsync();
    }

    [RelayCommand]
    private async Task BuscarGiroEstoqueAsync()
    {
        if (!DataFiltro.HasValue) return;
        Relatorio.Clear();
        var dataBusca = DataFiltro.Value.Date;
        var itens = await _service.GerarLevantamentoInventarioAsync(dataBusca);
        foreach (var i in itens)
        {
            Relatorio.Add(i);
        }
        MensagemAviso = $"Encontrados {itens.Count} produtos com movimentação na data {dataBusca:dd/MM/yyyy}.";
        _ = LimparAvisoDepoisAsync();
    }

    // === COMANDOS DA ENTRADA DE NOTA ===
    [RelayCommand]
    private void AdicionarItemNaNota()
    {
        if (EntradaProdutoSelecionado == null || EntradaQtdItem <= 0 || EntradaCustoItem < 0) return;

        var existente = ItensEntrada.FirstOrDefault(x => x.Produto.Id == EntradaProdutoSelecionado.Id);
        if (existente != null)
        {
            existente.Quantidade += EntradaQtdItem;
            existente.CustoUnitario = EntradaCustoItem;
        }
        else
        {
            ItensEntrada.Add(new ItemEntradaTemp
            {
                Produto = EntradaProdutoSelecionado,
                Quantidade = EntradaQtdItem,
                CustoUnitario = EntradaCustoItem
            });
        }

        OnPropertyChanged(nameof(TotalNotaEntrada));

        // Reseta campos do item para o próximo
        EntradaQtdItem = 1;
        EntradaCustoItem = 0m;
    }

    [RelayCommand]
    private void RemoverItemDaNota(ItemEntradaTemp item)
    {
        ItensEntrada.Remove(item);
        OnPropertyChanged(nameof(TotalNotaEntrada));
    }

    [RelayCommand]
    private async Task ConfirmarEntradaNotaAsync()
    {
        if (string.IsNullOrWhiteSpace(EntradaNumeroNota))
        {
            MensagemAviso = "Informe o Número da Nota ou Pedido!";
            _ = LimparAvisoDepoisAsync();
            return;
        }

        if (ItensEntrada.Count == 0)
        {
            MensagemAviso = "Adicione pelo menos 1 item na nota para dar entrada!";
            _ = LimparAvisoDepoisAsync();
            return;
        }

        var lista = ItensEntrada.Select(i => (i.Produto.Id, i.Quantidade, i.CustoUnitario)).ToList();

        await _service.RegistrarEntradaMercadoriaAsync(
            EntradaNumeroNota,
            string.IsNullOrWhiteSpace(EntradaFornecedor) ? "Fornecedor Padrão" : EntradaFornecedor,
            EntradaObservacao,
            lista);

        // Limpa campos da nota
        EntradaNumeroNota = string.Empty;
        EntradaFornecedor = string.Empty;
        EntradaObservacao = string.Empty;
        ItensEntrada.Clear();
        OnPropertyChanged(nameof(TotalNotaEntrada));

        // Recarrega o estoque atualizado e o histórico de entradas
        await CarregarProdutosAsync();
        await CarregarHistoricoEntradasAsync();

        MensagemAviso = "✅ Entrada de mercadoria registrada e estoque alimentado com sucesso!";
        _ = LimparAvisoDepoisAsync();
    }

    private async Task LimparAvisoDepoisAsync()
    {
        await Task.Delay(4000);
        MensagemAviso = string.Empty;
    }
}
