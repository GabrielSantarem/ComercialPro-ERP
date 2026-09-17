using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GetStartedApp.Models;

namespace GetStartedApp.Services;

public partial class PdvService
{
    // === GESTÃO DE INVENTÁRIO ===
    public async Task<List<RelatorioInventarioDto>> GerarLevantamentoInventarioAsync(DateTime data)
    {
        var inicio = data.Date;
        var fim = inicio.AddDays(1).AddTicks(-1);

        var itensVendidos = await _db.ItensVenda
            .Include(i => i.Venda)
            .Include(i => i.Produto)
            .Where(i => i.Venda.DataHora >= inicio && i.Venda.DataHora <= fim)
            .ToListAsync();

        var relatorio = itensVendidos
            .GroupBy(i => i.Produto)
            .Select(g => new RelatorioInventarioDto
            {
                ProdutoId = g.Key.Id,
                Nome = g.Key.Nome,
                EstoqueAtualSistema = g.Key.Estoque,
                QuantidadeVendida = g.Sum(x => x.Quantidade)
            })
            .OrderByDescending(r => r.QuantidadeVendida)
            .ToList();

        return relatorio;
    }

    // === GESTÃO DE ENTRADA MANUAL DE MERCADORIAS ===
    public async Task<List<EntradaMercadoria>> ObterHistoricoEntradasAsync()
    {
        return await _db.EntradasMercadoria
            .Include(e => e.Itens)
            .ThenInclude(i => i.Produto)
            .OrderByDescending(e => e.DataEntrada)
            .ToListAsync();
    }

    public async Task RegistrarEntradaMercadoriaAsync(
        string numeroNota, 
        string fornecedor, 
        string observacao, 
        List<(int ProdutoId, int Quantidade, decimal CustoUnitario)> itens)
    {
        if (itens.Count == 0) return;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var entrada = new EntradaMercadoria
            {
                NumeroNota = numeroNota,
                Fornecedor = fornecedor,
                Observacao = observacao,
                DataEntrada = DateTime.Now,
                ValorTotal = itens.Sum(x => x.Quantidade * x.CustoUnitario)
            };

            foreach (var item in itens)
            {
                var produto = await _db.Produtos.FindAsync(item.ProdutoId);
                if (produto != null)
                {
                    produto.Estoque += item.Quantidade;

                    entrada.Itens.Add(new ItemEntradaMercadoria
                    {
                        ProdutoId = item.ProdutoId,
                        QuantidadeEntrada = item.Quantidade,
                        CustoUnitario = item.CustoUnitario
                    });
                }
            }

            _db.EntradasMercadoria.Add(entrada);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Entrada de Mercadoria NF '{Nota}' registrada com sucesso. Total: R$ {Total}", numeroNota, entrada.ValorTotal);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erro ao registrar entrada de mercadorias da NF '{Nota}'", numeroNota);
            throw;
        }
    }
}
