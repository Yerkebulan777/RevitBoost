using Autodesk.Revit.UI;
using CommonUtils;
using Nice3point.Revit.Toolkit.External;
using RevitBoost.Commands;

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

            // Тестируем доступность иконок для диагностики
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