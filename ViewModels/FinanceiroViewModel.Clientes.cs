using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GetStartedApp.Models;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class FinanceiroViewModel
{
    // =========================================================================
    // 4. GESTÃO CADASTRAL DE CLIENTES & LIMITES DE CREDIÁRIO (REV-003)
    // =========================================================================
    public ObservableCollection<Cliente> ListaClientes { get; } = [];

    [ObservableProperty]
    public partial string BuscaClienteFiltro { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Cliente? ClienteSelecionado { get; set; }

    [ObservableProperty]
    public partial StatusCreditoClienteDto? StatusCreditoClienteSelecionado { get; set; }

    // Modal de Cadastro / Edição de Cliente
    [ObservableProperty] public partial bool IsModalClienteAberto { get; set; }
    [ObservableProperty] public partial int? EditandoClienteId { get; set; }
    [ObservableProperty] public partial string ClienteNomeInput { get; set; } = string.Empty;
    [ObservableProperty] public partial string ClienteCpfCnpjInput { get; set; } = string.Empty;
    [ObservableProperty] public partial string ClienteTelefoneInput { get; set; } = string.Empty;
    [ObservableProperty] public partial string ClienteEmailInput { get; set; } = string.Empty;
    [ObservableProperty] public partial string ClienteEnderecoInput { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal ClienteLimiteCreditoInput { get; set; } = 500m;
    [ObservableProperty] public partial bool ClienteBloqueadoInput { get; set; }
    [ObservableProperty] public partial string ClienteMotivoBloqueioInput { get; set; } = string.Empty;
    [ObservableProperty] public partial string MensagemErroClienteModal { get; set; } = string.Empty;

    partial void OnBuscaClienteFiltroChanged(string value) => _ = CarregarClientesAsync();

    partial void OnClienteSelecionadoChanged(Cliente? value)
    {
        if (value != null)
        {
            _ = AtualizarStatusCreditoSelecionadoAsync(value.Id);
        }
        else
        {
            StatusCreditoClienteSelecionado = null;
        }
    }

    private async Task AtualizarStatusCreditoSelecionadoAsync(int clienteId)
    {
        if (_clienteService == null) return;
        try
        {
            StatusCreditoClienteSelecionado = await _clienteService.AvaliarCreditoAsync(clienteId, 0.01m);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao avaliar status de crédito do cliente {Id}", clienteId);
        }
    }

    [RelayCommand]
    public async Task CarregarClientesAsync()
    {
        if (_clienteService == null) return;

        try
        {
            ListaClientes.Clear();
            var lista = await _clienteService.PesquisarClientesAsync(BuscaClienteFiltro);
            foreach (var c in lista) ListaClientes.Add(c);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar clientes.");
            MensagemFeedback = $"❌ Erro ao carregar clientes: {ex.Message}";
        }
    }

    [RelayCommand]
    public void AbrirModalNovoCliente()
    {
        EditandoClienteId = null;
        ClienteNomeInput = string.Empty;
        ClienteCpfCnpjInput = string.Empty;
        ClienteTelefoneInput = string.Empty;
        ClienteEmailInput = string.Empty;
        ClienteEnderecoInput = string.Empty;
        ClienteLimiteCreditoInput = 500m;
        ClienteBloqueadoInput = false;
        ClienteMotivoBloqueioInput = string.Empty;
        MensagemErroClienteModal = string.Empty;
        IsModalClienteAberto = true;
    }

    [RelayCommand]
    public void AbrirModalEditarCliente(Cliente? cliente)
    {
        if (cliente == null) return;

        EditandoClienteId = cliente.Id;
        ClienteNomeInput = cliente.Nome;
        ClienteCpfCnpjInput = cliente.CpfCnpj ?? string.Empty;
        ClienteTelefoneInput = cliente.Telefone ?? string.Empty;
        ClienteEmailInput = cliente.Email ?? string.Empty;
        ClienteEnderecoInput = cliente.Endereco ?? string.Empty;
        ClienteLimiteCreditoInput = cliente.LimiteCredito;
        ClienteBloqueadoInput = cliente.Bloqueado;
        ClienteMotivoBloqueioInput = cliente.MotivoBloqueio ?? string.Empty;
        MensagemErroClienteModal = string.Empty;
        IsModalClienteAberto = true;
    }

    [RelayCommand]
    public void FecharModalCliente()
    {
        IsModalClienteAberto = false;
        MensagemErroClienteModal = string.Empty;
    }

    [RelayCommand]
    public async Task SalvarClienteAsync()
    {
        if (_clienteService == null)
        {
            MensagemErroClienteModal = "Serviço de clientes não disponível.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ClienteNomeInput))
        {
            MensagemErroClienteModal = "O nome do cliente é obrigatório.";
            return;
        }

        try
        {
            var c = new Cliente
            {
                Id = EditandoClienteId ?? 0,
                Nome = ClienteNomeInput.Trim(),
                CpfCnpj = string.IsNullOrWhiteSpace(ClienteCpfCnpjInput) ? null : ClienteCpfCnpjInput.Trim(),
                Telefone = string.IsNullOrWhiteSpace(ClienteTelefoneInput) ? null : ClienteTelefoneInput.Trim(),
                Email = string.IsNullOrWhiteSpace(ClienteEmailInput) ? null : ClienteEmailInput.Trim(),
                Endereco = string.IsNullOrWhiteSpace(ClienteEnderecoInput) ? null : ClienteEnderecoInput.Trim(),
                LimiteCredito = Math.Max(0, ClienteLimiteCreditoInput),
                Bloqueado = ClienteBloqueadoInput,
                MotivoBloqueio = ClienteBloqueadoInput ? ClienteMotivoBloqueioInput : null
            };

            await _clienteService.SalvarClienteAsync(c);
            MensagemFeedback = $"✅ Cliente '{c.Nome}' salvo com sucesso!";
            FecharModalCliente();
            await CarregarClientesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar cliente.");
            MensagemErroClienteModal = $"Erro ao salvar: {ex.Message}";
        }
    }
}
