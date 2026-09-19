using System;
using System.IO;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services.Clientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GetStartedApp.Tests;

public class ClienteCrediarioTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;
    private readonly ClienteService _service;

    public ClienteCrediarioTests()
    {
        _dbName = $"test_cliente_{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _service = new ClienteService(_db, NullLogger<ClienteService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbName)) File.Delete(_dbName);
    }

    [Fact]
    public async Task SalvarCliente_ComCpfInvalido_DeveLancarExcecaoValidacao()
    {
        // Arrange: CPF com dígitos inválidos (todos iguais ou cálculo falso)
        var clienteInvalido = new Cliente
        {
            Nome = "Cliente Falso",
            CpfCnpj = "111.111.111-11",
            LimiteCredito = 500m
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SalvarClienteAsync(clienteInvalido));
    }

    [Fact]
    public async Task SalvarCliente_DuplicidadeCpf_DeveBloquearCadastro()
    {
        // Arrange: CPF Válido da Receita Federal (ex: algoritmo gera um válido para teste)
        // Usaremos um CPF matematicamente válido: 000.000.000-00 é repetido, mas um válido é por exemplo: 52998224725
        var cpfValido = "52998224725";

        var cliente1 = new Cliente
        {
            Nome = "Primeiro Cliente",
            CpfCnpj = cpfValido,
            LimiteCredito = 300m
        };
        await _service.SalvarClienteAsync(cliente1);

        // Act & Assert: Segundo cliente com mesmo CPF
        var cliente2 = new Cliente
        {
            Nome = "Segundo Cliente",
            CpfCnpj = cpfValido,
            LimiteCredito = 500m
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SalvarClienteAsync(cliente2));
    }

    [Fact]
    public async Task AvaliarCredito_ComValorAbaixoDoLimiteESemAtrasos_DeveAutorizarCompra()
    {
        // Arrange
        var cliente = new Cliente
        {
            Nome = "Cliente Bom Pagador",
            LimiteCredito = 500m
        };
        await _service.SalvarClienteAsync(cliente);

        // Act: Compra de R$ 200 (limite 500)
        var status = await _service.AvaliarCreditoAsync(cliente.Id, 200m);

        // Assert
        Assert.True(status.AptoParaCrediario);
        Assert.Null(status.MotivoRestricao);
        Assert.Equal(500m, status.LimiteTotal);
        Assert.Equal(500m, status.LimiteDisponivel);
        Assert.Equal(0m, status.SaldoDevedor);
    }

    [Fact]
    public async Task AvaliarCredito_ComValorExcedendoLimiteDisponivel_DeveRecusarOperacao()
    {
        // Arrange
        var cliente = new Cliente
        {
            Nome = "Cliente Limite Baixo",
            LimiteCredito = 300m
        };
        await _service.SalvarClienteAsync(cliente);

        // Adicionar conta a receber pendente de R$ 200 (sobrando R$ 100 de limite)
        _db.ContasReceber.Add(new ContaReceber
        {
            ClienteId = cliente.Id,
            ValorOriginal = 200m,
            DataEmissao = DateTime.Today,
            DataVencimento = DateTime.Today.AddDays(15),
            Status = "PENDENTE"
        });
        await _db.SaveChangesAsync();

        // Act: Tentativa de compra de R$ 150 (disponível é 100)
        var status = await _service.AvaliarCreditoAsync(cliente.Id, 150m);

        // Assert
        Assert.False(status.AptoParaCrediario);
        Assert.Contains("Limite de crédito insuficiente", status.MotivoRestricao);
        Assert.Equal(100m, status.LimiteDisponivel);
        Assert.Equal(200m, status.SaldoDevedor);
    }

    [Fact]
    public async Task AvaliarCredito_ComTitulosVencidosHaMaisDe5Dias_DeveBloquearPorInadimplencia()
    {
        // Arrange: Limite alto (1000m), mas possui 1 título vencido há 8 dias
        var cliente = new Cliente
        {
            Nome = "Cliente Inadimplente",
            LimiteCredito = 1000m
        };
        await _service.SalvarClienteAsync(cliente);

        _db.ContasReceber.Add(new ContaReceber
        {
            ClienteId = cliente.Id,
            ValorOriginal = 150m,
            DataEmissao = DateTime.Today.AddDays(-20),
            DataVencimento = DateTime.Today.AddDays(-8), // Vencido há 8 dias (> 5 dias)
            Status = "PENDENTE"
        });
        await _db.SaveChangesAsync();

        // Act: Tentativa de compra de apenas R$ 50
        var status = await _service.AvaliarCreditoAsync(cliente.Id, 50m);

        // Assert
        Assert.False(status.AptoParaCrediario);
        Assert.Contains("Inadimplência", status.MotivoRestricao);
        Assert.True(status.QuantidadeTitulosVencidos >= 1);
    }

    [Fact]
    public async Task AvaliarCredito_ClienteBloqueadoManualmente_DeveRecusarOperacao()
    {
        // Arrange
        var cliente = new Cliente
        {
            Nome = "Cliente Suspenso",
            LimiteCredito = 1000m,
            BloqueadoManualmente = true,
            MotivoBloqueio = "Suspeita de fraude em documentos"
        };
        await _service.SalvarClienteAsync(cliente);

        // Act
        var status = await _service.AvaliarCreditoAsync(cliente.Id, 50m);

        // Assert
        Assert.False(status.AptoParaCrediario);
        Assert.Contains("Cliente bloqueado administrativamente", status.MotivoRestricao);
    }
}
