using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;

namespace SentinelViewer
{
    static class StudioDependencies
    {
        const string Library = "CodeWalker.Core.dll";
        const string Download = "https://files.gta5-mods.com/uploads/codewalker-gtav-interactive-3d-map/1cfcd8-CodeWalker30_dev46.zip";
        const string LibrarySha256 = "3D2BB91B35CF0D1888C12772A9AF25441ADEA3CCC8510A149FCF0D47C8E71CA8";
        internal static string RuntimeDirectory(string applicationDirectory)
        {
            string runtime = Path.Combine(applicationDirectory, "runtime");
            return File.Exists(Path.Combine(runtime, "SharpDX.dll")) ? runtime : applicationDirectory;
        }

        static string LibraryPath
        {
            get { return Path.Combine(RuntimeDirectory(AppDomain.CurrentDomain.BaseDirectory), Library); }
        }

        internal static void RegisterResolver()
        {
            string directory = RuntimeDirectory(AppDomain.CurrentDomain.BaseDirectory);
            if (directory == AppDomain.CurrentDomain.BaseDirectory) return;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string name = new AssemblyName(args.Name).Name;
                if (name != "CodeWalker.Core" && name != "SharpDX" && name != "SharpDX.Mathematics") return null;
                string path = Path.Combine(directory, name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
        }

        internal static void PrepareForLaunch()
        {
            if (File.Exists(LibraryPath)) return;
            using (var form = new System.Windows.Forms.Form())
            {
                form.Text = "Egorika Vehicle Studio";
                form.ClientSize = new System.Drawing.Size(320, 80);
                form.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                form.ControlBox = false;
                form.Controls.Add(new System.Windows.Forms.Label
                {
                    Text = Ui.Text("Загрузка CodeWalker при первом запуске…", "Downloading CodeWalker for first launch…"),
                    AutoSize = true, Left = 12, Top = 14
                });
                form.Controls.Add(new System.Windows.Forms.ProgressBar
                {
                    Left = 12, Top = 40, Width = 296, Style = System.Windows.Forms.ProgressBarStyle.Marquee
                });
                Exception failure = null;
                form.Shown += async (sender, args) =>
                {
                    try { await System.Threading.Tasks.Task.Run((Action)EnsureCodeWalker); }
                    catch (Exception ex) { failure = ex; }
                    finally { form.Close(); }
                };
                form.ShowDialog();
                if (failure != null) throw new InvalidOperationException(failure.Message, failure);
            }
        }

        internal static void EnsureCodeWalker()
        {
            string target = LibraryPath;
            if (File.Exists(target)) return;

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string pending = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var response = WebRequest.CreateHttp(Download).GetResponse())
                using (var source = response.GetResponseStream())
                using (var data = new MemoryStream())
                {
                    var buffer = new byte[81920];
                    for (int count; (count = source.Read(buffer, 0, buffer.Length)) > 0;)
                    {
                        if (data.Length + count > 16 * 1024 * 1024)
                            throw new InvalidDataException("CodeWalker download exceeded the expected size.");
                        data.Write(buffer, 0, count);
                    }
                    data.Position = 0;
                    using (var archive = new ZipArchive(data, ZipArchiveMode.Read))
                    {
                        var entry = archive.Entries.Single(file => file.Name == Library);
                        if (entry.Length > 16 * 1024 * 1024) throw new InvalidDataException("Unexpected CodeWalker library size.");
                        using (var input = entry.Open())
                        using (var output = File.Create(pending))
                            input.CopyTo(output);
                    }
                }
                using (var sha = SHA256.Create())
                using (var file = File.OpenRead(pending))
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                    if (hash != LibrarySha256) throw new InvalidDataException("CodeWalker SHA-256 verification failed.");
                }
                try { File.Move(pending, target); }
                catch (IOException) { if (!File.Exists(target)) throw; }
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }
}
