using System.Threading.Tasks;

namespace GetStartedApp.Services.Hardware;

public interface IGavetaDinheiroService
{
    byte[] ObterComandoAberturaGaveta();
    Task<bool> AcionarAberturaAsync(string portaOuImpressora);
}
