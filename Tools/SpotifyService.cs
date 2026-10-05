using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Jarvis.Tools;

public static class SpotifyService
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr janela);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr janela);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr janela, int comando);
    public static Task<string> PesquisarAsync(string pesquisa)
    {
        try
        {
            var endereco = $"spotify:search:{Uri.EscapeDataString(pesquisa)}";

            Process.Start(new ProcessStartInfo
            {
                FileName = endereco,
                UseShellExecute = true
            });

            return Task.FromResult(
                $"Abertura da pesquisa solicitada ao Spotify: {pesquisa}. "
                + "A tela não foi verificada e nenhuma reprodução foi solicitada."
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                $"Falha ao abrir a pesquisa do Spotify: {ex.Message}"
            );
        }
    }
    public static Task<string> TocarAsync(string pesquisa)
    {
        var tarefa = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        var thread = new Thread(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "spotify:",
                    UseShellExecute = true
                });

                IntPtr janela = IntPtr.Zero;

                // Espera até dez segundos pela janela do Spotify.
                for (int tentativa = 0; tentativa < 40; tentativa++)
                {
                    foreach (var processo in Process.GetProcessesByName("Spotify"))
                    {
                        using (processo)
                        {
                            if (processo.MainWindowHandle != IntPtr.Zero)
                            {
                                janela = processo.MainWindowHandle;
                                break;
                            }
                        }
                    }

                    if (janela != IntPtr.Zero)
                        break;

                    Thread.Sleep(250);
                }

                if (janela == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "Não encontrei a janela do Spotify."
                    );

                if (IsIconic(janela))
                    ShowWindow(janela, 9); // Restaura a janela minimizada.

                SetForegroundWindow(janela);
                Thread.Sleep(500);

                void Enviar(string teclas)
                {
                    if (GetForegroundWindow() != janela)
                        throw new InvalidOperationException(
                            "O Spotify perdeu o foco. Automação interrompida."
                        );

                    SendKeys.SendWait(teclas);
                }

                Enviar("^k"); // Ctrl+K: busca rápida.
                Thread.Sleep(500);

                Enviar("^a"); // Seleciona uma pesquisa anterior.

                // Envia caracteres especiais como texto, não como atalhos.
                foreach (char caractere in pesquisa)
                {
                    var texto = caractere switch
                    {
                        '+' or '^' or '%' or '~' or '(' or ')' or '[' or ']'
                            => "{" + caractere + "}",
                        '{' => "{{}",
                        '}' => "{}}",
                        _ => caractere.ToString()
                    };

                    Enviar(texto);
                }

                Thread.Sleep(1500); // Dá tempo para os resultados carregarem.

                Enviar("+{ENTER}"); // Shift+Enter.
                Thread.Sleep(300);
                Enviar("{ESC}");

                tarefa.SetResult(
                    $"Sequência de reprodução enviada ao Spotify para: {pesquisa}. "
                    + "O início do áudio e a faixa escolhida não foram verificados."
                );
            }
            catch (Exception ex)
            {
                tarefa.SetResult(
                    $"Falha na automação do Spotify: {ex.Message} "
                    + "Não confirme reprodução."
                );
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tarefa.Task;
    }
}