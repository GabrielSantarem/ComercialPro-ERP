using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GetStartedApp.Services;
using GetStartedApp.Services.Inteligencia;
using Microsoft.EntityFrameworkCore;
using GetStartedApp.Data;
using Serilog;

namespace GetStartedApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly PdvService _pdvService;
    private readonly AppDbContext _db;
    private readonly IInteligenciaComercialService? _inteligenciaService;

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

    // Inteligência de Estoque Sem Giro & Comissões (REV-003)
    public ObservableCollection<ItemProdutoSemGiroDto> ProdutosSemGiro { get; } = [];
    public ObservableCollection<ComissaoVendedorDto> ComissoesVendedores { get; } = [];

    [ObservableProperty]
    private int _diasCorteSemGiro = 60;

    [ObservableProperty]
    private decimal _capitalTotalParado;

    [ObservableProperty]
    private decimal _totalComissoesAPagar;

    // Indicadores de Estoque Global
    [ObservableProperty]
    private int _totalProdutosEstoque;

    [ObservableProperty]
    private decimal _valorEstoque;

    public DashboardViewModel(PdvService pdvService, AppDbContext db, IInteligenciaComercialService? inteligenciaService = null)
    {
        _pdvService = pdvService;
        _db = db;
        _inteligenciaService = inteligenciaService;
    }

    partial void OnFiltroPeriodoSelecionadoChanged(string value)
    {
        _ = CarregarMetricasAsync();
    }

    [RelayCommand]
    public async Task AlterarCorteSemGiroAsync(string diasStr)
    {
        if (int.TryParse(diasStr, out int dias))
        {
            DiasCorteSemGiro = dias;
            await CarregarProdutosSemGiroAsync();
        }
    }

    [RelayCommand]
    public async Task CarregarProdutosSemGiroAsync()
    {
        if (_inteligenciaService == null) return;

        try
        {
            var semGiro = await _inteligenciaService.ObterProdutosSemGiroAsync(DiasCorteSemGiro);
            ProdutosSemGiro.Clear();
            foreach (var item in semGiro) ProdutosSemGiro.Add(item);
            CapitalTotalParado = semGiro.Sum(s => s.CapitalParadoTotal);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao obter produtos sem giro.");
        }
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

            // 2. Saúde Geral do Estoque (Patrimônio) baseado no Custo Real de Aquisição
            var produtos = await _db.Produtos.ToListAsync();
            TotalProdutosEstoque = produtos.Sum(p => p.Estoque);
            ValorEstoque = produtos.Sum(p => (p.CustoUltimaCompra > 0 ? p.CustoUltimaCompra : p.Preco * 0.6m) * p.Estoque);

            // 3. Inteligência Comercial REV-003: Produtos Sem Giro & Comissões de Atendentes
            if (_inteligenciaService != null)
            {
                await CarregarProdutosSemGiroAsync();

                var comissoes = await _inteligenciaService.CalcularComissoesAsync(dataIni, dataFim);
                ComissoesVendedores.Clear();
                foreach (var c in comissoes) ComissoesVendedores.Add(c);
                TotalComissoesAPagar = comissoes.Sum(c => c.ValorComissaoTotal);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao carregar indicadores gerenciais e saúde do estoque no Dashboard.");
        }
        finally
        {
            Carregando = false;
        }
    }
}
