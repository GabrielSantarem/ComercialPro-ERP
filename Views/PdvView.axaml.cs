using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using GetStartedApp.ViewModels;

namespace GetStartedApp.Views;

public partial class PdvView : UserControl
{
    public PdvView()
    {
        AvaloniaXamlLoader.Load(this);

        // Capturar teclas globalmente quando a view estiver em foco (F2)
        this.KeyDown += PdvView_KeyDown;
    }

    private void PdvView_KeyDown(object? sender, KeyEventArgs e)
    {
        // Ao apertar F2, força o ponteiro a focar na Caixa de Pesquisa do código de barras / texto.
        if (e.Key == Key.F2)
        {
            var txtPesquisa = this.FindControl<TextBox>("TxtPesquisa");
            txtPesquisa?.Focus();
            e.Handled = true;
        }
    }
}