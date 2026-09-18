using System;
using System.Linq;
using System.Text;
using GetStartedApp.Models.Fiscal;

namespace GetStartedApp.Services.Fiscal;

public class DanfeNfceTermicaService
{
    public string GerarDanfeTermica(
        ConfiguracaoFiscalEmpresa empresa, 
        DadosEmissaoNfce dados, 
        long numeroNfce, 
        string chaveAcesso, 
        string urlQrCode, 
        DateTime dataEmissao)
    {
        var sb = new StringBuilder();
        decimal totalProdutos = dados.Itens.Sum(i => i.ValorTotal);
        decimal totalTributos = dados.Itens.Sum(i => i.ValorTotal * (i.AliquotaTributosAproximadosPercentual / 100m));

        // Formatador de Chave de 44 dígitos em grupos de 4
        var chaveFormatada = string.Empty;
        for (int i = 0; i < chaveAcesso.Length; i += 4)
        {
            chaveFormatada += chaveAcesso.Substring(i, Math.Min(4, chaveAcesso.Length - i)) + " ";
        }
        chaveFormatada = chaveFormatada.Trim();

        sb.AppendLine("================================================================");
        sb.AppendLine($"               {empresa.RazaoSocial.ToUpperInvariant()}");
        sb.AppendLine($" CNPJ: {empresa.Cnpj} | IE: {empresa.InscricaoEstadual}");
        sb.AppendLine($" {empresa.Logradouro}, {empresa.Numero} - {empresa.Bairro}");
        sb.AppendLine($" {empresa.Municipio} - {empresa.Uf} | CEP: {empresa.Cep}");
        sb.AppendLine("================================================================");
        sb.AppendLine("                     DANFE NFC-e                                ");
        sb.AppendLine("    Documento Auxiliar da Nota Fiscal de Consumidor Eletrônica  ");
        sb.AppendLine("            Não permite aproveitamento de crédito de ICMS       ");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine(string.Format("{0,-4} {1,-28} {2,6} {3,8} {4,10}", "#", "DESCRIÇÃO", "QTD", "VL UNIT", "TOTAL"));
        sb.AppendLine("----------------------------------------------------------------");

        int idx = 1;
        foreach (var item in dados.Itens)
        {
            var desc = item.DescricaoProduto.Length > 28 ? item.DescricaoProduto.Substring(0, 25) + "..." : item.DescricaoProduto;
            sb.AppendLine(string.Format("{0,-4:D3} {1,-28} {2,6:N0} {3,8:N2} {4,10:N2}", idx++, desc, item.Quantidade, item.ValorUnitario, item.ValorTotal));
        }

        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine(string.Format(" QTD TOTAL DE ITENS: {0,43}", dados.Itens.Count));
        sb.AppendLine(string.Format(" VALOR TOTAL R$: {0,47:N2}", totalProdutos));
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine(" FORMAS DE PAGAMENTO:");
        
        foreach (var pag in dados.Pagamentos)
        {
            var nomeForma = pag.MeioPagamento switch
            {
                "01" => "Dinheiro",
                "03" => "Cartão de Crédito",
                "04" => "Cartão de Débito",
                "17" => "PIX",
                _ => "Outros"
            };
            sb.AppendLine(string.Format("   {0,-30} R$ {1,25:N2}", nomeForma, pag.Valor));
        }

        if (dados.ValorTroco > 0)
        {
            sb.AppendLine(string.Format("   {0,-30} R$ {1,25:N2}", "Troco", dados.ValorTroco));
        }

        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine(string.Format(" Trib Aprox R$: {0:N2} ({1:N0}%) Federal e Estadual - Lei 12.741", totalTributos, totalProdutos > 0 ? (totalTributos / totalProdutos * 100) : 0));
        sb.AppendLine("----------------------------------------------------------------");

        if (empresa.Ambiente == 2)
        {
            sb.AppendLine("****************************************************************");
            sb.AppendLine("  EMITIDA EM AMBIENTE DE HOMOLOGAÇÃO - SEM VALOR FISCAL         ");
            sb.AppendLine("****************************************************************");
        }

        if (dados.ModoContingenciaOffline)
        {
            sb.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            sb.AppendLine("  EMITIDA EM CONTINGÊNCIA OFFLINE (SEFAZ FORA DO AR)            ");
            sb.AppendLine("  Pendente de transmissão à SEFAZ em até 24 horas               ");
            sb.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        }

        sb.AppendLine(string.Format(" NFC-e Nº: {0:D9}  Série: {1:D3}  Data: {2:dd/MM/yyyy HH:mm:ss}", numeroNfce, empresa.SerieNfce, dataEmissao));
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine(" CHAVE DE ACESSO:");
        sb.AppendLine($" {chaveFormatada}");
        sb.AppendLine("----------------------------------------------------------------");

        if (!string.IsNullOrWhiteSpace(dados.CpfConsumidor))
        {
            sb.AppendLine($" CONSUMIDOR: CPF {dados.CpfConsumidor} - {(dados.NomeConsumidor ?? "CONSUMIDOR")}");
        }
        else
        {
            sb.AppendLine(" CONSUMIDOR NÃO IDENTIFICADO (CONSUMIDOR FINAL)");
        }

        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("              CONSULTE PELA CHAVE DE ACESSO OU QR-CODE          ");
        sb.AppendLine($" QR-Code: {urlQrCode}");
        sb.AppendLine("================================================================");

        return sb.ToString();
    }
}
