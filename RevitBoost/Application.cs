using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using RevitBoost.Commands;

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
        button.SetImage("/RevitBoost;component/Resources/Icons/RibbonIcon16.png");
        button.SetLargeImage("/RevitBoost;component/Resources/Icons/RibbonIcon32.png");
    }

}