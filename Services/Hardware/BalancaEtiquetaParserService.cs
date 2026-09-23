using System;
using System.Linq;

namespace GetStartedApp.Services.Hardware;

public class BalancaEtiquetaParserService : IBalancaEtiquetaParserService
{
    public ResultadoParserBalanca DecodificarCodigo(
        string codigoBarras, 
        decimal precoUnitarioCadastro, 
        ModoCodigoBalanca modo = ModoCodigoBalanca.ValorTotal, 
        int tamanhoCodigoProduto = 4)
    {
        if (string.IsNullOrWhiteSpace(codigoBarras))
        {
            return new ResultadoParserBalanca(false, string.Empty, 0m, 0m, "Código de barras vazio.");
        }

        var codigoLimpo = codigoBarras.Trim();

        // Código de balança EAN-13 deve ter 12 ou 13 dígitos numéricos
        if (codigoLimpo.Length != 12 && codigoLimpo.Length != 13)
        {
            return new ResultadoParserBalanca(false, string.Empty, 0m, 0m, "Tamanho de código de barras inválido para balança.");
        }

        if (!codigoLimpo.StartsWith('2'))
        {
            return new ResultadoParserBalanca(false, string.Empty, 0m, 0m, "Código não possui o prefixo 2 de produto pesável.");
        }

        if (!codigoLimpo.All(char.IsDigit))
        {
            return new ResultadoParserBalanca(false, string.Empty, 0m, 0m, "Código contém caracteres não numéricos.");
        }

        // Validação e normalização do tamanho do código interno do produto
        if (tamanhoCodigoProduto < 1 || tamanhoCodigoProduto > 6)
        {
            tamanhoCodigoProduto = 4;
        }

        var codigoProduto = codigoLimpo.Substring(1, tamanhoCodigoProduto);

        // Preço zero ou negativo no cadastro é erro impeditivo de lançamento
        if (precoUnitarioCadastro <= 0m)
        {
            return new ResultadoParserBalanca(true, codigoProduto, 0m, 0m, "Preço unitário do produto no cadastro não pode ser zero ou negativo.");
        }

        // Extração da carga útil (Payload: 5 dígitos representando valor ou peso)
        string payloadStr;
        if (codigoLimpo.Length == 13)
        {
            // No padrão GS1/ABRAS de 13 dígitos:
            // Dígitos 2 a 5/6: Código do Produto
            // Dígitos 7 a 11 (0-based: index 6, length 5): Payload de valor ou peso
            // Se tamanho 4 e o dígito 6 for '0' (filler), o payload está estritamente nos dígitos 7 a 11.
            // Se o dígito 6 não for '0', o payload de 5 dígitos pode começar no índice 5 (1 + tamanho).
            if (tamanhoCodigoProduto == 4 && codigoLimpo[5] != '0')
            {
                payloadStr = codigoLimpo.Substring(5, 5);
            }
            else
            {
                payloadStr = codigoLimpo.Substring(6, 5);
            }
        }
        else // 12 dígitos
        {
            var inicioPayload = 1 + tamanhoCodigoProduto;
            var tamanhoDisponivel = Math.Min(5, codigoLimpo.Length - 1 - inicioPayload);
            payloadStr = codigoLimpo.Substring(inicioPayload, Math.Max(1, tamanhoDisponivel));
        }

        if (!int.TryParse(payloadStr, out int valorNumerico) || valorNumerico < 0)
        {
            return new ResultadoParserBalanca(true, codigoProduto, 0m, 0m, "Não foi possível extrair a carga útil numérica da etiqueta.");
        }

        if (modo == ModoCodigoBalanca.ValorTotal)
        {
            // Os dígitos representam o valor total a pagar em centavos de R$ (ex: 01450 = R$ 14,50)
            decimal valorTotal = valorNumerico / 100m;
            decimal quantidadeCalculada = Math.Round(valorTotal / precoUnitarioCadastro, 3);

            return new ResultadoParserBalanca(
                IsCodigoBalanca: true,
                CodigoProduto: codigoProduto,
                QuantidadeOuPeso: quantidadeCalculada,
                ValorTotalCalculado: valorTotal);
        }
        else // ModoCodigoBalanca.PesoLiquido
        {
            // Os dígitos representam o peso em gramas / 3 casas decimais (ex: 01500 = 1,500 kg)
            decimal pesoCalculado = valorNumerico / 1000m;
            decimal valorTotalCalculado = Math.Round(pesoCalculado * precoUnitarioCadastro, 2);

            return new ResultadoParserBalanca(
                IsCodigoBalanca: true,
                CodigoProduto: codigoProduto,
                QuantidadeOuPeso: pesoCalculado,
                ValorTotalCalculado: valorTotalCalculado);
        }
    }
}
