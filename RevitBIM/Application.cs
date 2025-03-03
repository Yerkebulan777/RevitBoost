using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using RevitBIM.Commands;

namespace RevitBIM;

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
        RibbonPanel panel = Application.CreatePanel("Commands", "RevitBIM");
        PushButton button = panel.AddPushButton<StartupCommand>("Execute");
        button.SetImage("/RevitBIM;component/Resources/Icons/RibbonIcon16.png");
        button.SetLargeImage("/RevitBIM;component/Resources/Icons/RibbonIcon32.png");
    }
}