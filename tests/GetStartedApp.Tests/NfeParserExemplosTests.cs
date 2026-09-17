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

    private string ObterCaminho(string subCaminho)
    {
        var caminhoCompleto = Path.GetFullPath(Path.Combine(_pastaExemplos, subCaminho));
        if (!File.Exists(caminhoCompleto))
        {
            caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "exemplos_nfe", subCaminho);
            if (!File.Exists(caminhoCompleto))
            {
                caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "../../../exemplos_nfe", subCaminho);
            }
        }
        return caminhoCompleto;
    }

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
        var caminho = ObterCaminho(nomeArquivo);
        Assert.True(File.Exists(caminho), $"Arquivo não encontrado: {caminho}");

        using var stream = File.OpenRead(caminho);
        var dto = _parser.ParseFromStream(stream);

        Assert.NotNull(dto);
        Assert.Equal(fornecedorEsperado, dto.EmitenteRazaoSocial);
        Assert.Equal(qtdItensEsperada, dto.Itens.Count);
        Assert.Equal(qtdDuplicatasEsperada, dto.Duplicatas.Count);
        Assert.True(dto.ValorTotalNfe > 0);
    }

    [Fact]
    public void Deve_Parsear_Nfe_55_Padrao_Com_Sucesso()
    {
        var caminho = ObterCaminho("NFE_55/nfe_55_86273497000137_32261905_1789684870030.xml");
        Assert.True(File.Exists(caminho));

        using var stream = File.OpenRead(caminho);
        var dto = _parser.ParseFromStream(stream);

        Assert.NotNull(dto);
        Assert.Equal("55", dto.ModeloDocumento);
        Assert.Contains("Mercantil", dto.TipoDocumentoDescricao);
        Assert.Equal("EMITENTE LTDA", dto.EmitenteRazaoSocial);
        Assert.Single(dto.Itens);
        Assert.Equal(667.07m, dto.ValorTotalNfe);
    }

    [Fact]
    public void Deve_Parsear_Nfce_65_E_Extrair_Pagamento_Avista()
    {
        var caminho = ObterCaminho("NFCE/nfce_86273497000137_38467892_1789684870031.xml");
        Assert.True(File.Exists(caminho));

        using var stream = File.OpenRead(caminho);
        var dto = _parser.ParseFromStream(stream);

        Assert.NotNull(dto);
        Assert.Equal("65", dto.ModeloDocumento);
        Assert.Contains("NFC-e", dto.TipoDocumentoDescricao);
        Assert.Equal("LOJA DO CONSUMIDOR FINAL", dto.EmitenteRazaoSocial);
        Assert.Single(dto.Itens);
        Assert.Equal(905.91m, dto.ValorTotalNfe);
        Assert.Single(dto.Duplicatas);
        Assert.Equal(905.91m, dto.Duplicatas[0].Valor);
    }

    [Fact]
    public void Deve_Rejeitar_Bpe_Com_Mensagem_Clara()
    {
        var caminho = ObterCaminho("BPE/bpe_86273497000137_66839376_1789684870031.xml");
        Assert.True(File.Exists(caminho));

        using var stream = File.OpenRead(caminho);
        var ex = Assert.Throws<InvalidOperationException>(() => _parser.ParseFromStream(stream));

        Assert.Contains("BP-e", ex.Message);
        Assert.Contains("Bilhete de Passagem", ex.Message);
    }

    [Fact]
    public void Deve_Rejeitar_Cte_Os_Com_Mensagem_Clara()
    {
        var caminho = ObterCaminho("CTE_OS/cte_os_86273497000137_18980889_1789684870030.xml");
        Assert.True(File.Exists(caminho));

        using var stream = File.OpenRead(caminho);
        var ex = Assert.Throws<InvalidOperationException>(() => _parser.ParseFromStream(stream));

        Assert.Contains("CT-e", ex.Message);
        Assert.Contains("Conhecimento de Transporte", ex.Message);
    }

    [Fact]
    public void Deve_Rejeitar_Nfse_Municipal_Bh_Com_Mensagem_Clara()
    {
        var caminho = ObterCaminho("NFE_BH/nfe_bh_86273497000137_53722387_1789684870030.xml");
        Assert.True(File.Exists(caminho));

        using var stream = File.OpenRead(caminho);
        var ex = Assert.Throws<InvalidOperationException>(() => _parser.ParseFromStream(stream));

        Assert.Contains("NFS-e Municipal", ex.Message);
        Assert.Contains("ABRASF", ex.Message);
    }

    [Fact]
    public void Deve_Rejeitar_Nfse_Nacional_Com_Mensagem_Clara()
    {
        var caminho = ObterCaminho("NFSE_NACIONAL/nfse_nacional_86273497000137_01731583_1789684870032.xml");
        Assert.True(File.Exists(caminho));

        using var stream = File.OpenRead(caminho);
        var ex = Assert.Throws<InvalidOperationException>(() => _parser.ParseFromStream(stream));

        Assert.Contains("NFS-e Nacional", ex.Message);
        Assert.Contains("SPED", ex.Message);
    }
}
