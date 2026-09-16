using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GetStartedApp.ViewModels;
using Serilog;

namespace GetStartedApp.Views;

public partial class PdvView : UserControl
{
    public PdvView()
    {
        AvaloniaXamlLoader.Load(this);

        this.AddHandler(InputElement.KeyDownEvent, PdvView_KeyDownTunnel, RoutingStrategies.Tunnel);

        this.AttachedToVisualTree += (s, e) =>
        {
            var txt = this.FindControl<TextBox>("TxtPesquisa");
            txt?.Focus();
        };
    }

    private async void PdvView_KeyDownTunnel(object? sender, KeyEventArgs e)
    {
        Log.Information("[PDV TECLADO] Tecla detectada no PdvView: {Key}, PhysicalKey: {PhysKey}", e.Key, e.PhysicalKey);

        if (e.Key == Key.F2)
        {
            var txtPesquisa = this.FindControl<TextBox>("TxtPesquisa");
            txtPesquisa?.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            Log.Information("[PDV TECLADO] ENTER/RETURN interceptado no PdvView!");

            var txtPesquisa = this.FindControl<TextBox>("TxtPesquisa");
            if (this.DataContext is PdvViewModel vm)
            {
                // Sincroniza o texto diretamente do TextBox se não estiver vazio
                if (txtPesquisa != null && !string.IsNullOrWhiteSpace(txtPesquisa.Text))
                {
                    vm.TextoPesquisa = txtPesquisa.Text;
                }

                Log.Information("[PDV TECLADO] Disparando LancarPrimeiroResultadoCommand com texto '{Texto}'", vm.TextoPesquisa);
                await vm.LancarPrimeiroResultadoCommand.ExecuteAsync(null);

                if (txtPesquisa != null)
                {
                    txtPesquisa.Text = string.Empty;
                    txtPesquisa.Focus();
                }

                e.Handled = true;
            }
        }
    }
}
