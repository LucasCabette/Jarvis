using System.Drawing;
using System.Windows.Forms;
using Jarvis.AI;

namespace Jarvis.UI;

public class MainForm : Form
{
    private readonly GeminiService _gemini;

    private readonly RichTextBox _historico;
    private readonly TextBox _mensagem;
    private readonly Button _enviar;
    private readonly Label _status;

    private bool _processando;

    public MainForm()
    {
        Text = "Jarvis";
        Width = 900;
        Height = 650;
        MinimumSize = new Size(600, 450);
        StartPosition = FormStartPosition.CenterScreen;

        _gemini = new GeminiService();

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

        _status = new Label
        {
            Text = "Pronto",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };

        var entrada = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };

        entrada.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100)
        );

        entrada.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 100)
        );

        entrada.Controls.Add(_mensagem, 0, 0);
        entrada.Controls.Add(_enviar, 1, 0);

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

        Shown += (_, _) => _mensagem.Focus();
    }

    private async Task EnviarMensagemAsync()
    {
        if (_processando)
            return;

        var texto = _mensagem.Text.Trim();

        if (string.IsNullOrWhiteSpace(texto))
            return;

        _processando = true;

        _enviar.Enabled = false;
        _mensagem.Enabled = false;
        _status.Text = "Processando...";

        AdicionarMensagem("Você", texto);
        _mensagem.Clear();

        try
        {
            var resposta = await _gemini.EnviarMensagem(texto);

            if (IsDisposed || Disposing)
                return;

            AdicionarMensagem("Jarvis", resposta);
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