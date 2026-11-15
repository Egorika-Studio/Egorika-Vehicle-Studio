using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;

namespace SentinelViewer
{
    static class StudioUpdate
    {
        internal const string Repository = "Egorika-Studio/Egorika-Vehicle-Studio";
        const string AssetName = "EgorikaVehicleStudio.exe";
        internal static readonly Version Current = typeof(StudioUpdate).Assembly.GetName().Version;
        const string ReadyMarker = "ready.sha256";
        internal static bool Pending { get; private set; }

        internal sealed class Release
        {
            internal Version Version;
            internal Uri Download;
            internal string Hash;
            internal int Size;
        }

        internal static Release FindRelease()
        {
            using (var client = Client())
                return ParseRelease(client.DownloadString(new Uri("https://api.github.com/repos/" + Repository + "/releases/latest")));
        }

        internal static Release ParseRelease(string json)
        {
            var release = new JavaScriptSerializer().Deserialize<ReleaseMetadata>(json);
            string tag = release == null ? null : release.tag_name;
            Version version;
            if (String.IsNullOrEmpty(tag) || !Regex.IsMatch(tag, @"\Av\d+\.\d+\.\d+\z") ||
                !Version.TryParse(tag.Substring(1) + ".0", out version) || version <= Current) return null;

            var asset = (release.assets ?? new AssetMetadata[0]).SingleOrDefault(item => item.name == AssetName);
            string url = "https://github.com/" + Repository + "/releases/download/" + tag + "/" + AssetName;
            if (asset == null || asset.browser_download_url != url || asset.size <= 0 || asset.size > 16 * 1024 * 1024 ||
                asset.digest == null || !Regex.IsMatch(asset.digest, @"\Asha256:[a-fA-F0-9]{64}\z"))
                throw new InvalidDataException("The GitHub release has no verifiable Windows update.");
            return new Release { Version = version, Download = new Uri(url), Hash = asset.digest.Substring(7), Size = asset.size };
        }

        sealed class ReleaseMetadata
        {
            public string tag_name { get; set; }
            public AssetMetadata[] assets { get; set; }
        }

        sealed class AssetMetadata
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
            public string digest { get; set; }
            public int size { get; set; }
        }

        static WebClient Client()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "EgorikaVehicleStudio/" + Current.ToString(3);
            return client;
        }

        internal static void Stage(Release release)
        {
            string directory = StudioDependencies.RuntimeDirectory(Path.GetDirectoryName(ExecutablePath()));
            string folder = Path.Combine(directory, ".egorika-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string candidate = Path.Combine(folder, AssetName);
            try
            {
                using (var client = Client())
                using (var download = client.OpenRead(release.Download))
                using (var output = File.Create(candidate))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = download.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (output.Length + read > release.Size)
                            throw new InvalidDataException("The update exceeds the published file size.");
                        output.Write(buffer, 0, read);
                    }
                }
                var assembly = AssemblyName.GetAssemblyName(candidate);
                if (new FileInfo(candidate).Length != release.Size || !HashMatches(candidate, release.Hash) ||
                    assembly.Name != "EgorikaVehicleStudio" || assembly.Version != release.Version)
                    throw new InvalidDataException("The update did not match the GitHub release.");
                string marker = Path.Combine(folder, ReadyMarker);
                File.WriteAllText(marker + ".tmp", release.Hash);
                File.Move(marker + ".tmp", marker);
                Pending = true;
            }
            catch
            {
                Directory.Delete(folder, true);
                throw;
            }
        }

        static string ExecutablePath() { return typeof(StudioUpdate).Assembly.Location; }

        static bool HashMatches(string path, string expected)
        {
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
                return String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static bool TryApplyOnRestart()
        {
            string exe = ExecutablePath();
            string runtime = StudioDependencies.RuntimeDirectory(Path.GetDirectoryName(exe));
            if (!String.Equals(Path.GetFileName(exe), AssetName, StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                foreach (string folder in Directory.EnumerateDirectories(runtime, ".egorika-update-*"))
                {
                    string marker = Path.Combine(folder, ReadyMarker);
                    if (!File.Exists(marker)) continue;
                    string hash = File.ReadAllText(marker);
                    string candidate = Path.Combine(folder, AssetName);
                    if (!Regex.IsMatch(hash, @"\A[a-fA-F0-9]{64}\z") || !File.Exists(candidate) ||
                        AssemblyName.GetAssemblyName(candidate).Version <= Current) continue;
                    string helper = Path.Combine(folder, "apply-update.exe");
                    File.Move(marker, marker + ".applying");
                    try
                    {
                        File.Copy(exe, helper, true);
                        using (var process = Process.GetCurrentProcess())
                            Process.Start(new ProcessStartInfo(helper,
                                "--apply-studio-update " + process.Id + " \"" + exe + "\" " + hash)
                            { UseShellExecute = false, CreateNoWindow = true });
                        return true;
                    }
                    catch
                    {
                        File.Move(marker + ".applying", marker);
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Vehicle Studio update: " + ex.Message);
            }
            return false;
        }

        internal static int Apply(string pid, string target, string hash)
        {
            string staging = Path.GetDirectoryName(ExecutablePath());
            string candidate = Path.Combine(staging, AssetName);
            string directory = StudioDependencies.RuntimeDirectory(Path.GetDirectoryName(target));
            if (!String.Equals(Path.GetDirectoryName(staging), directory, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(staging).StartsWith(".egorika-update-", StringComparison.Ordinal) ||
                !Regex.IsMatch(hash, @"\A[a-fA-F0-9]{64}\z")) return 1;
            string backup = Path.Combine(staging, "previous.exe");
            try
            {
                Process original = null;
                try { original = Process.GetProcessById(Int32.Parse(pid)); }
                catch (ArgumentException) { }
                if (original != null)
                    using (original)
                        if (!original.WaitForExit(30000)) throw new IOException("The editor did not exit.");

                var replacement = AssemblyName.GetAssemblyName(candidate);
                if (!HashMatches(candidate, hash) || replacement.Name != "EgorikaVehicleStudio" ||
                    replacement.Version <= AssemblyName.GetAssemblyName(target).Version)
                    throw new InvalidDataException("The staged update is invalid or already installed.");
                File.Replace(candidate, target, backup);
                try
                {
                    Process.Start(new ProcessStartInfo(target) { WorkingDirectory = Path.GetDirectoryName(target), UseShellExecute = false });
                }
                catch
                {
                    File.Replace(backup, target, null);
                    throw;
                }
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(staging, "update-error.txt"), ex.Message);
                MessageBox.Show(ex.Message, "Egorika Vehicle Studio");
                return 1;
            }
        }
    }

    sealed partial class Viewer
    {
        bool checkingStudioUpdate;
        async void CheckForStudioUpdate() { await CheckStudioUpdate(false); }
        async void CheckForStudioUpdateOnStartup() { await CheckStudioUpdate(true); }

        async Task CheckStudioUpdate(bool automatic)
        {
            if (checkingStudioUpdate) return;
            if (StudioUpdate.Pending)
            {
                if (!automatic) ShowUpdateReady();
                return;
            }
            checkingStudioUpdate = true;
            try
            {
                var release = await Task.Run(() => StudioUpdate.FindRelease());
                if (release == null)
                {
                    if (!automatic) MessageBox.Show(this, Ui.Text("Новых версий нет.", "No new version is available.") +
                        " (" + StudioUpdate.Current.ToString(3) + ")", AppName);
                    return;
                }
                ShowUpdateNotice(Ui.Text("Доступно обновление ", "Update available: ") + release.Version.ToString(3));
                if (automatic) return;
                if (MessageBox.Show(this, Ui.Text("Скачать версию ", "Download version ") + release.Version.ToString(3) +
                    Ui.Text(" и установить при следующем запуске?", " and install it at the next launch?"),
                    AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                await Task.Run(() => StudioUpdate.Stage(release));
                ShowUpdateNotice(Ui.Text("Обновление готово к следующему запуску.", "Update ready for the next launch."));
                ShowUpdateReady();
            }
            catch (Exception ex)
            {
                if (!automatic) MessageBox.Show(this, Ui.Text("Не удалось обновиться. ", "Could not update. ") + ex.Message, AppName);
            }
            finally { checkingStudioUpdate = false; }
        }

        void ShowUpdateNotice(string text)
        {
            updateNotice.ToolTip = text;
            System.Windows.Automation.AutomationProperties.SetName(updateNotice, text);
            updateNotice.Visibility = Visibility.Visible;
        }

        void ShowUpdateReady()
        {
            MessageBox.Show(this, Ui.Text("Обновление готово. Оно установится при следующем запуске.",
                "The update is ready and will be installed at the next launch."), AppName);
        }
    }
}
