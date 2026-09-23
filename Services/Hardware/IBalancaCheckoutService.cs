using System.Threading;
using System.Threading.Tasks;

namespace GetStartedApp.Services.Hardware;

public interface IBalancaCheckoutService
{
    Task<decimal> LerPesoAsync(CancellationToken cancellationToken = default);
    bool IsConectada { get; }
}
