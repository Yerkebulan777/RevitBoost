using Autodesk.Revit.UI;
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
            RibbonPanel panel = Application.CreatePanel("Commands", "RevitBoost");
            PushButton button = panel.AddPushButton<LintelLabelingCommand>("Execute");
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Icons");
            Uri smallImageUri = new Uri(Path.Combine(basePath, "RibbonIcon16.png"), UriKind.RelativeOrAbsolute);
            Uri largeImageUri = new Uri(Path.Combine(basePath, "RibbonIcon32.png"), UriKind.RelativeOrAbsolute);

            button.Image = new BitmapImage(smallImageUri);
            button.LargeImage = new BitmapImage(largeImageUri);
        }
    }
}