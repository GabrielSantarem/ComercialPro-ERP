using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GetStartedApp.Services.Etiquetas;

public class EtiquetaGondolaService : IEtiquetaGondolaService
{
    static EtiquetaGondolaService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public List<ModeloFolhaEtiqueta> ObterModelosSuportados()
    {
        return
        [
            new ModeloFolhaEtiqueta(
                Nome: "Pimaco 6180",
                Colunas: 3,
                Linhas: 10,
                MargemSuperiorMm: 21.2f,
                MargemLateralMm: 4.8f,
                LarguraEtiquetaMm: 66.7f,
                AlturaEtiquetaMm: 25.4f,
                EspacamentoHorizontalMm: 2.5f,
                EspacamentoVerticalMm: 0.0f),

            new ModeloFolhaEtiqueta(
                Nome: "Pimaco 6182",
                Colunas: 2,
                Linhas: 7,
                MargemSuperiorMm: 15.15f,
                MargemLateralMm: 3.4f,
                LarguraEtiquetaMm: 101.6f,
                AlturaEtiquetaMm: 38.1f,
                EspacamentoHorizontalMm: 0.0f,
                EspacamentoVerticalMm: 0.0f)
        ];
    }

    public byte[] GerarFolhaEtiquetasPdf(
        List<ItemEtiquetaGondolaDto> produtos, 
        ModeloFolhaEtiqueta modelo)
    {
        var listaProdutos = produtos ?? [];
        int colunas = Math.Max(1, modelo.Colunas);
        int linhas = Math.Max(1, modelo.Linhas);
        int capacidadePorPagina = colunas * linhas;

        // Particionar produtos em páginas conforme a capacidade do modelo
        var paginas = new List<List<ItemEtiquetaGondolaDto>>();
        if (listaProdutos.Count == 0)
        {
            paginas.Add([]);
        }
        else
        {
            for (int i = 0; i < listaProdutos.Count; i += capacidadePorPagina)
            {
                paginas.Add(listaProdutos.Skip(i).Take(capacidadePorPagina).ToList());
            }
        }

        var dataAtualFormatada = DateTime.Today.ToString("dd/MM/yyyy");

        var doc = Document.Create(container =>
        {
            foreach (var paginaItens in paginas)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginTop(modelo.MargemSuperiorMm, Unit.Millimetre);
                    page.MarginBottom(modelo.MargemSuperiorMm, Unit.Millimetre);
                    page.MarginLeft(modelo.MargemLateralMm, Unit.Millimetre);
                    page.MarginRight(modelo.MargemLateralMm, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(7).FontFamily("Arial"));

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            for (int c = 0; c < colunas; c++)
                            {
                                cols.RelativeColumn();
                            }
                        });

                        int itensRenderizados = 0;
                        foreach (var item in paginaItens)
                        {
                            table.Cell()
                                .Height(modelo.AlturaEtiquetaMm, Unit.Millimetre)
                                .Padding(2)
                                .Border(0.5f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Element(c => RenderizarEtiqueta(c, item, dataAtualFormatada));
                            
                            itensRenderizados++;
                        }

                        // Preencher células vazias da última página para manter alinhamento
                        while (itensRenderizados < capacidadePorPagina)
                        {
                            table.Cell()
                                .Height(modelo.AlturaEtiquetaMm, Unit.Millimetre)
                                .Padding(2)
                                .Element(c => { });
                            
                            itensRenderizados++;
                        }
                    });
                });
            }
        });

        return doc.GeneratePdf();
    }

    private void RenderizarEtiqueta(IContainer container, ItemEtiquetaGondolaDto item, string dataFormatada)
    {
        // Truncamento defensivo do nome do produto para evitar estouro da etiqueta
        var nomeProduto = item.Nome?.Trim() ?? "PRODUTO";
        if (nomeProduto.Length > 42)
        {
            nomeProduto = nomeProduto.Substring(0, 39) + "...";
        }

        var precoFracionado = item.PrecoPorQuiloOuLitro.HasValue
            ? $"R$ {item.PrecoPorQuiloOuLitro.Value:N2}"
            : (item.UnidadeMedida?.ToUpperInvariant() == "KG" 
                ? $"R$ {item.PrecoVenda:N2}" 
                : $"R$ {item.PrecoVenda:N2} / {item.UnidadeMedida ?? "UN"}");

        var temEan = !string.IsNullOrWhiteSpace(item.CodigoBarras) && 
                     !item.CodigoBarras.Equals("SEM GTIN", StringComparison.OrdinalIgnoreCase);

        container.Column(col =>
        {
            // 1. Nome do Produto (destaque em bold)
            col.Item().Text(nomeProduto)
                .FontSize(7.5f)
                .Bold()
                .LineHeight(1.05f);

            // 2. Preço de Venda à Vista em Super Destaque
            col.Item().Row(r =>
            {
                r.RelativeItem().AlignLeft().AlignMiddle().Text(t =>
                {
                    t.Span("R$ ").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
                    t.Span($"{item.PrecoVenda:N2}").FontSize(14).Black().FontColor(Colors.Black);
                });

                r.AutoItem().AlignRight().AlignBottom().Column(sub =>
                {
                    sub.Item().Text($"P/ {item.UnidadeMedida ?? "UN"}: {precoFracionado}")
                        .FontSize(5.5f)
                        .SemiBold()
                        .FontColor(Colors.Grey.Darken2);
                    sub.Item().Text($"Ref: {dataFormatada}")
                        .FontSize(5f)
                        .FontColor(Colors.Grey.Medium);
                });
            });

            // 3. Código de barras / Código interno
            col.Item().Row(r =>
            {
                if (temEan)
                {
                    // Mini representação visual de barras (SVG)
                    r.RelativeItem().Svg(@"<svg viewBox=""0 0 100 12"" xmlns=""http://www.w3.org/2000/svg"">
                        <rect x=""0"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""4"" y=""0"" width=""1"" height=""12"" fill=""#000""/>
                        <rect x=""7"" y=""0"" width=""3"" height=""12"" fill=""#000""/>
                        <rect x=""12"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""16"" y=""0"" width=""1"" height=""12"" fill=""#000""/>
                        <rect x=""19"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""23"" y=""0"" width=""3"" height=""12"" fill=""#000""/>
                        <rect x=""28"" y=""0"" width=""1"" height=""12"" fill=""#000""/>
                        <rect x=""31"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""35"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""39"" y=""0"" width=""1"" height=""12"" fill=""#000""/>
                        <rect x=""42"" y=""0"" width=""3"" height=""12"" fill=""#000""/>
                        <rect x=""47"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""51"" y=""0"" width=""1"" height=""12"" fill=""#000""/>
                        <rect x=""54"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""58"" y=""0"" width=""3"" height=""12"" fill=""#000""/>
                        <rect x=""63"" y=""0"" width=""1"" height=""12"" fill=""#000""/>
                        <rect x=""66"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""70"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""74"" y=""0"" width=""1"" height=""12"" fill=""#000""/>
                        <rect x=""77"" y=""0"" width=""3"" height=""12"" fill=""#000""/>
                        <rect x=""82"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""86"" y=""0"" width=""1"" height=""12"" fill=""#000""/>
                        <rect x=""89"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                        <rect x=""93"" y=""0"" width=""3"" height=""12"" fill=""#000""/>
                        <rect x=""98"" y=""0"" width=""2"" height=""12"" fill=""#000""/>
                    </svg>");

                    r.AutoItem().PaddingLeft(3).AlignRight().Text($"{item.CodigoBarras} (Cód: {item.CodigoInterno})")
                        .FontSize(5.5f)
                        .SemiBold();
                }
                else
                {
                    r.RelativeItem().Text($"[SEM EAN] Código: {item.CodigoInterno}")
                        .FontSize(5.5f)
                        .Italic()
                        .FontColor(Colors.Grey.Darken2);
                }
            });
        });
    }
}
