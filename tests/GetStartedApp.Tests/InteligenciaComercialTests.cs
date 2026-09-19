using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services.Inteligencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GetStartedApp.Tests;

public class InteligenciaComercialTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;
    private readonly InteligenciaComercialService _service;
    private readonly int _vendedor1Id;
    private readonly int _vendedor2Id;

    public InteligenciaComercialTests()
    {
        _dbName = $"test_intel_{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var v1 = new Vendedor { Nome = "Ana Vendedora", PercentualComissao = 10m };
        var v2 = new Vendedor { Nome = "Bruno Vendedor", PercentualComissao = 5m };
        _db.Vendedores.AddRange(v1, v2);
        _db.SaveChanges();

        _vendedor1Id = v1.Id;
        _vendedor2Id = v2.Id;

        _service = new InteligenciaComercialService(_db, NullLogger<InteligenciaComercialService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbName)) File.Delete(_dbName);
    }

    [Fact]
    public async Task ObterProdutosSemGiro_ComProdutoSemVendaHaMaisDe60Dias_DeveListarComCalculoDeCapitalParado()
    {
        // Arrange: Produto cadastrado há 120 dias, com última venda há 80 dias, 10 unidades em estoque a R$ 40 de custo
        var p = new Produto
        {
            Nome = "Produto Encalhado",
            Preco = 80m,
            CustoUltimaCompra = 40m,
            Estoque = 10,
            DataCadastro = DateTime.Today.AddDays(-120)
        };
        _db.Produtos.Add(p);
        await _db.SaveChangesAsync();

        var venda = new Venda
        {
            VendedorId = _vendedor1Id,
            DataHora = DateTime.Today.AddDays(-80),
            ValorTotal = 80m,
            Status = "FINALIZADA",
            Itens = [new ItemVenda { ProdutoId = p.Id, Quantidade = 1, PrecoUnitario = 80m }]
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync();

        // Act
        var semGiro = await _service.ObterProdutosSemGiroAsync(diasMinimosSemVenda: 60);

        // Assert
        Assert.NotEmpty(semGiro);
        var item = semGiro.FirstOrDefault(x => x.ProdutoId == p.Id);
        Assert.NotNull(item);
        Assert.Equal(10, item.SaldoEstoque);
        Assert.Equal(40m, item.CustoUnitario);
        Assert.Equal(400m, item.CapitalParadoTotal); // 10 * 40m = 400m
        Assert.True(item.DiasSemGiro >= 80);
    }

    [Fact]
    public async Task ObterProdutosSemGiro_ComProdutoVendidoRecentemente_NaoDeveListar()
    {
        // Arrange: Venda há 5 dias
        var p = new Produto
        {
            Nome = "Produto com Giro Alto",
            Preco = 50m,
            CustoUltimaCompra = 25m,
            Estoque = 20,
            DataCadastro = DateTime.Today.AddDays(-100)
        };
        _db.Produtos.Add(p);
        await _db.SaveChangesAsync();

        var venda = new Venda
        {
            VendedorId = _vendedor1Id,
            DataHora = DateTime.Today.AddDays(-5),
            ValorTotal = 50m,
            Status = "FINALIZADA",
            Itens = [new ItemVenda { ProdutoId = p.Id, Quantidade = 1, PrecoUnitario = 50m }]
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync();

        // Act
        var semGiro = await _service.ObterProdutosSemGiroAsync(diasMinimosSemVenda: 60);

        // Assert
        Assert.DoesNotContain(semGiro, x => x.ProdutoId == p.Id);
    }

    [Fact]
    public async Task ObterProdutosSemGiro_ComEstoqueZerado_NaoDeveConsiderarCapitalParado()
    {
        // Arrange: Produto com última venda há 90 dias, mas estoque é ZERO
        var p = new Produto
        {
            Nome = "Produto Esgotado",
            Preco = 100m,
            CustoUltimaCompra = 50m,
            Estoque = 0,
            DataCadastro = DateTime.Today.AddDays(-150)
        };
        _db.Produtos.Add(p);
        await _db.SaveChangesAsync();

        // Act
        var semGiro = await _service.ObterProdutosSemGiroAsync(diasMinimosSemVenda: 60);

        // Assert
        Assert.DoesNotContain(semGiro, x => x.ProdutoId == p.Id);
    }

    [Fact]
    public async Task ObterProdutosSemGiro_ProdutoCadastradoRecentementeSemVendas_NaoDeveListar()
    {
        // Arrange: Produto cadastrado há apenas 4 dias e nunca vendeu
        var p = new Produto
        {
            Nome = "Produto Novo Chegou Hoje",
            Preco = 120m,
            CustoUltimaCompra = 60m,
            Estoque = 15,
            DataCadastro = DateTime.Today.AddDays(-4)
        };
        _db.Produtos.Add(p);
        await _db.SaveChangesAsync();

        // Act: Filtro de corte de 60 dias
        var semGiro = await _service.ObterProdutosSemGiroAsync(diasMinimosSemVenda: 60);

        // Assert: Não pode constar como encalhado há 60 dias
        Assert.DoesNotContain(semGiro, x => x.ProdutoId == p.Id);
    }

    [Fact]
    public async Task CalcularComissoes_DeveIgnorarVendasCanceladas()
    {
        // Arrange: 1 venda autorizada de 200m e 1 venda cancelada de 500m para Ana (10% comissao)
        var vendaValida = new Venda
        {
            VendedorId = _vendedor1Id,
            DataHora = DateTime.Today,
            ValorTotal = 200m,
            Status = "FINALIZADA"
        };
        var vendaCancelada = new Venda
        {
            VendedorId = _vendedor1Id,
            DataHora = DateTime.Today,
            ValorTotal = 500m,
            Status = "CANCELADA",
            DataHoraCancelamento = DateTime.Today
        };
        _db.Vendas.AddRange(vendaValida, vendaCancelada);
        await _db.SaveChangesAsync();

        // Act
        var comissoes = await _service.CalcularComissoesAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        // Assert
        var comissaoAna = comissoes.FirstOrDefault(c => c.VendedorId == _vendedor1Id);
        Assert.NotNull(comissaoAna);
        Assert.Equal(1, comissaoAna.TotalVendas);
        Assert.Equal(200m, comissaoAna.FaturamentoTotal);
        Assert.Equal(20m, comissaoAna.ValorComissaoTotal); // 10% de 200m
    }

    [Fact]
    public async Task CalcularComissoes_DeveAplicarPercentualParametrizadoPorVendedor()
    {
        // Arrange:
        // Ana: 10% comissão, faturou 1.000m -> comissão 100m
        // Bruno: 5% comissão, faturou 2.000m -> comissão 100m
        var vendaAna = new Venda { VendedorId = _vendedor1Id, DataHora = DateTime.Today, ValorTotal = 1000m, Status = "FINALIZADA" };
        var vendaBruno = new Venda { VendedorId = _vendedor2Id, DataHora = DateTime.Today, ValorTotal = 2000m, Status = "FINALIZADA" };

        _db.Vendas.AddRange(vendaAna, vendaBruno);
        await _db.SaveChangesAsync();

        // Act
        var comissoes = await _service.CalcularComissoesAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        // Assert
        var cAna = comissoes.First(c => c.VendedorId == _vendedor1Id);
        var cBruno = comissoes.First(c => c.VendedorId == _vendedor2Id);

        Assert.Equal(100m, cAna.ValorComissaoTotal);
        Assert.Equal(100m, cBruno.ValorComissaoTotal);
        Assert.Equal(10m, cAna.PercentualComissao);
        Assert.Equal(5m, cBruno.PercentualComissao);
    }
}
