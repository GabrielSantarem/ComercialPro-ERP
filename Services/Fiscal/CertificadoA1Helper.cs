using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GetStartedApp.Models.Fiscal;

namespace GetStartedApp.Services.Fiscal;

public static class CertificadoA1Helper
{
    public static X509Certificate2 ObterOuGerarCertificado(ConfiguracaoFiscalEmpresa config)
    {
        if (!config.UsarCertificadoTesteAutoassinado && 
            !string.IsNullOrWhiteSpace(config.CaminhoCertificadoPfx) && 
            File.Exists(config.CaminhoCertificadoPfx))
        {
            return X509CertificateLoader.LoadPkcs12FromFile(
                config.CaminhoCertificadoPfx, 
                config.SenhaCertificado ?? string.Empty, 
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        }

        // Gera certificado autoassinado de teste válido para assinatura digital XML-DSig
        return GerarCertificadoTesteEmMemoria(config.RazaoSocial, config.Cnpj);
    }

    public static X509Certificate2 GerarCertificadoTesteEmMemoria(string razaoSocial, string cnpj)
    {
        using var rsa = RSA.Create(2048);
        var subject = $"CN={razaoSocial}:{cnpj}, OU=ERP TESTE, O=COMERCIAL PRO, C=BR";
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, 
                critical: true));

        using var cert = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
        var pfxBytes = cert.Export(X509ContentType.Pfx, "cert_teste_erp");

        return X509CertificateLoader.LoadPkcs12(
            pfxBytes, 
            "cert_teste_erp", 
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }
}
