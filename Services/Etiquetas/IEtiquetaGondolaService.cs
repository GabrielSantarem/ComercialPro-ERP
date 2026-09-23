using System.Collections.Generic;

namespace GetStartedApp.Services.Etiquetas;

public record ModeloFolhaEtiqueta(
    string Nome,
    int Colunas,
    int Linhas,
    float MargemSuperiorMm,
    float MargemLateralMm,
    float LarguraEtiquetaMm,
    float AlturaEtiquetaMm,
    float EspacamentoHorizontalMm,
    float EspacamentoVerticalMm);

public record ItemEtiquetaGondolaDto(
    int ProdutoId,
    string Nome,
    string CodigoBarras,
    string CodigoInterno,
    decimal PrecoVenda,
    string UnidadeMedida,
    decimal? PrecoPorQuiloOuLitro = null);

public interface IEtiquetaGondolaService
{
    byte[] GerarFolhaEtiquetasPdf(
        List<ItemEtiquetaGondolaDto> produtos, 
        ModeloFolhaEtiqueta modelo);
        
    List<ModeloFolhaEtiqueta> ObterModelosSuportados();
}
