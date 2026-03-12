using System.Text;

namespace Utf8Converter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Required for proper Windows Forms DPI awareness on modern Windows
        ApplicationConfiguration.Initialize();

        // Register Windows-1250, 1252, ISO-8859-* and all other code pages
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Application.Run(new MainForm());
    }
}
