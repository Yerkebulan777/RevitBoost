using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;


namespace CommonUtils
{
    public static class StringHelper
    {
        public static string ReplaceInvalidChars(string textLine)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();

            StringBuilder result = new(textLine.Length);

            if (!string.IsNullOrWhiteSpace(textLine))
            {
                textLine = textLine.TrimEnd('_');
                textLine = textLine.Normalize();

                foreach (char c in textLine)
                {
                    if (!invalidChars.Contains(c))
                    {
                        result.Append(c);
                    }
                }
            }

            return result.ToString();
        }


        public static string NormalizeLength(string textLine, int lenght = 100)
        {
            if (!string.IsNullOrEmpty(textLine) && textLine.Length > lenght)
            {
                int emptyIndex = textLine.LastIndexOf(string.Empty, lenght);

                if (emptyIndex != -1)
                {
                    textLine = $"{textLine.Substring(0, emptyIndex).Trim()}...";
                }
            }

            return textLine;
        }


        public static void CopyToClipboard(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка при копировании в буфер обмена: {ex.Message}");
                }
            }
        }
    }


}
