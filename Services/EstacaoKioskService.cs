using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using GetStartedApp.Models;

namespace GetStartedApp.Services;

public class EstacaoKioskService
{
    private readonly string _caminhoArquivo;
    private readonly ILogger<EstacaoKioskService> _logger;
    private ConfiguracaoEstacao _config;

    public ConfiguracaoEstacao Configuracao => _config;

    public string ModoAtual => _config.ModoEstacao.ToUpperInvariant();
    public string NomeTerminal => _config.NomeTerminal;
    public bool IsModoRestrito => ModoAtual == "BALCAO" || ModoAtual == "CAIXA";

    public EstacaoKioskService(ILogger<EstacaoKioskService> logger, string? caminhoArquivo = null)
    {
        _logger = logger;
        _caminhoArquivo = caminhoArquivo ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "estacao_kiosk.json");
        _config = CarregarOuCriarPadrao();
    }

    private ConfiguracaoEstacao CarregarOuCriarPadrao()
    {
        try
        {
            if (File.Exists(_caminhoArquivo))
            {
                var json = File.ReadAllText(_caminhoArquivo);
                var config = JsonSerializer.Deserialize<ConfiguracaoEstacao>(json);
                if (config != null) return config;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar arquivo de estação kiosk ({Caminho}). Usando padrão.", _caminhoArquivo);
        }

        var padrao = new ConfiguracaoEstacao();
        SalvarConfiguracao(padrao);
        return padrao;
    }

    public void SalvarConfiguracao(ConfiguracaoEstacao novaConfig)
    {
        try
        {
            _config = novaConfig;
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_caminhoArquivo, json);
            _logger.LogInformation("Configuração de Estação Kiosk salva. Modo: {Modo}, Terminal: {Nome}",
                _config.ModoEstacao, _config.NomeTerminal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar arquivo de estação kiosk ({Caminho})", _caminhoArquivo);
            throw;
        }
    }

    public bool ValidarSenhaMaster(string? senhaDigitada)
    {
        if (string.IsNullOrWhiteSpace(senhaDigitada)) return false;
        return string.Equals(_config.SenhaMaster.Trim(), senhaDigitada.Trim(), StringComparison.Ordinal);
    }

    public bool PodeAcessarModulo(string modulo, string? senhaMaster = null)
    {
        if (ModoAtual == "GERENCIAL") return true;

        if (ValidarSenhaMaster(senhaMaster)) return true;

        if (ModoAtual == "BALCAO")
        {
            return modulo.Equals("BALCAO", StringComparison.OrdinalIgnoreCase);
        }

        if (ModoAtual == "CAIXA")
        {
            return modulo.Equals("PDV", StringComparison.OrdinalIgnoreCase) ||
                   modulo.Equals("CAIXA", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
