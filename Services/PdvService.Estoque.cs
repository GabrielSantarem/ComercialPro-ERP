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

        foreach (var item in itens)
        {
            if (item.Quantidade <= 0)
            {
                throw new ArgumentException($"A quantidade de entrada para o produto #{item.ProdutoId} deve ser maior que zero.", nameof(itens));
            }

            if (item.CustoUnitario < 0)
            {
                throw new ArgumentException($"O custo unitário de entrada para o produto #{item.ProdutoId} não pode ser negativo.", nameof(itens));
            }
        }

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
                    produto.CustoUltimaCompra = item.CustoUnitario;

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

    // === CADASTRO AUTOMÁTICO OU VINCULAÇÃO DE PRODUTO VIA XML ===
    public async Task<Produto> ObterOuCriarProdutoPorXmlAsync(string descricao, string ean, string ncm, string unidade, decimal precoVendaSugerido, decimal custoUnitario)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("A descrição do produto é obrigatória e não pode ser vazia ou nula.", nameof(descricao));
        }

        Produto? produto = null;

        // 1. Tenta encontrar por Código de Barras (EAN)
        if (!string.IsNullOrWhiteSpace(ean))
        {
            produto = await _db.Produtos.FirstOrDefaultAsync(p => p.CodigoBarras == ean);
        }

        // 2. Se não achou por EAN, tenta encontrar por Nome exato
        if (produto == null)
        {
            var descTrim = descricao.Trim().ToLower();
            produto = await _db.Produtos.FirstOrDefaultAsync(p => p.Nome.ToLower() == descTrim);
        }

        // 3. Se não existe, cria o produto automaticamente no catálogo
        if (produto == null)
        {
            var margemPadrao = 1.40m; // 40% de margem padrão caso não informada
            var precoFinal = precoVendaSugerido > 0 
                ? precoVendaSugerido 
                : Math.Round(custoUnitario * margemPadrao, 2);

            produto = new Produto
            {
                Nome = descricao.Trim(),
                CodigoBarras = string.IsNullOrWhiteSpace(ean) ? null : ean.Trim(),
                Ncm = string.IsNullOrWhiteSpace(ncm) ? "0000.00.00" : ncm.Trim(),
                UnidadeMedida = string.IsNullOrWhiteSpace(unidade) ? "UN" : unidade.Trim().ToUpper(),
                Preco = precoFinal > 0 ? precoFinal : 1.00m,
                CustoUltimaCompra = custoUnitario,
                Estoque = 0 // Estoque será incrementado pelo faturamento da nota
            };

            _db.Produtos.Add(produto);
            await _db.SaveChangesAsync();

            _logger.LogInformation("[PRODUTO NOVO CRIADO VIA XML] #{Id} - '{Nome}' (EAN: {Ean}) - Custo: R$ {Custo:N2}",
                produto.Id, produto.Nome, produto.CodigoBarras, produto.CustoUltimaCompra);
        }
        else
        {
            // Atualiza campos fiscais caso estejam em branco
            if (string.IsNullOrWhiteSpace(produto.CodigoBarras) && !string.IsNullOrWhiteSpace(ean))
                produto.CodigoBarras = ean;

            if (string.IsNullOrWhiteSpace(produto.Ncm) && !string.IsNullOrWhiteSpace(ncm))
                produto.Ncm = ncm;

            produto.CustoUltimaCompra = custoUnitario;
            await _db.SaveChangesAsync();
        }

        return produto;
    }
}
