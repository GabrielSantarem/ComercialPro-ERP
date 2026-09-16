using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using GetStartedApp.Data;

namespace GetStartedApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly AppDbContext _db;

    [ObservableProperty]
    private int _totalVendasHoje;

    [ObservableProperty]
    private decimal _faturamentoHoje;

    [ObservableProperty]
    private int _totalProdutosEstoque;

    [ObservableProperty]
    private decimal _valorEstoque;

    [ObservableProperty]
    private bool _carregando;

    public DashboardViewModel(AppDbContext db)
    {
        _db = db;
    }

    public async Task CarregarMetricasAsync()
    {
        Carregando = true;

        try
        {
            var inicioDia = DateTime.Today;
            var fimDia = inicioDia.AddDays(1).AddTicks(-1);

            // 1. Vendas e Faturamento de Hoje
            var vendasHoje = await _db.Vendas
                .Where(v => v.DataHora >= inicioDia && v.DataHora <= fimDia)
                .ToListAsync();

            TotalVendasHoje = vendasHoje.Count;
            FaturamentoHoje = vendasHoje.Sum(v => v.ValorTotal);

            // 2. Saúde do Estoque
            var produtos = await _db.Produtos.ToListAsync();
            TotalProdutosEstoque = produtos.Sum(p => p.Estoque);
            ValorEstoque = produtos.Sum(p => p.Preco * p.Estoque);
        }
        catch
        {
            /* Evitar crash silencioso caso o DB ainda esteja inicializando, 
               mas aqui já estamos seguros pelo SplashWindow */
        }
        finally
        {
            Carregando = false;
        }
    }
}