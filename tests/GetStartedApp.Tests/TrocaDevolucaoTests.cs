using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GetStartedApp.Tests;

public class TrocaDevolucaoTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;
    private readonly TrocaDevolucaoService _service;
    private readonly int _produto1Id;
    private readonly int _produto2Id;
    private readonly int _vendedorId;

    public TrocaDevolucaoTests()
    {
        _dbName = $"test_troca_{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var vendedor = new Vendedor { Nome = "Vendedor Teste", PercentualComissao = 5m };
        _db.Vendedores.Add(vendedor);
        _db.SaveChanges();
        _vendedorId = vendedor.Id;

        var p1 = new Produto { Nome = "Camiseta Polo", Preco = 100m, Estoque = 10, CustoUltimaCompra = 50m };
        var p2 = new Produto { Nome = "Calça Jeans", Preco = 200m, Estoque = 5, CustoUltimaCompra = 90m };
        _db.Produtos.AddRange(p1, p2);
        _db.SaveChanges();

        _produto1Id = p1.Id;
        _produto2Id = p2.Id;

        _service = new TrocaDevolucaoService(_db, NullLogger<TrocaDevolucaoService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbName)) File.Delete(_dbName);
    }

    [Fact]
    public async Task EmitirValeTroca_ComProdutosValidos_DeveAtualizarEstoque_E_GerarCodigoUnico()
    {
        // Arrange
        var itens = new List<ItemDevolucaoDto>
        {
            new(_produto1Id, Quantidade: 2, PrecoUnitario: 100m, DestinarAvaria: false, Motivo: "Tamanho incorreto")
        };

        // Act
        var vale = await _service.EmitirValeTrocaAsync(itens, "Troca de numeração", "João Silva", "123.456.789-00");

        // Assert
        Assert.NotNull(vale);
        Assert.StartsWith("VALE-", vale.Codigo);
        Assert.Equal(200m, vale.ValorOriginal);
        Assert.Equal(200m, vale.SaldoDisponivel);
        Assert.Equal("ATIVO", vale.Status);

        // Estoque do produto 1 deve ter subido de 10 para 12
        var produtoDb = await _db.Produtos.FindAsync(_produto1Id);
        Assert.NotNull(produtoDb);
        Assert.Equal(12, produtoDb.Estoque);

        // Deve registrar ajuste em AjustesEstoque
        var ajuste = await _db.AjustesEstoque.FirstOrDefaultAsync(a => a.ProdutoId == _produto1Id);
        Assert.NotNull(ajuste);
        Assert.Equal("ENTRADA_AVULSA", ajuste.TipoAjuste);
        Assert.Equal(2, ajuste.QuantidadeDiferenca);
    }

    [Fact]
    public async Task EmitirValeTroca_ComDestinoAvaria_DeveRegistrarPerdaSemIncrementarEstoqueVenda()
    {
        // Arrange
        var itens = new List<ItemDevolucaoDto>
        {
            new(_produto2Id, Quantidade: 1, PrecoUnitario: 200m, DestinarAvaria: true, Motivo: "Zíper quebrado")
        };

        // Act
        var vale = await _service.EmitirValeTrocaAsync(itens, "Devolução por defeito", "Maria Santos", null);

        // Assert
        Assert.NotNull(vale);
        Assert.Equal(200m, vale.ValorOriginal);

        // Estoque não deve ter subido para venda comercial (continua 5)
        var produtoDb = await _db.Produtos.FindAsync(_produto2Id);
        Assert.NotNull(produtoDb);
        Assert.Equal(5, produtoDb.Estoque);

        // Ajuste deve ser marcado como SAIDA_AVARIA
        var ajuste = await _db.AjustesEstoque.FirstOrDefaultAsync(a => a.ProdutoId == _produto2Id);
        Assert.NotNull(ajuste);
        Assert.Equal("SAIDA_AVARIA", ajuste.TipoAjuste);
    }

    [Fact]
    public async Task ResgatarValeCredito_ComValorMenorQueCompra_DeveZerarSaldoVale_E_RetornarAbatimento()
    {
        // Arrange: Vale de R$ 100
        var itens = new List<ItemDevolucaoDto>
        {
            new(_produto1Id, Quantidade: 1, PrecoUnitario: 100m, DestinarAvaria: false, Motivo: "Troca")
        };
        var vale = await _service.EmitirValeTrocaAsync(itens, "Troca", "Carlos", null);

        // Act: Compra de R$ 150 (maior que o vale de 100)
        var (sucesso, _, valorAbatido) = await _service.ResgatarValeCreditoAsync(vale.Codigo, 150m, vendaDestinoId: 99);

        // Assert
        Assert.True(sucesso);
        Assert.Equal(100m, valorAbatido);

        var valeDb = await _db.ValesCredito.FindAsync(vale.Id);
        Assert.NotNull(valeDb);
        Assert.Equal(0m, valeDb.SaldoDisponivel);
        Assert.Equal("UTILIZADO", valeDb.Status);
        Assert.NotNull(valeDb.DataUtilizacaoTotal);
    }

    [Fact]
    public async Task ResgatarValeCredito_ComValorMaiorQueCompra_DeveManterSaldoRestanteNoVale()
    {
        // Arrange: Vale de R$ 200
        var itens = new List<ItemDevolucaoDto>
        {
            new(_produto2Id, Quantidade: 1, PrecoUnitario: 200m, DestinarAvaria: false, Motivo: "Troca")
        };
        var vale = await _service.EmitirValeTrocaAsync(itens, "Troca", "Carlos", null);

        // Act: Compra de R$ 70 (menor que o vale de 200)
        var (sucesso, _, valorAbatido) = await _service.ResgatarValeCreditoAsync(vale.Codigo, 70m, vendaDestinoId: 100);

        // Assert
        Assert.True(sucesso);
        Assert.Equal(70m, valorAbatido);

        var valeDb = await _db.ValesCredito.FindAsync(vale.Id);
        Assert.NotNull(valeDb);
        Assert.Equal(130m, valeDb.SaldoDisponivel);
        Assert.Equal("ATIVO", valeDb.Status);
        Assert.Null(valeDb.DataUtilizacaoTotal);
    }

    [Fact]
    public async Task ResgatarValeCredito_ExpiradoOuJaUtilizado_DeveRecusarOperacao()
    {
        // Arrange 1: Vale expirado
        var valeExpirado = new ValeCredito
        {
            Codigo = "VALE-EXPIRADO-TESTE",
            ValorOriginal = 100m,
            SaldoDisponivel = 100m,
            DataEmissao = DateTime.Now.AddDays(-40),
            DataValidade = DateTime.Now.AddDays(-10),
            Status = "ATIVO"
        };
        _db.ValesCredito.Add(valeExpirado);
        await _db.SaveChangesAsync();

        // Act 1
        var (sucesso1, msg1, _) = await _service.ResgatarValeCreditoAsync("VALE-EXPIRADO-TESTE", 50m, 1);

        // Assert 1
        Assert.False(sucesso1);
        Assert.Contains("expirado", msg1, StringComparison.OrdinalIgnoreCase);

        // Arrange 2: Vale já utilizado
        var valeUtilizado = new ValeCredito
        {
            Codigo = "VALE-UTILIZADO-TESTE",
            ValorOriginal = 100m,
            SaldoDisponivel = 0m,
            DataEmissao = DateTime.Now,
            DataValidade = DateTime.Now.AddDays(30),
            Status = "UTILIZADO",
            DataUtilizacaoTotal = DateTime.Now
        };
        _db.ValesCredito.Add(valeUtilizado);
        await _db.SaveChangesAsync();

        // Act 2
        var (sucesso2, msg2, _) = await _service.ResgatarValeCreditoAsync("VALE-UTILIZADO-TESTE", 50m, 2);

        // Assert 2
        Assert.False(sucesso2);
        Assert.Contains("já liquidado", msg2, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmitirValeTroca_QuantidadeSuperiorAVendida_DeveLancarExcecao()
    {
        // Arrange: Venda original com 1 unidade do produto 1
        var venda = new Venda
        {
            VendedorId = _vendedorId,
            DataHora = DateTime.Now,
            ValorTotal = 100m,
            Itens = [new ItemVenda { ProdutoId = _produto1Id, Quantidade = 1, PrecoUnitario = 100m }]
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync();

        // Act & Assert: Tentar devolver 3 unidades quando só comprou 1
        var itensDevolucao = new List<ItemDevolucaoDto>
        {
            new(_produto1Id, Quantidade: 3, PrecoUnitario: 100m, DestinarAvaria: false, Motivo: "Devolução excessiva")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.EmitirValeTrocaAsync(itensDevolucao, "Tentativa irregular", "Fraude", null, vendaOrigemId: venda.Id));
    }
}
