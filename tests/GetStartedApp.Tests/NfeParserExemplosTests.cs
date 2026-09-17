using System;
using System.IO;
using GetStartedApp.Services.Fiscal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GetStartedApp.Tests;

public class NfeParserExemplosTests
{
    private readonly NfeXmlParserService _parser = new(NullLogger<NfeXmlParserService>.Instance);
    private readonly string _pastaExemplos = Path.Combine(AppContext.BaseDirectory, "../../../../../exemplos_nfe");

    [Theory]
    [InlineData("nfe_001_distribuidora_embalagens.xml", "PLASTIPACK INDUSTRIA DE EMBALAGENS LTDA", 3, 3)]
    [InlineData("nfe_002_atacado_alimentos.xml", "DISTRIBUIDORA CENTRAL DE ALIMENTOS MINAS LTDA", 3, 2)]
    [InlineData("nfe_003_hortifruti_sem_gtin.xml", "COOPERATIVA AGRICOLA VALE DO SOL", 3, 1)]
    [InlineData("nfe_004_produtos_limpeza.xml", "QUIMICA LIMPA BRASIL PRODUTOS SANEANTES LTDA", 3, 3)]
    public void Deve_Parsear_Xmls_De_Exemplo_Com_Sucesso(
        string nomeArquivo, 
        string fornecedorEsperado, 
        int qtdItensEsperada, 
        int qtdDuplicatasEsperada)
    {
        var caminhoCompleto = Path.GetFullPath(Path.Combine(_pastaExemplos, nomeArquivo));
        
        // Se por ventura estiver em diretório relativo diferente durante dotnet test
        if (!File.Exists(caminhoCompleto))
        {
            // Tenta achar na pasta raiz do projeto
            caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "exemplos_nfe", nomeArquivo);
            if (!File.Exists(caminhoCompleto))
            {
                caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "../../../exemplos_nfe", nomeArquivo);
            }
        }

        Assert.True(File.Exists(caminhoCompleto), $"Arquivo de exemplo não encontrado: {caminhoCompleto}");

        using var stream = File.OpenRead(caminhoCompleto);
        var dto = _parser.ParseFromStream(stream);

        Assert.NotNull(dto);
        Assert.Equal(fornecedorEsperado, dto.EmitenteRazaoSocial);
        Assert.Equal(qtdItensEsperada, dto.Itens.Count);
        Assert.Equal(qtdDuplicatasEsperada, dto.Duplicatas.Count);
        Assert.True(dto.ValorTotalNfe > 0, "O valor total da nota deve ser positivo.");
    }
}
