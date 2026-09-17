using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Xunit;

namespace GetStartedApp.Tests;

public class FinanceiroReceberTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public FinanceiroReceberTests()
    {
        _dbName = $"test_fin_receber_{Guid.NewGuid():N}.db";
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
    public async Task Deve_Registrar_Conta_A_Receber_Crediario_Com_Sucesso()
    {
        var titulo = await _service.RegistrarContaReceberManualAsync(
            clienteNome: "João da Silva",
            clienteCpfCnpj: "123.456.789-00",
            clienteTelefone: "(31) 99887-6655",
            numeroDocumento: "FAT-001",
            numeroParcela: "1/2",
            valor: 150.00m,
            dataVencimento: DateTime.Today.AddDays(15),
            observacao: "Venda no fiado balcão");

        Assert.NotNull(titulo);
        Assert.True(titulo.Id > 0);
        Assert.Equal("João da Silva", titulo.ClienteNome);
        Assert.Equal(150.00m, titulo.ValorOriginal);
        Assert.Equal(150.00m, titulo.ValorFinal);
        Assert.Equal("PENDENTE", titulo.Status);
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Conta_A_Receber_Com_Cliente_Vazio_Ou_Valor_Zero()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegistrarContaReceberManualAsync("", "123", "", "DOC", "1/1", 100m, DateTime.Today));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegistrarContaReceberManualAsync("Cliente", "123", "", "DOC", "1/1", 0m, DateTime.Today));
    }

    [Fact]
    public async Task Deve_Liquidar_Conta_A_Receber_Com_Juros_E_Forma_Pagamento()
    {
        var titulo = await _service.RegistrarContaReceberManualAsync(
            "Maria Oliveira", "999.888.777-66", "", "REC-10", "1/1", 200.00m, DateTime.Today.AddDays(-5));

        Assert.True(titulo.IsVencido);
        Assert.True(titulo.DiasAtraso >= 5);

        // Recebe com R$ 10 de juros por atraso
        await _service.LiquidarContaReceberAsync(
            titulo.Id,
            valorRecebido: 210.00m,
            juros: 10.00m,
            desconto: 0m,
            formaRecebimento: "PIX",
            observacao: "Recebido via chave aleatória");

        var atualizado = await _db.ContasReceber.FindAsync(titulo.Id);
        Assert.NotNull(atualizado);
        Assert.Equal("RECEBIDO", atualizado.Status);
        Assert.Equal(210.00m, atualizado.ValorRecebido);
        Assert.Equal(10.00m, atualizado.JurosMulta);
        Assert.Equal("PIX", atualizado.FormaRecebimento);
        Assert.NotNull(atualizado.DataRecebimento);
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Liquidar_Titulo_Ja_Recebido_Ou_Cancelado()
    {
        var titulo = await _service.RegistrarContaReceberManualAsync(
            "Carlos Eduardo", "", "", "DOC-20", "1/1", 50.00m, DateTime.Today);

        await _service.LiquidarContaReceberAsync(titulo.Id, 50.00m, formaRecebimento: "Dinheiro");

        // Segunda tentativa deve falhar
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.LiquidarContaReceberAsync(titulo.Id, 50.00m));
    }

    [Fact]
    public async Task Deve_Cancelar_Conta_A_Receber_Com_Motivo()
    {
        var titulo = await _service.RegistrarContaReceberManualAsync(
            "Cliente Devolucao", "", "", "DEV-01", "1/1", 80.00m, DateTime.Today);

        await _service.CancelarContaReceberAsync(titulo.Id, "Mercadoria devolvida integralmente");

        var atualizado = await _db.ContasReceber.FindAsync(titulo.Id);
        Assert.NotNull(atualizado);
        Assert.Equal("CANCELADO", atualizado.Status);
        Assert.Contains("Mercadoria devolvida", atualizado.Observacao);
    }

    [Fact]
    public async Task Deve_Calcular_Fluxo_De_Caixa_Consolidado_Pagar_Vs_Receber()
    {
        // 1. Título a Pagar: R$ 300,00 pendente
        await _service.RegistrarContaPagarManualAsync("Fornecedor Embalagens", "", "NF-1", "1/1", 300.00m, DateTime.Today.AddDays(10));

        // 2. Título a Receber: R$ 500,00 pendente
        await _service.RegistrarContaReceberManualAsync("Cliente Atacado", "", "", "FAT-10", "1/1", 500.00m, DateTime.Today.AddDays(5));

        // 3. Título já liquidado no mês: R$ 120,00 recebido
        var rec = await _service.RegistrarContaReceberManualAsync("Cliente Balcão", "", "", "FAT-11", "1/1", 120.00m, DateTime.Today);
        await _service.LiquidarContaReceberAsync(rec.Id, 120.00m, formaRecebimento: "PIX");

        var fluxo = await _service.ObterResumoFluxoCaixaAsync();

        Assert.NotNull(fluxo);
        Assert.Equal(300.00m, fluxo.TotalPagarPendente);
        Assert.Equal(500.00m, fluxo.TotalReceberPendente);
        Assert.Equal(200.00m, fluxo.SaldoProjetado); // 500 - 300
        Assert.Equal(120.00m, fluxo.TotalRecebidoMes);
    }
}
