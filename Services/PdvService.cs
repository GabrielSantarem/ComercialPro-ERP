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
                new Produto { Nome = "Sacola Branca 2k", Preco = 0.50m, Estoque = 100 },
                new Produto { Nome = "Fita Adesiva Marrom", Preco = 7.90m, Estoque = 30 },
                new Produto { Nome = "Copo Descartável 200ml", Preco = 4.50m, Estoque = 50 },
                new Produto { Nome = "Papel Filme 30cm", Preco = 12.00m, Estoque = 15 }
            );
            await _db.SaveChangesAsync();

            var p1 = await _db.Produtos.FirstOrDefaultAsync(p => p.Id == 1);
            var p3 = await _db.Produtos.FirstOrDefaultAsync(p => p.Id == 3);
            if(p1 != null && p3 != null)
            {
                var vendaOntem = new Venda {
                    VendedorId = 1,
                    DataHora = DateTime.Now.AddDays(-1).AddHours(-4),
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

    public async Task SalvarPedidoAsync(int vendedorId, IEnumerable<(Produto Produto, int Quantidade)> carrinho, string formaPagamento = "Dinheiro")
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var valorTotal = carrinho.Sum(x => x.Quantidade * x.Produto.Preco);
            var venda = new Venda
            {
                VendedorId = vendedorId,
                DataHora = DateTime.Now,
                ValorTotal = valorTotal
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

            // Se houver um turno de caixa aberto, acumula o valor da venda na gaveta/turno
            var turnoAtivo = await _db.CaixasTurno.FirstOrDefaultAsync(c => c.Status == "ABERTO");
            if (turnoAtivo != null)
            {
                if (formaPagamento.Equals("Dinheiro", StringComparison.OrdinalIgnoreCase))
                {
                    turnoAtivo.TotalVendasDinheiro += valorTotal;
                }
                else
                {
                    turnoAtivo.TotalVendasOutros += valorTotal;
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation("Venda concluída com sucesso! Total: {Total}, VendedorId: {VendedorId}, Pagamento: {Forma}", venda.ValorTotal, vendedorId, formaPagamento);
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

    // === CONTROLE DE TURNOS DE CAIXA (ABERTURA, SANGRIA, FECHAMENTO) ===

    public async Task<CaixaTurno?> ObterTurnoAtualAsync()
    {
        return await _db.CaixasTurno
            .Include(c => c.Vendedor)
            .Include(c => c.Movimentacoes)
            .FirstOrDefaultAsync(c => c.Status == "ABERTO");
    }

    public async Task<CaixaTurno> AbrirCaixaAsync(int vendedorId, decimal saldoInicial, string observacao = "")
    {
        var existente = await _db.CaixasTurno.FirstOrDefaultAsync(c => c.Status == "ABERTO");
        if (existente != null)
        {
            throw new InvalidOperationException($"Já existe um turno de caixa aberto (Turno #{existente.Id}). Feche o turno atual antes de abrir outro.");
        }

        if (saldoInicial < 0)
        {
            throw new ArgumentException("O fundo de troco inicial não pode ser negativo.", nameof(saldoInicial));
        }

        var turno = new CaixaTurno
        {
            VendedorId = vendedorId,
            DataAbertura = DateTime.Now,
            SaldoInicial = saldoInicial,
            Status = "ABERTO",
            Observacao = observacao
        };

        _db.CaixasTurno.Add(turno);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Turno de Caixa #{Id} aberto com saldo inicial de R$ {Saldo:N2} por Vendedor {VendedorId}", turno.Id, saldoInicial, vendedorId);
        return turno;
    }

    public async Task<MovimentacaoCaixa> RegistrarSuprimentoAsync(int caixaTurnoId, decimal valor, string motivo)
    {
        if (valor <= 0) throw new ArgumentException("O valor do suprimento deve ser maior que zero.", nameof(valor));

        var turno = await _db.CaixasTurno.FindAsync(caixaTurnoId);
        if (turno == null || turno.Status != "ABERTO")
        {
            throw new InvalidOperationException("O turno informado não existe ou não está aberto.");
        }

        turno.TotalSuprimentos += valor;

        var mov = new MovimentacaoCaixa
        {
            CaixaTurnoId = caixaTurnoId,
            DataHora = DateTime.Now,
            Tipo = "SUPRIMENTO",
            Valor = valor,
            Motivo = motivo
        };

        _db.MovimentacoesCaixa.Add(mov);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Suprimento de R$ {Valor:N2} registrado no Caixa #{Id}. Motivo: {Motivo}", valor, caixaTurnoId, motivo);
        return mov;
    }

    public async Task<MovimentacaoCaixa> RegistrarSangriaAsync(int caixaTurnoId, decimal valor, string motivo)
    {
        if (valor <= 0) throw new ArgumentException("O valor da sangria deve ser maior que zero.", nameof(valor));

        var turno = await _db.CaixasTurno.FindAsync(caixaTurnoId);
        if (turno == null || turno.Status != "ABERTO")
        {
            throw new InvalidOperationException("O turno informado não existe ou não está aberto.");
        }

        if (valor > turno.SaldoEsperadoEmDinheiro)
        {
            throw new InvalidOperationException($"Sangria não permitida! Valor solicitado (R$ {valor:N2}) é maior do que o saldo físico disponível na gaveta (R$ {turno.SaldoEsperadoEmDinheiro:N2}).");
        }

        turno.TotalSangrias += valor;

        var mov = new MovimentacaoCaixa
        {
            CaixaTurnoId = caixaTurnoId,
            DataHora = DateTime.Now,
            Tipo = "SANGRIA",
            Valor = valor,
            Motivo = motivo
        };

        _db.MovimentacoesCaixa.Add(mov);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Sangria de R$ {Valor:N2} registrada no Caixa #{Id}. Motivo: {Motivo}", valor, caixaTurnoId, motivo);
        return mov;
    }

    public async Task<CaixaTurno> FecharCaixaAsync(int caixaTurnoId, decimal saldoInformado, string observacao = "")
    {
        var turno = await _db.CaixasTurno
            .Include(c => c.Vendedor)
            .Include(c => c.Movimentacoes)
            .FirstOrDefaultAsync(c => c.Id == caixaTurnoId);

        if (turno == null || turno.Status != "ABERTO")
        {
            throw new InvalidOperationException("O turno informado não existe ou já foi fechado.");
        }

        turno.DataFechamento = DateTime.Now;
        turno.SaldoInformado = saldoInformado;
        turno.DiferencaQuebra = saldoInformado - turno.SaldoEsperadoEmDinheiro;
        turno.Status = "FECHADO";
        if (!string.IsNullOrWhiteSpace(observacao))
        {
            turno.Observacao = string.IsNullOrWhiteSpace(turno.Observacao) 
                ? observacao 
                : $"{turno.Observacao} | {observacao}";
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Caixa #{Id} fechado. Esperado: R$ {Esperado:N2}, Informado: R$ {Informado:N2}, Diferença: R$ {Dif:N2}",
            turno.Id, turno.SaldoEsperadoEmDinheiro, saldoInformado, turno.DiferencaQuebra);

        return turno;
    }

    public async Task<List<CaixaTurno>> ObterHistoricoTurnosAsync()
    {
        return await _db.CaixasTurno
            .Include(c => c.Vendedor)
            .Include(c => c.Movimentacoes)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();
    }
}
