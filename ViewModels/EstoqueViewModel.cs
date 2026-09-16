using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;

namespace GetStartedApp.ViewModels;

public partial class EstoqueViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial DateTime? DataFiltro { get; set; } = DateTime.Today;

    private readonly PdvService _service;

    public ObservableCollection<Produto> ProdutosLista { get; } = [];
    public ObservableCollection<RelatorioInventarioDto> Relatorio { get; } = [];

    [ObservableProperty]
    private string _mensagemAviso = string.Empty;

    // Campos form novo Cadastro
    [ObservableProperty] private string _novoNome = string.Empty;
    [ObservableProperty] private decimal _novoPreco;
    [ObservableProperty] private int _novoEstoque;

    public EstoqueViewModel(PdvService service)
    {
        _service = service;
        _ = CarregarProdutosAsync();
        _ = BuscarGiroEstoqueAsync();
    }

    [RelayCommand]
    private async Task CarregarProdutosAsync()
    {
        ProdutosLista.Clear();
        var lista = await _service.ObterTodosProdutosAsync();
        foreach (var p in lista) ProdutosLista.Add(p);
    }

    [RelayCommand]
    private async Task SalvarNovoProdutoAsync()
    {
        if(string.IsNullOrWhiteSpace(NovoNome)) return;

        var pro = new Produto {
            Nome = NovoNome,
            Preco = NovoPreco,
            Estoque = NovoEstoque
        };
        await _service.SalvarProdutoAsync(pro);

        // Limpar form
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

    private async Task LimparAvisoDepoisAsync()
    {
        await Task.Delay(3000);
        MensagemAviso = string.Empty;
    }
}
