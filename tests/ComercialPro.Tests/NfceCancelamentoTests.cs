using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Models.Fiscal;
using GetStartedApp.Services.Fiscal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GetStartedApp.Tests;

public class NfceCancelamentoTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;
    private readonly NfceCancelamentoService _service;
    private readonly int _vendedorId;

    public NfceCancelamentoTests()
    {
        _dbName = $"test_nfce_canc_{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var vendedor = new Vendedor { Nome = "Operador Padrão" };
        _db.Vendedores.Add(vendedor);
        _db.SaveChanges();
        _vendedorId = vendedor.Id;

        _service = new NfceCancelamentoService(_db, NullLogger<NfceCancelamentoService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbName)) File.Delete(_dbName);
    }

    [Fact]
    public async Task JustificativaMenorQue15Caracteres_DeveRetornarErroSefazSemAlterarDados()
    {
        // Arrange
        var prod = new Produto { Nome = "Item Teste", Preco = 10m, Estoque = 5 };
        _db.Produtos.Add(prod);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var venda = new Venda
        {
            VendedorId = _vendedorId,
            DataHora = DateTime.Now,
            ValorTotal = 10m,
            FormaPagamento = "Dinheiro",
            Status = "CONCLUIDA",
            Itens = new List<ItemVenda> { new ItemVenda { ProdutoId = prod.Id, Quantidade = 1, PrecoUnitario = 10m } }
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act (Justificativa com apenas 10 caracteres)
        var resultado = await _service.CancelarVendaAsync(venda.Id, "Erro no cx", "1234");

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.Contains("15 caracteres", resultado.Mensagem);

        var vendaDb = await _db.Vendas.FindAsync(new object[] { venda.Id }, TestContext.Current.CancellationToken);
        Assert.Equal("CONCLUIDA", vendaDb!.Status);

        var prodDb = await _db.Produtos.FindAsync(new object[] { prod.Id }, TestContext.Current.CancellationToken);
        Assert.Equal(5, prodDb!.Estoque);
    }

    [Fact]
    public async Task SenhaSupervisorIncorreta_DeveRecusarCancelamento()
    {
        // Arrange
        var venda = new Venda
        {
            VendedorId = _vendedorId,
            DataHora = DateTime.Now,
            ValorTotal = 50m,
            FormaPagamento = "PIX",
            Status = "CONCLUIDA"
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var resultado = await _service.CancelarVendaAsync(venda.Id, "Cliente desistiu da compra antes da entrega", "senha_errada");

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.Contains("supervisor", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);

        var vendaDb = await _db.Vendas.FindAsync(new object[] { venda.Id }, TestContext.Current.CancellationToken);
        Assert.Equal("CONCLUIDA", vendaDb!.Status);
    }

    [Fact]
    public async Task Cancelamento_DentroDoPrazo_ComSenhaCorreta_DeveEstornarEstoqueEAtualizarStatus()
    {
        // Arrange
        var prod1 = new Produto { Nome = "Produto 1", Preco = 25m, Estoque = 10 };
        var prod2 = new Produto { Nome = "Produto 2", Preco = 15m, Estoque = 8 };
        _db.Produtos.AddRange(prod1, prod2);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var turno = new CaixaTurno
        {
            VendedorId = _vendedorId,
            DataAbertura = DateTime.Now.AddHours(-1),
            SaldoInicial = 100m,
            TotalVendasDinheiro = 65m,
            Status = "ABERTO"
        };
        _db.CaixasTurno.Add(turno);

        var venda = new Venda
        {
            VendedorId = _vendedorId,
            DataHora = DateTime.Now.AddMinutes(-5),
            ValorTotal = 65m,
            FormaPagamento = "Dinheiro",
            Status = "CONCLUIDA",
            ChaveAcessoNfce = "35260912345678000195650010000000011234567801",
            ProtocoloAutorizacaoNfce = "135260000001",
            Itens = new List<ItemVenda>
            {
                new ItemVenda { ProdutoId = prod1.Id, Quantidade = 2, PrecoUnitario = 25m }, // Devolve +2
                new ItemVenda { ProdutoId = prod2.Id, Quantidade = 1, PrecoUnitario = 15m }  // Devolve +1
            }
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var resultado = await _service.CancelarVendaAsync(venda.Id, "Cliente desistiu integralmente dos itens comprados", "1234");

        // Assert
        Assert.True(resultado.Sucesso);
        Assert.Equal(135, resultado.CodigoStatusSefaz);
        Assert.NotNull(resultado.XmlEventoAssinado);
        Assert.Contains("procEventoNFe", resultado.XmlEventoAssinado);

        // Verifica status da venda
        var vendaDb = await _db.Vendas.FindAsync(new object[] { venda.Id }, TestContext.Current.CancellationToken);
        Assert.Equal("CANCELADA", vendaDb!.Status);
        Assert.NotNull(vendaDb.DataHoraCancelamento);
        Assert.NotNull(vendaDb.ProtocoloCancelamento);
        Assert.Equal("Cliente desistiu integralmente dos itens comprados", vendaDb.JustificativaCancelamento);

        // Verifica estoque estornado
        var prod1Db = await _db.Produtos.FindAsync(new object[] { prod1.Id }, TestContext.Current.CancellationToken);
        Assert.Equal(12, prod1Db!.Estoque); // 10 + 2

        var prod2Db = await _db.Produtos.FindAsync(new object[] { prod2.Id }, TestContext.Current.CancellationToken);
        Assert.Equal(9, prod2Db!.Estoque); // 8 + 1

        // Verifica registro de auditoria de estoque (AjusteEstoque)
        var ajustes = await _db.AjustesEstoque.Where(a => a.Motivo.Contains($"#{venda.Id}")).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, ajustes.Count);
        Assert.All(ajustes, a => Assert.Equal("ENTRADA_AVULSA", a.TipoAjuste));

        // Verifica estorno no caixa do turno
        var turnoDb = await _db.CaixasTurno.FindAsync(new object[] { turno.Id }, TestContext.Current.CancellationToken);
        Assert.Equal(0m, turnoDb!.TotalVendasDinheiro); // 65 - 65 = 0
    }

    [Fact]
    public async Task Cancelamento_Apos30Minutos_DeveFalharComRegraSefaz220()
    {
        // Arrange
        var venda = new Venda
        {
            VendedorId = _vendedorId,
            DataHora = DateTime.Now.AddMinutes(-35), // 35 min atrás (> 30 min)
            ValorTotal = 80m,
            FormaPagamento = "Cartão de Crédito",
            Status = "CONCLUIDA"
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var resultado = await _service.CancelarVendaAsync(venda.Id, "Cancelamento solicitado fora do prazo regulamentar", "admin");

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.Equal(220, resultado.CodigoStatusSefaz);
        Assert.Contains("expirado", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);

        var vendaDb = await _db.Vendas.FindAsync(new object[] { venda.Id }, TestContext.Current.CancellationToken);
        Assert.Equal("CONCLUIDA", vendaDb!.Status);
    }

    [Fact]
    public async Task CancelarVenda_JaCancelada_DeveFalharComAviso()
    {
        // Arrange
        var venda = new Venda
        {
            VendedorId = _vendedorId,
            DataHora = DateTime.Now.AddMinutes(-10),
            ValorTotal = 40m,
            Status = "CANCELADA"
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var resultado = await _service.CancelarVendaAsync(venda.Id, "Tentativa de cancelamento duplo de cupom", "1234");

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.Contains("já se encontra com status CANCELADA", resultado.Mensagem);
    }

    [Fact]
    public async Task SimularRejeicaoSefaz_NaoDeveAlterarEstoqueNemStatusVenda()
    {
        // Arrange
        var prod = new Produto { Nome = "Produto Teste Rejeicao", Preco = 30m, Estoque = 15 };
        _db.Produtos.Add(prod);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var venda = new Venda
        {
            VendedorId = _vendedorId,
            DataHora = DateTime.Now.AddMinutes(-5),
            ValorTotal = 30m,
            Status = "CONCLUIDA",
            Itens = new List<ItemVenda> { new ItemVenda { ProdutoId = prod.Id, Quantidade = 2, PrecoUnitario = 15m } }
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act (dispara trigger de simulação de rejeição)
        var resultado = await _service.CancelarVendaAsync(venda.Id, "Justificativa válida com [SIMULAR_REJEICAO_SEFAZ]", "1234");

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.Equal(220, resultado.CodigoStatusSefaz);

        var vendaDb = await _db.Vendas.FindAsync(new object[] { venda.Id }, TestContext.Current.CancellationToken);
        Assert.Equal("CONCLUIDA", vendaDb!.Status);

        var prodDb = await _db.Produtos.FindAsync(new object[] { prod.Id }, TestContext.Current.CancellationToken);
        Assert.Equal(15, prodDb!.Estoque); // Estoque mantido intacto

        var ajustes = await _db.AjustesEstoque.Where(a => a.ProdutoId == prod.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(ajustes);
    }

    [Fact]
    public async Task CancelarUltimaNfceAsync_SemVendas_DeveRetornarErroAmigavel()
    {
        // Act
        var resultado = await _service.CancelarUltimaNfceAsync("Justificativa com mais de 15 caracteres", "1234");

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.Contains("Nenhuma venda", resultado.Mensagem);
    }
}
