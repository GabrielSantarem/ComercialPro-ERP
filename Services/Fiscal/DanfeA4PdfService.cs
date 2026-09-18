using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GetStartedApp.Models.Fiscal;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GetStartedApp.Services.Fiscal;

public class DanfeA4PdfService
{
    static DanfeA4PdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GerarDanfeA4Pdf(DadosEmissaoNfce dados, RetornoEmissaoNfce retorno, ConfiguracaoFiscalEmpresa empresa)
    {
        var totalNota = dados.Itens.Sum(x => x.ValorTotal);
        var agora = DateTime.Now;

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(15, Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(7).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // 1. CANHOTO DE RECEBIMENTO
                    col.Item().Border(1).BorderColor(Colors.Grey.Medium).Padding(4).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem(3).Text(t =>
                            {
                                t.Span("RECEBEMOS DE ").FontSize(6).SemiBold();
                                t.Span(empresa.RazaoSocial.ToUpper()).FontSize(6).Bold();
                                t.Span(" OS PRODUTOS/SERVIÇOS CONSTANTES DA NOTA FISCAL INDICADA AO LADO.").FontSize(6);
                            });
                            r.RelativeItem(1).Border(1).BorderColor(Colors.Grey.Medium).Padding(2).AlignCenter().Column(cn =>
                            {
                                cn.Item().Text("NF-e / NFC-e").FontSize(6).Bold();
                                cn.Item().Text($"Nº {retorno.NumeroNota:D9}").FontSize(8).Black();
                                cn.Item().Text($"SÉRIE {retorno.Serie:D3}").FontSize(6);
                            });
                        });

                        c.Item().PaddingTop(3).Row(r =>
                        {
                            r.RelativeItem(1.5f).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text("DATA DE RECEBIMENTO:\n____/____/________").FontSize(6);
                            r.RelativeItem(3.5f).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text("IDENTIFICAÇÃO E ASSINATURA DO RECEBEDOR:\n ").FontSize(6);
                        });
                    });

                    // Linha separadora
                    col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                    // 2. QUADRO SUPERIOR DO EMITENTE & DADOS DANFE
                    col.Item().Border(1).BorderColor(Colors.Grey.Medium).Row(r =>
                    {
                        // 2.1 Identificação do Emitente
                        r.RelativeItem(2.2f).Padding(4).Column(ce =>
                        {
                            ce.Item().Text(empresa.NomeFantasia.ToUpper()).FontSize(9).Black();
                            ce.Item().Text(empresa.RazaoSocial).FontSize(7).SemiBold();
                            ce.Item().Text($"{empresa.Logradouro}, {empresa.Numero} - {empresa.Bairro}").FontSize(6);
                            ce.Item().Text($"{empresa.Municipio}/{empresa.Uf} - CEP: {empresa.Cep}").FontSize(6);
                            ce.Item().Text($"CNPJ: {empresa.Cnpj} - IE: {empresa.InscricaoEstadual}").FontSize(6);
                        });

                        // 2.2 Quadro Central: DANFE
                        r.RelativeItem(1.3f).BorderLeft(1).BorderRight(1).BorderColor(Colors.Grey.Medium).Padding(4).AlignCenter().Column(cd =>
                        {
                            cd.Item().Text("DANFE").FontSize(11).Black();
                            cd.Item().Text("Documento Auxiliar da\nNota Fiscal Eletrônica").FontSize(6).AlignCenter();
                            cd.Item().PaddingTop(2).Row(rt =>
                            {
                                rt.AutoItem().Border(1).Padding(2).Text("1").FontSize(8).Bold();
                                rt.RelativeItem().PaddingLeft(3).Text("0 - ENTRADA\n1 - SAÍDA").FontSize(5);
                            });
                            cd.Item().PaddingTop(2).Text($"Nº {retorno.NumeroNota:D9}").FontSize(9).Bold();
                            cd.Item().Text($"SÉRIE: {retorno.Serie:D3} - FL 1/1").FontSize(6).SemiBold();
                        });

                        // 2.3 Chave de Acesso e Protocolo
                        r.RelativeItem(2.5f).Padding(4).Column(ck =>
                        {
                            ck.Item().Text("CHAVE DE ACESSO").FontSize(6).Bold();
                            ck.Item().Text(FormatadorDanfe.FormatarChave44(retorno.ChaveAcesso)).FontSize(7.5f).Black().FontFamily("Courier New");

                            ck.Item().PaddingTop(4).Text("Consulta de autenticidade no portal nacional da NF-e").FontSize(5.5f);
                            ck.Item().Text("www.nfe.fazenda.gov.br/portal ou no site da Sefaz Autorizadora").FontSize(5.5f).Italic();

                            ck.Item().PaddingTop(4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Column(cp =>
                            {
                                cp.Item().Text("PROTOCOLO DE AUTORIZAÇÃO DE USO").FontSize(5).Bold();
                                var prot = retorno.EmitidaEmContingencia ? "EMITIDA EM CONTINGÊNCIA (OFFLINE)" : "135260000000001";
                                cp.Item().Text($"{prot} - {agora:dd/MM/yyyy HH:mm:ss}").FontSize(6.5f);
                            });
                        });
                    });

                    // 2.4 Natureza da Operação
                    col.Item().Border(1).BorderColor(Colors.Grey.Medium).Padding(2).Row(r =>
                    {
                        r.RelativeItem(3).Text(t =>
                        {
                            t.Span("NATUREZA DA OPERAÇÃO: ").FontSize(6).Bold();
                            t.Span(retorno.EmitidaEmContingencia ? "VENDA AO CONSUMIDOR (EMITIDA EM CONTINGÊNCIA)" : "VENDA AO CONSUMIDOR").FontSize(6);
                        });
                        r.RelativeItem(2).Text(t =>
                        {
                            t.Span("INSCRIÇÃO ESTADUAL: ").FontSize(6).Bold();
                            t.Span(empresa.InscricaoEstadual).FontSize(6);
                        });
                    });

                    // 3. DESTINATÁRIO / REMETENTE
                    col.Item().PaddingTop(2).Text("DESTINATÁRIO / REMETENTE").FontSize(6).Bold();
                    col.Item().Border(1).BorderColor(Colors.Grey.Medium).Padding(3).Row(r =>
                    {
                        r.RelativeItem(3).Column(c =>
                        {
                            var nome = string.IsNullOrWhiteSpace(dados.NomeConsumidor) ? "CONSUMIDOR NÃO IDENTIFICADO" : dados.NomeConsumidor.ToUpper();
                            c.Item().Text($"NOME / RAZÃO SOCIAL: {nome}").FontSize(6.5f).Bold();
                            c.Item().Text("ENDEREÇO: NÃO INFORMADO").FontSize(6);
                        });
                        r.RelativeItem(2).Column(c =>
                        {
                            var cpf = string.IsNullOrWhiteSpace(dados.CpfConsumidor) ? "NÃO IDENTIFICADO" : dados.CpfConsumidor;
                            c.Item().Text($"CNPJ / CPF: {cpf}").FontSize(6.5f).Bold();
                            c.Item().Text($"DATA DE EMISSÃO: {agora:dd/MM/yyyy HH:mm}").FontSize(6);
                        });
                    });

                    // 4. CÁLCULO DO IMPOSTO
                    col.Item().PaddingTop(2).Text("CÁLCULO DO IMPOSTO").FontSize(6).Bold();
                    col.Item().Border(1).BorderColor(Colors.Grey.Medium).Padding(3).Row(r =>
                    {
                        r.RelativeItem().Column(c => { c.Item().Text("BASE CÁLC. ICMS").FontSize(5).Bold(); c.Item().Text("0,00").FontSize(6.5f); });
                        r.RelativeItem().Column(c => { c.Item().Text("VALOR ICMS").FontSize(5).Bold(); c.Item().Text("0,00").FontSize(6.5f); });
                        r.RelativeItem().Column(c => { c.Item().Text("BASE ICMS ST").FontSize(5).Bold(); c.Item().Text("0,00").FontSize(6.5f); });
                        r.RelativeItem().Column(c => { c.Item().Text("VALOR ICMS ST").FontSize(5).Bold(); c.Item().Text("0,00").FontSize(6.5f); });
                        r.RelativeItem().Column(c => { c.Item().Text("TOTAL PRODUTOS").FontSize(5).Bold(); c.Item().Text($"R$ {totalNota:N2}").FontSize(6.5f).Bold(); });
                        r.RelativeItem().Column(c => { c.Item().Text("TOTAL DA NOTA").FontSize(5).Bold(); c.Item().Text($"R$ {totalNota:N2}").FontSize(7.5f).Black(); });
                    });

                    // 5. DADOS DOS PRODUTOS / SERVIÇOS
                    col.Item().PaddingTop(2).Text("DADOS DOS PRODUTOS / SERVIÇOS").FontSize(6).Bold();
                    col.Item().Border(1).BorderColor(Colors.Grey.Medium).Table(t =>
                    {
                        t.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(20); // Item
                            columns.ConstantColumn(50); // Cód
                            columns.RelativeColumn(3);  // Descrição
                            columns.ConstantColumn(45); // NCM
                            columns.ConstantColumn(30); // CSOSN
                            columns.ConstantColumn(30); // CFOP
                            columns.ConstantColumn(25); // UN
                            columns.ConstantColumn(30); // QTD
                            columns.ConstantColumn(45); // V. UNIT
                            columns.ConstantColumn(45); // V. TOTAL
                        });

                        t.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(1).Text("#").FontSize(5).Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(1).Text("CÓDIGO").FontSize(5).Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(1).Text("DESCRIÇÃO DO PRODUTO / SERVIÇO").FontSize(5).Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(1).Text("NCM/SH").FontSize(5).Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(1).Text("CST").FontSize(5).Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(1).Text("CFOP").FontSize(5).Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(1).Text("UN").FontSize(5).Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(1).AlignRight().Text("QTD").FontSize(5).Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(1).AlignRight().Text("V. UNIT").FontSize(5).Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(1).AlignRight().Text("V. TOTAL").FontSize(5).Bold();
                        });

                        int num = 1;
                        foreach (var item in dados.Itens)
                        {
                            var bg = num % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                            t.Cell().Background(bg).Padding(1).Text(num.ToString()).FontSize(5.5f);
                            t.Cell().Background(bg).Padding(1).Text(item.CodigoProduto).FontSize(5.5f);
                            t.Cell().Background(bg).Padding(1).Text(item.DescricaoProduto).FontSize(5.5f).SemiBold();
                            t.Cell().Background(bg).Padding(1).Text(item.Ncm).FontSize(5.5f);
                            t.Cell().Background(bg).Padding(1).Text(item.Csosn).FontSize(5.5f);
                            t.Cell().Background(bg).Padding(1).Text(item.Cfop.ToString()).FontSize(5.5f);
                            t.Cell().Background(bg).Padding(1).Text(item.UnidadeComercial).FontSize(5.5f);
                            t.Cell().Background(bg).Padding(1).AlignRight().Text($"{item.Quantidade:N2}").FontSize(5.5f);
                            t.Cell().Background(bg).Padding(1).AlignRight().Text($"R$ {item.ValorUnitario:N2}").FontSize(5.5f);
                            t.Cell().Background(bg).Padding(1).AlignRight().Text($"R$ {item.ValorTotal:N2}").FontSize(5.5f).Bold();
                            num++;
                        }
                    });

                    // 6. DADOS ADICIONAIS & OBSERVAÇÕES
                    col.Item().PaddingTop(2).Text("DADOS ADICIONAIS").FontSize(6).Bold();
                    col.Item().Border(1).BorderColor(Colors.Grey.Medium).Padding(4).Column(ca =>
                    {
                        ca.Item().Text("INFORMAÇÕES COMPLEMENTARES:").FontSize(5.5f).Bold();
                        ca.Item().Text($"Trib aprox: R$ {totalNota * 0.1345m:N2} Federal e R$ {totalNota * 0.17m:N2} Estadual (Fonte: IBPT)").FontSize(5.5f);
                        ca.Item().Text("DOCUMENTO EMITIDO POR ME OU EPP OPTANTE PELO SIMPLES NACIONAL. NÃO GERA DIREITO A CRÉDITO FISCAL DE IPI.").FontSize(5.5f);
                        if (retorno.EmitidaEmContingencia)
                        {
                            ca.Item().Text("EMITIDA EM CONTINGÊNCIA - PENDENTE DE TRANSMISSÃO CONFORME LEGISLAÇÃO VIGENTE.").FontSize(5.5f).Bold();
                        }
                        if (dados.Pagamentos.Count > 0)
                        {
                            var pagTxt = string.Join(" | ", dados.Pagamentos.Select(p => $"Cód {p.MeioPagamento}: R$ {p.Valor:N2}"));
                            ca.Item().Text($"Formas de Pagamento: {pagTxt}").FontSize(5.5f);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("DANFE A4 gerado pelo Sistema ERP GetStartedApp com Zeus Automação & QuestPDF | Página 1 de 1").FontSize(5).Italic();
                });
            });
        });

        return documento.GeneratePdf();
    }

    public string SalvarPdf(byte[] pdfBytes, string chaveAcesso, string? diretorioDestino = null)
    {
        var dir = diretorioDestino ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "danfe_pdf");
        Directory.CreateDirectory(dir);

        var arquivo = Path.Combine(dir, $"DANFE_{chaveAcesso}.pdf");
        File.WriteAllBytes(arquivo, pdfBytes);
        return arquivo;
    }

    public bool AbrirVisualizacaoEImpressao(string caminhoPdf)
    {
        if (!File.Exists(caminhoPdf)) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = caminhoPdf,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            // Fallback para Linux xdg-open explícito se UseShellExecute falhar
            try
            {
                Process.Start("xdg-open", $"\"{caminhoPdf}\"");
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

public static class FormatadorDanfe
{
    public static string FormatarChave44(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave) || chave.Length != 44) return chave;
        return string.Join(" ", Enumerable.Range(0, 11).Select(i => chave.Substring(i * 4, 4)));
    }
}
