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
            RibbonPanel panel = Application.CreatePanel("Commands", "RevitBoost");

            PushButton lintelButton = panel.AddPushButton<LintelLabelingCommand>("Lintel Assignment");
            PushButton levelButton = panel.AddPushButton<LevelAssignmentCommand>("Level Assignment");

            BitmapImage smallIcon = LoadEmbeddedIcon("RibbonIcon16.png");
            BitmapImage largeIcon = LoadEmbeddedIcon("RibbonIcon32.png");

            if (smallIcon != null && largeIcon != null)
            {
                levelButton.Image = smallIcon;
                lintelButton.Image = smallIcon;
                levelButton.LargeImage = largeIcon;
                lintelButton.LargeImage = largeIcon;
            }
        }

        private BitmapImage LoadEmbeddedIcon(string resourceName)
        {
            try
            {
                // Получаем текущую сборку
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

                string fullResourceName = $"RevitBoost.Resources.Icons.{resourceName}";

                using Stream stream = assembly.GetManifestResourceStream(fullResourceName);

                if (stream != null)
                {
                    BitmapImage bitmap = new();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Важно для потокобезопасности
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading embedded icon {resourceName}: {ex.Message}");
            }

            return null;
        }



    }
}