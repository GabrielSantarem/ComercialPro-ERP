using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Xunit;

namespace GetStartedApp.Tests;

public class InteligenciaVendasTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public InteligenciaVendasTests()
    {
        _dbName = $"test_intel_vendas_{Guid.NewGuid():N}.db";
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
        if (File.Exists(_dbName)) File.Delete(_dbName);
    }

    [Fact]
    public async Task Deve_Classificar_Produtos_Na_Curva_ABC_Corretamente()
    {
        // Cadastrar Vendedor
        var vendedor = new Vendedor { Nome = "Vendedor Teste" };
        _db.Vendedores.Add(vendedor);

        // Cadastrar 3 Produtos
        var pCarroChefe = new Produto { Nome = "Produto Top (Classe A)", Preco = 100m, CustoUltimaCompra = 50m, Estoque = 100 };
        var pMedio = new Produto { Nome = "Produto Médio (Classe B)", Preco = 15m, CustoUltimaCompra = 8m, Estoque = 100 };
        var pBaixo = new Produto { Nome = "Produto Cauda (Classe C)", Preco = 5m, CustoUltimaCompra = 2m, Estoque = 100 };

        _db.Produtos.AddRange(pCarroChefe, pMedio, pBaixo);
        await _db.SaveChangesAsync();

        // Registrar Venda 1: 8 unidades de pCarroChefe = R$ 800,00 (80% do faturamento total de R$ 1.000)
        await _service.SalvarPedidoAsync(vendedor.Id, new List<(Produto, int)> { (pCarroChefe, 8) });

        // Registrar Venda 2: 10 unidades de pMedio = R$ 150,00 (15% do faturamento)
        await _service.SalvarPedidoAsync(vendedor.Id, new List<(Produto, int)> { (pMedio, 10) });

        // Registrar Venda 3: 10 unidades de pBaixo = R$ 50,00 (5% do faturamento)
        await _service.SalvarPedidoAsync(vendedor.Id, new List<(Produto, int)> { (pBaixo, 10) });

        var indicadores = await _service.ObterIndicadoresVendasAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        Assert.NotNull(indicadores);
        Assert.Equal(1000m, indicadores.TotalFaturado);
        Assert.Equal(3, indicadores.QuantidadeVendas);
        Assert.Equal(28, indicadores.QuantidadeItensVendidos);

        Assert.Equal(3, indicadores.CurvaAbc.Count);
        Assert.Equal("A", indicadores.CurvaAbc[0].ClasseAbc);
        Assert.Equal(pCarroChefe.Id, indicadores.CurvaAbc[0].ProdutoId);
        Assert.Equal(80m, Math.Round(indicadores.CurvaAbc[0].PercentualDoTotal, 2));

        Assert.Equal("B", indicadores.CurvaAbc[1].ClasseAbc);
        Assert.Equal(pMedio.Id, indicadores.CurvaAbc[1].ProdutoId);

        Assert.Equal("C", indicadores.CurvaAbc[2].ClasseAbc);
        Assert.Equal(pBaixo.Id, indicadores.CurvaAbc[2].ProdutoId);
    }

    [Fact]
    public async Task Deve_Calcular_DRE_Gerencial_Com_CMV_E_Despesas_Operacionais()
    {
        var vendedor = new Vendedor { Nome = "Maria Atendente" };
        _db.Vendedores.Add(vendedor);

        var produto = new Produto { Nome = "Teclado Mecânico", Preco = 200m, CustoUltimaCompra = 120m, Estoque = 10 };
        _db.Produtos.Add(produto);
        await _db.SaveChangesAsync();

        // Venda: 2 teclados = R$ 400 de receita; Custo CMV = 2 * 120 = R$ 240
        await _service.SalvarPedidoAsync(vendedor.Id, new List<(Produto, int)> { (produto, 2) });

        // Despesa Operacional Paga: R$ 50 de conta de luz
        var conta = await _service.RegistrarContaPagarManualAsync("Enel", "", "Luz-01", "1/1", 50m, DateTime.Today);
        await _service.LiquidarContaPagarAsync(conta.Id, 50m, "PIX");

        var indicadores = await _service.ObterIndicadoresVendasAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        var dre = indicadores.Dre;
        Assert.Equal(400m, dre.ReceitaBruta);
        Assert.Equal(240m, dre.CustoMercadoriasVendidas);
        Assert.Equal(160m, dre.LucroBruto); // 400 - 240
        Assert.Equal(40m, dre.MargemBrutaPercentual); // 160 / 400 = 40%
        Assert.Equal(50m, dre.DespesasOperacionaisPagas);
        Assert.Equal(110m, dre.LucroLiquido); // 160 - 50 = 110
        Assert.Equal(27.5m, dre.MargemLiquidaPercentual); // 110 / 400 = 27.5%
    }

    [Fact]
    public async Task Deve_Calcular_Ranking_De_Vendedores_E_Ticket_Medio()
    {
        var v1 = new Vendedor { Nome = "Carlos" };
        var v2 = new Vendedor { Nome = "Ana" };
        _db.Vendedores.AddRange(v1, v2);

        var prod = new Produto { Nome = "Mouse", Preco = 50m, CustoUltimaCompra = 20m, Estoque = 20 };
        _db.Produtos.Add(prod);
        await _db.SaveChangesAsync();

        // Carlos faz 2 vendas de 1 mouse = R$ 100 total
        await _service.SalvarPedidoAsync(v1.Id, new List<(Produto, int)> { (prod, 1) });
        await _service.SalvarPedidoAsync(v1.Id, new List<(Produto, int)> { (prod, 1) });

        // Ana faz 1 venda de 3 mouses = R$ 150 total
        await _service.SalvarPedidoAsync(v2.Id, new List<(Produto, int)> { (prod, 3) });

        var indicadores = await _service.ObterIndicadoresVendasAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        Assert.Equal(2, indicadores.RankingVendedores.Count);
        // Primeiro deve ser a Ana (R$ 150)
        Assert.Equal(v2.Id, indicadores.RankingVendedores[0].VendedorId);
        Assert.Equal(150m, indicadores.RankingVendedores[0].TotalFaturado);
        Assert.Equal(150m, indicadores.RankingVendedores[0].TicketMedio);

        // Segundo deve ser o Carlos (R$ 100)
        Assert.Equal(v1.Id, indicadores.RankingVendedores[1].VendedorId);
        Assert.Equal(100m, indicadores.RankingVendedores[1].TotalFaturado);
        Assert.Equal(50m, indicadores.RankingVendedores[1].TicketMedio); // 100 / 2 = 50
    }

    [Fact]
    public async Task Periodo_Sem_Vendas_Nao_Deve_Lancar_Excecao_De_Divisao_Por_Zero()
    {
        var indicadores = await _service.ObterIndicadoresVendasAsync(DateTime.Today.AddYears(-10), DateTime.Today.AddYears(-9));

        Assert.NotNull(indicadores);
        Assert.Equal(0m, indicadores.TotalFaturado);
        Assert.Equal(0, indicadores.QuantidadeVendas);
        Assert.Equal(0m, indicadores.TicketMedio);
        Assert.Empty(indicadores.CurvaAbc);
        Assert.Equal(0m, indicadores.Dre.MargemBrutaPercentual);
        Assert.Equal(0m, indicadores.Dre.MargemLiquidaPercentual);
    }
}
