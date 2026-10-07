using System.Windows.Forms;
using Jarvis.UI;
using Jarvis.AI;
using Jarvis.Agent;
using Jarvis.Tools;

namespace Jarvis;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Monta as dependências uma vez. A janela não conhece o SDK do Google.
        var gemini = new GeminiService();
        var jarvis = new JarvisAgent(gemini, new FerramentasJarvis());
        Application.Run(new MainForm(jarvis, gemini));
    }
}
