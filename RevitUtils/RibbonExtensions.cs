using Autodesk.Revit.UI;
using System.Reflection;

namespace RevitUtils
{
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
