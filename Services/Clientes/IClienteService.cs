using System.Collections.Generic;
using System.Threading.Tasks;
using GetStartedApp.Models;

namespace GetStartedApp.Services.Clientes;

public interface IClienteService
{
    Task<List<Cliente>> PesquisarClientesAsync(string termo);
    Task<Cliente> SalvarClienteAsync(Cliente cliente);
    Task<StatusCreditoClienteDto> AvaliarCreditoAsync(int clienteId, decimal valorNovaCompra);
}
