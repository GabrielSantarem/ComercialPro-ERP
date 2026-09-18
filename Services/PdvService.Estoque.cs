using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GetStartedApp.Models;

namespace GetStartedApp.Services;


public class ItemAuditoriaEstoqueDto
{
    public int ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = string.Empty;
    public string CodigoBarras { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = "UN";
    public decimal PrecoVenda { get; set; }
    public decimal CustoUnitario { get; set; }
    public int SaldoSistema { get; set; }
    public int? SaldoFisicoContado { get; set; }

    public int Diferenca => (SaldoFisicoContado ?? SaldoSistema) - SaldoSistema;
    public decimal ImpactoCusto => Diferenca * CustoUnitario;
    public decimal ImpactoVenda => Diferenca * PrecoVenda;

    public string StatusDivergencia => SaldoFisicoContado == null
        ? "PENDENTE"
        : (Diferenca == 0 ? "BATIDO_OK" : (Diferenca < 0 ? "QUEBRA" : "SOBRA"));
}

public class ResumoAuditoriaDto
{
    public int TotalItensAuditados { get; set; }
    public int TotalItensQuebra { get; set; }
    public int TotalItensSobra { get; set; }
    public int TotalItensBatidos { get; set; }
    public decimal PrejuizoQuebrasCusto { get; set; }
    public decimal ValorSobrasCusto { get; set; }
    public decimal SaldoLiquidoCusto => ValorSobrasCusto - PrejuizoQuebrasCusto;
    public List<ItemAuditoriaEstoqueDto> Itens { get; set; } = [];
}

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
            .Include(e => e.Titulos)
            .OrderByDescending(e => e.DataEntrada)
            .ToListAsync();
    }

    public async Task<EntradaMercadoria> RegistrarEntradaMercadoriaAsync(
        string numeroNota, 
        string fornecedor, 
        string observacao, 
        List<(int ProdutoId, int Quantidade, decimal CustoUnitario)> itens,
        List<(string NumeroParcela, DateTime Vencimento, decimal Valor)>? parcelas = null,
        string fornecedorCnpj = "")
    {
        if (itens.Count == 0) return new EntradaMercadoria();

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

            // Integração Financeira: Cria as duplicatas em Contas a Pagar
            if (parcelas != null && parcelas.Count > 0)
            {
                foreach (var p in parcelas)
                {
                    var titulo = new ContaPagar
                    {
                        EntradaMercadoriaId = entrada.Id,
                        FornecedorNome = fornecedor,
                        FornecedorCnpj = fornecedorCnpj,
                        NumeroDocumento = numeroNota,
                        NumeroParcela = p.NumeroParcela,
                        Valor = p.Valor,
                        DataEmissao = DateTime.Today,
                        DataVencimento = p.Vencimento,
                        Status = "PENDENTE"
                    };
                    _db.ContasPagar.Add(titulo);
                }
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Entrada de Mercadoria NF '{Nota}' registrada com sucesso. Total: R$ {Total}. Títulos Financeiros gerados: {QtdTitulos}",
                numeroNota, entrada.ValorTotal, parcelas?.Count ?? 0);

            return entrada;
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

    // === GESTÃO DE ESTOQUE MÍNIMO & AJUSTES/BAIXAS POR AVARIA ===

    public async Task<AjusteEstoque> RegistrarAjusteEstoqueAsync(
        int produtoId,
        int quantidadeDiferenca,
        string tipoAjuste,
        string motivo,
        string responsavel = "Operador Padrão")
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException("O motivo ou justificativa do ajuste de estoque é obrigatório.", nameof(motivo));
        }

        if (quantidadeDiferenca == 0)
        {
            throw new ArgumentException("A quantidade ajustada não pode ser zero.", nameof(quantidadeDiferenca));
        }

        var produto = await _db.Produtos.FindAsync(produtoId);
        if (produto == null)
        {
            throw new InvalidOperationException($"Produto #{produtoId} não encontrado.");
        }

        var estoqueAnterior = produto.Estoque;
        var estoqueNovo = estoqueAnterior + quantidadeDiferenca;

        if (estoqueNovo < 0)
        {
            throw new InvalidOperationException($"O estoque do produto '{produto.Nome}' não pode ficar negativo (Estoque atual: {estoqueAnterior}, Ajuste solicitado: {quantidadeDiferenca}).");
        }

        produto.Estoque = estoqueNovo;

        var ajuste = new AjusteEstoque
        {
            ProdutoId = produtoId,
            DataHora = DateTime.Now,
            TipoAjuste = string.IsNullOrWhiteSpace(tipoAjuste) ? "AVARIA" : tipoAjuste.Trim().ToUpper(),
            QuantidadeDiferenca = quantidadeDiferenca,
            EstoqueAnterior = estoqueAnterior,
            EstoqueNovo = estoqueNovo,
            Motivo = motivo.Trim(),
            Responsavel = string.IsNullOrWhiteSpace(responsavel) ? "Operador Padrão" : responsavel.Trim()
        };

        _db.AjustesEstoque.Add(ajuste);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Ajuste de Estoque #{Id} no produto #{ProdId} ({Nome}). Diferença: {Dif} ({Ant} -> {Novo}). Motivo: {Motivo}",
            ajuste.Id, produto.Id, produto.Nome, quantidadeDiferenca, estoqueAnterior, estoqueNovo, motivo);

        return ajuste;
    }

    public async Task<List<Produto>> ObterProdutosComEstoqueCriticoAsync()
    {
        return await _db.Produtos
            .Where(p => p.Estoque <= p.EstoqueMinimo)
            .OrderBy(p => p.Estoque)
            .ToListAsync();
    }

    public async Task<List<AjusteEstoque>> ObterHistoricoAjustesAsync(int? produtoId = null)
    {
        var query = _db.AjustesEstoque
            .Include(a => a.Produto)
            .AsQueryable();

        if (produtoId.HasValue)
        {
            query = query.Where(a => a.ProdutoId == produtoId.Value);
        }

        return await query.OrderByDescending(a => a.DataHora).ToListAsync();
    }

    public async Task AtualizarEstoqueMinimoProdutoAsync(int produtoId, int novoMinimo)
    {
        if (novoMinimo < 0)
        {
            throw new ArgumentException("O estoque mínimo não pode ser negativo.", nameof(novoMinimo));
        }

        var produto = await _db.Produtos.FindAsync(produtoId);
        if (produto == null)
        {
            throw new InvalidOperationException($"Produto #{produtoId} não encontrado.");
        }

        produto.EstoqueMinimo = novoMinimo;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Estoque mínimo do produto #{Id} ('{Nome}') atualizado para {Min} unidades.",
            produto.Id, produto.Nome, novoMinimo);
    }

    // === AUDITORIA & INVENTÁRIO FÍSICO ===

    public async Task<List<ItemAuditoriaEstoqueDto>> ObterItensParaAuditoriaAsync()
    {
        var produtos = await _db.Produtos.OrderBy(p => p.Nome).ToListAsync();
        return produtos.Select(p => new ItemAuditoriaEstoqueDto
        {
            ProdutoId = p.Id,
            ProdutoNome = p.Nome,
            CodigoBarras = p.CodigoBarras ?? string.Empty,
            UnidadeMedida = p.UnidadeMedida ?? "UN",
            PrecoVenda = p.Preco,
            CustoUnitario = p.CustoUltimaCompra,
            SaldoSistema = p.Estoque,
            SaldoFisicoContado = null
        }).ToList();
    }

    public ResumoAuditoriaDto CalcularResumoAuditoria(IEnumerable<ItemAuditoriaEstoqueDto> itens)
    {
        var lista = itens.ToList();
        var contados = lista.Where(x => x.SaldoFisicoContado.HasValue).ToList();

        var resumo = new ResumoAuditoriaDto
        {
            TotalItensAuditados = contados.Count,
            TotalItensQuebra = contados.Count(x => x.Diferenca < 0),
            TotalItensSobra = contados.Count(x => x.Diferenca > 0),
            TotalItensBatidos = contados.Count(x => x.Diferenca == 0),
            PrejuizoQuebrasCusto = contados.Where(x => x.Diferenca < 0).Sum(x => Math.Abs(x.ImpactoCusto)),
            ValorSobrasCusto = contados.Where(x => x.Diferenca > 0).Sum(x => x.ImpactoCusto),
            Itens = lista
        };

        return resumo;
    }

    public string GerarTextoFolhaContagemCega(IEnumerable<ItemAuditoriaEstoqueDto> itens)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                 FOLHA DE AUDITORIA E CONTAGEM CEGA DE ESTOQUE                  ");
        sb.AppendLine($" Data de Emissão: {DateTime.Now:dd/MM/yyyy HH:mm} | Loja / Depósito Principal");
        sb.AppendLine(" REGRA: Anote a contagem física real. Não consulte o sistema durante a contagem.");
        sb.AppendLine("================================================================================");
        sb.AppendLine(string.Format("{0,-14} | {1,-38} | {2,-4} | {3,-12}", "CÓD. BARRAS", "DESCRIÇÃO DO PRODUTO", "UN", "CONTAGEM FÍSICA"));
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (var item in itens)
        {
            var ean = string.IsNullOrWhiteSpace(item.CodigoBarras) ? $"#{item.ProdutoId:D5}" : item.CodigoBarras;
            var desc = item.ProdutoNome.Length > 38 ? item.ProdutoNome.Substring(0, 35) + "..." : item.ProdutoNome;
            sb.AppendLine(string.Format("{0,-14} | {1,-38} | {2,-4} | [ _________ ]", ean, desc, item.UnidadeMedida));
        }

        sb.AppendLine("================================================================================");
        sb.AppendLine(" Responsável pela Contagem: ____________________________________________________");
        sb.AppendLine(" Visto do Gerente / Conferência: _______________________________________________");
        return sb.ToString();
    }

    public async Task<int> EfetivarInventarioFisicoAsync(
        List<(int ProdutoId, int SaldoFisicoContado)> contagens,
        string responsavel,
        string observacao = "Inventário Físico Periódico")
    {
        if (string.IsNullOrWhiteSpace(responsavel))
        {
            throw new ArgumentException("O nome do responsável pela auditoria é obrigatório.", nameof(responsavel));
        }

        foreach (var c in contagens)
        {
            if (c.SaldoFisicoContado < 0)
            {
                throw new ArgumentException($"O saldo físico contado não pode ser negativo (Produto #{c.ProdutoId} informado: {c.SaldoFisicoContado}).", nameof(contagens));
            }
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            int alterados = 0;
            foreach (var c in contagens)
            {
                var produto = await _db.Produtos.FindAsync(c.ProdutoId);
                if (produto == null) continue;

                if (produto.Estoque != c.SaldoFisicoContado)
                {
                    var estoqueAnterior = produto.Estoque;
                    var diferenca = c.SaldoFisicoContado - estoqueAnterior;

                    produto.Estoque = c.SaldoFisicoContado;

                    var ajuste = new AjusteEstoque
                    {
                        ProdutoId = produto.Id,
                        DataHora = DateTime.Now,
                        TipoAjuste = "BALANCO_FISICO",
                        QuantidadeDiferenca = diferenca,
                        EstoqueAnterior = estoqueAnterior,
                        EstoqueNovo = c.SaldoFisicoContado,
                        Motivo = string.IsNullOrWhiteSpace(observacao) ? "Inventário Físico Periódico" : observacao.Trim(),
                        Responsavel = responsavel.Trim()
                    };

                    _db.AjustesEstoque.Add(ajuste);
                    alterados++;
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Inventário Físico efetivado por '{Responsavel}'. {Alterados} produtos ajustados com sucesso.",
                responsavel, alterados);

            return alterados;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erro ao efetivar inventário físico");
            throw;
        }
    }

}
