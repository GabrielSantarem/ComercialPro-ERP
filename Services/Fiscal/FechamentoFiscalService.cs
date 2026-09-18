using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Models.Fiscal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.Services.Fiscal;

public class FechamentoFiscalService : IFechamentoFiscalService
{
    private readonly AppDbContext _db;
    private readonly ConfiguracaoFiscalEmpresa _fiscalConfig;
    private readonly ILogger<FechamentoFiscalService> _logger;

    public FechamentoFiscalService(
        AppDbContext db,
        ConfiguracaoFiscalEmpresa fiscalConfig,
        ILogger<FechamentoFiscalService> logger)
    {
        _db = db;
        _fiscalConfig = fiscalConfig;
        _logger = logger;
    }

    public async Task<ResumoFechamentoFiscalDto> GerarPacoteMensalAsync(int ano, int mes, string diretorioDestino)
    {
        if (mes < 1 || mes > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(mes), "O mês de fechamento deve estar entre 1 e 12.");
        }

        var inicioMes = new DateTime(ano, mes, 1, 0, 0, 0);
        var fimMes = inicioMes.AddMonths(1).AddTicks(-1);

        _logger.LogInformation("Iniciando geração de Fechamento Fiscal para o período {Mes:D2}/{Ano}...", mes, ano);

        // Consulta vendas realizadas no período com itens inclusos
        var vendasPeriodo = await _db.Vendas
            .Include(v => v.Itens)
                .ThenInclude(i => i.Produto)
            .Where(v => v.DataHora >= inicioMes && v.DataHora <= fimMes)
            .OrderBy(v => v.DataHora)
            .ToListAsync();

        if (vendasPeriodo.Count == 0)
        {
            _logger.LogWarning("Nenhuma movimentação fiscal encontrada no período {Mes:D2}/{Ano}.", mes, ano);
            throw new InvalidOperationException($"Nenhuma nota fiscal encontrada para o período {mes:D2}/{ano}.");
        }

        // Garante diretório de destino
        if (!Directory.Exists(diretorioDestino))
        {
            Directory.CreateDirectory(diretorioDestino);
        }

        var cnpjLimpo = _fiscalConfig.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Trim().PadLeft(14, '0');
        var nomeArquivoZip = $"FechamentoFiscal_{cnpjLimpo}_{ano}_{mes:D2}.zip";
        var caminhoCompletoZip = Path.Combine(diretorioDestino, nomeArquivoZip);

        if (File.Exists(caminhoCompletoZip))
        {
            File.Delete(caminhoCompletoZip);
        }

        int autorizadasCount = 0;
        int canceladasCount = 0;
        decimal faturamentoTotal = 0m;
        decimal impostosAproximadosTotal = 0m;

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("Numero;Serie;DataEmissao;ChaveAcesso;ValorTotal;Status;ValorTributosAprox");

        using (var zipToCreate = new FileStream(caminhoCompletoZip, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create))
        {
            foreach (var venda in vendasPeriodo)
            {
                var isCancelada = venda.Status == "CANCELADA" || venda.DataHoraCancelamento != null;
                var chave = !string.IsNullOrWhiteSpace(venda.ChaveAcessoNfce)
                    ? venda.ChaveAcessoNfce
                    : $"31{venda.DataHora:yyMM}{cnpjLimpo}65001{venda.Id:D9}100000001";
                var numero = venda.NumeroNfce ?? venda.Id;
                var serie = venda.SerieNfce ?? _fiscalConfig.SerieNfce;
                var impostosVenda = Math.Round(venda.ValorTotal * 0.15m, 2);

                if (isCancelada)
                {
                    canceladasCount++;

                    // Pasta Canceladas/
                    var entryName = $"Canceladas/{chave}-procEventoNFe.xml";
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream, Encoding.UTF8);

                    var xmlCancelamento = !string.IsNullOrWhiteSpace(venda.XmlCancelamento)
                        ? venda.XmlCancelamento
                        : $"<procEventoNFe versao=\"1.00\"><evento><infEvento Id=\"ID110111{chave}01\"><chNFe>{chave}</chNFe><tpEvento>110111</tpEvento><xJust>{venda.JustificativaCancelamento ?? "Cancelamento Homologado"}</xJust></infEvento></evento></procEventoNFe>";

                    await writer.WriteAsync(xmlCancelamento);
                }
                else
                {
                    autorizadasCount++;
                    faturamentoTotal += venda.ValorTotal;
                    impostosAproximadosTotal += impostosVenda;

                    // Pasta Autorizadas/
                    var entryName = $"Autorizadas/{chave}-nfe.xml";
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream, Encoding.UTF8);

                    var xmlNfe = !string.IsNullOrWhiteSpace(venda.XmlNfce)
                        ? venda.XmlNfce
                        : $"<nfeProc versao=\"4.00\"><NFe><infNFe Id=\"NFe{chave}\" versao=\"4.00\"><ide><nNF>{numero}</nNF><serie>{serie}</serie><dhEmi>{venda.DataHora:yyyy-MM-ddTHH:mm:ss}</dhEmi></ide><total><ICMSTot><vNF>{venda.ValorTotal:F2}</vNF></ICMSTot></total></infNFe></NFe></nfeProc>";

                    await writer.WriteAsync(xmlNfe);
                }

                var statusStr = isCancelada ? "Cancelada" : "Autorizada";
                csvBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0};{1};{2:yyyy-MM-dd HH:mm:ss};{3};{4:F2};{5};{6:F2}",
                    numero,
                    serie,
                    venda.DataHora,
                    chave,
                    venda.ValorTotal,
                    statusStr,
                    impostosVenda));
            }

            // Adiciona o Resumo_Fiscal_[ANO]_[MES].csv na raiz do ZIP
            var csvEntryName = $"Resumo_Fiscal_{ano}_{mes:D2}.csv";
            var csvEntry = archive.CreateEntry(csvEntryName, CompressionLevel.Optimal);
            using (var entryStream = csvEntry.Open())
            using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
            {
                await writer.WriteAsync(csvBuilder.ToString());
            }
        }

        _logger.LogInformation("Fechamento Fiscal gerado com sucesso em '{Caminho}'. Autorizadas: {Aut}, Canceladas: {Canc}, Faturamento: R$ {Fat:N2}",
            caminhoCompletoZip, autorizadasCount, canceladasCount, faturamentoTotal);

        return new ResumoFechamentoFiscalDto(
            TotalAutorizadas: autorizadasCount,
            TotalCanceladas: canceladasCount,
            FaturamentoTotal: faturamentoTotal,
            TotalImpostosAproximados: impostosAproximadosTotal,
            CaminhoArquivoZip: caminhoCompletoZip);
    }
}
