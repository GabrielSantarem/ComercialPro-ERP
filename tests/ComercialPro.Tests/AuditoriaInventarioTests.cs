using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Xunit;

namespace GetStartedApp.Tests;

public class AuditoriaInventarioTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public AuditoriaInventarioTests()
    {
        _dbName = $"test_inventario_{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _service = new PdvService(_db, NullLogger<PdvService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbName))
        {
            try { File.Delete(_dbName); } catch { }
        }
    }

    [Fact]
    public async Task Deve_Gerar_Lista_Para_Auditoria_Com_Todos_Produtos()
    {
        _db.Produtos.AddRange(
            new Produto { Nome = "Arroz 5kg", CodigoBarras = "7891", Preco = 25m, CustoUltimaCompra = 18m, Estoque = 40 },
            new Produto { Nome = "Feijão 1kg", CodigoBarras = "7892", Preco = 8m, CustoUltimaCompra = 5m, Estoque = 25 }
        );
        await _db.SaveChangesAsync();

        var itens = await _service.ObterItensParaAuditoriaAsync();

        Assert.Equal(2, itens.Count);
        Assert.All(itens, i => Assert.Null(i.SaldoFisicoContado));
        Assert.All(itens, i => Assert.Equal("PENDENTE", i.StatusDivergencia));
    }

    [Fact]
    public void Deve_Calcular_Divergencia_De_Quebra_E_Sobra_Com_Impacto_Financeiro()
    {
        var itens = new List<ItemAuditoriaEstoqueDto>
        {
            // Produto 1: Sistema = 20, Contado = 18 -> Faltam 2 (Quebra / Furto)
            new() { ProdutoId = 1, ProdutoNome = "Sabão", CustoUnitario = 10m, PrecoVenda = 15m, SaldoSistema = 20, SaldoFisicoContado = 18 },
            // Produto 2: Sistema = 10, Contado = 13 -> Sobram 3 (Sobra)
            new() { ProdutoId = 2, ProdutoNome = "Detergente", CustoUnitario = 4m, PrecoVenda = 6m, SaldoSistema = 10, SaldoFisicoContado = 13 },
            // Produto 3: Sistema = 5, Contado = 5 -> Bateu 100%
            new() { ProdutoId = 3, ProdutoNome = "Esponja", CustoUnitario = 2m, PrecoVenda = 3m, SaldoSistema = 5, SaldoFisicoContado = 5 }
        };

        var resumo = _service.CalcularResumoAuditoria(itens);

        Assert.Equal(3, resumo.TotalItensAuditados);
        Assert.Equal(1, resumo.TotalItensQuebra);
        Assert.Equal(1, resumo.TotalItensSobra);
        Assert.Equal(1, resumo.TotalItensBatidos);

        // Quebra: 2 * R$ 10,00 = R$ 20,00 de prejuízo
        Assert.Equal(20m, resumo.PrejuizoQuebrasCusto);

        // Sobra: 3 * R$ 4,00 = R$ 12,00 de sobra
        Assert.Equal(12m, resumo.ValorSobrasCusto);

        // Saldo líquido = 12 - 20 = -8
        Assert.Equal(-8m, resumo.SaldoLiquidoCusto);
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Contagem_Fisica_Negativa_Ao_Efetivar_Inventario()
    {
        var prod = new Produto { Nome = "Item Teste", Estoque = 10 };
        _db.Produtos.Add(prod);
        await _db.SaveChangesAsync();

        var contagens = new List<(int, int)> { (prod.Id, -5) };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.EfetivarInventarioFisicoAsync(contagens, "Gerente Loja")
        );
    }

    [Fact]
    public async Task Efetivar_Inventario_Deve_Ajustar_Estoque_E_Gravar_AjusteEstoque_Com_Responsavel()
    {
        var p1 = new Produto { Nome = "Item A", Preco = 10m, Estoque = 20 };
        var p2 = new Produto { Nome = "Item B", Preco = 20m, Estoque = 15 };
        _db.Produtos.AddRange(p1, p2);
        await _db.SaveChangesAsync();

        // Contagem: p1 tinha 20, agora tem 18 (-2). p2 tinha 15, agora tem 19 (+4).
        var contagens = new List<(int, int)>
        {
            (p1.Id, 18),
            (p2.Id, 19)
        };

        var alterados = await _service.EfetivarInventarioFisicoAsync(contagens, "Auditor Marcos", "Inventário Anual de Prateleira");

        Assert.Equal(2, alterados);

        // Verificar estoques atualizados
        var p1Db = await _db.Produtos.FindAsync(p1.Id);
        var p2Db = await _db.Produtos.FindAsync(p2.Id);
        Assert.Equal(18, p1Db!.Estoque);
        Assert.Equal(19, p2Db!.Estoque);

        // Verificar histórico de ajustes de auditoria
        var ajustes = await _db.AjustesEstoque.OrderBy(a => a.ProdutoId).ToListAsync();
        Assert.Equal(2, ajustes.Count);

        Assert.Equal(p1.Id, ajustes[0].ProdutoId);
        Assert.Equal("BALANCO_FISICO", ajustes[0].TipoAjuste);
        Assert.Equal(-2, ajustes[0].QuantidadeDiferenca);
        Assert.Equal(20, ajustes[0].EstoqueAnterior);
        Assert.Equal(18, ajustes[0].EstoqueNovo);
        Assert.Equal("Auditor Marcos", ajustes[0].Responsavel);

        Assert.Equal(p2.Id, ajustes[1].ProdutoId);
        Assert.Equal(4, ajustes[1].QuantidadeDiferenca);
        Assert.Equal(15, ajustes[1].EstoqueAnterior);
        Assert.Equal(19, ajustes[1].EstoqueNovo);
    }

    [Fact]
    public async Task Itens_Sem_Divergencia_Nao_Devem_Gerar_Ajustes_Desnecessarios()
    {
        var p = new Produto { Nome = "Item Correto", Estoque = 50 };
        _db.Produtos.Add(p);
        await _db.SaveChangesAsync();

        // Contagem física é idêntica ao sistema (50)
        var contagens = new List<(int, int)> { (p.Id, 50) };

        var alterados = await _service.EfetivarInventarioFisicoAsync(contagens, "Gerente");

        Assert.Equal(0, alterados);
        Assert.Empty(await _db.AjustesEstoque.ToListAsync());
    }

    [Fact]
    public void Folha_De_Contagem_Cega_Nao_Deve_Exibir_Saldo_Do_Sistema()
    {
        var itens = new List<ItemAuditoriaEstoqueDto>
        {
            new() { ProdutoId = 1, ProdutoNome = "Produto Segredo", CodigoBarras = "12345678", SaldoSistema = 888 }
        };

        var folha = _service.GerarTextoFolhaContagemCega(itens);

        // Deve conter campo em branco para preenchimento manual
        Assert.Contains("[ _________ ]", folha);
        Assert.Contains("Produto Segredo", folha);
        Assert.Contains("12345678", folha);
        Assert.Contains("FOLHA DE AUDITORIA E CONTAGEM CEGA", folha);

        // NÃO deve vazar a quantidade do sistema no texto da folha
        Assert.DoesNotContain("888", folha);
    }
}
