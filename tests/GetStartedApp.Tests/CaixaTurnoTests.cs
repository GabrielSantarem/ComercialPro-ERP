using System;
using System.Collections.Generic;
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

public class CaixaTurnoTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public CaixaTurnoTests()
    {
        // Banco SQLite em memória para isolamento total dos testes
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        // Popula vendedor de teste
        _db.Vendedores.Add(new Vendedor { Id = 1, Nome = "Operador Teste" });
        _db.Produtos.AddRange(
            new Produto { Id = 1, Nome = "Sacola Branca 2k", Preco = 1.00m, Estoque = 100 },
            new Produto { Id = 2, Nome = "Copo Descartável", Preco = 5.00m, Estoque = 50 }
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
    public async Task Deve_Abrir_Caixa_Com_Saldo_Inicial_Correto()
    {
        // Arrange & Act
        var turno = await _service.AbrirCaixaAsync(vendedorId: 1, saldoInicial: 150.00m, "Abertura de teste");

        // Assert
        Assert.NotNull(turno);
        Assert.Equal("ABERTO", turno.Status);
        Assert.Equal(150.00m, turno.SaldoInicial);
        Assert.Equal(150.00m, turno.SaldoEsperadoEmDinheiro);
        Assert.Null(turno.DataFechamento);
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Abrir_Dois_Caixas_Simultaneamente()
    {
        // Arrange: abre o primeiro caixa
        await _service.AbrirCaixaAsync(1, 100.00m);

        // Act & Assert: tentar abrir o segundo deve lançar InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AbrirCaixaAsync(1, 200.00m)
        );

        Assert.Contains("Já existe um turno de caixa aberto", ex.Message);
    }

    [Fact]
    public async Task Deve_Registrar_Suprimento_E_Aumentar_Saldo_Esperado()
    {
        // Arrange
        var turno = await _service.AbrirCaixaAsync(1, 100.00m);

        // Act: adiciona R$ 50 de troco extra
        var suprimento = await _service.RegistrarSuprimentoAsync(turno.Id, 50.00m, "Moedas para troco");

        // Assert
        Assert.Equal("SUPRIMENTO", suprimento.Tipo);
        Assert.Equal(50.00m, suprimento.Valor);
        Assert.Equal(50.00m, turno.TotalSuprimentos);
        Assert.Equal(150.00m, turno.SaldoEsperadoEmDinheiro);
    }

    [Fact]
    public async Task Deve_Registrar_Sangria_E_Deduzir_Saldo_Esperado()
    {
        // Arrange: caixa com R$ 200 inicial
        var turno = await _service.AbrirCaixaAsync(1, 200.00m);

        // Act: retira R$ 80 para o cofre
        var sangria = await _service.RegistrarSangriaAsync(turno.Id, 80.00m, "Retirada de segurança p/ cofre");

        // Assert
        Assert.Equal("SANGRIA", sangria.Tipo);
        Assert.Equal(80.00m, sangria.Valor);
        Assert.Equal(80.00m, turno.TotalSangrias);
        Assert.Equal(120.00m, turno.SaldoEsperadoEmDinheiro); // 200 - 80 = 120
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Sangria_Maior_Que_Saldo_Disponivel_Em_Dinheiro()
    {
        // Arrange: caixa com apenas R$ 50
        var turno = await _service.AbrirCaixaAsync(1, 50.00m);

        // Act & Assert: tentar retirar R$ 100 deve ser barrado para evitar fraude/erro
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RegistrarSangriaAsync(turno.Id, 100.00m, "Retirada excessiva")
        );

        Assert.Contains("Sangria não permitida", ex.Message);
    }

    [Fact]
    public async Task Deve_Fechar_Caixa_E_Calcular_Quebra_Corretamente()
    {
        // Arrange: R$ 100 inicial + R$ 50 suprimento = R$ 150 esperado
        var turno = await _service.AbrirCaixaAsync(1, 100.00m);
        await _service.RegistrarSuprimentoAsync(turno.Id, 50.00m, "Troco");

        // Act: Fechamento cego - operador contou R$ 145 (faltaram R$ 5)
        var turnoFechado = await _service.FecharCaixaAsync(turno.Id, saldoInformado: 145.00m, "Conferência final");

        // Assert
        Assert.Equal("FECHADO", turnoFechado.Status);
        Assert.NotNull(turnoFechado.DataFechamento);
        Assert.Equal(145.00m, turnoFechado.SaldoInformado);
        Assert.Equal(-5.00m, turnoFechado.DiferencaQuebra); // Falta de R$ 5,00
    }

    [Fact]
    public async Task Deve_Acumular_Vendas_No_Turno_Aberto()
    {
        // Arrange: abre turno com R$ 100
        var turno = await _service.AbrirCaixaAsync(1, 100.00m);
        var produto = await _db.Produtos.FirstAsync(p => p.Id == 1); // R$ 1.00

        // Act: Realiza uma venda de 10 unidades em Dinheiro
        var carrinho = new List<(Produto Produto, int Quantidade)> { (produto, 10) };
        await _service.SalvarPedidoAsync(1, carrinho, "Dinheiro");

        // Assert
        Assert.Equal(10.00m, turno.TotalVendasDinheiro);
        Assert.Equal(110.00m, turno.SaldoEsperadoEmDinheiro); // 100 inicial + 10 venda = 110
    }

    [Theory]
    [InlineData("5*sacola", 5, "sacola")]
    [InlineData("12*copo descartavel", 12, "copo descartavel")]
    [InlineData("fita", 1, "fita")]
    [InlineData("3 * papel", 3, "papel")]
    public void Deve_Processar_Multiplicador_PDV(string input, int qtdEsperada, string termoEsperado)
    {
        // Testa o método estático ou a regra de parsing da busca rápida do PDV
        var (qtd, termo) = PdvViewModel.ProcessarMultiplicador(input);

        Assert.Equal(qtdEsperada, qtd);
        Assert.Equal(termoEsperado, termo);
    }
}
