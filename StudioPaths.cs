using System;
using System.IO;

namespace SentinelViewer
{
    static class StudioPaths
    {
        internal static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
        internal static readonly string Thumbnails = Path.Combine(Root, "thumbnails");
        static readonly string LegacyRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SentinelViewer");

        internal static string Setting(string name)
        {
            string path = Path.Combine(Root, "settings", name);
            ImportLegacy(Path.Combine(LegacyRoot, name), path);
            return path;
        }

        internal static string ModThumbnail(string filename)
        {
            string path = Path.Combine(Thumbnails, "mods", filename);
            ImportLegacy(Path.Combine(LegacyRoot, "thumbnails", filename), path);
            return path;
        }

        static void ImportLegacy(string source, string path)
        {
            if (File.Exists(path) || !File.Exists(source)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Copy(source, path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
