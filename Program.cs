using System.Windows.Forms;
using Jarvis.UI;

namespace Jarvis;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        Application.Run(new MainForm());
    }
}