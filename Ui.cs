using System;
using System.IO;
using System.Windows;

namespace SentinelViewer
{
    static class Ui
    {
        static readonly string LanguagePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SentinelViewer", "language.txt");
        public static bool English { get; private set; }

        static Ui() { English = ReadLanguage(); }

        internal static void UseEnglishForCheck() { English = true; }

        static bool ReadLanguage()
        {
            try { return File.Exists(LanguagePath) && File.ReadAllText(LanguagePath).Trim() == "en"; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public static string Text(string russian, string english) { return English ? english : russian; }

        public static void ChangeLanguage()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LanguagePath));
                File.WriteAllText(LanguagePath, English ? "ru" : "en");
                MessageBox.Show(Text("English selected. Restart the application.", "Выбран русский. Перезапустите программу."),
                    "Egorika Vehicle Studio");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Egorika Vehicle Studio"); }
        }
    }
}
