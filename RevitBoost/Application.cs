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

            try
            {
                // Загружаем иконки из embedded ресурсов
                BitmapImage smallIcon = LoadEmbeddedImage("/RevitBoost;component/Resources/Icons/RibbonIcon16.png");
                BitmapImage largeIcon = LoadEmbeddedImage("/RevitBoost;component/Resources/Icons/RibbonIcon32.png");

                if (smallIcon != null && largeIcon != null)
                {
                    lintelButton.Image = smallIcon;
                    lintelButton.LargeImage = largeIcon;

                    levelButton.Image = smallIcon;
                    levelButton.LargeImage = largeIcon;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading icons: {ex.Message}");
                // Плагин продолжит работать без иконок
            }
        }

        private BitmapImage LoadEmbeddedImage(string resourcePath)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,{resourcePath}", UriKind.Absolute);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // Важно для многопоточности
                return bitmap;
            }
            catch
            {
                return null;
            }
        }



    }
}