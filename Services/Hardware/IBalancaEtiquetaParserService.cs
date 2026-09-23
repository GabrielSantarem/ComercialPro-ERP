namespace GetStartedApp.Services.Hardware;

public enum ModoCodigoBalanca
{
    ValorTotal, // Os dígitos representam o valor a pagar (R$)
    PesoLiquido // Os dígitos representam o peso em gramas/kg
}

public record ResultadoParserBalanca(
    bool IsCodigoBalanca,
    string CodigoProduto,
    decimal QuantidadeOuPeso,
    decimal ValorTotalCalculado,
    string MensagemErro = "");

public interface IBalancaEtiquetaParserService
{
    ResultadoParserBalanca DecodificarCodigo(
        string codigoBarras, 
        decimal precoUnitarioCadastro, 
        ModoCodigoBalanca modo = ModoCodigoBalanca.ValorTotal,
        int tamanhoCodigoProduto = 4);
}
