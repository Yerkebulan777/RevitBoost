using Autodesk.Revit.UI;
using CommonUtils;
using RevitBoost.Commands;
using RevitUtils;
using System.Diagnostics;

namespace RevitBoost
{
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                Host.Start();
                CreateRibbon(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                StringHelper.CopyToClipboard($"Не удалось загрузить RevitBoost: {ex.Message}");
                _ = TaskDialog.Show("Ошибка загрузки", $"Не удалось загрузить RevitBoost: {ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            Host.Stop();
            return Result.Succeeded;
        }

        private static void CreateRibbon(UIControlledApplication application)
        {
            try
            {
                RibbonPanel panel = RibbonHelper.CreatePanel(application, "Commands", "RevitBoost");

                PushButton lintelButton = panel.AddPushButton<LintelLabelingCommand>("Lintel assignment");
                PushButton levelButton = panel.AddPushButton<LevelAssignmentCommand>("Level assignment");
                PushButton exportPdfButton = panel.AddPushButton<ExportToPdfCommand>("Export to PDF");

                try
                {
                    System.Windows.Media.Imaging.BitmapImage smallIcon = IconHelper.GetSmallIcon();
                    System.Windows.Media.Imaging.BitmapImage largeIcon = IconHelper.GetLargeIcon();

                    if (smallIcon != null && largeIcon != null)
                    {
                        levelButton.Image = smallIcon;
                        lintelButton.Image = smallIcon;
                        exportPdfButton.Image = smallIcon;
                        levelButton.LargeImage = largeIcon;
                        lintelButton.LargeImage = largeIcon;
                        exportPdfButton.LargeImage = largeIcon;
                    }
                }
                catch (Exception iconEx)
                {
                    Debug.WriteLine($"Не удалось загрузить иконки: {iconEx.Message}");
                }
            }
            catch (Exception ex)
            {
                _ = TaskDialog.Show("Ошибка создания ribbon", ex.Message);
                throw;
            }
        }

    }
}