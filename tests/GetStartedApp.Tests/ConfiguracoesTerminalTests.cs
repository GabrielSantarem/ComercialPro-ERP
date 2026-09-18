using System;
using System.IO;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GetStartedApp.Tests;

public class ConfiguracoesTerminalTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;

    public ConfiguracoesTerminalTests()
    {
        _dbName = $"test_config_{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbName)) File.Delete(_dbName);
    }

    [Fact]
    public async Task ConfiguracoesViewModel_DeveCarregarValoresPadrao_DaBaseDeDados()
    {
        // Arrange
        var service = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new ConfiguracoesViewModel(service);

        // Act
        await vm.CarregarDadosIniciaisAsync();

        // Assert
        Assert.Equal("Terminal Caixa 01", vm.NomeEstacao);
        Assert.Equal("EPSON TM-T20X", vm.ModeloImpressora);
        Assert.Equal("80mm", vm.LarguraBobina);
        Assert.Equal("USB / Spooler", vm.PortaComunicacao);
        Assert.True(vm.CortarPapelAutomatico);
    }

    [Fact]
    public async Task ConfiguracoesViewModel_SalvarConfiguracoesHardware_DevePersistirNoBanco()
    {
        // Arrange
        var service = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new ConfiguracoesViewModel(service);
        await vm.CarregarDadosIniciaisAsync();

        // Act: Modifica configurações
        vm.NomeEstacao = "Caixa Balcão Rápido";
        vm.ModeloImpressora = "ELGIN i9";
        vm.LarguraBobina = "58mm";
        vm.PortaComunicacao = "COM2";
        vm.CortarPapelAutomatico = false;
        vm.ModoImpressaoPadrao = "DANFE Simplificado A4";
        vm.IntegracaoBalancaHabilitada = true;
        vm.ModeloBalanca = "Filizola Platina";

        await vm.SalvarConfiguracoesHardwareAsync();

        // Assert: Verifica persistência no SQLite
        var salvo = await _db.ConfiguracoesTerminal.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(salvo);
        Assert.Equal("Caixa Balcão Rápido", salvo.NomeEstacao);
        Assert.Equal("ELGIN i9", salvo.ModeloImpressora);
        Assert.Equal("58mm", salvo.LarguraBobina);
        Assert.Equal("COM2", salvo.PortaComunicacao);
        Assert.False(salvo.CortarPapelAutomatico);
        Assert.Equal("DANFE Simplificado A4", salvo.ModoImpressaoPadrao);
        Assert.True(salvo.IntegracaoBalancaHabilitada);
        Assert.Equal("Filizola Platina", salvo.ModeloBalanca);
    }

    [Fact]
    public void ConfiguracoesViewModel_TestarImpressaoCupom_DeveGerarCupomTesteEExibirModal()
    {
        // Arrange
        var service = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new ConfiguracoesViewModel(service);
        vm.ModeloImpressora = "BEMATECH MP-4200";
        vm.LarguraBobina = "80mm";

        // Act: Dispara simulação de impressão de teste
        vm.TestarImpressaoCupomCommand.Execute(null);

        // Assert
        Assert.True(vm.ModalCupomTesteVisivel);
        Assert.NotEmpty(vm.UltimoCupomTeste);
        Assert.Contains("BEMATECH MP-4200", vm.UltimoCupomTeste);
        Assert.Contains("Item de Teste", vm.UltimoCupomTeste);

        // Act: Fecha modal
        vm.FecharModalCupomTesteCommand.Execute(null);
        Assert.False(vm.ModalCupomTesteVisivel);
    }
}
