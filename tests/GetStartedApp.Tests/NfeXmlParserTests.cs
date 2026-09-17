using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.Services.Fiscal;
using Xunit;

namespace GetStartedApp.Tests;

public class NfeXmlParserTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PdvService _pdvService;
    private readonly NfeXmlParserService _parser;

    private const string XmlExemploSefaz = """
    <?xml version="1.0" encoding="UTF-8"?>
    <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
      <NFe>
        <infNFe Id="NFe35260912345678000190550010001489201892837461" versao="4.00">
          <ide>
            <cUF>35</cUF>
            <cNF>08928374</cNF>
            <natOp>1.102 - COMPRA PARA COMERCIALIZACAO</natOp>
            <mod>55</mod>
            <serie>1</serie>
            <nNF>148920</nNF>
            <dhEmi>2026-09-17T08:30:00-03:00</dhEmi>
          </ide>
          <emit>
            <CNPJ>12345678000190</CNPJ>
            <xNome>DISTRIBUIDORA DE EMBALAGENS E DESCARTAVEIS LTDA</xNome>
            <xFant>DISTRIBUIDORA CENTRAL</xFant>
            <enderEmit>
              <UF>SP</UF>
            </enderEmit>
            <IE>110293847112</IE>
          </emit>
          <det nItem="1">
            <prod>
              <cProd>EMB-201</cProd>
              <cEAN>7891234560011</cEAN>
              <xProd>SACOLA PLASTICA REFORCADA 2K BRANCA</xProd>
              <NCM>39232190</NCM>
              <CFOP>5102</CFOP>
              <uCom>FD</uCom>
              <qCom>5.0000</qCom>
              <vUnCom>40.0000</vUnCom>
              <vProd>200.00</vProd>
              <vFrete>10.00</vFrete>
            </prod>
          </det>
          <det nItem="2">
            <prod>
              <cProd>DESC-99</cProd>
              <cEAN>SEM GTIN</cEAN>
              <xProd>COPO DESCARTAVEL 180ML TRANSPARENTE</xProd>
              <NCM>39241000</NCM>
              <CFOP>5102</CFOP>
              <uCom>CX</uCom>
              <qCom>2.0000</qCom>
              <vUnCom>50.0000</vUnCom>
              <vProd>100.00</vProd>
              <vFrete>10.00</vFrete>
            </prod>
          </det>
          <total>
            <ICMSTot>
              <vProd>300.00</vProd>
              <vFrete>20.00</vFrete>
              <vSeg>0.00</vSeg>
              <vDesc>0.00</vDesc>
              <vOutro>0.00</vOutro>
              <vNF>320.00</vNF>
            </ICMSTot>
          </total>
          <cobr>
            <dup>
              <nDup>001</nDup>
              <dVenc>2026-10-17</dVenc>
              <vDup>160.00</vDup>
            </dup>
            <dup>
              <nDup>002</nDup>
              <dVenc>2026-11-17</dVenc>
              <vDup>160.00</vDup>
            </dup>
          </cobr>
        </infNFe>
      </NFe>
    </nfeProc>
    """;

    public NfeXmlParserTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _db.Vendedores.Add(new Vendedor { Id = 1, Nome = "Operador Fiscal" });
        _db.SaveChanges();

        _pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        _parser = new NfeXmlParserService(NullLogger<NfeXmlParserService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Deve_Interpretar_Cabecalho_E_Totais_Do_Xml_Sefaz()
    {
        // Act
        var nfe = _parser.ParseFromString(XmlExemploSefaz);

        // Assert
        Assert.NotNull(nfe);
        Assert.Equal("148920", nfe.NumeroNota);
        Assert.Equal("1", nfe.Serie);
        Assert.Equal("35260912345678000190550010001489201892837461", nfe.ChaveAcesso);
        Assert.Equal("12345678000190", nfe.EmitenteCnpj);
        Assert.Equal("DISTRIBUIDORA DE EMBALAGENS E DESCARTAVEIS LTDA", nfe.EmitenteRazaoSocial);
        Assert.Equal("SP", nfe.EmitenteUf);
        Assert.Equal(300.00m, nfe.ValorProdutos);
        Assert.Equal(20.00m, nfe.ValorFrete);
        Assert.Equal(320.00m, nfe.ValorTotalNfe);

        // Duplicatas
        Assert.Equal(2, nfe.Duplicatas.Count);
        Assert.Equal(160.00m, nfe.Duplicatas[0].Valor);
        Assert.Equal(160.00m, nfe.Duplicatas[1].Valor);
    }

    [Fact]
    public void Deve_Extrair_Itens_E_Normalizar_Ean_Do_Xml()
    {
        // Act
        var nfe = _parser.ParseFromString(XmlExemploSefaz);

        // Assert
        Assert.Equal(2, nfe.Itens.Count);

        var item1 = nfe.Itens[0];
        Assert.Equal("EMB-201", item1.CodigoProduto);
        Assert.Equal("7891234560011", item1.CodigoEan);
        Assert.Equal("SACOLA PLASTICA REFORCADA 2K BRANCA", item1.Descricao);
        Assert.Equal(5, item1.QuantidadeComercial);
        Assert.Equal(40.00m, item1.ValorUnitario);
        Assert.Equal(200.00m, item1.ValorTotalBruto);
        Assert.Equal(10.00m, item1.ValorFreteRateado);

        // Item 2 com 'SEM GTIN' deve normalizar para vazio
        var item2 = nfe.Itens[1];
        Assert.Equal(string.Empty, item2.CodigoEan);
        Assert.Equal("COPO DESCARTAVEL 180ML TRANSPARENTE", item2.Descricao);
    }

    [Fact]
    public async Task Deve_Cadastrar_Produto_Automatico_Se_Nao_Existir_No_Erp()
    {
        // Arrange: Catálogo está vazio
        Assert.Empty(await _db.Produtos.ToListAsync());

        // Act: Pede para obter ou criar produto do XML
        var produto = await _pdvService.ObterOuCriarProdutoPorXmlAsync(
            descricao: "PRATO DESCARTAVEL 15CM BRANCO",
            ean: "789999990001",
            ncm: "39241000",
            unidade: "CX",
            precoVendaSugerido: 0, // Automático com margem padrão
            custoUnitario: 10.00m);

        // Assert
        Assert.NotNull(produto);
        Assert.True(produto.Id > 0);
        Assert.Equal("PRATO DESCARTAVEL 15CM BRANCO", produto.Nome);
        Assert.Equal("789999990001", produto.CodigoBarras);
        Assert.Equal("39241000", produto.Ncm);
        Assert.Equal("CX", produto.UnidadeMedida);
        Assert.Equal(10.00m, produto.CustoUltimaCompra);
        Assert.Equal(14.00m, produto.Preco); // 10 * 1.40
        Assert.Equal(0, produto.Estoque); // Estoque zero até faturar a entrada
    }

    [Fact]
    public async Task Deve_Vincular_Produto_Existente_Por_Ean()
    {
        // Arrange: Pré-cadastra produto com o mesmo EAN
        var existente = new Produto
        {
            Nome = "Sacola 2k Cadastrada Anteriormente",
            CodigoBarras = "7891234560011",
            Preco = 50.00m,
            Estoque = 20
        };
        _db.Produtos.Add(existente);
        await _db.SaveChangesAsync();

        // Act: Chama o serviço com dados do XML
        var vinculado = await _pdvService.ObterOuCriarProdutoPorXmlAsync(
            descricao: "SACOLA PLASTICA REFORCADA",
            ean: "7891234560011",
            ncm: "39232190",
            unidade: "FD",
            precoVendaSugerido: 0,
            custoUnitario: 35.00m);

        // Assert: Deve retornar a mesma entidade sem duplicar cadastro
        Assert.Equal(existente.Id, vinculado.Id);
        Assert.Equal("Sacola 2k Cadastrada Anteriormente", vinculado.Nome);
        Assert.Equal(35.00m, vinculado.CustoUltimaCompra);
        Assert.Equal(1, await _db.Produtos.CountAsync());
    }
}
