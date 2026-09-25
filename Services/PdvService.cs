using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GetStartedApp.Data;
using GetStartedApp.Models;

namespace GetStartedApp.Services;

public partial class PdvService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PdvService> _logger;
    private readonly IBackupDatabaseService? _backupService;

    public PdvService(AppDbContext db, ILogger<PdvService> logger, IBackupDatabaseService? backupService = null)
    {
        _db = db;
        _logger = logger;
        _backupService = backupService;
    }

    public async Task InicializarBancoDadosAsync()
    {
        await _db.Database.MigrateAsync();
        _logger.LogInformation("Banco de Dados inicializado/verificado com sucesso.");

        if (!await _db.ConfiguracoesTerminal.AnyAsync())
        {
            _db.ConfiguracoesTerminal.Add(new ConfiguracaoTerminal());
            await _db.SaveChangesAsync();
        }

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

    public async Task AdicionarVendedorAsync(Vendedor vendedor)
    {
        if (vendedor == null || string.IsNullOrWhiteSpace(vendedor.Nome)) return;
        _db.Vendedores.Add(vendedor);
        await _db.SaveChangesAsync();
    }

    public async Task AdicionarVendedorAsync(string nome)
    {
        if(string.IsNullOrWhiteSpace(nome)) return;
        _db.Vendedores.Add(new Vendedor { Nome = nome });
        await _db.SaveChangesAsync();
    }

    public async Task<ConfiguracaoTerminal> ObterConfiguracaoTerminalAsync()
    {
        var config = await _db.ConfiguracoesTerminal.FirstOrDefaultAsync();
        if (config == null)
        {
            config = new ConfiguracaoTerminal();
            _db.ConfiguracoesTerminal.Add(config);
            await _db.SaveChangesAsync();
        }
        return config;
    }

    public async Task SalvarConfiguracaoTerminalAsync(ConfiguracaoTerminal config)
    {
        var existente = await _db.ConfiguracoesTerminal.FirstOrDefaultAsync();
        if (existente == null)
        {
            _db.ConfiguracoesTerminal.Add(config);
        }
        else
        {
            existente.NomeEstacao = config.NomeEstacao;
            existente.ModeloImpressora = config.ModeloImpressora;
            existente.LarguraBobina = config.LarguraBobina;
            existente.PortaComunicacao = config.PortaComunicacao;
            existente.CortarPapelAutomatico = config.CortarPapelAutomatico;
            existente.ModoImpressaoPadrao = config.ModoImpressaoPadrao;
            existente.ImprimirComandaBalcaoAutomatico = config.ImprimirComandaBalcaoAutomatico;
            existente.IntegracaoBalancaHabilitada = config.IntegracaoBalancaHabilitada;
            existente.ModeloBalanca = config.ModeloBalanca;
            existente.ModoBalancaEtiqueta = config.ModoBalancaEtiqueta;
            existente.AcionarGavetaAutomaticamente = config.AcionarGavetaAutomaticamente;
            existente.UsarEmuladorBalanca = config.UsarEmuladorBalanca;
            existente.TamanhoCodigoBalanca = config.TamanhoCodigoBalanca;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<List<Produto>> PesquisarProdutosAsync(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return new List<Produto>();
        var terms = texto.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var produtos = await _db.Produtos.ToListAsync();
        return produtos.Where(p => terms.All(t => 
            p.Nome.ToLowerInvariant().Contains(t) || 
            (p.CodigoBarras != null && p.CodigoBarras.ToLowerInvariant().Contains(t)) ||
            p.Id.ToString() == t)).ToList();
    }

    public async Task<Venda?> ObterUltimaVendaAsync()
    {
        return await _db.Vendas
            .Include(v => v.Itens)
            .OrderByDescending(v => v.Id)
            .FirstOrDefaultAsync();
    }

    public async Task VincularDadosFiscaisVendaAsync(int vendaId, string chaveAcesso, long numero, int serie, string xml)
    {
        var venda = await _db.Vendas.FindAsync(vendaId);
        if (venda != null)
        {
            venda.ChaveAcessoNfce = chaveAcesso;
            venda.NumeroNfce = numero;
            venda.SerieNfce = serie;
            venda.XmlNfce = xml;
            venda.ProtocoloAutorizacaoNfce = $"135{DateTime.Now:yy}000{numero:D6}";
            await _db.SaveChangesAsync();
        }
    }

    public async Task<Venda> SalvarPedidoAsync(
        int vendedorId, 
        IEnumerable<(Produto Produto, int Quantidade)> carrinho, 
        string formaPagamento = "Dinheiro",
        IEnumerable<(string Forma, decimal Valor)>? pagamentosDetalhados = null)
    {
        var itensList = carrinho.ToList();
        if (itensList.Count == 0)
        {
            throw new InvalidOperationException("Não é possível registrar uma venda sem itens no carrinho.");
        }

        foreach (var item in itensList)
        {
            if (item.Quantidade <= 0)
            {
                throw new ArgumentException($"A quantidade do produto '{item.Produto.Nome}' deve ser maior que zero.", nameof(carrinho));
            }
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var valorTotal = itensList.Sum(x => x.Quantidade * x.Produto.Preco);
            var venda = new Venda
            {
                VendedorId = vendedorId,
                DataHora = DateTime.Now,
                ValorTotal = valorTotal,
                FormaPagamento = formaPagamento,
                Status = "AUTORIZADA"
            };
            _db.Vendas.Add(venda);
            await _db.SaveChangesAsync();

            foreach (var item in itensList)
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
                var parcelas = pagamentosDetalhados?.ToList();
                if (parcelas != null && parcelas.Count > 0)
                {
                    var valorDinheiro = parcelas
                        .Where(p => p.Forma.Contains("Dinheiro", StringComparison.OrdinalIgnoreCase))
                        .Sum(p => p.Valor);
                    var valorOutros = parcelas
                        .Where(p => !p.Forma.Contains("Dinheiro", StringComparison.OrdinalIgnoreCase))
                        .Sum(p => p.Valor);

                    turnoAtivo.TotalVendasDinheiro += valorDinheiro;
                    turnoAtivo.TotalVendasOutros += valorOutros;
                }
                else
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
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation("Venda concluída com sucesso! Total: {Total}, VendedorId: {VendedorId}, Pagamento: {Forma}", venda.ValorTotal, vendedorId, formaPagamento);
            return venda;
        }
        catch (Exception ex) { 
            _logger.LogError(ex, "Erro ao salvar o pedido no banco de dados. Fazendo rollback da transação.");
            await transaction.RollbackAsync(); 
            throw; 
        }
    }
}
