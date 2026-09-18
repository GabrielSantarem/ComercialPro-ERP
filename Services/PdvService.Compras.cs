using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GetStartedApp.Models;

namespace GetStartedApp.Services;

public class ItemSugestaoCompraDto
{
    public int ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = string.Empty;
    public string CodigoBarras { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = "UN";
    public int EstoqueAtual { get; set; }
    public int EstoqueMinimo { get; set; }
    public int TotalVendidoPeriodo { get; set; }
    public decimal ConsumoMedioDiario { get; set; }
    public int PontoDePedido { get; set; }
    public int QuantidadeSugerida { get; set; }
    public decimal CustoUltimaCompra { get; set; }
    public decimal CustoTotalEstimado => QuantidadeSugerida * CustoUltimaCompra;
    public string StatusReposicao { get; set; } = "ESTAVEL"; // RUPTURA_CRITICA, URGENTE, COMPRAR, ESTAVEL
}

public class ResumoSugestaoComprasDto
{
    public int TotalProdutosAnalisados { get; set; }
    public int TotalItensParaComprar { get; set; }
    public int TotalItensEmRuptura { get; set; }
    public decimal OrcamentoTotalEstimado { get; set; }
    public int DiasHistoricoAnalisado { get; set; }
    public int LeadTimeFornecedorDias { get; set; }
    public int DiasCoberturaDesejada { get; set; }
    public List<ItemSugestaoCompraDto> Itens { get; set; } = [];
}

public partial class PdvService
{
    public async Task<ResumoSugestaoComprasDto> CalcularSugestaoComprasAsync(
        int diasHistorico = 30,
        int leadTimeDias = 7,
        int coberturaDias = 15)
    {
        if (diasHistorico <= 0) diasHistorico = 30;
        if (leadTimeDias < 0) leadTimeDias = 0;
        if (coberturaDias <= 0) coberturaDias = 15;

        var dataInicio = DateTime.Today.AddDays(-diasHistorico);

        // 1. Obter todos os produtos
        var produtos = await _db.Produtos.OrderBy(p => p.Nome).ToListAsync();

        // 2. Obter vendas consolidadas no período
        var vendasPorProduto = await _db.ItensVenda
            .Include(i => i.Venda)
            .Where(i => i.Venda.DataHora >= dataInicio)
            .GroupBy(i => i.ProdutoId)
            .Select(g => new { ProdutoId = g.Key, TotalQtd = g.Sum(x => x.Quantidade) })
            .ToDictionaryAsync(x => x.ProdutoId, x => x.TotalQtd);

        var itens = new List<ItemSugestaoCompraDto>();

        foreach (var p in produtos)
        {
            int totalVendido = vendasPorProduto.TryGetValue(p.Id, out var qtd) ? qtd : 0;
            decimal consumoDiario = Math.Round((decimal)totalVendido / diasHistorico, 2);

            // Ponto de Pedido = (ConsumoDiário * LeadTime) + Estoque Mínimo
            int pontoPedido = (int)Math.Ceiling(consumoDiario * leadTimeDias) + p.EstoqueMinimo;

            // Necessidade = ConsumoDiário * (LeadTime + Cobertura) + Estoque Mínimo
            int necessidade = (int)Math.Ceiling(consumoDiario * (leadTimeDias + coberturaDias)) + p.EstoqueMinimo;
            int qtdSugerida = Math.Max(0, necessidade - p.Estoque);

            string status;
            if (p.Estoque <= 0)
            {
                status = "RUPTURA_CRITICA";
                if (qtdSugerida == 0) qtdSugerida = p.EstoqueMinimo > 0 ? p.EstoqueMinimo : 1;
            }
            else if (p.Estoque <= p.EstoqueMinimo)
            {
                status = "URGENTE";
                if (qtdSugerida == 0) qtdSugerida = (p.EstoqueMinimo - p.Estoque) + 1;
            }
            else if (p.Estoque <= pontoPedido)
            {
                status = "COMPRAR";
            }
            else
            {
                status = "ESTAVEL";
                qtdSugerida = 0; // Estoque suficiente
            }

            itens.Add(new ItemSugestaoCompraDto
            {
                ProdutoId = p.Id,
                ProdutoNome = p.Nome,
                CodigoBarras = p.CodigoBarras ?? string.Empty,
                UnidadeMedida = p.UnidadeMedida ?? "UN",
                EstoqueAtual = p.Estoque,
                EstoqueMinimo = p.EstoqueMinimo,
                TotalVendidoPeriodo = totalVendido,
                ConsumoMedioDiario = consumoDiario,
                PontoDePedido = pontoPedido,
                QuantidadeSugerida = qtdSugerida,
                CustoUltimaCompra = p.CustoUltimaCompra,
                StatusReposicao = status
            });
        }

        var itensParaComprar = itens.Where(i => i.QuantidadeSugerida > 0).ToList();

        return new ResumoSugestaoComprasDto
        {
            TotalProdutosAnalisados = produtos.Count,
            TotalItensParaComprar = itensParaComprar.Count,
            TotalItensEmRuptura = itens.Count(i => i.StatusReposicao == "RUPTURA_CRITICA"),
            OrcamentoTotalEstimado = itensParaComprar.Sum(i => i.CustoTotalEstimado),
            DiasHistoricoAnalisado = diasHistorico,
            LeadTimeFornecedorDias = leadTimeDias,
            DiasCoberturaDesejada = coberturaDias,
            Itens = itens
        };
    }

    public string GerarTextoCotacaoFornecedor(IEnumerable<ItemSugestaoCompraDto> itens, string fornecedorNome = "")
    {
        var lista = itens.Where(i => i.QuantidadeSugerida > 0).OrderBy(i => i.ProdutoNome).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("================================================================================");
        sb.AppendLine("               SOLICITAÇÃO DE COTAÇÃO DE COMPRAS DE MERCADORIAS                 ");
        sb.AppendLine($" Data: {DateTime.Now:dd/MM/yyyy HH:mm} | Fornecedor: {(string.IsNullOrWhiteSpace(fornecedorNome) ? "GERAL / A DEFINIR" : fornecedorNome)}");
        sb.AppendLine("================================================================================");
        sb.AppendLine(string.Format("{0,-14} | {1,-34} | {2,-4} | {3,-10} | {4,-10}", "CÓD. BARRAS", "DESCRIÇÃO DO ITEM", "UN", "QTD SOLIC.", "PREÇO UNIT."));
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (var item in lista)
        {
            var ean = string.IsNullOrWhiteSpace(item.CodigoBarras) ? $"#{item.ProdutoId:D5}" : item.CodigoBarras;
            var desc = item.ProdutoNome.Length > 34 ? item.ProdutoNome.Substring(0, 31) + "..." : item.ProdutoNome;
            sb.AppendLine(string.Format("{0,-14} | {1,-34} | {2,-4} | {3,10} | [ R$ _____ ]", ean, desc, item.UnidadeMedida, $"{item.QuantidadeSugerida} {item.UnidadeMedida}"));
        }

        sb.AppendLine("================================================================================");
        sb.AppendLine($" Total de Itens Solicitados: {lista.Count} produtos distintos");
        sb.AppendLine(" Condições de Pagamento: _______________________________________________________");
        sb.AppendLine(" Prazo de Entrega (Lead Time): _____ dias");
        sb.AppendLine(" Vendedor / Representante: ____________________________________________________");
        sb.AppendLine("================================================================================");

        return sb.ToString();
    }
}
