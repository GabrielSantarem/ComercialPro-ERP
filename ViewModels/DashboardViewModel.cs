using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GetStartedApp.Services;
using Microsoft.EntityFrameworkCore;
using GetStartedApp.Data;

namespace GetStartedApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly PdvService _pdvService;
    private readonly AppDbContext _db;

    [ObservableProperty]
    private bool _carregando;

    // Filtros de Período
    public ObservableCollection<string> FiltrosPeriodo { get; } = 
        ["Hoje", "Últimos 7 dias", "Este Mês", "Últimos 30 dias", "Todo o Período"];

    [ObservableProperty]
    private string _filtroPeriodoSelecionado = "Este Mês";

    // Indicadores Gerais e DRE
    [ObservableProperty]
    private ResumoIndicadoresVendasDto _resumo = new();

    [ObservableProperty]
    private DreGerencialDto _dre = new();

    // Coleções para tabelas
    public ObservableCollection<ItemCurvaAbcDto> ItensCurvaAbc { get; } = [];
    public ObservableCollection<VendedorDesempenhoDto> RankingVendedores { get; } = [];

    // Indicadores de Estoque Global
    [ObservableProperty]
    private int _totalProdutosEstoque;

    [ObservableProperty]
    private decimal _valorEstoque;

    public DashboardViewModel(PdvService pdvService, AppDbContext db)
    {
        _pdvService = pdvService;
        _db = db;
    }

    partial void OnFiltroPeriodoSelecionadoChanged(string value)
    {
        _ = CarregarMetricasAsync();
    }

    [RelayCommand]
    public async Task CarregarMetricasAsync()
    {
        Carregando = true;

        try
        {
            var hoje = DateTime.Today;
            DateTime dataIni;
            DateTime dataFim = hoje;

            switch (FiltroPeriodoSelecionado)
            {
                case "Hoje":
                    dataIni = hoje;
                    break;
                case "Últimos 7 dias":
                    dataIni = hoje.AddDays(-7);
                    break;
                case "Este Mês":
                    dataIni = new DateTime(hoje.Year, hoje.Month, 1);
                    break;
                case "Últimos 30 dias":
                    dataIni = hoje.AddDays(-30);
                    break;
                case "Todo o Período":
                    dataIni = new DateTime(2020, 1, 1);
                    break;
                default:
                    dataIni = new DateTime(hoje.Year, hoje.Month, 1);
                    break;
            }

            // 1. Obter Inteligência de Vendas (Curva ABC, DRE, Vendedores)
            var indicadores = await _pdvService.ObterIndicadoresVendasAsync(dataIni, dataFim);
            Resumo = indicadores;
            Dre = indicadores.Dre;

            ItensCurvaAbc.Clear();
            foreach (var item in indicadores.CurvaAbc)
            {
                ItensCurvaAbc.Add(item);
            }

            RankingVendedores.Clear();
            foreach (var v in indicadores.RankingVendedores)
            {
                RankingVendedores.Add(v);
            }

            // 2. Saúde Geral do Estoque (Patrimônio)
            var produtos = await _db.Produtos.ToListAsync();
            TotalProdutosEstoque = produtos.Sum(p => p.Estoque);
            ValorEstoque = produtos.Sum(p => p.Preco * p.Estoque);
        }
        catch
        {
            /* Proteção contra falhas transitórias de conexão */
        }
        finally
        {
            Carregando = false;
        }
    }
}
