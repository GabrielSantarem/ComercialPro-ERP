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

public class SugestaoComprasTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public SugestaoComprasTests()
    {
        _dbName = $"test_compras_{Guid.NewGuid():N}.db";
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
    public async Task Deve_Calcular_Consumo_Medio_Diario_E_Ponto_De_Pedido_Corretamente()
    {
        // 1. Criar vendedor e produto
        var vendedor = new Vendedor { Nome = "Balcão" };
        var prod = new Produto 
        { 
            Nome = "Café Especial 500g", 
            Preco = 20m, 
            CustoUltimaCompra = 12m, 
            Estoque = 10, 
            EstoqueMinimo = 5 
        };
        _db.Vendedores.Add(vendedor);
        _db.Produtos.Add(prod);
        await _db.SaveChangesAsync();

        // 2. Simular venda de 30 unidades nos últimos 30 dias (1 por dia)
        var venda = new Venda
        {
            VendedorId = vendedor.Id,
            DataHora = DateTime.Today.AddDays(-10),
            ValorTotal = 600m,
            Itens = [new ItemVenda { ProdutoId = prod.Id, Quantidade = 30, PrecoUnitario = 20m }]
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync();

        // Lead Time = 7 dias, Cobertura = 15 dias, Histórico = 30 dias
        // Consumo médio diário = 30 / 30 = 1.00 un/dia
        // Ponto de Pedido = (1.00 * 7) + 5 = 12 unidades.
        // Como o estoque atual é 10 (menor que o ponto de pedido 12), status deve ser "COMPRAR"
        var resultado = await _service.CalcularSugestaoComprasAsync(30, 7, 15);

        var item = resultado.Itens.First(i => i.ProdutoId == prod.Id);

        Assert.Equal(1.00m, item.ConsumoMedioDiario);
        Assert.Equal(12, item.PontoDePedido);
        Assert.Equal("COMPRAR", item.StatusReposicao);
        Assert.True(item.QuantidadeSugerida > 0);
    }

    [Fact]
    public async Task Deve_Identificar_Ruptura_Critica_Quando_Estoque_Zerado()
    {
        var prod = new Produto 
        { 
            Nome = "Açúcar 1kg", 
            Preco = 5m, 
            CustoUltimaCompra = 3m, 
            Estoque = 0, 
            EstoqueMinimo = 10 
        };
        _db.Produtos.Add(prod);
        await _db.SaveChangesAsync();

        var resultado = await _service.CalcularSugestaoComprasAsync(30, 7, 15);

        var item = resultado.Itens.First(i => i.ProdutoId == prod.Id);
        Assert.Equal("RUPTURA_CRITICA", item.StatusReposicao);
        Assert.Equal(10, item.QuantidadeSugerida); // Recompõe ao menos o estoque mínimo
        Assert.Equal(1, resultado.TotalItensEmRuptura);
    }

    [Fact]
    public async Task Produto_Com_Estoque_Abundante_Deve_Ficar_Estavel_Com_Qtd_Zero()
    {
        var prod = new Produto 
        { 
            Nome = "Farinha 1kg", 
            Preco = 4m, 
            CustoUltimaCompra = 2m, 
            Estoque = 100, 
            EstoqueMinimo = 5 
        };
        _db.Produtos.Add(prod);
        await _db.SaveChangesAsync();

        var resultado = await _service.CalcularSugestaoComprasAsync(30, 7, 15);

        var item = resultado.Itens.First(i => i.ProdutoId == prod.Id);
        Assert.Equal("ESTAVEL", item.StatusReposicao);
        Assert.Equal(0, item.QuantidadeSugerida);
        Assert.Equal(0m, item.CustoTotalEstimado);
    }

    [Fact]
    public void Folha_De_Cotacao_Deve_Gerar_Texto_Com_Itens_E_Campos_De_Preco()
    {
        var itens = new List<ItemSugestaoCompraDto>
        {
            new() 
            { 
                ProdutoId = 1, 
                ProdutoNome = "Óleo de Soja 900ml", 
                CodigoBarras = "789123456", 
                UnidadeMedida = "UN", 
                QuantidadeSugerida = 24 
            },
            new() 
            { 
                ProdutoId = 2, 
                ProdutoNome = "Sal Refinado 1kg", 
                CodigoBarras = "789654321", 
                UnidadeMedida = "UN", 
                QuantidadeSugerida = 0 // Não deve aparecer na cotação
            }
        };

        var texto = _service.GerarTextoCotacaoFornecedor(itens, "Distribuidora Alvorada");

        Assert.Contains("SOLICITAÇÃO DE COTAÇÃO DE COMPRAS", texto);
        Assert.Contains("Distribuidora Alvorada", texto);
        Assert.Contains("Óleo de Soja 900ml", texto);
        Assert.Contains("24 UN", texto);
        Assert.Contains("[ R$ _____ ]", texto);

        // O item com quantidade zero não deve constar na folha
        Assert.DoesNotContain("Sal Refinado", texto);
    }
}
