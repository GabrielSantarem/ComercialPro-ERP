using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GetStartedApp.Services.Inteligencia;

public record ItemProdutoSemGiroDto(
    int ProdutoId,
    string Nome,
    string CodigoBarras,
    int SaldoEstoque,
    decimal CustoUnitario,
    decimal CapitalParadoTotal,
    DateTime? DataUltimaVenda,
    int DiasSemGiro);

public record ComissaoVendedorDto(
    int VendedorId,
    string VendedorNome,
    decimal PercentualComissao,
    int TotalVendas,
    decimal FaturamentoTotal,
    decimal ValorComissaoTotal);

public interface IInteligenciaComercialService
{
    Task<List<ItemProdutoSemGiroDto>> ObterProdutosSemGiroAsync(int diasMinimosSemVenda = 60);
    Task<List<ComissaoVendedorDto>> CalcularComissoesAsync(DateTime inicio, DateTime fim);
}
