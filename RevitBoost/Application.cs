using Nice3point.Revit.Toolkit.External;
using RevitBoost.Commands;
using System.IO;
using System.Windows.Media.Imaging;


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
            Autodesk.Revit.UI.RibbonPanel panel = Application.CreatePanel("Commands", "RevitBoost");

            Autodesk.Revit.UI.PushButton lintelButton = panel.AddPushButton<LintelLabelingCommand>("Lintel Assignment");

            Autodesk.Revit.UI.PushButton levelButton = panel.AddPushButton<LevelAssignmentCommand>("Level Assignment");

            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Icons");

            Uri smallImageUri = new(Path.Combine(basePath, "RibbonIcon16.png"), UriKind.RelativeOrAbsolute);
            Uri largeImageUri = new(Path.Combine(basePath, "RibbonIcon32.png"), UriKind.RelativeOrAbsolute);

            lintelButton.Image = new BitmapImage(smallImageUri);
            lintelButton.LargeImage = new BitmapImage(largeImageUri);

            levelButton.Image = new BitmapImage(smallImageUri);
            levelButton.LargeImage = new BitmapImage(largeImageUri);
        }



    }
}