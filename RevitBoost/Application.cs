using Autodesk.Revit.UI;
using CommonUtils;
using Nice3point.Revit.Toolkit.External;
using RevitBoost.Commands;
using System.IO;
using System.Windows.Media.Imaging;
using PushButton = Autodesk.Revit.UI.PushButton;
using RibbonPanel = Autodesk.Revit.UI.RibbonPanel;

namespace RevitBoost
{
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
            ResourceDiagnostic.ShowCompleteResourceInfo();

            RibbonPanel panel = Application.CreatePanel("Commands", "RevitBoost");

            var lintelButton = panel.AddPushButton<LintelLabelingCommand>("Lintel Assignment");
            var levelButton = panel.AddPushButton<LevelAssignmentCommand>("Level Assignment");

            var smallIcon = IconHelper.GetIcon("RibbonIcon16.png");
            var largeIcon = IconHelper.GetIcon("RibbonIcon32.png");

            if (smallIcon != null && largeIcon != null)
            {
                levelButton.Image = smallIcon;
                lintelButton.Image = smallIcon;
                levelButton.LargeImage = largeIcon;
                lintelButton.LargeImage = largeIcon;
            }
            else
            {
                TaskDialog.Show("RevitBoost", "Icons not found, using default Revit icons.");
            }
        }



    }
}