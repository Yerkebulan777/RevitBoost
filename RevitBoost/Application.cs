using Autodesk.Revit.UI;
using CommonUtils;
using RevitBoost.Commands;
using RevitUtils;

namespace RevitBoost
{
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            Host.Start();
            CreateRibbon(application);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            Host.Stop();
            return Result.Succeeded;
        }

        private static void CreateRibbon(UIControlledApplication application)
        {
            RibbonPanel panel = RibbonHelper.CreatePanel(application, "Commands", "RevitBoost");

            PushButton lintelButton = panel.AddPushButton<LintelLabelingCommand>("Lintel Assignment");
            PushButton levelButton = panel.AddPushButton<LevelAssignmentCommand>("Level Assignment");

            string testResult = IconHelper.TestIconAvailability();
            System.Diagnostics.Debug.WriteLine(testResult);

            // Загружаем иконки используя правильные имена ресурсов
            System.Windows.Media.Imaging.BitmapImage smallIcon = IconHelper.GetSmallIcon();
            System.Windows.Media.Imaging.BitmapImage largeIcon = IconHelper.GetLargeIcon();

            if (smallIcon != null && largeIcon != null)
            {
                // Применяем иконки к кнопкам
                levelButton.Image = smallIcon;
                lintelButton.Image = smallIcon;
                levelButton.LargeImage = largeIcon;
                lintelButton.LargeImage = largeIcon;

                System.Diagnostics.Debug.WriteLine("🎉 Иконки успешно применены к кнопкам ribbon!");

                // Опционально: показываем успех пользователю
                _ = TaskDialog.Show("Успех", "Иконки успешно загружены и применены!");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Не все иконки удалось загрузить, используются стандартные");

                // Можно показать пользователю информацию о проблеме
                string message = "Некоторые иконки не удалось загрузить:\n" + testResult;
                _ = TaskDialog.Show("Информация об иконках", message);
            }
        }

    }
}