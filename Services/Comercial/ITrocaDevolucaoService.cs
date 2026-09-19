using System.Collections.Generic;
using System.Threading.Tasks;
using GetStartedApp.Models;

namespace GetStartedApp.Services.Comercial;

public interface ITrocaDevolucaoService
{
    Task<ValeCredito> EmitirValeTrocaAsync(
        List<ItemDevolucaoDto> itensDevolvidos,
        string motivoGeral,
        string? clienteNome,
        string? clienteCpf,
        int? vendaOrigemId = null);

    Task<(bool Sucesso, string Mensagem, decimal ValorAbatido)> ResgatarValeCreditoAsync(
        string codigoVale,
        decimal valorNecessario,
        int vendaDestinoId);
        
    Task<ValeCredito?> ConsultarValeAsync(string codigoVale);
}
