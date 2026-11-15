using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace SentinelViewer
{
    static class Ui
    {
        static readonly string LanguagePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SentinelViewer", "language.txt");
        internal static readonly KeyValuePair<string, string>[] Languages =
        {
            new KeyValuePair<string, string>("ru", "Русский"),
            new KeyValuePair<string, string>("en", "English"),
            new KeyValuePair<string, string>("es", "Español"),
            new KeyValuePair<string, string>("fr", "Français"),
            new KeyValuePair<string, string>("pt-BR", "Português (Brasil)")
        };
        internal static string Language { get; private set; }
        static readonly Dictionary<string, string[]> translations = LoadTranslations();
        static Ui() { Language = ReadLanguage(); }
        internal static void UseEnglishForCheck() { Language = "en"; }

        static string ReadLanguage()
        {
            try
            {
                if (File.Exists(LanguagePath))
                {
                    string code = File.ReadAllText(LanguagePath).Trim();
                    if (Languages.Any(language => language.Key == code)) return code;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            var system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (system == "pt") return "pt-BR";
            return Languages.Any(language => language.Key == system) ? system : "en";
        }

        static Dictionary<string, string[]> LoadTranslations()
        {
            var items = new Dictionary<string, string[]>(StringComparer.Ordinal);
            using (var stream = typeof(Ui).Assembly.GetManifestResourceStream("Translations.txt"))
            using (var reader = new StreamReader(stream))
            {
                for (string line; (line = reader.ReadLine()) != null;)
                {
                    if (line.Length == 0) continue;
                    var fields = line.Split('¦');
                    if (fields.Length == 4) items.Add(fields[0], fields);
                }
            }
            return items;
        }

        public static string Text(string russian, string english) { return Translate(Language, russian, english); }

        internal static string Translate(string code, string russian, string english)
        {
            if (code == "ru") return russian;
            int column = code == "es" ? 1 : code == "fr" ? 2 : code == "pt-BR" ? 3 : 0;
            string[] row;
            // Keep whitespace around fragments composed with file names, counts and error messages.
            string key = english.Trim();
            if (column == 0 || !translations.TryGetValue(key, out row)) return english;
            int start = english.Length - english.TrimStart().Length;
            return english.Substring(0, start) + row[column] + english.Substring(start + key.Length);
        }

        internal static void ChangeLanguage(string code)
        {
            if (Language == code) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LanguagePath));
                File.WriteAllText(LanguagePath, code);
                MessageBox.Show(Translate(code, "Перезапустите программу, чтобы применить язык.",
                    "Restart the application to apply the language."), "Egorika Vehicle Studio");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Egorika Vehicle Studio"); }
        }

    }
}
