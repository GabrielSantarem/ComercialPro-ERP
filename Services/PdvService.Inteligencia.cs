using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GetStartedApp.Models;

namespace GetStartedApp.Services;

public class ItemCurvaAbcDto
{
    public int ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = string.Empty;
    public string CodigoBarras { get; set; } = string.Empty;
    public int QuantidadeVendida { get; set; }
    public decimal FaturamentoTotal { get; set; }
    public decimal CustoTotal { get; set; }
    public decimal LucroBrutoTotal => FaturamentoTotal - CustoTotal;
    public decimal MargemPercentual => FaturamentoTotal > 0 ? (LucroBrutoTotal / FaturamentoTotal) * 100 : 0m;
    public decimal PercentualDoTotal { get; set; }
    public decimal PercentualAcumulado { get; set; }
    public string ClasseAbc { get; set; } = "C"; // "A", "B", "C"

    public string CorFundoBadge => ClasseAbc switch
    {
        "A" => "#E8F8F5",
        "B" => "#FEF9E7",
        _ => "#EBEDEF"
    };

    public string CorTextoBadge => ClasseAbc switch
    {
        "A" => "#27AE60",
        "B" => "#D35400",
        _ => "#7F8C8D"
    };
}

public class DreGerencialDto
{
    public DateTime PeriodoInicio { get; set; }
    public DateTime PeriodoFim { get; set; }
    public decimal ReceitaBruta { get; set; }
    public decimal CustoMercadoriasVendidas { get; set; }
    public decimal LucroBruto => ReceitaBruta - CustoMercadoriasVendidas;
    public decimal MargemBrutaPercentual => ReceitaBruta > 0 ? (LucroBruto / ReceitaBruta) * 100 : 0m;
    public decimal DespesasOperacionaisPagas { get; set; }
    public decimal LucroLiquido => LucroBruto - DespesasOperacionaisPagas;
    public decimal MargemLiquidaPercentual => ReceitaBruta > 0 ? (LucroLiquido / ReceitaBruta) * 100 : 0m;
}

public class VendedorDesempenhoDto
{
    public int VendedorId { get; set; }
    public string VendedorNome { get; set; } = string.Empty;
    public int QuantidadeVendas { get; set; }
    public decimal TotalFaturado { get; set; }
    public decimal TicketMedio => QuantidadeVendas > 0 ? TotalFaturado / QuantidadeVendas : 0m;
}

public class ResumoIndicadoresVendasDto
{
    public decimal TotalFaturado { get; set; }
    public int QuantidadeVendas { get; set; }
    public int QuantidadeItensVendidos { get; set; }
    public decimal TicketMedio => QuantidadeVendas > 0 ? TotalFaturado / QuantidadeVendas : 0m;

    public List<ItemCurvaAbcDto> CurvaAbc { get; set; } = [];
    public DreGerencialDto Dre { get; set; } = new();
    public List<VendedorDesempenhoDto> RankingVendedores { get; set; } = [];
}

public partial class PdvService
{
    public async Task<ResumoIndicadoresVendasDto> ObterIndicadoresVendasAsync(DateTime? inicio = null, DateTime? fim = null)
    {
        if (inicio.HasValue && fim.HasValue && inicio.Value.Date > fim.Value.Date)
        {
            throw new ArgumentException("A data inicial não pode ser superior à data final.", nameof(inicio));
        }

        var dataIni = inicio?.Date ?? DateTime.Today.AddDays(-30);
        var dataFim = fim?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Today.AddDays(1).AddTicks(-1);

        // 1. Filtrar vendas do período
        var vendas = await _db.Vendas
            .Include(v => v.Vendedor)
            .Include(v => v.Itens)
                .ThenInclude(i => i.Produto)
            .Where(v => v.DataHora >= dataIni && v.DataHora <= dataFim)
            .ToListAsync();

        var resumo = new ResumoIndicadoresVendasDto
        {
            QuantidadeVendas = vendas.Count,
            TotalFaturado = vendas.Sum(v => v.ValorTotal),
            QuantidadeItensVendidos = vendas.SelectMany(v => v.Itens).Sum(i => i.Quantidade)
        };

        // 2. Curva ABC de Produtos
        var todosItensVenda = vendas.SelectMany(v => v.Itens).ToList();
        var gruposProduto = todosItensVenda
            .GroupBy(i => i.ProdutoId)
            .Select(g =>
            {
                var primeiro = g.First();
                var qtd = g.Sum(x => x.Quantidade);
                var faturamento = g.Sum(x => x.Quantidade * x.PrecoUnitario);
                var custoUnitario = primeiro.Produto?.CustoUltimaCompra ?? 0m;
                var custoTotal = qtd * custoUnitario;

                return new ItemCurvaAbcDto
                {
                    ProdutoId = g.Key,
                    ProdutoNome = primeiro.Produto?.Nome ?? $"Produto #{g.Key}",
                    CodigoBarras = primeiro.Produto?.CodigoBarras ?? string.Empty,
                    QuantidadeVendida = qtd,
                    FaturamentoTotal = faturamento,
                    CustoTotal = custoTotal
                };
            })
            .OrderByDescending(x => x.FaturamentoTotal)
            .ToList();

        decimal acumuladoValor = 0m;
        var totalFaturamentoGeral = resumo.TotalFaturado;

        foreach (var item in gruposProduto)
        {
            if (totalFaturamentoGeral > 0)
            {
                item.PercentualDoTotal = (item.FaturamentoTotal / totalFaturamentoGeral) * 100;
                acumuladoValor += item.FaturamentoTotal;
                item.PercentualAcumulado = (acumuladoValor / totalFaturamentoGeral) * 100;

                if (item.PercentualAcumulado <= 80.01m)
                {
                    item.ClasseAbc = "A";
                }
                else if (item.PercentualAcumulado <= 95.01m)
                {
                    item.ClasseAbc = "B";
                }
                else
                {
                    item.ClasseAbc = "C";
                }
            }
            else
            {
                item.ClasseAbc = "C";
            }
        }

        resumo.CurvaAbc = gruposProduto;

        // 3. Despesas Operacionais Pagas no Período (Contas a Pagar)
        var despesasPagas = await _db.ContasPagar
            .Where(c => c.Status == "PAGO" && c.DataPagamento.HasValue 
                     && c.DataPagamento.Value >= dataIni && c.DataPagamento.Value <= dataFim)
            .SumAsync(c => c.ValorPago ?? c.Valor);

        var cmv = gruposProduto.Sum(p => p.CustoTotal);

        // 4. DRE Gerencial Consolidado
        resumo.Dre = new DreGerencialDto
        {
            PeriodoInicio = dataIni,
            PeriodoFim = dataFim,
            ReceitaBruta = resumo.TotalFaturado,
            CustoMercadoriasVendidas = cmv,
            DespesasOperacionaisPagas = despesasPagas
        };

        // 5. Ranking de Vendedores
        var ranking = vendas
            .Where(v => v.Vendedor != null)
            .GroupBy(v => v.VendedorId)
            .Select(g => new VendedorDesempenhoDto
            {
                VendedorId = g.Key,
                VendedorNome = g.First().Vendedor?.Nome ?? $"Vendedor #{g.Key}",
                QuantidadeVendas = g.Count(),
                TotalFaturado = g.Sum(v => v.ValorTotal)
            })
            .OrderByDescending(v => v.TotalFaturado)
            .ToList();

        resumo.RankingVendedores = ranking;

        return resumo;
    }
}
