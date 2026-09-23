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

public class ContasPagarTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public ContasPagarTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _db.Produtos.AddRange(
            new Produto { Id = 1, Nome = "Fardo Sacola 2k", Preco = 10.00m, Estoque = 0 },
            new Produto { Id = 2, Nome = "Caixa Copos 200ml", Preco = 50.00m, Estoque = 0 }
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
    public async Task Deve_Gerar_Contas_A_Pagar_Ao_Registrar_Entrada_Com_Parcelas()
    {
        // Arrange: 2 itens na NF totalizando R$ 300,00 divididos em 3 parcelas de R$ 100,00
        var itens = new List<(int ProdutoId, int Quantidade, decimal CustoUnitario)>
        {
            (1, 10, 10.00m), // 100
            (2, 4, 50.00m)   // 200
        };

        var parcelas = new List<(string NumeroParcela, DateTime Vencimento, decimal Valor)>
        {
            ("01/03", DateTime.Today.AddDays(30), 100.00m),
            ("02/03", DateTime.Today.AddDays(60), 100.00m),
            ("03/03", DateTime.Today.AddDays(90), 100.00m)
        };

        // Act: Registra entrada com financeiro
        var entrada = await _service.RegistrarEntradaMercadoriaAsync(
            "NF-9988",
            "Distribuidora PlastPack Ltda",
            "Entrada via XML com duplicatas",
            itens,
            parcelas,
            "12.345.678/0001-99");

        // Assert
        Assert.NotNull(entrada);
        Assert.Equal(300.00m, entrada.ValorTotal);

        var titulos = await _service.ObterContasPagarAsync();
        Assert.Equal(3, titulos.Count);
        Assert.All(titulos, t =>
        {
            Assert.Equal("PENDENTE", t.Status);
            Assert.Equal("Distribuidora PlastPack Ltda", t.FornecedorNome);
            Assert.Equal("12.345.678/0001-99", t.FornecedorCnpj);
            Assert.Equal("NF-9988", t.NumeroDocumento);
            Assert.Equal(100.00m, t.Valor);
            Assert.Equal(entrada.Id, t.EntradaMercadoriaId);
        });

        Assert.Equal(DateTime.Today.AddDays(30), titulos[0].DataVencimento);
        Assert.Equal(DateTime.Today.AddDays(60), titulos[1].DataVencimento);
        Assert.Equal(DateTime.Today.AddDays(90), titulos[2].DataVencimento);
    }

    [Fact]
    public async Task Deve_Liquidar_Conta_A_Pagar_Com_Sucesso()
    {
        // Arrange
        var conta = await _service.RegistrarContaPagarManualAsync(
            "Fornecedor Bobinas",
            "99.888.777/0001-11",
            "NF-100",
            "1/1",
            250.00m,
            DateTime.Today.AddDays(15));

        // Act: Baixa o título via PIX
        await _service.LiquidarContaPagarAsync(conta.Id, 250.00m, "PIX", "Pago com desconto");

        // Assert
        var atualizado = await _db.ContasPagar.FindAsync(conta.Id);
        Assert.NotNull(atualizado);
        Assert.Equal("PAGO", atualizado.Status);
        Assert.Equal(250.00m, atualizado.ValorPago);
        Assert.Equal("PIX", atualizado.FormaPagamento);
        Assert.NotNull(atualizado.DataPagamento);
        Assert.Contains("Pago com desconto", atualizado.Observacao);
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Liquidar_Conta_Ja_Paga()
    {
        // Arrange
        var conta = await _service.RegistrarContaPagarManualAsync("Fornecedor X", "", "NF-1", "1/1", 50m, DateTime.Today);
        await _service.LiquidarContaPagarAsync(conta.Id, 50m, "Boleto");

        // Act & Assert: segunda liquidação deve ser rejeitada
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.LiquidarContaPagarAsync(conta.Id, 50m, "Boleto")
        );

        Assert.Contains("já foi liquidado", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Nao_Deve_Permitir_Liquidar_Conta_Com_Valor_Invalido(decimal valorInvalido)
    {
        var conta = await _service.RegistrarContaPagarManualAsync("Fornecedor Y", "", "NF-2", "1/1", 100m, DateTime.Today);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.LiquidarContaPagarAsync(conta.Id, valorInvalido, "Dinheiro")
        );
    }

    [Fact]
    public async Task Deve_Calcular_Resumo_Financeiro_Com_Titulos_Vencidos_E_Pendentes()
    {
        // 1. Título Vencido (venceu há 5 dias) = R$ 150
        var vctoPassado = DateTime.Today.AddDays(-5);
        await _service.RegistrarContaPagarManualAsync("Forn 1", "", "NF-01", "1/1", 150m, vctoPassado);

        // 2. Título Pendente no Prazo (vence daqui a 10 dias) = R$ 200
        var vctoFuturo = DateTime.Today.AddDays(10);
        await _service.RegistrarContaPagarManualAsync("Forn 2", "", "NF-02", "1/1", 200m, vctoFuturo);

        // 3. Título Liquidado no mês = R$ 300
        var pago = await _service.RegistrarContaPagarManualAsync("Forn 3", "", "NF-03", "1/1", 300m, DateTime.Today);
        await _service.LiquidarContaPagarAsync(pago.Id, 300m, "Transferência");

        // Act: Obtém resumo financeiro
        var resumo = await _service.ObterResumoFinanceiroContasPagarAsync();

        // Assert:
        // Total pendente: 150 (vencido) + 200 (a vencer) = 350
        Assert.Equal(350.00m, resumo.TotalPendente);
        Assert.Equal(2, resumo.QuantidadePendentes);

        // Total vencido: 150
        Assert.Equal(150.00m, resumo.TotalVencido);
        Assert.Equal(1, resumo.QuantidadeVencidos);

        // Total pago no mês: 300
        Assert.Equal(300.00m, resumo.TotalPagoNoMes);
    }

    [Fact]
    public async Task Deve_Filtrar_Contas_Por_Status_Vencidos_E_Pagos()
    {
        // 1 vencido
        await _service.RegistrarContaPagarManualAsync("Forn 1", "", "DOC-1", "1/1", 100m, DateTime.Today.AddDays(-2));
        // 1 a vencer
        await _service.RegistrarContaPagarManualAsync("Forn 2", "", "DOC-2", "1/1", 200m, DateTime.Today.AddDays(5));
        // 1 pago
        var pago = await _service.RegistrarContaPagarManualAsync("Forn 3", "", "DOC-3", "1/1", 300m, DateTime.Today);
        await _service.LiquidarContaPagarAsync(pago.Id, 300m, "PIX");

        // Filtro VENCIDOS
        var vencidos = await _service.ObterContasPagarAsync(statusFiltro: "VENCIDOS");
        Assert.Single(vencidos);
        Assert.Equal("DOC-1", vencidos[0].NumeroDocumento);

        // Filtro PAGO
        var pagos = await _service.ObterContasPagarAsync(statusFiltro: "PAGO");
        Assert.Single(pagos);
        Assert.Equal("DOC-3", pagos[0].NumeroDocumento);

        // Filtro PENDENTE
        var pendentes = await _service.ObterContasPagarAsync(statusFiltro: "PENDENTE");
        Assert.Equal(2, pendentes.Count);
    }

    [Fact]
    public async Task Deve_Cancelar_Titulo_Pendente_E_Impedir_Cancelamento_De_Pago()
    {
        var pendente = await _service.RegistrarContaPagarManualAsync("Forn Cancel", "", "DOC-99", "1/1", 80m, DateTime.Today.AddDays(3));
        await _service.CancelarContaPagarAsync(pendente.Id, "Nota cancelada pelo fornecedor");

        var canceladoDb = await _db.ContasPagar.FindAsync(pendente.Id);
        Assert.Equal("CANCELADO", canceladoDb!.Status);
        Assert.Contains("Nota cancelada pelo fornecedor", canceladoDb.Observacao);

        // Tentar liquidar cancelado deve falhar
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.LiquidarContaPagarAsync(pendente.Id, 80m, "Dinheiro")
        );

        // Título já pago não pode ser cancelado
        var pago = await _service.RegistrarContaPagarManualAsync("Forn Pago", "", "DOC-88", "1/1", 100m, DateTime.Today);
        await _service.LiquidarContaPagarAsync(pago.Id, 100m, "PIX");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelarContaPagarAsync(pago.Id, "Motivo")
        );
    }
}
