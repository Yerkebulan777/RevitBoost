using Autodesk.Revit.UI;
using CommonUtils;
using RevitBoost.Commands;
using System.Reflection;

namespace RevitBoost
{
    /// <summary>
    ///     Application entry point
    /// </summary>

    public class Application : IExternalApplication
    {
        private void CreateRibbon(UIControlledApplication application)
        {
            RibbonPanel panel = CreatePanel(application, "Commands", "RevitBoost");

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

        public RibbonPanel CreatePanel(UIControlledApplication application, string tabName, string panelName)
        {
            foreach (RibbonPanel panel in application.GetRibbonPanels(Tab.AddIns))
            {
                if (panel.Name == panelName)
                {
                    return panel;
                }
            }

            return application.CreateRibbonPanel(tabName, panelName);
        }


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
    }

    /// <summary>
    /// Расширения для работы с Nice3point.Revit.Toolkit командами
    /// </summary>
    public static class RibbonExtensions
    {
        /// <summary>
        /// Добавляет кнопку для команды, наследующейся от Nice3point ExternalCommand
        /// </summary>
        /// <typeparam name="TCommand">Тип команды из Nice3point toolkit</typeparam>
        /// <param name="panel">Панель ленты Revit</param>
        /// <param name="buttonText">Текст, отображаемый на кнопке</param>
        /// <returns>Созданная кнопка</returns>
        public static PushButton AddPushButton<TCommand>(this RibbonPanel panel, string buttonText) where TCommand : IExternalCommand, new()
        {
            Type commandType = typeof(TCommand);

            // Создаем уникальное имя для кнопки на основе полного имени типа
            string buttonName = commandType.FullName;

            // Получаем путь к сборке, содержащей команду
            string assemblyPath = Assembly.GetAssembly(commandType).Location;

            // Создаем данные для кнопки с правильными параметрами
            PushButtonData buttonData = new(
                buttonName,        // Внутреннее имя кнопки (должно быть уникальным)
                buttonText,        // Отображаемый на кнопке текст
                assemblyPath,      // Путь к DLL файлу с командой
                commandType.FullName // Полное имя класса команды
            );

            // Добавляем кнопку на панель и приводим результат к нужному типу
            return panel.AddItem(buttonData) as PushButton;
        }
    }

}