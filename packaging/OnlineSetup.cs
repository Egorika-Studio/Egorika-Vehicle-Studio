using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

internal sealed class OnlineSetup
{
    private enum Step { Options, Installing, Complete, Failed }

    private readonly Window window;
    private readonly string scratch;
    private Step step;
    private string destination;
    private string log;

    [STAThread]
    private static void Main()
    {
        string scratch = Path.Combine(Path.GetTempPath(), "EgorikaVehicleSetup-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(scratch);
            var app = new Application();
            app.Run(new OnlineSetup(scratch).window);
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Egorika Vehicle Studio Setup", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            try { Directory.Delete(scratch, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private OnlineSetup(string scratch)
    {
        this.scratch = scratch;
        using (Stream layout = Resource("layout"))
            window = (Window)XamlReader.Load(layout);

        Extract("regular", Path.Combine(scratch, "SourceSans3-Regular.ttf"));
        Extract("light", Path.Combine(scratch, "SourceSans3-Light.ttf"));
        var fonts = new Uri(scratch + Path.DirectorySeparatorChar);
        window.FontFamily = new FontFamily(fonts, "./SourceSans3-Regular.ttf#Source Sans 3");
        Text("Heading").FontFamily = new FontFamily(fonts, "./SourceSans3-Light.ttf#Source Sans 3 Light");

        var wordmark = new BitmapImage();
        using (Stream image = Resource("wordmark"))
        {
            wordmark.BeginInit();
            wordmark.CacheOption = BitmapCacheOption.OnLoad;
            wordmark.StreamSource = image;
            wordmark.EndInit();
        }
        Control<Image>("Wordmark").Source = wordmark;

        string registeredFolder = Microsoft.Win32.Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\{5CE9BB33-9DE7-480D-9DA3-147BF8114533}_is1",
            "Inno Setup: App Path", null) as string;
        Control<TextBox>("Folder").Text = registeredFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Egorika Vehicle Studio");
        ComboBox language = Control<ComboBox>("Language");
        language.SelectedIndex = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? 1 : 0;
        language.SelectionChanged += delegate { Render(); };
        Control<Button>("Browse").Click += Browse;
        Control<Button>("Primary").Click += Advance;
        Control<Button>("Back").Click += delegate { step = Step.Options; Render(); };
        window.Closing += delegate(object sender, CancelEventArgs args) { args.Cancel = step == Step.Installing; };
        Render();
    }

    private T Control<T>(string name) where T : class { return (T)window.FindName(name); }
    private TextBlock Text(string name) { return Control<TextBlock>(name); }
    private bool Russian { get { return Control<ComboBox>("Language").SelectedIndex == 1; } }
    private string Label(string english, string russian) { return Russian ? russian : english; }
    private static Visibility Show(bool visible) { return visible ? Visibility.Visible : Visibility.Collapsed; }

    private void Render()
    {
        Text("OptionsLabel").Text = Label("Options", "Параметры");
        Text("InstallingLabel").Text = Label("Installing", "Установка");
        Text("ResultLabel").Text = Label("Result", "Результат");
        Highlight("Options", step == Step.Options);
        Highlight("Installing", step == Step.Installing);
        Highlight("Result", step == Step.Complete || step == Step.Failed);
        Control<StackPanel>("Options").Visibility = Show(step == Step.Options);
        Control<ComboBox>("Language").IsEnabled = step != Step.Installing;
        Control<ProgressBar>("Progress").Visibility = Show(step == Step.Installing);
        Control<CheckBox>("Launch").Visibility = Show(step == Step.Complete);
        Control<Button>("Back").Visibility = Show(step == Step.Failed);
        Control<Button>("Primary").IsEnabled = step != Step.Installing;

        Text("FolderLabel").Text = Label("Installation folder", "Папка установки");
        Control<Button>("Browse").Content = Label("Choose…", "Выбрать…");
        Control<CheckBox>("Desktop").Content = Label("Create a desktop shortcut", "Создать ярлык на рабочем столе");
        Control<CheckBox>("Launch").Content = Label("Launch Egorika Vehicle Studio", "Запустить Egorika Vehicle Studio");
        Control<Button>("Back").Content = Label("Options", "Параметры");

        switch (step)
        {
            case Step.Options:
                Text("Heading").Text = Label("Welcome to Vehicle Studio", "Добро пожаловать в Vehicle Studio");
                Control<Button>("Primary").Content = Label("Install", "Установить");
                Text("Status").Text = "";
                break;
            case Step.Installing:
                Text("Heading").Text = Label("Installing Vehicle Studio", "Установка Vehicle Studio");
                Control<Button>("Primary").Content = Label("Installing…", "Установка…");
                Text("Status").Text = Label("Downloading, verifying and installing files…", "Скачиваем, проверяем и устанавливаем файлы…");
                break;
            case Step.Complete:
                Text("Heading").Text = Label("Installation complete", "Установка завершена");
                Control<Button>("Primary").Content = Label("Finish", "Готово");
                Text("Status").Text = destination;
                break;
            case Step.Failed:
                Text("Heading").Text = Label("Installation failed", "Не удалось установить программу");
                Control<Button>("Primary").Content = Label("Retry", "Повторить");
                Text("Status").Text = Label("Installation log: ", "Журнал установки: ") + log;
                break;
        }
    }

    private void Highlight(string name, bool active)
    {
        Control<Border>(name + "Tab").Background = active
            ? new SolidColorBrush(Color.FromRgb(42, 56, 50)) : Brushes.Transparent;
        Text(name + "Label").Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(170, 180, 176));
    }

    private void Browse(object sender, RoutedEventArgs args)
    {
        using (var dialog = new Forms.FolderBrowserDialog())
        {
            dialog.Description = Label("Choose the installation folder", "Выберите папку установки");
            dialog.SelectedPath = Control<TextBox>("Folder").Text;
            if (dialog.ShowDialog() == Forms.DialogResult.OK)
                Control<TextBox>("Folder").Text = dialog.SelectedPath;
        }
    }

    private async void Advance(object sender, RoutedEventArgs args)
    {
        if (step == Step.Complete)
        {
            try
            {
                if (Control<CheckBox>("Launch").IsChecked == true)
                    Process.Start(new ProcessStartInfo(Path.Combine(destination, "EgorikaVehicleStudio.exe")) { WorkingDirectory = destination });
                window.Close();
            }
            catch (Exception error) { Text("Status").Text = error.Message; }
            return;
        }

        try
        {
            string folder = Control<TextBox>("Folder").Text.Trim();
            if (!Path.IsPathRooted(folder) || folder.Contains("\""))
                throw new ArgumentException(Label("Choose a full installation path.", "Укажите полный путь к папке установки."));
            destination = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(destination, Path.GetPathRoot(destination).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(Label("Choose a folder inside the drive.", "Выберите папку внутри диска."));
        }
        catch (Exception error)
        {
            Text("Status").Text = error.Message;
            return;
        }

        string engine = Path.Combine(scratch, "InstallEngine.exe");
        log = Path.Combine(Path.GetTempPath(), "EgorikaVehicleSetup-" + Guid.NewGuid().ToString("N") + ".log");
        string arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=\"" + destination
            + "\" /LOG=\"" + log + "\" /LANG=" + (Russian ? "russian" : "english")
            + (Control<CheckBox>("Desktop").IsChecked == true ? " /TASKS=desktopicon" : " /TASKS=");
        step = Step.Installing;
        Render();
        try
        {
            int exitCode = await Task.Run(() =>
            {
                Extract("engine", engine);
                using (Process process = Process.Start(new ProcessStartInfo(engine, arguments)
                {
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    process.WaitForExit();
                    return process.ExitCode;
                }
            });
            step = exitCode == 0 ? Step.Complete : Step.Failed;
            Render();
            if (step == Step.Failed)
                Text("Status").Text = Label("Setup exit code: ", "Код ошибки установки: ") + exitCode + Environment.NewLine + Text("Status").Text;
        }
        catch (Exception error)
        {
            step = Step.Failed;
            Render();
            Text("Status").Text = error.Message;
        }
    }

    private static Stream Resource(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }
    private static void Extract(string name, string path)
    {
        using (Stream source = Resource(name))
        using (FileStream target = File.Create(path))
            source.CopyTo(target);
    }
}
