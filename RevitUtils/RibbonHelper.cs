using Autodesk.Revit.UI;
using System.Reflection;

namespace RevitUtils
{
    public static class RibbonHelper
    {
        public static RibbonPanel CreatePanel(UIControlledApplication application, string panelName)
        {
            foreach (RibbonPanel panel in application.GetRibbonPanels(Tab.AddIns))
            {
                if (panel.Name == panelName)
                {
                    return panel;
                }
            }

            return application.CreateRibbonPanel(panelName);
        }


        /// <summary>
        /// Добавляет кнопку для команды, наследующейся от Nice3point ExternalCommand
        /// </summary>
        public static PushButton AddPushButton<TCommand>(this RibbonPanel panel, string buttonText) where TCommand : IExternalCommand, new()
        {
            Type commandType = typeof(TCommand);
            string buttonName = commandType.FullName;

            // Безопасное получение пути к сборке
            Assembly assembly = Assembly.GetAssembly(commandType);
            string assemblyPath = assembly?.Location ?? assembly?.CodeBase;

            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new InvalidOperationException($"Не удалось определить путь к сборке для команды {commandType.Name}");
            }

            PushButtonData buttonData = new(
                buttonName,
                buttonText,
                assemblyPath,
                commandType.FullName
            );

            return panel.AddItem(buttonData) as PushButton;
        }
    }
}
