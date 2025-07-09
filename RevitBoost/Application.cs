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
                TaskDialog.Show("Ошибка загрузки", $"Не удалось загрузить RevitBoost: {ex.Message}");
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

                PushButton lintelButton = panel.AddPushButton<LintelLabelingCommand>("Lintel Assignment");
                PushButton levelButton = panel.AddPushButton<LevelAssignmentCommand>("Level Assignment");

                try
                {
                    var smallIcon = IconHelper.GetSmallIcon();
                    var largeIcon = IconHelper.GetLargeIcon();

                    if (smallIcon != null && largeIcon != null)
                    {
                        levelButton.Image = smallIcon;
                        lintelButton.Image = smallIcon;
                        levelButton.LargeImage = largeIcon;
                        lintelButton.LargeImage = largeIcon;
                    }
                }
                catch (Exception iconEx)
                {
                    Debug.WriteLine($"Не удалось загрузить иконки: {iconEx.Message}");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка создания ribbon", ex.Message);
                throw;
            }
        }

    }
}