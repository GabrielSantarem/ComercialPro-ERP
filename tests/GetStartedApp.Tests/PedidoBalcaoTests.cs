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
using Xunit;

namespace GetStartedApp.Tests;

public class PedidoBalcaoTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public PedidoBalcaoTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _db.Vendedores.AddRange(
            new Vendedor { Id = 1, Nome = "Vendedor Balcão 1" },
            new Vendedor { Id = 2, Nome = "Vendedora Balcão 2" }
        );

        _db.Produtos.AddRange(
            new Produto { Id = 1, Nome = "Sacola 2k", Preco = 2.00m, Estoque = 100 },
            new Produto { Id = 2, Nome = "Fita Gomada", Preco = 10.00m, Estoque = 50 }
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
    public async Task Deve_Criar_Pedido_Balcao_Aguardando_Pagamento()
    {
        // Arrange
        var p1 = await _db.Produtos.FirstAsync(p => p.Id == 1);
        var itens = new List<(Produto, int)> { (p1, 3) }; // 3 x R$ 2 = R$ 6

        // Act: Vendedor 1 lança pré-venda para Seu Pedro
        var pedido = await _service.CriarPedidoBalcaoAsync(
            vendedorId: 1, 
            clienteNome: "Seu Pedro", 
            clienteCpf: "123.456.789-00", 
            itens);

        // Assert
        Assert.NotNull(pedido);
        Assert.Equal("AGUARDANDO_PAGAMENTO", pedido.Status);
        Assert.Equal("Seu Pedro", pedido.ClienteNome);
        Assert.Equal(6.00m, pedido.ValorTotal);
        Assert.Equal("PED-0001", pedido.NumeroComanda);
        Assert.Null(pedido.VendaId);
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Criar_Pedido_Balcao_Sem_Itens()
    {
        // Arrange
        var itensVazios = new List<(Produto, int)>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CriarPedidoBalcaoAsync(1, "Cliente Vazio", "", itensVazios)
        );
    }

    [Fact]
    public async Task Deve_Listar_Fila_De_Pedidos_FIFO()
    {
        // Arrange: cria 2 pedidos com intervalo
        var p1 = await _db.Produtos.FirstAsync(p => p.Id == 1);
        var p2 = await _db.Produtos.FirstAsync(p => p.Id == 2);

        var ped1 = await _service.CriarPedidoBalcaoAsync(1, "Cliente A", "", [(p1, 1)]);
        var ped2 = await _service.CriarPedidoBalcaoAsync(2, "Cliente B", "", [(p2, 1)]);

        // Act: Caixa puxa fila
        var fila = await _service.ObterPedidosAguardandoPagamentoAsync();

        // Assert: Cliente A deve ser o primeiro
        Assert.Equal(2, fila.Count);
        Assert.Equal(ped1.Id, fila[0].Id);
        Assert.Equal(ped2.Id, fila[1].Id);
    }

    [Fact]
    public async Task Deve_Faturar_Pedido_Balcao_No_Caixa_E_Baixar_Estoque_E_Alimentar_Gaveta()
    {
        // Arrange: 
        // 1. Abre turno no caixa com R$ 100
        var turno = await _service.AbrirCaixaAsync(1, 100.00m);

        // 2. Vendedor cria pedido de R$ 20 (2x Fita de R$ 10)
        var p2 = await _db.Produtos.FirstAsync(p => p.Id == 2);
        int estoqueInicial = p2.Estoque; // 50
        var pedido = await _service.CriarPedidoBalcaoAsync(2, "Dona Laura", "", [(p2, 2)]);

        // Act: Operador do Caixa fatura o pedido em Dinheiro
        var venda = await _service.FaturarPedidoBalcaoNoCaixaAsync(pedido.Id, "Dinheiro");

        // Assert
        Assert.NotNull(venda);
        Assert.Equal(20.00m, venda.ValorTotal);

        // Pedido agora deve estar FATURADO
        var pedidoDb = await _db.PedidosBalcao.FindAsync(pedido.Id);
        Assert.Equal("FATURADO", pedidoDb!.Status);
        Assert.Equal(venda.Id, pedidoDb.VendaId);

        // Estoque baixado de 50 para 48
        var produtoDb = await _db.Produtos.FindAsync(p2.Id);
        Assert.Equal(estoqueInicial - 2, produtoDb!.Estoque);

        // Gaveta do caixa recebeu os R$ 20: 100 inicial + 20 venda = 120
        Assert.Equal(20.00m, turno.TotalVendasDinheiro);
        Assert.Equal(120.00m, turno.SaldoEsperadoEmDinheiro);
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Faturar_Pedido_Ja_Faturado()
    {
        // Arrange
        var p1 = await _db.Produtos.FirstAsync(p => p.Id == 1);
        var pedido = await _service.CriarPedidoBalcaoAsync(1, "Cliente Duplo", "", [(p1, 1)]);
        await _service.FaturarPedidoBalcaoNoCaixaAsync(pedido.Id, "PIX");

        // Act & Assert: tentar faturar novamente deve dar erro
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.FaturarPedidoBalcaoNoCaixaAsync(pedido.Id, "Dinheiro")
        );

        Assert.Contains("não está aguardando pagamento", ex.Message);
    }
}
