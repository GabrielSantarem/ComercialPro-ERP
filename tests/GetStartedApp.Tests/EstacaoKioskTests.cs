using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Xunit;

namespace GetStartedApp.Tests;

public class EstacaoKioskTests : IDisposable
{
    private readonly string _tempConfigFile;

    public EstacaoKioskTests()
    {
        _tempConfigFile = Path.Combine(Path.GetTempPath(), $"kiosk_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempConfigFile))
        {
            try { File.Delete(_tempConfigFile); } catch { }
        }
    }

    [Fact]
    public void ConfiguracaoPadrao_DeveSerGerencialComSenhaAdmin()
    {
        var service = new EstacaoKioskService(NullLogger<EstacaoKioskService>.Instance, _tempConfigFile);

        Assert.Equal("GERENCIAL", service.ModoAtual);
        Assert.False(service.IsModoRestrito);
        Assert.True(service.ValidarSenhaMaster("admin"));
        Assert.True(service.PodeAcessarModulo("DASHBOARD"));
        Assert.True(service.PodeAcessarModulo("FINANCEIRO"));
        Assert.True(service.PodeAcessarModulo("ESTOQUE"));
    }

    [Fact]
    public void ModoBalcao_DevePermitirApenasBalcaoSemSenha_E_BloquearOutrosModulos()
    {
        var service = new EstacaoKioskService(NullLogger<EstacaoKioskService>.Instance, _tempConfigFile);
        service.SalvarConfiguracao(new ConfiguracaoEstacao
        {
            ModoEstacao = "BALCAO",
            NomeTerminal = "BALCAO-01",
            SenhaMaster = "1234"
        });

        Assert.Equal("BALCAO", service.ModoAtual);
        Assert.True(service.IsModoRestrito);

        // Balcão pode acessar BALCAO livremente
        Assert.True(service.PodeAcessarModulo("BALCAO"));

        // Bloqueado para módulos gerenciais sem senha
        Assert.False(service.PodeAcessarModulo("DASHBOARD"));
        Assert.False(service.PodeAcessarModulo("FINANCEIRO"));
        Assert.False(service.PodeAcessarModulo("ESTOQUE"));
        Assert.False(service.PodeAcessarModulo("PDV"));

        // Com senha master correta, deve liberar
        Assert.True(service.PodeAcessarModulo("DASHBOARD", "1234"));
        Assert.True(service.PodeAcessarModulo("FINANCEIRO", "1234"));
    }

    [Fact]
    public void ModoCaixa_DevePermitirApenasPdvSemSenha_E_BloquearGestao()
    {
        var service = new EstacaoKioskService(NullLogger<EstacaoKioskService>.Instance, _tempConfigFile);
        service.SalvarConfiguracao(new ConfiguracaoEstacao
        {
            ModoEstacao = "CAIXA",
            NomeTerminal = "CAIXA-01",
            SenhaMaster = "secret123"
        });

        Assert.Equal("CAIXA", service.ModoAtual);
        Assert.True(service.IsModoRestrito);

        // Caixa pode acessar PDV / CAIXA
        Assert.True(service.PodeAcessarModulo("PDV"));
        Assert.True(service.PodeAcessarModulo("CAIXA"));

        // Bloqueado para DRE, Estoque e Financeiro
        Assert.False(service.PodeAcessarModulo("DASHBOARD"));
        Assert.False(service.PodeAcessarModulo("FINANCEIRO"));

        // Senha incorreta deve rejeitar
        Assert.False(service.PodeAcessarModulo("FINANCEIRO", "senha_errada"));

        // Senha correta libera
        Assert.True(service.PodeAcessarModulo("FINANCEIRO", "secret123"));
    }

    [Fact]
    public void SalvarEAlterarModo_DevePersistirNoArquivoJson()
    {
        var service1 = new EstacaoKioskService(NullLogger<EstacaoKioskService>.Instance, _tempConfigFile);
        service1.SalvarConfiguracao(new ConfiguracaoEstacao
        {
            ModoEstacao = "BALCAO",
            NomeTerminal = "ATENDIMENTO-03",
            SenhaMaster = "gerente99"
        });

        // Nova instância recarregando do mesmo arquivo
        var service2 = new EstacaoKioskService(NullLogger<EstacaoKioskService>.Instance, _tempConfigFile);
        Assert.Equal("BALCAO", service2.ModoAtual);
        Assert.Equal("ATENDIMENTO-03", service2.NomeTerminal);
        Assert.True(service2.ValidarSenhaMaster("gerente99"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("errado")]
    public void ValidarSenhaMaster_ComEntradaInvalida_DeveRetornarFalse(string? senhaInvalida)
    {
        var service = new EstacaoKioskService(NullLogger<EstacaoKioskService>.Instance, _tempConfigFile);
        Assert.False(service.ValidarSenhaMaster(senhaInvalida));
    }
}
