using Autodesk.Revit.UI;
using CommonUtils;

namespace RevitUtils;

public static class DialogHelper
{
    /// <summary>
    /// Отображает результат с копированием в буфер обмена
    /// </summary>
    public static void ShowInfo(string title, string message)
    {
        StringHelper.CopyToClipboard(message);

        TaskDialog dialog = new(title)
        {
            MainContent = message,
            MainIcon = TaskDialogIcon.TaskDialogIconInformation,
            CommonButtons = TaskDialogCommonButtons.Ok
        };

        dialog.Show();
    }

    /// <summary>
    /// Отображает ошибку
    /// </summary>
    public static void ShowError(string title, string message)
    {
        StringHelper.CopyToClipboard(message);
        TaskDialog dialog = new(title)
        {
            MainContent = message,
            MainIcon = TaskDialogIcon.TaskDialogIconWarning,
            CommonButtons = TaskDialogCommonButtons.Ok
        };

        dialog.Show();
    }


}