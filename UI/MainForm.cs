using Jarvis.Audio;
using System.Drawing;
using System.Windows.Forms;
using Jarvis.Agent;

namespace Jarvis.UI;

public class MainForm : Form
{
    private readonly JarvisAgent _jarvis;
    private readonly ITranscritorAudio _transcritor;

    private readonly RichTextBox _historico;
    private readonly TextBox _mensagem;
    private readonly Button _enviar;
    private readonly Label _status;
    private bool _processando;
    private readonly GravadorMicrofone _gravador = new();
    private readonly Button _microfone;
    private bool _capturando;
    private VozService? _voz;
    private readonly Button _pararVoz;


    public MainForm(JarvisAgent jarvis, ITranscritorAudio transcritor)
    {
        Text = "Jarvis";
        Width = 900;
        Height = 650;
        MinimumSize = new Size(600, 450);
        StartPosition = FormStartPosition.CenterScreen;

        _jarvis = jarvis;
        _transcritor = transcritor;

        try
        {
            _voz = new VozService();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"O chat continuará funcionando sem voz.\n\n{ex.Message}",
                "Voz indisponível",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        _historico = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 11),
            DetectUrls = false
        };

        _mensagem = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11),
            PlaceholderText = "Digite sua mensagem..."
        };

        _enviar = new Button
        {
            Text = "Enviar",
            Dock = DockStyle.Fill
        };

        _microfone = new Button
        {
            Text = "Falar",
            Dock = DockStyle.Fill
        };

        _pararVoz = new Button
        {
            Text = "Parar voz",
            Dock = DockStyle.Fill,
            Enabled = _voz is not null
        };

        _status = new Label
        {
            Text = "Pronto",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };

        var entrada = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty
        };

        entrada.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100)
        );

        entrada.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 100)
        );

        entrada.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 100)
        );

        entrada.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 100)
        );

        entrada.Controls.Add(_mensagem, 0, 0);
        entrada.Controls.Add(_microfone, 1, 0);
        entrada.Controls.Add(_enviar, 2, 0);
        entrada.Controls.Add(_pararVoz, 3, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100)
        );

        layout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100)
        );

        layout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 44)
        );

        layout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 30)
        );

        layout.Controls.Add(_historico, 0, 0);
        layout.Controls.Add(entrada, 0, 1);
        layout.Controls.Add(_status, 0, 2);

        Controls.Add(layout);

        AcceptButton = _enviar;

        _enviar.Click += async (_, _) =>
        {
            await EnviarMensagemAsync();
        };

        _microfone.Click += async (_, _) =>
        {
            await AlternarMicrofoneAsync();
        };

        _pararVoz.Click += (_, _) =>
        {
            _voz?.Parar();
        };

        FormClosed += (_, _) =>
        {
            _gravador.Dispose();
            _voz?.Dispose();
        };

        Shown += (_, _) => _mensagem.Focus();
    }

    private async Task AlternarMicrofoneAsync()
    {
        if (_processando)
            return;

        if (!_capturando)
        {
            try
            {
                _voz?.Parar();
                _gravador.Iniciar();

                _capturando = true;
                _microfone.Text = "Parar";
                _enviar.Enabled = false;
                _mensagem.Enabled = false;
                _status.Text = "Gravando... clique em Parar ao terminar.";
            }
            catch (Exception ex)
            {
                AdicionarMensagem(
                    "Sistema",
                    $"Não consegui iniciar o microfone: {ex.Message}"
                );
            }

            return;
        }

        _microfone.Enabled = false;
        _status.Text = "Finalizando gravação...";

        try
        {
            var audio = await _gravador.PararAsync();

            if (IsDisposed || Disposing)
                return;

        _status.Text = "Transcrevendo...";

        var transcricao = await _transcritor.TranscreverAudioAsync(audio);

        if (IsDisposed || Disposing)
            return;

        if (string.IsNullOrWhiteSpace(transcricao))
        {
            AdicionarMensagem(
                "Sistema",
                "Não identifiquei fala compreensível. Tente novamente."
            );
        }
        else
        {
            // O campo atual tem apenas uma linha.
            var texto = transcricao
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            // Preserva um texto que você já estivesse escrevendo.
            _mensagem.Text = string.IsNullOrWhiteSpace(_mensagem.Text)
                ? texto
                : $"{_mensagem.Text.TrimEnd()} {texto}";

            _mensagem.SelectionStart = _mensagem.TextLength;
        }

        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
            {
                AdicionarMensagem(
                    "Sistema",
                    $"Falha na gravação ou transcrição: {ex.Message}"
                );
            }
        }
        finally
        {
            _capturando = false;

            if (!IsDisposed && !Disposing)
            {
                _microfone.Text = "Falar";
                _microfone.Enabled = true;
                _enviar.Enabled = true;
                _mensagem.Enabled = true;
                _status.Text = "Pronto";

                if (Form.ActiveForm == this)
                    _mensagem.Focus();
            }
        }
    }
    

    private async Task EnviarMensagemAsync()
    {
        if (_processando || _capturando)
            return;
        
        var texto = _mensagem.Text.Trim();

        if (string.IsNullOrWhiteSpace(texto))
            return;

        _processando = true;

        _enviar.Enabled = false;
        _microfone.Enabled = false;
        _mensagem.Enabled = false;
        _status.Text = "Processando...";

        AdicionarMensagem("Você", texto);
        _mensagem.Clear();

        try
        {
            _voz?.Parar();
            var resposta = await _jarvis.EnviarMensagemAsync(texto);

            if (IsDisposed || Disposing)
                return;

            AdicionarMensagem("Jarvis", resposta);
            try
            {
                _voz?.Falar(resposta);
            }
            catch (Exception ex)
            {
                AdicionarMensagem(
                    "Sistema",
                    $"A resposta chegou, mas não consegui lê-la: {ex.Message}"
                );
            }
        }
        catch (Exception)
        {
            if (!IsDisposed && !Disposing)
            {
                AdicionarMensagem(
                    "Sistema",
                    "Não foi possível concluir o envio."
                );
            }
        }
        finally
        {
            _processando = false;

            if (!IsDisposed && !Disposing)
            {
                _enviar.Enabled = true;
                _microfone.Enabled = true;
                _mensagem.Enabled = true;
                _status.Text = "Pronto";

                // Evita tomar o foco se o Spotify estiver na frente.
                if (Form.ActiveForm == this)
                    _mensagem.Focus();
            }
        }
    }

    private void AdicionarMensagem(string autor, string texto)
    {
        _historico.AppendText($"{autor}:\n{texto}\n\n");

        _historico.SelectionStart = _historico.TextLength;
        _historico.ScrollToCaret();
    }
}
