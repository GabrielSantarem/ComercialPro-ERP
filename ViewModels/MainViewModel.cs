using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace GetStartedApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [RelayCommand]
    private void NavigateToConfiguracoes() => CurrentPage = _services.GetRequiredService<ConfiguracoesViewModel>();
    private readonly IServiceProvider _services;

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; } = null!;

    public MainViewModel(IServiceProvider services)
    {
        _services = services;
        // Iniciar já abrindo o PDV para testes
        NavigateToPdv();
    }

    [RelayCommand]
    private void NavigateToDashboard() => CurrentPage = _services.GetRequiredService<DashboardViewModel>();

    [RelayCommand]
    private void NavigateToPdv() 
    {
        var vm = _services.GetRequiredService<PdvViewModel>();
        _ = vm.InicializarAsync();
        CurrentPage = vm;
    }

    [RelayCommand]
    private void NavigateToEstoque() => CurrentPage = _services.GetRequiredService<EstoqueViewModel>();
}
