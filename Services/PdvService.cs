using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GetStartedApp.Data;
using GetStartedApp.Models;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.Services;

public class RelatorioInventarioDto
{
    public int ProdutoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int QuantidadeVendida { get; set; }
    public int EstoqueAtualSistema { get; set; }
}

public class PdvService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PdvService> _logger;

    public PdvService(AppDbContext db, ILogger<PdvService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InicializarBancoDadosAsync()
    {
        await _db.Database.MigrateAsync();
        _logger.LogInformation("Banco de Dados inicializado/verificado.");

        if (!await _db.Vendedores.AnyAsync())
        {
            _db.Vendedores.AddRange(
                new Vendedor { Nome = "João (Gerente)" },
                new Vendedor { Nome = "Maria (Caixa)" },
                new Vendedor { Nome = "Carlos (Atendente)" }
            );
            await _db.SaveChangesAsync();
        }

        if (!await _db.Produtos.AnyAsync())
        {
            _db.Produtos.AddRange(
                new Produto { Nome = "SACOLA BRANCA 2K (100 unds)", Preco = 12.50m, Estoque = 150 },
                new Produto { Nome = "SACOLA BRANCA 2K (Meio Milheiro)", Preco = 55.00m, Estoque = 30 },
                new Produto { Nome = "BOBINA PICOTADA 3x40 (Rolo 500g)", Preco = 18.90m, Estoque = 40 },
                new Produto { Nome = "COPO DESCARTÁVEL 200ml TRANSPARENTE (100 unds)", Preco = 6.99m, Estoque = 300 }
            );
            await _db.SaveChangesAsync();
        }

        // Criar vendas fictícias de "ONTEM" para podermos testar a tela de inventário
        if (!await _db.Vendas.AnyAsync())
        {
            var p1 = await _db.Produtos.FirstOrDefaultAsync(p => p.Id == 1);
            var p3 = await _db.Produtos.FirstOrDefaultAsync(p => p.Id == 3);
            if(p1 != null && p3 != null)
            {
                var vendaOntem = new Venda {
                    VendedorId = 1,
                    DataHora = DateTime.Now.AddDays(-1).AddHours(-4), // Venda feita ontem
                    ValorTotal = (2 * p1.Preco) + (1 * p3.Preco)
                };
                vendaOntem.Itens.Add(new ItemVenda { ProdutoId = p1.Id, Quantidade = 2, PrecoUnitario = p1.Preco });
                vendaOntem.Itens.Add(new ItemVenda { ProdutoId = p3.Id, Quantidade = 1, PrecoUnitario = p3.Preco });
                
                _db.Vendas.Add(vendaOntem);
                await _db.SaveChangesAsync();
            }
        }
    }

    public async Task<List<Vendedor>> ObterVendedoresAsync() => await _db.Vendedores.ToListAsync();
    public async Task<List<Produto>> ObterTodosProdutosAsync() => await _db.Produtos.ToListAsync();

    public async Task SalvarProdutoAsync(Produto p)
    {
        if (p.Id == 0) _db.Produtos.Add(p);
        else _db.Produtos.Update(p);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Produto>> PesquisarProdutosAsync(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return new List<Produto>();
        var terms = texto.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var produtos = await _db.Produtos.ToListAsync();
        return produtos.Where(p => terms.All(t => p.Nome.ToLowerInvariant().Contains(t))).ToList();
    }

    public async Task SalvarPedidoAsync(int vendedorId, IEnumerable<(Produto Produto, int Quantidade)> carrinho)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var venda = new Venda
            {
                VendedorId = vendedorId,
                DataHora = DateTime.Now,
                ValorTotal = carrinho.Sum(x => x.Quantidade * x.Produto.Preco)
            };
            _db.Vendas.Add(venda);
            await _db.SaveChangesAsync();

            foreach (var item in carrinho)
            {
                var itemVenda = new ItemVenda
                {
                    VendaId = venda.Id,
                    ProdutoId = item.Produto.Id,
                    Quantidade = item.Quantidade,
                    PrecoUnitario = item.Produto.Preco
                };
                _db.ItensVenda.Add(itemVenda);

                var produtoDb = await _db.Produtos.FindAsync(item.Produto.Id);
                if (produtoDb != null) produtoDb.Estoque -= item.Quantidade;
            }
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation("Venda concluída com sucesso! Total: {Total}, VendedorId: {VendedorId}", venda.ValorTotal, vendedorId);
        }
        catch (Exception ex) { 
            _logger.LogError(ex, "Erro ao salvar o pedido no banco de dados. Fazendo rollback da transação.");
            await transaction.RollbackAsync(); 
            throw; 
        }
    }

    // NOVA FUNÇÃO: INVENTÁRIO
    public async Task<List<RelatorioInventarioDto>> GerarLevantamentoInventarioAsync(DateTime data)
    {
        // Pega inicio e fim do dia alvo
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

    public async Task AdicionarVendedorAsync(string nome)
    {
        if(string.IsNullOrWhiteSpace(nome)) return;
        _db.Vendedores.Add(new Vendedor { Nome = nome });
        await _db.SaveChangesAsync();
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
                    // Alimenta o estoque físico do produto
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
