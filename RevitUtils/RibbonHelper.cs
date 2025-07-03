using Autodesk.Revit.UI;
using System.Reflection;

namespace RevitUtils
{
    public static class RibbonHelper
    {
        public static PushButton AddPushButton<TCommand>(this RibbonPanel panel, string buttonText) where TCommand : IExternalCommand, new()
        {
            Type commandType = typeof(TCommand);
            string buttonName = commandType.FullName;
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


        public static RibbonPanel CreatePanel(UIControlledApplication application, string panelName, string tabName)
        {
            RibbonPanel resultPanel = null;
            application.CreateRibbonTab(tabName);

            foreach (RibbonPanel ribbonPanel in application.GetRibbonPanels(tabName))
            {
                if (ribbonPanel.Name.Equals(panelName))
                {
                    resultPanel = ribbonPanel;
                    break;
                }
            }

            return resultPanel ?? application.CreateRibbonPanel(tabName, panelName);
        }


        public static PulldownButton AddPullDownButton(this RibbonPanel panel, string internalName, string buttonText)
        {
            PulldownButtonData itemData = new PulldownButtonData(internalName, buttonText);
            return (PulldownButton)panel.AddItem(itemData);
        }


        public static SplitButton AddSplitButton(this RibbonPanel panel, string internalName, string buttonText)
        {
            SplitButtonData itemData = new SplitButtonData(internalName, buttonText);
            return (SplitButton)panel.AddItem(itemData);
        }


        public static RadioButtonGroup AddRadioButtonGroup(this RibbonPanel panel, string internalName)
        {
            RadioButtonGroupData itemData = new RadioButtonGroupData(internalName);
            return (RadioButtonGroup)panel.AddItem(itemData);
        }


        public static ComboBox AddComboBox(this RibbonPanel panel, string internalName)
        {
            ComboBoxData itemData = new ComboBoxData(internalName);
            return (ComboBox)panel.AddItem(itemData);
        }

    }
}
