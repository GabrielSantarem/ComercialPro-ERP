using System.Threading;
using System.Threading.Tasks;

namespace GetStartedApp.Services.Hardware;

public class BalancaMockService : IBalancaCheckoutService
{
    public decimal PesoConfigurado { get; set; } = 1.250m;
    public bool IsConectada => true;

    public Task<decimal> LerPesoAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PesoConfigurado);
    }
}
