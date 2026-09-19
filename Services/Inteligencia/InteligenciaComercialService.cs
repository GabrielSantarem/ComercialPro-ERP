using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.Services.Inteligencia;

public class InteligenciaComercialService : IInteligenciaComercialService
{
    private readonly AppDbContext _db;
    private readonly ILogger<InteligenciaComercialService> _logger;

    public InteligenciaComercialService(AppDbContext db, ILogger<InteligenciaComercialService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ItemProdutoSemGiroDto>> ObterProdutosSemGiroAsync(int diasMinimosSemVenda = 60)
    {
        if (diasMinimosSemVenda < 1) diasMinimosSemVenda = 1;

        var hoje = DateTime.Today;

        // Produtos com estoque positivo (produtos zerados não contam como capital parado)
        var produtos = await _db.Produtos
            .Where(p => p.Estoque > 0)
            .ToListAsync();

        // Buscar a data da última venda não-cancelada de todos os produtos
        var ultimasVendas = await _db.ItensVenda
            .Where(iv => iv.Venda.Status != "CANCELADA" && iv.Venda.DataHoraCancelamento == null)
            .GroupBy(iv => iv.ProdutoId)
            .Select(g => new
            {
                ProdutoId = g.Key,
                DataUltimaVenda = g.Max(x => x.Venda.DataHora)
            })
            .ToDictionaryAsync(x => x.ProdutoId, x => x.DataUltimaVenda);

        var resultado = new List<ItemProdutoSemGiroDto>();

        foreach (var p in produtos)
        {
            DateTime dataReferencia;
            DateTime? dataUltimaVenda = null;

            if (ultimasVendas.TryGetValue(p.Id, out var dtVenda))
            {
                dataReferencia = dtVenda;
                dataUltimaVenda = dtVenda;
            }
            else
            {
                // Se nunca vendeu, a referência é a data de cadastro do produto
                dataReferencia = p.DataCadastro;
            }

            var diasSemGiro = (int)(hoje - dataReferencia.Date).TotalDays;

            if (diasSemGiro >= diasMinimosSemVenda)
            {
                var custo = p.CustoUltimaCompra > 0m ? p.CustoUltimaCompra : p.Preco;
                var capitalParado = Math.Round(p.Estoque * custo, 2);

                resultado.Add(new ItemProdutoSemGiroDto(
                    ProdutoId: p.Id,
                    Nome: p.Nome,
                    CodigoBarras: p.CodigoBarras ?? "SEM CÓDIGO",
                    SaldoEstoque: p.Estoque,
                    CustoUnitario: custo,
                    CapitalParadoTotal: capitalParado,
                    DataUltimaVenda: dataUltimaVenda,
                    DiasSemGiro: Math.Max(0, diasSemGiro)
                ));
            }
        }

        _logger.LogInformation("Radar de Produtos Sem Giro ({Dias} dias): {Total} produtos identificados.",
            diasMinimosSemVenda, resultado.Count);

        return resultado.OrderByDescending(r => r.CapitalParadoTotal).ToList();
    }

    public async Task<List<ComissaoVendedorDto>> CalcularComissoesAsync(DateTime inicio, DateTime fim)
    {
        var dataInicio = inicio.Date;
        var dataFim = fim.Date.AddDays(1).AddTicks(-1);

        // Vendas faturadas no período excluindo expressamente vendas canceladas
        var vendasValidas = await _db.Vendas
            .Where(v => v.DataHora >= dataInicio && v.DataHora <= dataFim &&
                        v.Status != "CANCELADA" && v.DataHoraCancelamento == null)
            .ToListAsync();

        var vendedores = await _db.Vendedores.ToListAsync();
        var vendasPorVendedor = vendasValidas.GroupBy(v => v.VendedorId).ToDictionary(g => g.Key, g => g.ToList());

        var resultado = new List<ComissaoVendedorDto>();

        foreach (var vendedor in vendedores)
        {
            if (vendasPorVendedor.TryGetValue(vendedor.Id, out var vendas))
            {
                var totalVendas = vendas.Count;
                var faturamentoTotal = vendas.Sum(v => v.ValorTotal);
                var aliquota = vendedor.PercentualComissao;
                var valorComissao = Math.Round(faturamentoTotal * (aliquota / 100m), 2);

                resultado.Add(new ComissaoVendedorDto(
                    VendedorId: vendedor.Id,
                    VendedorNome: vendedor.Nome,
                    PercentualComissao: aliquota,
                    TotalVendas: totalVendas,
                    FaturamentoTotal: faturamentoTotal,
                    ValorComissaoTotal: valorComissao
                ));
            }
        }

        _logger.LogInformation("Comissões calculadas para o período {Inicio:d} a {Fim:d}: {Count} atendentes comissionados.",
            inicio, fim, resultado.Count);

        return resultado.OrderByDescending(r => r.FaturamentoTotal).ToList();
    }
}
