using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;

namespace SentinelViewer
{
    sealed partial class Viewer
    {
        const string ProjectFilter = "Egorika Vehicle Project (*.egvehicle)|*.egvehicle";
        static readonly string RecentPath = StudioPaths.Setting("recent-projects.txt");
        string projectPath;

        async Task<bool> EnsureArchives()
        {
            if (gameTextures != null) return true;
            bool wasLoading = loading;
            loading = true;
            try
            {
                string folder = File.Exists(GameSettings) ? File.ReadAllText(GameSettings).Trim() : null;
                if (String.IsNullOrWhiteSpace(folder) || !File.Exists(Path.Combine(folder, "GTA5.exe"))) folder = ChooseGame();
                if (folder == null) return false;
                status.Text = Ui.Text("Читаю оглавления архивов GTA V…", "Indexing GTA V archives…");
                gameTextures = await Task.Run(() => new GameTextures(folder));
                gameFolder = folder;
                SaveGameFolder(folder);
                return true;
            }
            catch (Exception ex) { status.Text = ex.Message; return false; }
            finally { loading = wasLoading; }
        }

        async Task ConnectSavedArchives()
        {
            if (gameTextures != null || !File.Exists(GameSettings)) return;
            string folder;
            try { folder = File.ReadAllText(GameSettings).Trim(); }
            catch (IOException) { return; }
            if (!File.Exists(Path.Combine(folder, "GTA5.exe"))) return;
            try
            {
                gameTextures = await Task.Run(() => new GameTextures(folder));
                gameFolder = folder;
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }

        static string[] RecentProjects()
        {
            try { return File.Exists(RecentPath) ? File.ReadAllLines(RecentPath).Where(File.Exists).Take(8).ToArray() : new string[0]; }
            catch (IOException) { return new string[0]; }
            catch (UnauthorizedAccessException) { return new string[0]; }
        }

        static void RememberProject(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RecentPath));
                File.WriteAllLines(RecentPath, new[] { path }.Concat(RecentProjects()).Distinct(StringComparer.OrdinalIgnoreCase).Take(8));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        void ShowProjectLauncher()
        {
            var launch = new Window { Owner = this, Title = Ui.Text("Проекты", "Projects") + " · " + AppName, Width = 610, Height = 400,
                ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Icon = Icon };
            StudioTheme.Apply(launch);
            var root = new DockPanel { Background = StudioTheme.Background, Margin = new Thickness(20) };
            launch.Content = root;
            var hero = new StackPanel { Background = StudioTheme.Panel, Margin = new Thickness(0, 0, 0, 16) };
            hero.Children.Add(new Image { Source = Icon, Width = 52, Height = 52, Margin = new Thickness(0, 12, 0, 4) });
            hero.Children.Add(new TextBlock { Text = AppName, FontSize = 28, FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 12) });
            DockPanel.SetDock(hero, Dock.Top);
            root.Children.Add(hero);
            var columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            root.Children.Add(columns);
            var actions = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            columns.Children.Add(actions);
            actions.Children.Add(new TextBlock { Text = Ui.Text("Проект", "Project"), Margin = new Thickness(0, 0, 0, 8) });
            actions.Children.Add(Button(Ui.Text("Создать проект", "New project"), () => { launch.Close(); NewProject(); }));
            actions.Children.Add(Button(Ui.Text("Открыть проект…", "Open project…"), () => { launch.Close(); PickProject(); }));
            var recentColumn = new DockPanel { Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(recentColumn, 1);
            columns.Children.Add(recentColumn);
            var recentTitle = new TextBlock { Text = Ui.Text("Недавние проекты", "Recent projects"), Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(recentTitle, Dock.Top);
            recentColumn.Children.Add(recentTitle);
            var recent = new StackPanel();
            recentColumn.Children.Add(new ScrollViewer { Content = recent, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var paths = RecentProjects();
            if (paths.Length == 0) recent.Children.Add(new TextBlock { Text = Ui.Text("Пока нет сохранённых проектов", "No saved projects yet") });
            foreach (string path in paths)
            {
                var item = Button(Path.GetFileNameWithoutExtension(path), () => { launch.Close(); OpenProject(path); });
                item.ToolTip = path;
                recent.Children.Add(item);
            }
            launch.ShowDialog();
        }

        void NewProject()
        {
            if (loading) return;
            if (vehicle != null && MessageBox.Show(this, Ui.Text("Создать новый проект и закрыть текущий?",
                "Create a new project and close the current one?"), AppName, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            CloseTuningEditor();
            projectPath = null;
            currentNormal = null; currentHigh = null; currentDictionaries = null;
            autoCarcols = null; sourceArchives = null; packageFolder = null;
            projectParts.Clear(); projectVariations = null; manualLiveryPath = null;
            vehicle = null;
            wireMeshes.Clear(); tuningChoices.Clear(); hidden.Clear(); controls.Clear(); boneParents.Clear();
            isolatedBone = null; isolatedTuning = null; isolatedCenter = null; previousLod = null;
            tree.Items.Clear(); tuningTree.Items.Clear(); levels.Items.Clear(); scene.Children.Clear();
            spawnColors.ItemsSource = null; liveries.ItemsSource = null;
            foreach (var field in paintFields.Values) field.Clear();
            renderMode.SelectedIndex = 3;
            xray.IsChecked = showFloorGrid.IsChecked = showSceneAxes.IsChecked = showViewportInfo.IsChecked = false;
            showStatistics.IsChecked = showOrigin.IsChecked = showSky.IsChecked = false;
            showHoverOutline.IsChecked = true;
            foreach (var axis in visibleAxes) axis.IsChecked = true;
            gridScale.Value = 1;
            gridExtent.Value = 8;
            viewportInfo.Visibility = Visibility.Collapsed;
            outlineModels.Clear(); DrawTuningOutline();
            environmentChoice.SelectedIndex = 0;
            clock.Value = 780; reflect.Value = 0.45;
            aoEnabled.IsChecked = true; hideChildren.IsChecked = false;
            projection.IsChecked = false;
            ResetCamera();
            Title = AppName;
            status.Text = Ui.Text("Новый проект. Выберите модель в меню Import.", "New project. Select a model from Import.");
        }

        void PickProject()
        {
            if (loading) return;
            var dialog = new OpenFileDialog { Filter = ProjectFilter };
            if (dialog.ShowDialog(this) == true) OpenProject(dialog.FileName);
        }

        static XElement Attr(string name, object value) { return new XElement(name, new XAttribute("value", value ?? "")); }
        static string ReadAttr(XElement element, string name)
        {
            var node = element.Element(name);
            return node == null ? null : (string)node.Attribute("value");
        }

        static int IntAttr(XElement element, string name, int fallback)
        {
            int value;
            return Int32.TryParse(ReadAttr(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        static double DoubleAttr(XElement element, string name, double fallback)
        {
            double value;
            return Double.TryParse(ReadAttr(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                !Double.IsNaN(value) && !Double.IsInfinity(value) ? value : fallback;
        }

        async void SaveProject(bool saveAs) { await SaveProjectAsync(saveAs, null); }

        async Task SaveProjectAsync(bool saveAs, string explicitPath)
        {
            if (loading || vehicle == null)
            {
                status.Text = Ui.Text("Для сохранения сначала откройте модель.", "Open a vehicle before saving a project.");
                return;
            }
            string destination = explicitPath ?? projectPath;
            if (destination == null || saveAs)
            {
                var dialog = new SaveFileDialog { Filter = ProjectFilter, DefaultExt = ".egvehicle",
                    FileName = Path.GetFileNameWithoutExtension(currentNormal) + ".egvehicle" };
                if (dialog.ShowDialog(this) != true) return;
                destination = dialog.FileName;
            }
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (!ReadyEditor()) return;
                var draft = tuningEditor.CaptureProject();
                if (draft == null) return;
                string normal = currentNormal, high = currentHigh;
                string[] ytd = currentDictionaries ?? new string[0];
                string originalPackage = packageFolder;
                var root = new XElement("EgorikaVehicleProject", new XAttribute("version", 1),
                    Attr("lod", IsIsolated ? previousLod : levels.SelectedItem), Attr("render", renderMode.SelectedIndex), Attr("time", clock.Value),
                    Attr("xray", xray.IsChecked == true), Attr("grid", showFloorGrid.IsChecked == true),
                    Attr("axes", showSceneAxes.IsChecked == true), Attr("viewportInfo", showViewportInfo.IsChecked == true),
                    Attr("statistics", showStatistics.IsChecked == true), Attr("origin", showOrigin.IsChecked == true),
                    Attr("sky", showSky.IsChecked == true), Attr("hoverOutline", showHoverOutline.IsChecked == true),
                    Attr("axisMask", String.Concat(visibleAxes.Select(axis => axis.IsChecked == true ? "1" : "0"))),
                    Attr("gridScale", gridScale.Value), Attr("gridExtent", gridExtent.Value),
                    Attr("reflections", reflect.Value), Attr("environment", environmentChoice.SelectedIndex),
                    Attr("ao", aoEnabled.IsChecked == true), Attr("children", hideChildren.IsChecked == true),
                    Attr("projection", projection.IsChecked == true), Attr("yaw", yaw), Attr("pitch", pitch),
                    Attr("distance", IsIsolated ? previousDistance : distance),
                    Attr("free", freeCamera.IsChecked == true), Attr("flyYaw", flyYaw), Attr("flyPitch", flyPitch),
                    Attr("flyX", freePosition.X), Attr("flyY", freePosition.Y), Attr("flyZ", freePosition.Z),
                    Attr("unbound", hideUnboundGeometry),
                    Attr("panX", orbitPan.X), Attr("panY", orbitPan.Y), Attr("panZ", orbitPan.Z), Attr("game", gameTextures != null),
                    Attr("rpf", sourceArchives == null ? null : sourceArchives.OriginRpf),
                    Attr("versionPath", sourceArchives == null ? null : sourceArchives.SelectedPath),
                    Attr("package", originalPackage),
                    Attr("livery", liveries.SelectedItem == null ? null : ((LiveryOption)liveries.SelectedItem).Model),
                    new XElement("colors", paintFields.Select(field => new XElement("color", new XAttribute("layer", field.Key), field.Value.Text))),
                    new XElement("hidden", hidden.Select(tag => new XElement("bone", tag))),
                    new XElement("enabled", tuningChoices.Values.Where(choice => choice.Enabled).Select(choice =>
                        new XElement("part", Array.IndexOf(tuningEditor.PreviewParts, choice.Part)))),
                    new XElement("layout", panes.Select(pane => new XElement("pane", new XAttribute("floating", pane.Floating != null), pane.Position)),
                        Attr("column", workspace.ColumnDefinitions[0].ActualWidth /
                            Math.Max(1, workspace.ColumnDefinitions[0].ActualWidth + workspace.ColumnDefinitions[2].ActualWidth)),
                        Attr("row", workspace.RowDefinitions[0].ActualHeight /
                            Math.Max(1, workspace.RowDefinitions[0].ActualHeight + workspace.RowDefinitions[2].ActualHeight))),
                    draft);
                string[] partPaths = tuningEditor.PreviewParts.Select(part => part.FilePath).ToArray();
                string[] support = LocalProjectParts().ToArray();
                string manualLivery = manualLiveryPath;
                string variations = projectVariations ?? Path.Combine(Path.GetDirectoryName(normal), "carvariations.meta");
                loading = true;
                await Task.Run(() =>
                {
                    using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
                    {
                        int number = 0;
                        Func<string, string> store = path =>
                        {
                            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
                            string name = "assets/" + (++number).ToString(CultureInfo.InvariantCulture) + "/" + Path.GetFileName(path);
                            zip.CreateEntryFromFile(path, name, CompressionLevel.NoCompression);
                            return name;
                        };
                        root.Add(Attr("model", store(normal)), Attr("high", store(high)), Attr("variations", store(variations)), Attr("manualLivery", store(manualLivery)),
                            new XElement("textures", ytd.Select(path => Attr("file", store(path)))),
                            new XElement("partAssets", partPaths.Select(path => Attr("file", store(path)))),
                            new XElement("support", support.Select(path => Attr("file", store(path)))));
                        var entry = zip.CreateEntry("project.xml", CompressionLevel.Optimal);
                        using (var output = entry.Open()) new XDocument(root).Save(output);
                    }
                    if (File.Exists(destination)) File.Replace(temporary, destination, null);
                    else File.Move(temporary, destination);
                });
                projectPath = destination;
                if (explicitPath == null) RememberProject(destination);
                Title = AppName + " · " + Path.GetFileNameWithoutExtension(destination);
                status.Text = Ui.Text("Проект сохранён: ", "Project saved: ") + destination;
            }
            catch (Exception ex) { status.Text = ex.Message; }
            finally
            {
                loading = false;
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
            }
        }

        string projectVariations;
        Dictionary<string, string> projectParts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> LocalProjectParts()
        {
            var parts = tuningEditor.PreviewParts;
            var names = new HashSet<string>(parts.Select(part => part.Model).Concat(parts.SelectMany(part => TuningEditor.Names(part.Linked))),
                StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var folders = new[] { Path.GetDirectoryName(currentNormal),
                tuningEditor.ImportedPath == null ? null : Path.GetDirectoryName(tuningEditor.ImportedPath), packageFolder };
            foreach (var path in projectParts.Values)
                if (seen.Add(path) && File.Exists(path)) yield return path;
            foreach (string folder in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
                foreach (string path in Directory.EnumerateFiles(folder, "*.yft",
                    folder == packageFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    if (names.Contains(Path.GetFileNameWithoutExtension(path)) && seen.Add(path)) yield return path;
        }

        sealed class ProjectLoad
        {
            public XElement Root;
            public string Model, High, Variations, ManualLivery;
            public string[] Textures, Parts, Support;
        }

        static ProjectLoad ExtractProject(string path, string directory, List<string> created)
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                var manifest = zip.GetEntry("project.xml");
                if (manifest == null) throw new InvalidDataException(Ui.Text("В архиве нет проекта.", "The archive has no project."));
                XElement root;
                using (var stream = manifest.Open())
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                    root = XElement.Load(reader);
                if (root.Name != "EgorikaVehicleProject" || (int?)root.Attribute("version") != 1)
                    throw new InvalidDataException(Ui.Text("Версия проекта не поддерживается.", "Unsupported project version."));
                Directory.CreateDirectory(directory);
                created.Add(directory);
                Func<string, string> extract = value =>
                {
                    if (String.IsNullOrEmpty(value)) return null;
                    var pieces = value.Split('/');
                    int number;
                    if (pieces.Length != 3 || pieces[0] != "assets" ||
                        !Int32.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out number) ||
                        Path.GetFileName(pieces[2]) != pieces[2] || pieces[2] == "." || pieces[2] == ".." ||
                        pieces[2].IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        throw new InvalidDataException("Invalid project asset path.");
                    var entry = zip.GetEntry(value);
                    if (entry == null) throw new InvalidDataException("Missing project asset: " + value);
                    var folder = Path.Combine(directory, number.ToString(CultureInfo.InvariantCulture));
                    if (!Directory.Exists(folder)) { Directory.CreateDirectory(folder); created.Add(folder); }
                    var file = Path.Combine(folder, pieces[2]);
                    entry.ExtractToFile(file, true);
                    return file;
                };
                Func<string, string[]> files = name => root.Element(name) == null ? new string[0] :
                    root.Element(name).Elements("file").Select(item => extract((string)item.Attribute("value"))).ToArray();
                return new ProjectLoad { Root = root, Model = extract(ReadAttr(root, "model")), High = extract(ReadAttr(root, "high")),
                    Variations = extract(ReadAttr(root, "variations")), ManualLivery = extract(ReadAttr(root, "manualLivery")), Textures = files("textures"), Parts = files("partAssets"), Support = files("support") };
            }
        }

        async void OpenProject(string path) { await OpenProjectAsync(path, true); }

        async Task OpenProjectAsync(string path, bool remember)
        {
            if (loading) return;
            loading = true;
            status.Text = Ui.Text("Открываю проект…", "Opening project…");
            var created = new List<string>();
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), "EgorikaVehicle-" + Guid.NewGuid().ToString("N"));
                var data = await Task.Run(() => ExtractProject(path, directory, created));
                var root = data.Root;
                if (data.Model == null) throw new InvalidDataException(Ui.Text("В проекте нет модели.", "The project has no vehicle."));
                if (ReadAttr(root, "game") == "true" && gameTextures == null)
                    await EnsureArchives();
                string rpf = ReadAttr(root, "rpf");
                var source = !String.IsNullOrWhiteSpace(rpf) && File.Exists(rpf)
                    ? await Task.Run(() => GameTextures.OpenRpf(rpf)) : gameTextures;
                if (source != null) source.SelectVersion(ReadAttr(root, "versionPath"));
                var textures = data.Textures.Where(File.Exists).ToArray();
                var model = await Task.Run(() => new Vehicle(data.Model, data.High, textures, gameTextures ?? source, source));
                CloseTuningEditor();
                sourceArchives = source;
                packageFolder = Directory.Exists(ReadAttr(root, "package")) ? ReadAttr(root, "package") : null;
                autoCarcols = null;
                projectVariations = data.Variations;
                projectParts = data.Support.Where(File.Exists).GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                foreach (var field in paintFields.Values) field.Clear();
                Load(data.Model, data.High, textures, model, null, data.Variations);
                EnsureTuningEditor(null);
                tuningEditor.RestoreProject(root.Element("editor"), data.Parts, directory);
                PopulateProjectLiveries();
                loading = false;
                RestoreView(root);
                if (File.Exists(data.ManualLivery)) LoadManualLivery(data.ManualLivery);
                projectPath = path;
                Title = AppName + " · " + Path.GetFileNameWithoutExtension(path);
                if (remember) RememberProject(path);
            }
            catch (Exception ex) { status.Text = Ui.Text("Ошибка проекта: ", "Project error: ") + ex.Message; }
            finally
            {
                previewFolders.AddRange(created.AsEnumerable().Reverse());
                loading = false;
            }
        }

        void PopulateProjectLiveries()
        {
            var options = new List<LiveryOption> { new LiveryOption { Label = Ui.Text("Без ливреи", "No livery") } };
            options.AddRange(tuningEditor.PreviewParts.Where(part => part.Type == "VMT_LIVERY_MOD")
                .Select(part => new LiveryOption { Label = part.Model, Model = part.Model }));
            liveries.ItemsSource = options;
            liveries.SelectedIndex = 0;
        }

        void RestoreView(XElement root)
        {
            renderMode.SelectedIndex = Math.Max(0, Math.Min(3, IntAttr(root, "render", 3)));
            xray.IsChecked = ReadAttr(root, "xray") == "true";
            showFloorGrid.IsChecked = ReadAttr(root, "grid") == "true";
            showSceneAxes.IsChecked = ReadAttr(root, "axes") == "true";
            showViewportInfo.IsChecked = ReadAttr(root, "viewportInfo") == "true";
            showStatistics.IsChecked = ReadAttr(root, "statistics") == "true";
            showOrigin.IsChecked = ReadAttr(root, "origin") == "true";
            showSky.IsChecked = ReadAttr(root, "sky") == "true";
            showHoverOutline.IsChecked = ReadAttr(root, "hoverOutline") != "false";
            string axisMask = ReadAttr(root, "axisMask") ?? "";
            for (int i = 0; i < visibleAxes.Length; i++)
                visibleAxes[i].IsChecked = i >= axisMask.Length || axisMask[i] != '0';
            gridScale.Value = DoubleAttr(root, "gridScale", 1);
            gridExtent.Value = DoubleAttr(root, "gridExtent", 8);
            clock.Value = DoubleAttr(root, "time", 780);
            reflect.Value = DoubleAttr(root, "reflections", 0.45);
            environmentChoice.SelectedIndex = Math.Max(0, Math.Min(environmentChoice.Items.Count - 1, IntAttr(root, "environment", 0)));
            aoEnabled.IsChecked = ReadAttr(root, "ao") == "true";
            hideChildren.IsChecked = ReadAttr(root, "children") == "true";
            projection.IsChecked = ReadAttr(root, "projection") == "true";
            var lod = ReadAttr(root, "lod");
            if (levels.Items.Contains(lod)) levels.SelectedItem = lod;
            var colors = root.Element("colors");
            if (colors != null)
                foreach (var color in colors.Elements("color"))
                {
                    TextBox field;
                    if (paintFields.TryGetValue((int?)color.Attribute("layer") ?? 0, out field)) field.Text = color.Value;
                }
            ApplyPaint();
            var bones = root.Element("hidden");
            if (bones != null)
                foreach (var bone in bones.Elements("bone"))
                {
                    CheckBox box;
                    if (controls.TryGetValue((int)bone, out box)) box.IsChecked = false;
                }
            hideUnboundGeometry = ReadAttr(root, "unbound") == "true";
            string livery = ReadAttr(root, "livery");
            if (!String.IsNullOrEmpty(livery))
            {
                var option = liveries.Items.OfType<LiveryOption>().FirstOrDefault(item => item.Model == livery);
                if (option != null) liveries.SelectedItem = option;
            }
            var parts = tuningEditor.PreviewParts;
            var enabled = root.Element("enabled");
            var failures = new List<string>();
            if (enabled != null)
                foreach (var item in enabled.Elements("part"))
                {
                    int index = (int)item;
                    if (index < 0 || index >= parts.Length) continue;
                    try { PreviewTuning(parts[index], parts, tuningEditor.ImportedPath); }
                    catch (Exception ex) { failures.Add(ex.Message); }
                }
            var layout = root.Element("layout");
            if (layout != null)
            {
                foreach (var window in floatingPanes.ToArray()) window.Close();
                var savedPanes = layout.Elements("pane").ToArray();
                var positions = savedPanes.Select(item => (int)item).ToArray();
                if (positions.Length == panes.Count && positions.OrderBy(i => i).SequenceEqual(Enumerable.Range(0, panes.Count)))
                    for (int i = 0; i < panes.Count; i++) { panes[i].Position = positions[i]; PlacePane(panes[i]); }
                double column = Math.Max(0.15, Math.Min(0.85, DoubleAttr(layout, "column", 0.75)));
                double row = Math.Max(0.2, Math.Min(0.85, DoubleAttr(layout, "row", 0.66)));
                workspace.ColumnDefinitions[0].Width = new GridLength(column, GridUnitType.Star);
                workspace.ColumnDefinitions[2].Width = new GridLength(1 - column, GridUnitType.Star);
                workspace.RowDefinitions[0].Height = new GridLength(row, GridUnitType.Star);
                workspace.RowDefinitions[2].Height = new GridLength(1 - row, GridUnitType.Star);
                if (IsVisible && savedPanes.Length == panes.Count)
                    for (int i = 0; i < panes.Count; i++)
                        if ((bool?)savedPanes[i].Attribute("floating") == true) FloatPane(panes[i]);
            }
            yaw = DoubleAttr(root, "yaw", 0.85);
            pitch = ClampOrbitPitch(DoubleAttr(root, "pitch", 0.28));
            distance = Math.Max(0.15, DoubleAttr(root, "distance", vehicle.Radius * 2.9));
            orbitPan = new Vector3D(DoubleAttr(root, "panX", 0), DoubleAttr(root, "panY", 0), DoubleAttr(root, "panZ", 0));
            MoveCamera();
            if (ReadAttr(root, "free") == "true")
            {
                freeCamera.IsChecked = true;
                flyYaw = DoubleAttr(root, "flyYaw", flyYaw);
                flyPitch = ClampPitch(DoubleAttr(root, "flyPitch", flyPitch));
                freePosition = new Point3D(DoubleAttr(root, "flyX", freePosition.X), DoubleAttr(root, "flyY", freePosition.Y),
                    DoubleAttr(root, "flyZ", freePosition.Z));
                MoveCamera();
            }
            RefreshScene();
            if (failures.Count > 0) status.Text = String.Join(" · ", failures);
        }
        static void CompleteProjectCheck(Func<Task> action)
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            var previous = System.Threading.SynchronizationContext.Current;
            System.Threading.SynchronizationContext.SetSynchronizationContext(
                new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
            try
            {
                var task = action();
                var frame = new System.Windows.Threading.DispatcherFrame();
                task.ContinueWith(result => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                task.GetAwaiter().GetResult();
            }
            finally { System.Threading.SynchronizationContext.SetSynchronizationContext(previous); }
        }

        static void CheckProjectRoundTrip(string normal, string high, string[] textures, GameTextures archives)
        {
            string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".egvehicle");
            var before = new Viewer { gameTextures = archives };
            var after = new Viewer { gameTextures = archives };
            try
            {
                before.Load(normal, high, textures);
                before.paintFields[1].Text = "#287B91";
                before.ApplyPaint();
                before.levels.SelectedItem = "Medium";
                before.clock.Value = 430;
                before.renderMode.SelectedIndex = 2;
                before.projection.IsChecked = true;
                before.EnsureTuningEditor(null);
                if (archives != null)
                {
                    var selected = before.tuningEditor.PreviewParts.First(part => part.Model == "sent5_splt_0");
                    before.PreviewTuning(selected, before.tuningEditor.PreviewParts, before.tuningEditor.ImportedPath);
                }
                if (!before.tuningEditor.ImportPart(normal)) throw new InvalidOperationException("Project import fixture failed.");
                before.panes[0].Position = 2; before.panes[2].Position = 0;
                PlacePane(before.panes[0]); PlacePane(before.panes[2]);
                CompleteProjectCheck(() => before.SaveProjectAsync(false, file));
                if (!File.Exists(file)) throw new InvalidOperationException("Project was not saved: " + before.status.Text);
                CompleteProjectCheck(() => after.OpenProjectAsync(file, false));
                if (after.projectPath != file || after.vehicle == null || after.renderMode.SelectedIndex != 2 ||
                    after.clock.Value != 430 || (string)after.levels.SelectedItem != "Medium" ||
                    after.paintFields[1].Text != "#287B91" || after.projection.IsChecked != true || after.panes[0].Position != 2 ||
                    !File.Exists(after.tuningEditor.PreviewParts.Last().FilePath))
                    throw new InvalidOperationException("Project did not restore its model, settings and assets: " + after.status.Text);
                if (archives != null && (!after.tuningChoices.ContainsKey("VMT_BUMPER_F") ||
                    after.tuningChoices["VMT_BUMPER_F"].Part.Model != "sent5_splt_0"))
                    throw new InvalidOperationException("Project did not restore active tuning.");
                var draftPart = after.tuningEditor.PreviewParts.Last();
                if (!after.tuningEditor.Unassigned(draftPart) || !after.pendingTuning.Items.Contains(draftPart) ||
                    after.tuningEditor.PreviewXml().Contains("<modelName>" + draftPart.Model + "</modelName>"))
                    throw new InvalidOperationException("Unassigned model was lost or included in exported XML.");
                var parent = after.tuningEditor.PreviewParts.First(part => part.Type == "VMT_BONNET");
                after.tuningEditor.Link(parent, draftPart);
                if (after.tuningEditor.Unassigned(draftPart) || after.pendingTuning.Items.Contains(draftPart) ||
                    !TuningEditor.Names(parent.Linked).Contains(draftPart.Model))
                    throw new InvalidOperationException("Linking did not move the imported model out of the unassigned list.");
                after.tuningEditor.Unlink(parent, draftPart.Model);
                after.tuningEditor.Assign(draftPart, "VMT_ROOF");
                after.xmlPreview.Text = after.tuningEditor.PreviewXml();
                after.SelectTuningPart(draftPart);
                if (!after.xmlPreview.SelectedText.Contains("<type>VMT_ROOF</type>") ||
                    !after.xmlPreview.SelectedText.EndsWith("</Item>") ||
                    after.outlineModels.Count > 0)
                    throw new InvalidOperationException("Selection did not select the XML block independently of hover.");
                after.HoverTuning(draftPart);
                if (after.outlineModels.Count == 0)
                    throw new InvalidOperationException("Hover did not highlight the tuning geometry.");
                after.HoverTuning(null);
                if (after.outlineModels.Count > 0)
                    throw new InvalidOperationException("Hover highlight remained after leaving the tuning row.");
                after.SetTuningPart(draftPart, true);
                after.SetAllBones(false);
                if (after.scene.Children.OfType<GeometryModel3D>().Any() || after.visibleWindows.Any() ||
                    after.visibleDecals.Any() || after.tuningChecks[draftPart].IsChecked != true)
                    throw new InvalidOperationException("Hide all must hide tuning geometry and retain its fit selection.");
                after.SetAllBones(true);
                after.selectedTuningPart = null;
                after.renderMode.SelectedIndex = 0;
                if (after.wireMeshes.Count == 0) throw new InvalidOperationException("Wireframe did not generate mesh edges.");
                var wire = (ImageBrush)((DiffuseMaterial)after.wireMaterial).Brush;
                var bitmap = (System.Windows.Media.Imaging.BitmapSource)wire.ImageSource;
                var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
                bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
                if (pixels[0] != 0 || pixels[3] != 255 || pixels.Where((value, index) => index % 4 == 3).Any(alpha => alpha != 255))
                    throw new InvalidOperationException("Wireframe needs black edges and fully opaque faces.");
                after.xray.IsChecked = true;
                if (after.visibleWindows.Count == 0 || after.visibleWindows.Any(model =>
                    ((DiffuseMaterial)model.Material).Brush.Opacity >= 1) || wire.Opacity != 1)
                    throw new InvalidOperationException("X-Ray failed to fade Wireframe independently.");
                after.xray.IsChecked = false;
                after.showFloorGrid.IsChecked = after.showSceneAxes.IsChecked = after.showViewportInfo.IsChecked = true;
                if (after.viewportGuides.Children.Count != 4 || after.viewportInfo.Visibility != Visibility.Visible)
                    throw new InvalidOperationException("Viewport overlays were not enabled.");
                after.NavigateNumpad(7, true);
                if (Math.Abs(after.pitch + Math.PI / 2) > 1e-10 || after.camera.LookDirection.Z <= 0 ||
                    Math.Abs(Vector3D.DotProduct(after.camera.UpDirection, after.camera.LookDirection)) > 1e-10)
                    throw new InvalidOperationException("Bottom view is not aligned.");
                bool handled = false;
                after.NumpadHook(IntPtr.Zero, 0x100, new IntPtr(0x68), IntPtr.Zero, ref handled);
                if (!handled || Math.Abs(after.pitch + 5 * Math.PI / 12) > 1e-10)
                    throw new InvalidOperationException("NumPad orbit did not move 15 degrees.");
                after.renderMode.SelectedIndex = 1;
                if (after.decals.Children.Count != 0 || after.occlusion.Children.Count != 0)
                    throw new InvalidOperationException("Solid mode retained material overlays.");
            }
            finally
            {
                before.Close(); after.Close();
                if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
