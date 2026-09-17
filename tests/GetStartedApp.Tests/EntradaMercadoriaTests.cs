using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.ViewModels;
using Xunit;

namespace GetStartedApp.Tests;

public class EntradaMercadoriaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public EntradaMercadoriaTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        // Cadastra vendedor para turnos e vendas
        _db.Vendedores.Add(new Vendedor { Id = 1, Nome = "Operador de Estoque" });

        // Cadastra produtos com estoque inicial
        _db.Produtos.AddRange(
            new Produto { Id = 1, Nome = "Sacola Branca 2k", Preco = 2.50m, Estoque = 10 },
            new Produto { Id = 2, Nome = "Copo Descartável 200ml", Preco = 0.15m, Estoque = 50 }
        );
        _db.SaveChanges();

        _service = new PdvService(_db, NullLogger<PdvService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Deve_Registrar_Entrada_De_Mercadoria_E_Incrementar_Estoque()
    {
        // Arrange: 100 sacolas a R$ 1.20 cada
        var p1 = await _db.Produtos.FindAsync(1);
        int estoqueAntes = p1!.Estoque; // 10

        var itens = new List<(int ProdutoId, int Quantidade, decimal CustoUnitario)>
        {
            (1, 100, 1.20m)
        };

        // Act
        await _service.RegistrarEntradaMercadoriaAsync(
            numeroNota: "NF-99881",
            fornecedor: "Distribuidora de Embalagens",
            observacao: "Entrega via transportadora",
            itens: itens
        );

        // Assert: Estoque deve subir de 10 para 110
        var p1Atualizado = await _db.Produtos.FindAsync(1);
        Assert.Equal(110, p1Atualizado!.Estoque);

        // Verifica o histórico de entradas
        var historico = await _service.ObterHistoricoEntradasAsync();
        Assert.Single(historico);
        Assert.Equal("NF-99881", historico[0].NumeroNota);
        Assert.Equal(120.00m, historico[0].ValorTotal); // 100 * 1.20
        Assert.Single(historico[0].Itens);
        Assert.Equal(100, historico[0].Itens[0].QuantidadeEntrada);
    }

    [Fact]
    public void Deve_Calcular_Conversao_De_Unidade_Corretamente()
    {
        // Exemplo: Compra 2 caixas (QuantidadeFaturada = 2)
        // Cada caixa tem 500 unidades (FatorConversao = 500)
        // Total esperado no estoque = 1000 unidades
        var itemVm = new ItemNotaFiscalVm
        {
            QuantidadeFaturada = 2,
            FatorConversao = 500,
            PrecoUnitarioFaturado = 65.00m // R$ 65 por caixa
        };

        Assert.Equal(1000, itemVm.QuantidadeEstoque);
        Assert.Equal(130.00m, itemVm.TotalBruto); // 2 * 65 = 130
    }

    [Fact]
    public void Deve_Calcular_Custo_Real_Com_Rateio_De_Frete()
    {
        // Exemplo: Compra 1 fardo com 10 unidades a R$ 20 cada (Total Bruto = R$ 200)
        // Rateio de Frete/Despesas = R$ 20
        // Custo Real Total = R$ 220
        // Custo Unitário de Estoque = 220 / 10 = R$ 22,00 por unidade
        var itemVm = new ItemNotaFiscalVm
        {
            QuantidadeFaturada = 1,
            FatorConversao = 10,
            PrecoUnitarioFaturado = 200.00m,
            RateioDespesas = 20.00m
        };

        Assert.Equal(10, itemVm.QuantidadeEstoque);
        Assert.Equal(200.00m, itemVm.TotalBruto);
        Assert.Equal(220.00m, itemVm.CustoRealTotal);
        Assert.Equal(22.00m, itemVm.CustoUnitarioEstoque);
    }

    [Fact]
    public async Task Nao_Deve_Alterar_Estoque_Se_Lista_De_Itens_For_Vazia()
    {
        // Arrange
        var p1 = await _db.Produtos.FindAsync(1);
        int estoqueOriginal = p1!.Estoque;

        // Act: Envia lista vazia
        await _service.RegistrarEntradaMercadoriaAsync(
            numeroNota: "NF-VAZIA",
            fornecedor: "Nenhum",
            observacao: "",
            itens: []
        );

        // Assert: Estoque inalterado e nenhuma entrada registrada
        var p1Apos = await _db.Produtos.FindAsync(1);
        Assert.Equal(estoqueOriginal, p1Apos!.Estoque);

        var historico = await _service.ObterHistoricoEntradasAsync();
        Assert.Empty(historico);
    }

    [Fact]
    public async Task Deve_Gerar_Levantamento_De_Inventario_Correto()
    {
        // Arrange: Realiza vendas com o turno aberto
        await _service.AbrirCaixaAsync(1, 100m);
        var p1 = await _db.Produtos.FindAsync(1); // Sacola
        var p2 = await _db.Produtos.FindAsync(2); // Copo

        await _service.SalvarPedidoAsync(1, [(p1!, 3)], "Dinheiro");
        await _service.SalvarPedidoAsync(1, [(p2!, 10)], "PIX");

        // Act: Gera relatório do dia de hoje
        var relatorio = await _service.GerarLevantamentoInventarioAsync(DateTime.Today);

        // Assert
        Assert.NotNull(relatorio);
        Assert.Equal(2, relatorio.Count);

        // Produto mais vendido (Copo com 10) deve estar no topo
        var itemCopo = relatorio.First(r => r.ProdutoId == 2);
        Assert.Equal(10, itemCopo.QuantidadeVendida);

        var itemSacola = relatorio.First(r => r.ProdutoId == 1);
        Assert.Equal(3, itemSacola.QuantidadeVendida);
    }
}
