using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using RevitBoost.Commands;
using System.Windows.Media.Imaging;

namespace RevitBoost;

/// <summary>
///     Application entry point
/// </summary>
[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        Host.Start();
        CreateRibbon();
    }

    public override void OnShutdown()
    {
        Host.Stop();
    }

    private void CreateRibbon()
    {
        RibbonPanel panel = Application.CreatePanel("Commands", "RevitBoost");
        PushButton button = panel.AddPushButton<StartupCommand>("Execute");

        Uri smallImageUri = new Uri("pack://application:,,,/RevitBoost;component/Resources/Icons/RibbonIcon16.png");
        Uri largeImageUri = new Uri("pack://application:,,,/RevitBoost;component/Resources/Icons/RibbonIcon32.png");

        button.Image = new BitmapImage(smallImageUri);
        button.LargeImage = new BitmapImage(largeImageUri);
    }



}