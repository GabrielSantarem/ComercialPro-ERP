using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.Services.Clientes;

public class ClienteService : IClienteService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ClienteService> _logger;

    public ClienteService(AppDbContext db, ILogger<ClienteService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Cliente>> PesquisarClientesAsync(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo))
        {
            return await _db.Clientes
                .OrderBy(c => c.Nome)
                .Take(50)
                .ToListAsync();
        }

        var termoTrim = termo.Trim().ToLowerInvariant();
        var termoNumerico = Regex.Replace(termo, @"\D", "");

        return await _db.Clientes
            .Where(c => c.Nome.ToLower().Contains(termoTrim) ||
                        (!string.IsNullOrEmpty(termoNumerico) && c.CpfCnpj != null && c.CpfCnpj.Contains(termoNumerico)) ||
                        (c.Telefone != null && c.Telefone.Contains(termoTrim)) ||
                        (c.Email != null && c.Email.ToLower().Contains(termoTrim)))
            .OrderBy(c => c.Nome)
            .Take(50)
            .ToListAsync();
    }

    public async Task<Cliente> SalvarClienteAsync(Cliente cliente)
    {
        if (cliente == null)
            throw new ArgumentNullException(nameof(cliente));

        if (string.IsNullOrWhiteSpace(cliente.Nome))
            throw new ArgumentException("O nome do cliente é obrigatório.", nameof(cliente));

        // Validação e normalização de CPF/CNPJ
        if (!string.IsNullOrWhiteSpace(cliente.CpfCnpj))
        {
            var digitos = Regex.Replace(cliente.CpfCnpj, @"\D", "");
            if (!ValidarCpfCnpj(digitos))
            {
                throw new ArgumentException("CPF ou CNPJ inválido de acordo com os critérios oficiais da Receita Federal.", nameof(cliente.CpfCnpj));
            }

            cliente.CpfCnpj = FormatarCpfCnpj(digitos);

            // Bloqueio de duplicidade
            var duplicado = await _db.Clientes
                .AnyAsync(c => c.Id != cliente.Id && c.CpfCnpj == cliente.CpfCnpj);

            if (duplicado)
            {
                throw new InvalidOperationException($"Já existe um cliente cadastrado com o CPF/CNPJ '{cliente.CpfCnpj}'.");
            }
        }

        if (cliente.LimiteCredito < 0)
            throw new ArgumentException("O limite de crédito não pode ser negativo.", nameof(cliente.LimiteCredito));

        if (cliente.Id == 0)
        {
            cliente.DataCadastro = DateTime.Now;
            _db.Clientes.Add(cliente);
            await _db.SaveChangesAsync();

            // Vínculo retroativo com contas a receber existentes que possuam o mesmo CPF/CNPJ
            if (!string.IsNullOrWhiteSpace(cliente.CpfCnpj))
            {
                var digitos = Regex.Replace(cliente.CpfCnpj, @"\D", "");
                var contasHistoricas = await _db.ContasReceber
                    .Where(c => c.ClienteId == null && (c.ClienteCpfCnpj.Contains(digitos) || c.ClienteCpfCnpj == cliente.CpfCnpj))
                    .ToListAsync();

                foreach (var cr in contasHistoricas)
                {
                    cr.ClienteId = cliente.Id;
                    cr.ClienteNome = cliente.Nome;
                }

                if (contasHistoricas.Count > 0)
                {
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Vinculadas {Qtd} contas a receber históricas ao cliente #{Id} ({Nome})",
                        contasHistoricas.Count, cliente.Id, cliente.Nome);
                }
            }

            _logger.LogInformation("Cliente #{Id} ({Nome}) cadastrado com sucesso com limite R$ {Limite:N2}",
                cliente.Id, cliente.Nome, cliente.LimiteCredito);
        }
        else
        {
            var existente = await _db.Clientes.FindAsync(cliente.Id);
            if (existente == null)
                throw new InvalidOperationException($"Cliente com ID #{cliente.Id} não encontrado para atualização.");

            existente.Nome = cliente.Nome;
            existente.CpfCnpj = cliente.CpfCnpj;
            existente.Telefone = cliente.Telefone;
            existente.Email = cliente.Email;
            existente.Endereco = cliente.Endereco;
            existente.LimiteCredito = cliente.LimiteCredito;
            existente.BloqueadoManualmente = cliente.BloqueadoManualmente;
            existente.MotivoBloqueio = cliente.MotivoBloqueio;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Cliente #{Id} ({Nome}) atualizado com sucesso.", cliente.Id, cliente.Nome);
            return existente;
        }

        return cliente;
    }

    public async Task<StatusCreditoClienteDto> AvaliarCreditoAsync(int clienteId, decimal valorNovaCompra)
    {
        var cliente = await _db.Clientes.FindAsync(clienteId);
        if (cliente == null)
            throw new InvalidOperationException($"Cliente com ID #{clienteId} não encontrado no sistema.");

        // Regra 0: Bloqueio manual / administrativo
        if (cliente.BloqueadoManualmente)
        {
            return new StatusCreditoClienteDto(
                ClienteId: cliente.Id,
                Nome: cliente.Nome,
                LimiteTotal: cliente.LimiteCredito,
                SaldoDevedor: 0m,
                LimiteDisponivel: 0m,
                QuantidadeTitulosVencidos: 0,
                AptoParaCrediario: false,
                MotivoRestricao: $"Cliente bloqueado administrativamente: {cliente.MotivoBloqueio ?? "Sem motivo especificado"}.");
        }

        // Busca pendências em ContasReceber
        var digitosCpf = !string.IsNullOrWhiteSpace(cliente.CpfCnpj) ? Regex.Replace(cliente.CpfCnpj, @"\D", "") : string.Empty;

        var contasPendentes = await _db.ContasReceber
            .Where(c => c.Status == "PENDENTE" && (c.ClienteId == cliente.Id || (!string.IsNullOrEmpty(digitosCpf) && c.ClienteCpfCnpj.Contains(digitosCpf))))
            .ToListAsync();

        var saldoDevedor = contasPendentes.Sum(c => c.ValorFinal - (c.ValorRecebido ?? 0));
        var hoje = DateTime.Today;

        // Regra 2: Inadimplência (títulos vencidos há mais de 5 dias)
        var titulosVencidos = contasPendentes
            .Where(c => c.DataVencimento.Date < hoje && (hoje - c.DataVencimento.Date).TotalDays > 5)
            .ToList();

        var limiteDisponivel = Math.Max(0m, cliente.LimiteCredito - saldoDevedor);

        if (titulosVencidos.Count > 0)
        {
            var diasMaiorAtraso = (int)(hoje - titulosVencidos.Min(t => t.DataVencimento.Date)).TotalDays;
            return new StatusCreditoClienteDto(
                ClienteId: cliente.Id,
                Nome: cliente.Nome,
                LimiteTotal: cliente.LimiteCredito,
                SaldoDevedor: saldoDevedor,
                LimiteDisponivel: limiteDisponivel,
                QuantidadeTitulosVencidos: titulosVencidos.Count,
                AptoParaCrediario: false,
                MotivoRestricao: $"Inadimplência detectada: {titulosVencidos.Count} título(s) vencido(s) há {diasMaiorAtraso} dia(s). Regularização financeira necessária.");
        }

        // Regra: Sem limite de crédito
        if (cliente.LimiteCredito <= 0m)
        {
            return new StatusCreditoClienteDto(
                ClienteId: cliente.Id,
                Nome: cliente.Nome,
                LimiteTotal: cliente.LimiteCredito,
                SaldoDevedor: saldoDevedor,
                LimiteDisponivel: 0m,
                QuantidadeTitulosVencidos: 0,
                AptoParaCrediario: false,
                MotivoRestricao: "Cliente não possui limite de crédito configurado para crediário.");
        }

        // Regra 1: Excesso de Limite
        if (valorNovaCompra > limiteDisponivel)
        {
            return new StatusCreditoClienteDto(
                ClienteId: cliente.Id,
                Nome: cliente.Nome,
                LimiteTotal: cliente.LimiteCredito,
                SaldoDevedor: saldoDevedor,
                LimiteDisponivel: limiteDisponivel,
                QuantidadeTitulosVencidos: 0,
                AptoParaCrediario: false,
                MotivoRestricao: $"Limite de crédito insuficiente. Disponível: R$ {limiteDisponivel:N2}, Tentativa: R$ {valorNovaCompra:N2}.");
        }

        // Aprovado
        return new StatusCreditoClienteDto(
            ClienteId: cliente.Id,
            Nome: cliente.Nome,
            LimiteTotal: cliente.LimiteCredito,
            SaldoDevedor: saldoDevedor,
            LimiteDisponivel: limiteDisponivel,
            QuantidadeTitulosVencidos: 0,
            AptoParaCrediario: true,
            MotivoRestricao: null);
    }

    public static bool ValidarCpfCnpj(string documentoLimpo)
    {
        if (string.IsNullOrWhiteSpace(documentoLimpo)) return false;

        if (documentoLimpo.Length == 11)
            return ValidarCpf(documentoLimpo);

        if (documentoLimpo.Length == 14)
            return ValidarCnpj(documentoLimpo);

        return false;
    }

    private static bool ValidarCpf(string cpf)
    {
        if (cpf.Length != 11) return false;

        // Rejeita sequências repetidas como 111.111.111-11
        if (cpf.Distinct().Count() == 1) return false;

        var multiplicador1 = new int[9] { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        var multiplicador2 = new int[10] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        var tempCpf = cpf.Substring(0, 9);
        var soma = 0;

        for (int i = 0; i < 9; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador1[i];

        var resto = soma % 11;
        var digito1 = resto < 2 ? 0 : 11 - resto;

        tempCpf += digito1;
        soma = 0;

        for (int i = 0; i < 10; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador2[i];

        resto = soma % 11;
        var digito2 = resto < 2 ? 0 : 11 - resto;

        return cpf.EndsWith(digito1.ToString() + digito2.ToString());
    }

    private static bool ValidarCnpj(string cnpj)
    {
        if (cnpj.Length != 14) return false;

        if (cnpj.Distinct().Count() == 1) return false;

        var multiplicador1 = new int[12] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var multiplicador2 = new int[13] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        var tempCnpj = cnpj.Substring(0, 12);
        var soma = 0;

        for (int i = 0; i < 12; i++)
            soma += int.Parse(tempCnpj[i].ToString()) * multiplicador1[i];

        var resto = (soma % 11);
        var digito1 = resto < 2 ? 0 : 11 - resto;

        tempCnpj += digito1;
        soma = 0;

        for (int i = 0; i < 13; i++)
            soma += int.Parse(tempCnpj[i].ToString()) * multiplicador2[i];

        resto = (soma % 11);
        var digito2 = resto < 2 ? 0 : 11 - resto;

        return cnpj.EndsWith(digito1.ToString() + digito2.ToString());
    }

    public static string FormatarCpfCnpj(string digitos)
    {
        if (digitos.Length == 11)
            return Convert.ToUInt64(digitos).ToString(@"000\.000\.000\-00");

        if (digitos.Length == 14)
            return Convert.ToUInt64(digitos).ToString(@"00\.000\.000\/0000\-00");

        return digitos;
    }
}
