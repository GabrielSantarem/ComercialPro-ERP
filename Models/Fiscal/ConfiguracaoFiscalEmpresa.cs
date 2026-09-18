using System;

namespace GetStartedApp.Models.Fiscal;

public class ConfiguracaoFiscalEmpresa
{
    public string Cnpj { get; set; } = "12345678000190";
    public string RazaoSocial { get; set; } = "MERCADO COMERCIAL PRO ERP LTDA";
    public string NomeFantasia { get; set; } = "COMERCIAL PRO";
    public string InscricaoEstadual { get; set; } = "123456789111";
    public string Crt { get; set; } = "1"; // 1 - Simples Nacional, 3 - Regime Normal
    
    // Endereço do emitente
    public string Logradouro { get; set; } = "AVENIDA PAULISTA";
    public string Numero { get; set; } = "1000";
    public string Bairro { get; set; } = "BELA VISTA";
    public int CodigoMunicipioIbge { get; set; } = 3550308; // São Paulo
    public string Municipio { get; set; } = "SAO PAULO";
    public string Uf { get; set; } = "SP";
    public string Cep { get; set; } = "01310100";

    // Parâmetros de Emissão NFC-e
    public int SerieNfce { get; set; } = 1;
    public long UltimoNumeroNfce { get; set; } = 100;
    public int Ambiente { get; set; } = 2; // 1 - Produção, 2 - Homologação / Testes

    // CSC (Código de Segurança do Contribuinte para o QR-Code)
    public string IdCsc { get; set; } = "000001";
    public string CodigoCsc { get; set; } = "ABCD1234EFGH5678";

    // Certificado A1
    public string? CaminhoCertificadoPfx { get; set; }
    public string? SenhaCertificado { get; set; }
    public bool UsarCertificadoTesteAutoassinado { get; set; } = true;
}
