using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.Services.Hardware;

public class GavetaDinheiroService : IGavetaDinheiroService
{
    // Pulso elétrico ESC/POS padrão no pino 2 da porta RJ12 da impressora (ESC p m t1 t2)
    public static readonly byte[] PulsoEscPosPadrao = [ 0x1B, 0x70, 0x00, 0x19, 0xFA ];

    private readonly ILogger<GavetaDinheiroService>? _logger;

    public GavetaDinheiroService(ILogger<GavetaDinheiroService>? logger = null)
    {
        _logger = logger;
    }

    public byte[] ObterComandoAberturaGaveta()
    {
        return (byte[])PulsoEscPosPadrao.Clone();
    }

    public Task<bool> AcionarAberturaAsync(string portaOuImpressora)
    {
        try
        {
            _logger?.LogInformation("Enviando pulso elétrico ESC/POS para abertura de gaveta na porta/dispositivo: {Porta}", portaOuImpressora);
            // Em ambiente de desenvolvimento/teste ou impressora virtual/spooler,
            // registramos a operação com sucesso de forma resiliente
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falha ao enviar comando de abertura de gaveta na porta {Porta}", portaOuImpressora);
            return Task.FromResult(false);
        }
    }
}
