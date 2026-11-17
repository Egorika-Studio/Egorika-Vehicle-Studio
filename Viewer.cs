using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Globalization;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Win32;

namespace SentinelViewer
{
    sealed partial class Viewer : Window
    {
        readonly ComboBox levels = new ComboBox { MinWidth = 130, Margin = new Thickness(8, 0, 8, 0) };
        const string AppName = "Egorika Vehicle Studio";
        readonly CheckBox aoEnabled = new CheckBox { Content = "AO", IsChecked = true, Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center };
        TuningEditor tuningEditor;
        bool discardTuningEditor;
        readonly TreeView tuningTree = new TreeView();

        sealed class TuningChoice
        {
            public TuningEditor.Part Part;
            public bool Enabled;
            public Dictionary<string, List<Vehicle.Piece>> Pieces;
            public HashSet<int> HiddenBones;
            public CodeWalker.GameFiles.Texture Livery;
        }

        readonly Dictionary<string, TuningChoice> tuningChoices = new Dictionary<string, TuningChoice>();
        readonly Dictionary<TuningEditor.Part, CheckBox> tuningChecks = new Dictionary<TuningEditor.Part, CheckBox>();
        bool updatingTuningChecks;
        int? isolatedBone;
        TuningChoice isolatedTuning;
        string previousLod;
        double previousDistance;
        Point3D? isolatedCenter;
        double isolatedRadius;
        bool IsIsolated { get { return isolatedBone.HasValue || isolatedTuning != null; } }
        readonly TreeView tree = new TreeView();
        readonly CheckBox hideChildren = new CheckBox
        {
            Content = Ui.Text("Скрывать дочерние детали", "Hide child parts"),
            Margin = new Thickness(0, 8, 0, 0)
        };
        readonly Dictionary<int, int> boneParents = new Dictionary<int, int>();
        readonly TextBlock status = new TextBlock { Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
        readonly Button updateNotice = new Button { Content = "↓", Width = 18, Height = 18,
            Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(100, 190, 255)),
            Visibility = Visibility.Collapsed };
        readonly Viewport3D viewport = new Viewport3D { ClipToBounds = true, IsHitTestVisible = false };
        readonly PerspectiveCamera camera = new PerspectiveCamera { FieldOfView = 42, NearPlaneDistance = 0.12, FarPlaneDistance = 260 };
        readonly Model3DGroup scene = new Model3DGroup();
        readonly Model3DGroup windows = new Model3DGroup();
        readonly Model3DGroup decals = new Model3DGroup();
        readonly Model3DGroup occlusion = new Model3DGroup();
        long lastFlightTick;
        readonly List<GeometryModel3D> visibleWindows = new List<GeometryModel3D>();
        readonly List<GeometryModel3D> visibleDecals = new List<GeometryModel3D>();
        readonly CheckBox freeCamera = new CheckBox { Content = Ui.Text("Свободная камера", "Free camera"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        readonly string[][] palette = ReadPalette();
        readonly ComboBox liveries = new ComboBox { MinWidth = 135, Margin = new Thickness(0, 0, 8, 0) };
        readonly Slider clock = new Slider { Minimum = 0, Maximum = 1439, Value = 780 };
        readonly Slider reflect = new Slider { Minimum = 0, Maximum = 1, Value = 0.45 };
        readonly TextBlock clockLabel = new TextBlock();
        readonly Model3DGroup lighting = new Model3DGroup();
        readonly ComboBox environmentChoice = new ComboBox { MinWidth = 105, Margin = new Thickness(6, 0, 0, 0) };
        readonly Model3DGroup environment = new Model3DGroup();
        bool loading;
        Border stage;
        readonly ComboBox spawnColors = new ComboBox { Width = 235, Margin = new Thickness(0, 0, 8, 0) };
        readonly Dictionary<int, TextBox> paintFields = new Dictionary<int, TextBox>();
        readonly string textureLibrary = Path.Combine(StudioPaths.Root, "textures");
        Point3D freePosition;
        double flyPitch, flyYaw;
        double flightSpeed = 2.5;
        readonly HashSet<int> hidden = new HashSet<int>();
        readonly Dictionary<int, CheckBox> controls = new Dictionary<int, CheckBox>();
        Vehicle vehicle;
        GameTextures gameTextures;
        GameTextures sourceArchives;
        string packageFolder;
        string autoCarcols;
        readonly List<string> previewFolders = new List<string>();
        string gameFolder;
        static readonly string GameSettings = StudioPaths.Setting("gta-folder.txt");
        static readonly string SteamGameFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steamapps", "common", "Grand Theft Auto V");
        string currentNormal;
        string currentHigh;
        string[] currentDictionaries;
        Point? drag;
        double yaw = 0.85, pitch = 0.28, distance = 8;

        Viewer()
        {
            Closing += CloseWithSession;
            Closing += (s, e) =>
            {
                if (e.Cancel) return;
                StopAxisTransition();
                foreach (var window in floatingPanes.ToArray()) window.Close();
                CloseTuningEditor();
            };
            StudioTheme.Apply(this);
            SourceInitialized += (s, e) =>
            {
                var source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                if (source != null) source.AddHook(NumpadHook);
            };
            Title = AppName;
            using (var icon = typeof(Viewer).Assembly.GetManifestResourceStream("Egorika.png"))
                Icon = System.Windows.Media.Imaging.BitmapFrame.Create(icon,
                    System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            Width = 1200;
            Height = 800;
            MinWidth = 710;
            MinHeight = 470;
            var layout = new DockPanel();
            layout.Background = StudioTheme.Background;
            Content = layout;
            AddStudioMenu(layout);
            freeCamera.Checked += (s, e) =>
            {
                freePosition = camera.Position;
                flyYaw = yaw + Math.PI;
                flyPitch = -pitch;
                MoveCamera();
                RefreshScene();
            };
            freeCamera.Unchecked += (s, e) => { MoveCamera(); RefreshScene(); };
            EventHandler frame = (s, e) => Fly();
            freeCamera.Checked += (s, e) => CompositionTarget.Rendering += frame;
            freeCamera.Unchecked += (s, e) => CompositionTarget.Rendering -= frame;
            Closed += (s, e) => CompositionTarget.Rendering -= frame;
            var paintBar = new StackPanel { Orientation = Orientation.Horizontal };
            AddPaintField(paintBar, 1, Ui.Text("Основной", "Primary"), Ui.Text("Осн.", "Pri."));
            AddPaintField(paintBar, 2, Ui.Text("Дополнительный", "Secondary"), Ui.Text("Доп.", "Sec."));
            AddPaintField(paintBar, 3, Ui.Text("Перламутр", "Pearlescent"), Ui.Text("Перл.", "Pearl"));
            AddPaintField(paintBar, 6, Ui.Text("Салон", "Interior"), Ui.Text("Салон", "Int."));
            AddPaintField(paintBar, 4, Ui.Text("Диски", "Wheels"), Ui.Text("Диски", "Wheels"));
            AddPaintField(paintBar, 7, Ui.Text("Приборка", "Dashboard"), Ui.Text("Приб.", "Dash"));
            var applyColors = Button(Ui.Text("Применить цвета", "Apply colors"), ApplyPaint);
            applyColors.ToolTip = applyColors.Content;
            paintBar.Children.Add(applyColors);

            spawnColors.SelectionChanged += (s, e) =>
            {
                var preset = spawnColors.SelectedItem as SpawnColors;
                if (preset == null) return;
                for (int i = 0; i < SpawnLayers.Length; i++)
                    paintFields[SpawnLayers[i]].Text = i < preset.Colors.Length ? preset.Colors[i] : "";
                ApplyPaint();
            };
            DockPanel.SetDock(status, Dock.Bottom);
            layout.Children.Add(status);

            var playback = new CheckBox { Content = Ui.Text("Ход времени", "Play time"), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            aoEnabled.Checked += (s, e) => RefreshScene();
            aoEnabled.Unchecked += (s, e) => RefreshScene();
            environmentChoice.ItemsSource = new[] { Ui.Text("Без сцены", "No scene"), Ui.Text("Площадка", "Studio floor"), Ui.Text("Дорога", "Road") };
            environmentChoice.SelectedIndex = 0;
            environmentChoice.SelectionChanged += (s, e) => UpdateEnvironment();
            var cycle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            cycle.Tick += (s, e) => clock.Value = (clock.Value + 3) % 1440;
            playback.Checked += (s, e) => cycle.Start();
            playback.Unchecked += (s, e) => cycle.Stop();
            Closed += (s, e) => cycle.Stop();
            clock.ValueChanged += (s, e) => UpdateLighting();
            reflect.ValueChanged += (s, e) =>
            {
                if (vehicle == null) return;
                vehicle.SetReflections(reflect.Value);
                RefreshScene();
            };
            liveries.SelectionChanged += (s, e) => SelectLivery();
            var sidebar = new DockPanel { Margin = new Thickness(5), Background = StudioTheme.Panel };
            var visibility = new StackPanel();
            DockPanel.SetDock(visibility, Dock.Top);
            visibility.Children.Add(hideChildren);
            var visibilityButtons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
            visibilityButtons.Children.Add(Button(Ui.Text("Скрыть всё", "Hide all"), () => SetAllBones(false)));
            visibilityButtons.Children.Add(Button(Ui.Text("Показать всё", "Show all"), () => SetAllBones(true)));
            visibility.Children.Add(visibilityButtons);
            sidebar.Children.Add(visibility);
            sidebar.Children.Add(tree);

            stage = new Border { Focusable = true, Background = ViewportBackground };
            layout.Children.Add(CreateWorkspace(stage, paintBar, sidebar, playback));
            stage.Child = viewport;
            viewport.Camera = camera;
            viewport.SizeChanged += (s, e) => MoveCamera();
            viewport.Children.Add(new ModelVisual3D { Content = scene });
            levels.SelectionChanged += (s, e) =>
            {
                if (IsIsolated) FrameIsolation();
                RefreshScene();
            };
            stage.MouseLeftButtonDown += (s, e) => { StopAxisTransition(); stage.Focus(); drag = e.GetPosition(stage); stage.CaptureMouse(); };
            stage.MouseDown += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle) return;
                stage.Focus(); drag = e.GetPosition(stage); middleDrag = true; stage.CaptureMouse();
            };
            stage.MouseUp += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle) return;
                middleDrag = false; drag = null; stage.ReleaseMouseCapture();
            };
            stage.MouseLeftButtonUp += (s, e) => { drag = null; stage.ReleaseMouseCapture(); };
            stage.LostMouseCapture += (s, e) => { drag = null; middleDrag = false; };
            stage.MouseMove += (s, e) =>
            {
                if (!drag.HasValue) return;
                var point = e.GetPosition(stage);
                double dx = (point.X - drag.Value.X) * 0.009;
                double dy = (point.Y - drag.Value.Y) * 0.009;
                if ((panMode.IsChecked == true || middleDrag) && freeCamera.IsChecked != true)
                {
                    var right = Vector3D.CrossProduct(camera.LookDirection, camera.UpDirection);
                    right.Normalize();
                    var up = Vector3D.CrossProduct(right, camera.LookDirection);
                    up.Normalize();
                    orbitPan += (up * dy - right * dx) * distance * 0.42;
                }
                else if (freeCamera.IsChecked == true)
                {
                    flyYaw -= dx;
                    flyPitch = ClampPitch(flyPitch - dy);
                }
                else
                {
                    yaw -= dx;
                    pitch = ClampOrbitPitch(pitch + dy);
                }
                drag = point;
                MoveCamera();
            };
            stage.MouseWheel += (s, e) =>
            {
                if (freeCamera.IsChecked == true)
                {
                    flightSpeed = Math.Max(0.2, Math.Min(60, flightSpeed * Math.Pow(1.18, e.Delta / 120.0)));
                    return;
                }
                double radius = isolatedCenter.HasValue ? isolatedRadius : vehicle == null ? 3 : vehicle.Radius;
                distance = Math.Max(radius * 0.22, Math.Min(radius * 22, distance * Math.Pow(0.85, e.Delta / 120.0)));
                MoveCamera();
            };
            status.Text = Ui.Text("Откройте модель .yft. Перетаскивание вращает камеру, колесо меняет масштаб.", "Open a .yft model. Drag to orbit, scroll to zoom.");
            UpdateLighting();
            ResetCamera();
        }

        static string[][] ReadPalette()
        {
            using (var stream = typeof(Viewer).Assembly.GetManifestResourceStream("PaintPalette.tsv"))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split('\t')).ToArray();
        }

        bool changingVisibility;
        bool hideUnboundGeometry;
        void SetAllBones(bool visible)
        {
            hideUnboundGeometry = !visible;
            changingVisibility = true;
            try
            {
                foreach (var control in controls)
                {
                    control.Value.IsChecked = visible;
                    if (visible) hidden.Remove(control.Key);
                    else hidden.Add(control.Key);
                }
            }
            finally { changingVisibility = false; }
            RefreshScene();
        }

        void OpenTuning()
        {
            if (vehicle == null)
            {
                MessageBox.Show(this, Ui.Text("Сначала откройте YFT машины.", "Open a vehicle YFT first."), AppName);
                return;
            }
            EnsureTuningEditor(null);

            editorSlot.Focus();
        }

        void EnsureTuningEditor(string metadata)
        {
            if (tuningEditor != null) return;
            tuningEditor = new TuningEditor(Path.GetFileNameWithoutExtension(currentNormal),
                vehicle.Bones.Values.Select(bone => bone.Name), PreviewTuning) { Icon = Icon };
            tuningEditor.StatusChanged += message => status.Text = message;
            editorSlot.Content = tuningEditor.DetachEditorContent(this);
            tuningEditor.Workspace.Content = CreateTuningWorkspace();
            tuningEditor.PartsChanged += UpdateTuningTree;
            tuningEditor.PartsChanged += RefreshCarcolsPreview;
            tuningEditor.LinkChanged += name => RefreshCarcolsPreview();
            tuningEditor.LinkChanged += name => { highlightedTuning = null; };
            RefreshCarcolsPreview();
            tuningEditor.LinkChanged += RefreshLinkedTuning;
            tuningEditor.Closing += (s, e) =>
            {
                if (discardTuningEditor) return;
                e.Cancel = true;
                tuningEditor.Hide();
            };
            if (File.Exists(metadata)) tuningEditor.LoadCarcols(metadata, false);
        }

        void CloseTuningEditor()
        {
            if (tuningEditor == null) return;
            discardTuningEditor = true;
            try { tuningEditor.Close(); }
            finally
            {
                discardTuningEditor = false;
                tuningEditor = null;
                var pendingColumn = pendingTuning.Parent as Panel;
                var assignedColumn = tuningTree.Parent as Panel;
                if (pendingColumn != null) pendingColumn.Children.Remove(pendingTuning);
                if (assignedColumn != null) assignedColumn.Children.Remove(tuningTree);
                editorSlot.Content = null;
                RefreshCarcolsPreview();
                UpdateTuningTree();
            }
        }

        static string Category(TuningEditor.Part part)
        {
            return String.IsNullOrWhiteSpace(part.Type) ? Ui.Text("Без категории", "Uncategorized") : part.Type;
        }

        void SetTuningPart(TuningEditor.Part part, bool enabled)
        {
            if (updatingTuningChecks || tuningEditor == null) return;
            if (enabled)
            {
                try { PreviewTuning(part, tuningEditor.PreviewParts, tuningEditor.ImportedPath); }
                catch (Exception ex)
                {
                    SetTuningCheck(part, false);
                    status.Text = ex.Message;
                }
                return;
            }
            TuningChoice choice;
            if (!tuningChoices.TryGetValue(Category(part), out choice) || choice.Part != part) return;
            choice.Enabled = false;
            RefreshScene();
        }

        void SetTuningCheck(TuningEditor.Part part, bool value)
        {
            CheckBox box;
            if (!tuningChecks.TryGetValue(part, out box)) return;
            updatingTuningChecks = true;
            try { box.IsChecked = value; }
            finally { updatingTuningChecks = false; }
        }

        void RefreshLinkedTuning(string model)
        {
            if (tuningEditor == null) return;
            var affected = tuningChoices.Values.Where(choice => choice.Enabled &&
                TuningEditor.Names(choice.Part.Linked).Any(name => model == null ||
                    String.Equals(name, model, StringComparison.OrdinalIgnoreCase))).Select(choice => choice.Part).ToArray();
            foreach (var part in affected)
            {
                try { PreviewTuning(part, tuningEditor.PreviewParts, tuningEditor.ImportedPath); }
                catch (Exception ex) { status.Text = ex.Message; }
            }
        }

        void PreviewTuning(TuningEditor.Part selected, TuningEditor.Part[] parts, string metadata)
        {
            if (selected == null)
            {
                foreach (var choice in tuningChoices)
                {
                    choice.Value.Enabled = false;
                    SetTuningCheck(choice.Value.Part, false);
                }
                RefreshScene();
                return;
            }
            if (vehicle == null || loading) throw new InvalidOperationException(Ui.Text("Дождитесь загрузки машины.", "Wait for the vehicle to finish loading."));
            var choiceForPart = PrepareTuning(selected, parts, metadata);
            string category = Category(selected);
            TuningChoice previous;
            if (tuningChoices.TryGetValue(category, out previous) && previous.Part != selected)
                SetTuningCheck(previous.Part, false);
            tuningChoices[category] = choiceForPart;
            SetTuningCheck(selected, true);
            RefreshScene();
        }

        TuningChoice PrepareTuning(TuningEditor.Part selected, TuningEditor.Part[] parts, string metadata)
        {
            var main = FindTuningModel(selected.Model, selected.FilePath, metadata);
            var choiceForPart = new TuningChoice { Part = selected, Enabled = true };
            var hiddenNames = new HashSet<string>(TuningEditor.Names(selected.TurnOff), StringComparer.OrdinalIgnoreCase);
            if (selected.Type == "VMT_LIVERY_MOD")
            {
                choiceForPart.Livery = Vehicle.LiveryTexture(main);
                if (choiceForPart.Livery == null) throw new InvalidDataException(Ui.Text("В YFT этой ливреи не найдена текстура.", "No livery texture was found in this YFT."));
            }
            else
            {
                var preview = vehicle.BuildTuning(main, selected.Bone);
                foreach (string name in TuningEditor.Names(selected.Linked))
                {
                    var linked = parts.FirstOrDefault(part => String.Equals(part.Model, name, StringComparison.OrdinalIgnoreCase));
                    var file = FindTuningModel(name, linked == null ? null : linked.FilePath, metadata);
                    string anchor = tuningEditor == null ? null : tuningEditor.LinkedBone(name);
                    if (String.IsNullOrWhiteSpace(anchor) && linked != null) anchor = linked.Bone;
                    if (String.IsNullOrWhiteSpace(anchor)) anchor = selected.Bone;
                    var extra = vehicle.BuildTuning(file, anchor);
                    foreach (var level in preview.Keys) preview[level].AddRange(extra[level]);
                    if (linked != null) hiddenNames.UnionWith(TuningEditor.Names(linked.TurnOff));
                }
                if (preview.Values.All(items => items.Count == 0))
                    throw new InvalidDataException(Ui.Text("В YFT детали нет геометрии для просмотра.", "The part YFT has no displayable geometry."));
                choiceForPart.Pieces = preview;
            }
            choiceForPart.HiddenBones = new HashSet<int>(vehicle.Bones.Values.Where(bone => hiddenNames.Contains(bone.Name))
                .Select(bone => (int)bone.Tag));
            return choiceForPart;
        }

        IntPtr NumpadHook(IntPtr window, int message, IntPtr key, IntPtr flags, ref bool handled)
        {
            const int keyDown = 0x0100, decimalKey = 0x6E, deleteKey = 0x2E;
            const long extended = 1L << 24, repeated = 1L << 30;
            long bits = flags.ToInt64();
            if (message != keyDown || Keyboard.FocusedElement is TextBox) return IntPtr.Zero;
            long code = key.ToInt64();
            bool navigationKey = (bits & extended) == 0;
            if (code == decimalKey || (code == deleteKey && navigationKey))
            {
                handled = true;
                if ((bits & repeated) == 0) ToggleIsolation();
            }
            else
            {
                int digit = code >= 0x61 && code <= 0x69 ? (int)(code - 0x60) : 0;
                if (digit == 0 && navigationKey)
                {
                    // With Num Lock off, the keypad sends the navigation virtual keys.
                    digit = Array.IndexOf(new long[] { 0, 0x23, 0x28, 0x22, 0x25, 0x0C, 0x27, 0x24, 0x26, 0x21 }, code);
                }
                if (digit > 0 && (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
                {
                    handled = true;
                    if (digit == 2 || digit == 4 || digit == 6 || digit == 8 || (bits & repeated) == 0)
                        NavigateNumpad(digit, (Keyboard.Modifiers & ModifierKeys.Control) != 0);
                }
            }
            return IntPtr.Zero;
        }

        void ToggleIsolation()
        {
            if (vehicle == null || loading) return;
            if (IsIsolated)
            {
                isolatedBone = null;
                isolatedTuning = null;
                isolatedCenter = null;
                if (previousLod != null && levels.Items.Contains(previousLod)) levels.SelectedItem = previousLod;
                distance = previousDistance;
                previousLod = null;
                MoveCamera();
                RefreshScene();
                return;
            }

            try
            {
                if (isolationTreeIsTuning)
                {
                    var part = selectedTuningPart;
                    if (part != null && tuningEditor != null)
                        isolatedTuning = PrepareTuning(part, tuningEditor.PreviewParts, tuningEditor.ImportedPath);
                }
                else
                {
                    var node = tree.SelectedItem as TreeViewItem;
                    if (node != null && node.Tag is int) isolatedBone = (int)node.Tag;
                }

                if (!IsIsolated)
                {
                    status.Text = Ui.Text("Выберите деталь или кость в дереве.", "Select a part or bone in the tree.");
                    return;
                }
                string current = levels.SelectedItem as string;
                string visibleLevel = current != null && IsolationPieces(current).Any() ? current :
                    vehicle.Levels.Keys.FirstOrDefault(level => IsolationPieces(level).Any());
                if (visibleLevel == null)
                {
                    isolatedBone = null;
                    isolatedTuning = null;
                    status.Text = Ui.Text("У выбранной детали нет геометрии для изоляции.", "The selected part has no geometry to isolate.");
                    return;
                }
                previousLod = current;
                previousDistance = distance;
                levels.SelectedItem = visibleLevel;
                FrameIsolation();
                RefreshScene();
            }
            catch (Exception ex)
            {
                isolatedBone = null;
                isolatedTuning = null;
                isolatedCenter = null;
                status.Text = ex.Message;
            }
        }

        IEnumerable<Vehicle.Piece> IsolationPieces(string level)
        {
            if (isolatedBone.HasValue)
                return vehicle.Levels[level].Where(piece => piece.Bone == isolatedBone.Value);
            if (isolatedTuning == null) return Enumerable.Empty<Vehicle.Piece>();
            if (isolatedTuning.Livery != null)
                return vehicle.Levels[level].Where(piece => piece.LiveryMesh != null);
            return isolatedTuning.Pieces[level];
        }

        void FrameIsolation()
        {
            string level = levels.SelectedItem as string;
            if (level == null || !IsIsolated) return;
            var bounds = Rect3D.Empty;
            foreach (var piece in IsolationPieces(level)) bounds.Union(piece.Model.Bounds);
            if (bounds.IsEmpty) return;
            isolatedCenter = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
            isolatedRadius = Math.Max(0.18, new Vector3D(bounds.SizeX, bounds.SizeY, bounds.SizeZ).Length * 0.5);
            if (freeCamera.IsChecked != true)
            {
                distance = isolatedRadius * 3.1;
                MoveCamera();
            }
        }

        CodeWalker.GameFiles.YftFile FindTuningModel(string model, string explicitPath, string metadata)
        {
            if (String.IsNullOrWhiteSpace(model) || Path.GetFileName(model) != model)
                throw new InvalidDataException(Ui.Text("Некорректное имя модели детали.", "Invalid part model name."));
            string vehicleFolder = Path.GetDirectoryName(currentNormal);
            var candidates = new List<string> { explicitPath, Path.Combine(vehicleFolder, model + ".yft") };
            if (!String.IsNullOrEmpty(metadata))
                candidates.Add(Path.Combine(Path.GetDirectoryName(metadata), model + ".yft"));
            if (packageFolder != null)
                candidates.AddRange(Directory.EnumerateFiles(packageFolder, "*.yft", SearchOption.AllDirectories)
                    .Where(path => String.Equals(Path.GetFileNameWithoutExtension(path), model, StringComparison.OrdinalIgnoreCase)));
            string bundled;
            if (projectParts.TryGetValue(model, out bundled)) candidates.Insert(1, bundled);
            var local = candidates.FirstOrDefault(path => File.Exists(path));
            if (local != null)
            {
                var file = new CodeWalker.GameFiles.YftFile();
                file.Load(File.ReadAllBytes(local));
                return file;
            }
            var archive = sourceArchives == null ? null : sourceArchives.ReadModel(model);
            if (archive == null && gameTextures != null && gameTextures != sourceArchives) archive = gameTextures.ReadModel(model);
            if (archive != null) return archive;
            throw new FileNotFoundException(Ui.Text("Не найден ", "Cannot find ") + model + Ui.Text(".yft. Импортируйте YFT или подключите архивы игры.", ".yft. Import the YFT or connect the game archives."));
        }

        void UpdateEnvironment()
        {
            environment.Children.Clear();
            var center = vehicle == null ? new Point3D() : vehicle.Center;
            double ground = vehicle == null ? -0.75 : vehicle.Levels.Values.First().Min(p => p.Model.Bounds.Z) - 0.015;
            environment.Children.Add(EnvironmentScene.Create(environmentChoice.SelectedItem as string, center, ground));
        }

        void UpdateLighting()
        {
            int minutes = (int)clock.Value;
            clockLabel.Text = String.Format("{0:00}:{1:00}", minutes / 60, minutes % 60);
            if (renderMode.SelectedIndex != 3)
            {
                if (stage != null) stage.Background = ViewportBackground;
                lighting.Children.Clear();
                lighting.Children.Add(new AmbientLight(renderMode.SelectedIndex == 1 ? Color.FromRgb(100, 100, 100) : Colors.White));
                if (renderMode.SelectedIndex == 1)
                    lighting.Children.Add(new DirectionalLight(Color.FromRgb(200, 200, 200), new Vector3D(-1, 2, -3)));
                return;
            }
            double phase = (minutes - 360) * Math.PI / 720;
            double altitude = Math.Sin(phase);
            double daylight = Math.Max(0, altitude);
            double twilight = Math.Max(0, 1 - Math.Abs(altitude) * 5);
            Color sky = Color.FromRgb((byte)(24 + 88 * daylight + 40 * twilight),
                (byte)(29 + 100 * daylight + 16 * twilight), (byte)(49 + 105 * daylight));
            if (stage != null) stage.Background = showSky.IsChecked == true ? new SolidColorBrush(sky) : ViewportBackground;
            lighting.Children.Clear();
            lighting.Children.Add(new AmbientLight(Color.FromRgb((byte)(42 + 89 * daylight),
                (byte)(44 + 89 * daylight), (byte)(60 + 76 * daylight))));
            lighting.Children.Add(new DirectionalLight(Color.FromRgb((byte)(27 + 83 * daylight),
                (byte)(34 + 86 * daylight), (byte)(59 + 96 * daylight)), new Vector3D(1, -2, -1)));
            if (altitude > -0.08)
            {
                double energy = Math.Min(1, (altitude + 0.08) * 2);
                lighting.Children.Add(new DirectionalLight(Color.FromRgb((byte)(255 * energy),
                    (byte)((170 + 72 * daylight) * energy), (byte)((115 + 110 * daylight) * energy)),
                    new Vector3D(-Math.Cos(phase), 0.45, -Math.Max(0.05, altitude))));
            }
        }

        sealed class LiveryOption
        {
            public string Model;
            public string Label;
            public override string ToString() { return Label; }
        }

        void PopulateLiveries(string path)
        {
            var options = new List<LiveryOption> { new LiveryOption { Label = Ui.Text("Без ливреи", "No livery") } };
            if (File.Exists(path))
            {
                var document = new XmlDocument { XmlResolver = null };
                using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                    document.Load(reader);
                string model = Path.GetFileNameWithoutExtension(currentNormal);
                foreach (XmlNode kit in document.SelectNodes("//Kits/Item[kitName]"))
                {
                    if (!kit["kitName"].InnerText.EndsWith("_" + model + "_modkit", StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (XmlNode entry in kit.SelectNodes("visibleMods/Item[type='VMT_LIVERY_MOD'][modelName]"))
                    {
                        string name = entry["modelName"].InnerText.Trim();
                        options.Add(new LiveryOption { Model = name, Label = name });
                    }
                }
            }
            liveries.ItemsSource = options;
            liveries.SelectedIndex = 0;
        }

        string manualLiveryPath;
        void LoadManualLivery(string path)
        {
            var file = new CodeWalker.GameFiles.YftFile();
            file.Load(File.ReadAllBytes(path));
            ApplyLivery(Vehicle.LiveryTexture(file));
            liveries.SelectedIndex = -1;
            manualLiveryPath = path;
        }

        void SelectLivery()
        {
            var option = liveries.SelectedItem as LiveryOption;
            if (vehicle == null || option == null) return;
            try
            {
                manualLiveryPath = null;
                if (option.Model == null) { vehicle.SetLivery(null); RefreshScene(); return; }
                ApplyLivery(Vehicle.LiveryTexture(FindTuningModel(option.Model, null, tuningEditor == null ? null : tuningEditor.ImportedPath)));
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Ui.Text("Ливрея", "Livery")); }
        }

        void ApplyLivery(CodeWalker.GameFiles.Texture texture)
        {
            if (texture == null) throw new InvalidDataException(Ui.Text("Текстура ливреи не найдена. Подключите архивы GTA V или откройте YFT ливреи.", "Livery texture not found. Connect the GTA V archives or open a livery YFT."));
            vehicle.SetLivery(texture);
            RefreshScene();
        }

        static readonly int[] SpawnLayers = { 1, 2, 3, 4, 6, 7 };

        sealed class SpawnColors
        {
            public string Label;
            public string[] Colors;
            public override string ToString() { return Label; }
        }

        void OpenVariations()
        {
            if (currentNormal == null) return;
            var path = Pick(Ui.Text("Откройте carvariations.meta", "Open carvariations.meta"), "GTA V (*.meta;*.xml)|*.meta;*.xml");
            if (path == null) return;
            try
            {
                projectVariations = path;
                if (ReadVariations(path, Path.GetFileNameWithoutExtension(currentNormal)) == 0)
                    MessageBox.Show(this, Ui.Text("В файле нет цветов для этой модели.", "This file has no colors for the selected model."), Ui.Text("Цвета спавна", "Spawn colors"));
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Ui.Text("Ошибка чтения carvariations.meta", "Could not read carvariations.meta")); }
        }

        int ReadVariations(string path, string model)
        {
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                document.Load(reader);
            var modelEntry = document.SelectNodes("/CVehicleModelInfoVariation/variationData/Item").Cast<XmlNode>()
                .FirstOrDefault(item => item["modelName"] != null &&
                    String.Equals(item["modelName"].InnerText.Trim(), model, StringComparison.OrdinalIgnoreCase));
            var presets = new List<SpawnColors>();
            if (modelEntry != null)
                foreach (XmlNode entry in modelEntry.SelectNodes("colors/Item/indices"))
                {
                    var ids = entry.InnerText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (ids.Length == 0) continue;
                    var rows = ids.Select(id => palette.FirstOrDefault(row => row[0] == id)).ToArray();
                    presets.Add(new SpawnColors
                    {
                        Label = (presets.Count + 1) + ". " + (rows[0] == null ? ids[0] : rows[0][1]) +
                            " · " + String.Join("/", ids),
                        Colors = rows.Select(row => row == null ? "" : PaletteValue(row)).ToArray()
                    });
                }
            spawnColors.ItemsSource = presets;
            return presets.Count;
        }

        static string PaletteValue(string[] row) { return row[0] == "120" ? "Chrome" : row[2]; }

        void AddPaintField(Panel panel, int layer, string label, string compactLabel)
        {
            var channel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(channel);
            channel.Children.Add(new TextBlock { Text = label, ToolTip = label, Tag = compactLabel,
                Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center });
            var field = new TextBox { Width = 53, MaxLength = 7,
                VerticalContentAlignment = VerticalAlignment.Center, ToolTip = Ui.Text("#RRGGBB. Пустое поле возвращает исходный цвет.", "#RRGGBB. Leave blank to restore the original color.") };
            field.KeyDown += (s, e) => { if (e.Key == Key.Enter) ApplyPaint(); };
            paintFields.Add(layer, field);
            channel.Children.Add(field);
            var picker = new Button { Content = "▾", Width = 16, Margin = new Thickness(0, 0, 5, 0),
                ToolTip = Ui.Text("Палитра GTA V: ", "GTA V palette: ") + label };
            picker.Click += (sender, args) =>
            {
                var menu = new ContextMenu { PlacementTarget = picker, MaxHeight = 460 };
                var original = new MenuItem { Header = Ui.Text("Исходный цвет", "Original color") };
                original.Click += (s, e) => { field.Clear(); ApplyPaint(); };
                menu.Items.Add(original);
                foreach (var row in palette)
                {
                    string hex = row[2];
                    string value = PaletteValue(row);
                    var swatch = new Border { Width = 14, Height = 14,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value == "Chrome" ? "#C6D1DC" : hex)),
                        BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
                    var item = new MenuItem { Header = row[0] + "  " + row[1] + (value == "Chrome" ? "" : "  " + hex), Icon = swatch };
                    item.Click += (s, e) => { field.Text = value; ApplyPaint(); };
                    menu.Items.Add(item);
                }
                picker.ContextMenu = menu;
                menu.IsOpen = true;
            };
            channel.Children.Add(picker);
        }

        void ApplyPaint()
        {
            if (vehicle == null) return;
            var colors = new Dictionary<int, Color?>();
            var chrome = new HashSet<int>();
            foreach (var entry in paintFields)
            {
                string hex = entry.Value.Text.Trim().TrimStart('#');
                uint rgb;
                if (String.Equals(hex, "Chrome", StringComparison.OrdinalIgnoreCase))
                {
                    chrome.Add(entry.Key);
                    colors.Add(entry.Key, null);
                }
                else if (hex.Length == 0) colors.Add(entry.Key, null);
                else if (hex.Length == 6 && UInt32.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                    colors.Add(entry.Key, Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
                else
                {
                    MessageBox.Show(this, Ui.Text("Введите #RRGGBB или выберите Chrome в палитре.", "Enter #RRGGBB or select Chrome from the palette."), Ui.Text("Цвет кузова", "Paint color"));
                    entry.Value.Focus();
                    return;
                }
            }
            foreach (var color in colors) vehicle.SetPaint(color.Key, color.Value, chrome.Contains(color.Key));
            RefreshScene();
        }

        static double ClampPitch(double angle) { return Math.Max(-1.48, Math.Min(1.48, angle)); }
        static double ClampOrbitPitch(double angle) { return Math.Max(-Math.PI / 2, Math.Min(Math.PI / 2, angle)); }

        Vector3D FlightDirection()
        {
            return new Vector3D(Math.Cos(flyYaw) * Math.Cos(flyPitch),
                Math.Sin(flyYaw) * Math.Cos(flyPitch), Math.Sin(flyPitch));
        }

        void Fly()
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            double elapsed = Math.Min(0.05, (now - lastFlightTick) / (double)System.Diagnostics.Stopwatch.Frequency);
            lastFlightTick = now;
            var host = Window.GetWindow(stage);
            if ((host == null || !host.IsActive) || freeCamera.IsChecked != true || Keyboard.FocusedElement is TextBox) return;
            var forward = FlightDirection();
            var right = Vector3D.CrossProduct(forward, new Vector3D(0, 0, 1));
            right.Normalize();
            var step = new Vector3D();
            if (Keyboard.IsKeyDown(Key.W)) step += forward;
            if (Keyboard.IsKeyDown(Key.S)) step -= forward;
            if (Keyboard.IsKeyDown(Key.D)) step += right;
            if (Keyboard.IsKeyDown(Key.A)) step -= right;
            if (Keyboard.IsKeyDown(Key.E)) step.Z++;
            if (Keyboard.IsKeyDown(Key.Q)) step.Z--;
            if (step.LengthSquared == 0) return;
            step.Normalize();
            freePosition += step * (flightSpeed * elapsed * ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 3 : 1));
            camera.Position = freePosition;
            orthographic.Position = freePosition;
            if (outlineModels.Count > 0) DrawTuningOutline();
        }

        static Button Button(string text, Action action)
        {
            var button = new Button { Content = text, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 0) };
            button.Click += (s, e) => action();
            return button;
        }

        static string Pick(string title, string filter)
        {
            var dialog = new OpenFileDialog { Title = title, Filter = filter };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        async void OpenGameVehicle()
        {
            if (!loading && await EnsureArchives()) await OpenArchive(gameTextures);
        }

        async void OpenRpfVehicle()
        {
            if (loading) return;
            string path = Pick(Ui.Text("Откройте архив RPF", "Open an RPF archive"), "RPF (*.rpf)|*.rpf");
            if (path == null) return;
            try
            {
                loading = true;
                await ConnectSavedArchives();
                var source = await Task.Run(() => GameTextures.OpenRpf(path));
                loading = false;
                await OpenArchive(source);
            }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { loading = false; }
        }

        T Choose<T>(string title, IEnumerable<T> values, Func<T, string> label, bool thumbnails = false) where T : class
        {
            var items = values.ToArray();
            var picker = new Window { Owner = this, Title = title, Width = 850, Height = 430,
                WindowStartupLocation = WindowStartupLocation.CenterOwner };
            StudioTheme.Apply(picker);
            var layout = new DockPanel { Margin = new Thickness(8) };
            var search = new TextBox { Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(search, Dock.Top);
            layout.Children.Add(search);
            var list = new ListBox();
            if (thumbnails) list.ItemTemplate = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(VehicleCatalogRow)) };
            Action filter = () =>
            {
                string query = search.Text.Trim();
                list.ItemsSource = items.Where(item => label(item).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                if (list.Items.Count > 0) list.SelectedIndex = 0;
            };
            filter();
            search.TextChanged += (s, e) => filter();
            Action confirm = () => { if (list.SelectedItem != null) picker.DialogResult = true; };
            var open = Button(Ui.Text("Открыть", "Open"), confirm);
            DockPanel.SetDock(open, Dock.Bottom);
            layout.Children.Add(open);
            layout.Children.Add(list);
            list.MouseDoubleClick += (s, e) => confirm();
            search.KeyDown += (s, e) => { if (e.Key == Key.Enter) confirm(); };
            picker.Content = layout;
            picker.Loaded += (s, e) => search.Focus();
            return picker.ShowDialog() == true ? list.SelectedItem as T : null;
        }

        async Task OpenArchive(GameTextures source)
        {
            if (source == null || loading) return;
            GameTextures.ModelVersion[] models;
            loading = true;
            try { models = await Task.Run(() => source.Models().ToArray()); }
            catch (Exception ex) { status.Text = ex.Message; return; }
            finally { loading = false; }
            if (models.Length == 0)
            {
                status.Text = Ui.Text("В источнике не найден транспорт.", "No vehicles were found in this source.");
                return;
            }
            var option = Choose(Ui.Text("Выберите модель и версию", "Select a model and version"), models, item => item.ToString(), true);
            if (option == null) return;
            string folder = Path.Combine(Path.GetTempPath(), "EgorikaVehicle-" + Guid.NewGuid().ToString("N"));
            previewFolders.Add(folder);
            loading = true;
            status.Text = Ui.Text("Загружаю ", "Loading ") + option.Name + "…";
            try
            {
                string path = await Task.Run(() => source.ExportPreview(option, folder));
                await OpenVehicleFile(path, null, source, folder, source.OriginRpf == null ? null : source.OriginRpf + "|" + option);
            }
            catch (Exception ex) { status.Text = Ui.Text("Ошибка загрузки: ", "Load error: ") + ex.Message; }
            finally { loading = false; }
        }

        async void OpenPackage()
        {
            if (loading) return;
            using (var picker = new System.Windows.Forms.FolderBrowserDialog { Description = Ui.Text("Выберите папку ресурса FiveM", "Select a FiveM resource folder") })
            {
                if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                try
                {
                    string folder = picker.SelectedPath;
                    loading = true;
                    var files = await Task.Run(() => PackageVehicles(folder));
                    if (files.Length == 0)
                    {
                        status.Text = Ui.Text("В папке не найден транспорт.", "No vehicles were found in this folder.");
                        return;
                    }
                    var model = Choose(Ui.Text("Выберите модель из папки", "Select a model from the folder"), files, path => path, true);
                    if (model == null) return;
                    loading = true;
                    await OpenVehicleFile(model, folder, null, folder, model);
                }
                catch (Exception ex) { status.Text = Ui.Text("Ошибка загрузки: ", "Load error: ") + ex.Message; }
                finally { loading = false; }
            }
        }

        static string[] PackageVehicles(string folder)
        {
            var catalog = VehicleCatalog.FromFolder(folder, SearchOption.AllDirectories);
            var files = Directory.EnumerateFiles(folder, "*.yft", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("_hi.yft", StringComparison.OrdinalIgnoreCase));
            if (catalog.Count > 0)
                return files.Where(path => catalog.Contains(Path.GetFileNameWithoutExtension(path))).ToArray();
            return files.Where(path =>
            {
                try
                {
                    var file = new CodeWalker.GameFiles.YftFile();
                    file.Load(File.ReadAllBytes(path));
                    return VehicleCatalog.HasVehicleSkeleton(file);
                }
                catch (Exception) { return false; }
            }).ToArray();
        }

        async Task OpenVehicleFile(string model, string package, GameTextures source, string metaFolder, string thumbnailKey = null)
        {
            var directory = Path.GetDirectoryName(model);
            var name = Path.GetFileNameWithoutExtension(model);
            string high = Path.Combine(directory, name + "_hi.yft");
            if (!File.Exists(high)) high = null;
            var catalog = VehicleCatalog.FromFolder(package ?? directory, package == null ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);
            string[] paths = (package == null ? DictionaryFiles(directory, name) :
                Directory.EnumerateFiles(package, "*.ytd", SearchOption.AllDirectories)
                    .Where(path => !String.Equals(Path.GetDirectoryName(path), directory, StringComparison.OrdinalIgnoreCase))
                    .Concat(DictionaryFiles(directory, name))).ToArray();
            paths = PrioritizeTextures(paths, catalog.TextureDictionaries(name));
            if (source == null) await ConnectSavedArchives();
            var fallback = gameTextures ?? source;
            var loaded = await Task.Run(() => new Vehicle(model, high, paths, fallback, source));
            projectParts.Clear(); projectVariations = null;
            sourceArchives = source;
            packageFolder = package;
            autoCarcols = FindPackageMetadata(metaFolder, "carcols", name);
            string variations = FindPackageMetadata(metaFolder, "carvariations", name);
            projectVariations = variations;
            Load(model, high, paths, loaded, autoCarcols, variations);
            await CacheVehicleThumbnail(thumbnailKey, loaded);
        }

        async Task CacheVehicleThumbnail(string key, Vehicle loaded)
        {
            if (key == null || vehicle != loaded) return;
            var pieces = loaded.Levels.Values.FirstOrDefault();
            if (pieces == null) return;
            await Dispatcher.InvokeAsync(() => VehicleCatalogRow.CacheOpened(key, pieces),
                DispatcherPriority.ApplicationIdle).Task;
        }

        static string FindPackageMetadata(string folder, string kind, string name)
        {
            var files = Directory.GetFiles(folder, kind + ".meta", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string xml = File.ReadAllText(file);
                string escaped = System.Text.RegularExpressions.Regex.Escape(name);
                string pattern = kind == "carcols" ? @"<kitName>[^<]*_" + escaped + @"_modkit\s*</kitName>" :
                    @"<modelName>\s*" + escaped + @"\s*</modelName>";
                if (System.Text.RegularExpressions.Regex.IsMatch(xml, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    return file;
            }
            return kind == "carcols" && files.Length == 1 ? files[0] : null;
        }

        string ChooseGame()
        {
            var dialog = new OpenFileDialog { Title = Ui.Text("Укажите GTA5.exe из GTA V Legacy", "Select GTA5.exe from GTA V Legacy"), Filter = "GTA5.exe|GTA5.exe" };
            string initial = gameFolder ?? SteamGameFolder;
            if (File.Exists(Path.Combine(initial, "GTA5.exe"))) dialog.InitialDirectory = initial;
            return dialog.ShowDialog(this) == true ? Path.GetDirectoryName(dialog.FileName) : null;
        }

        static void SaveGameFolder(string folder)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GameSettings));
            File.WriteAllText(GameSettings, folder);
        }

        void RememberWindowPlacement()
        {
            string path = StudioPaths.Setting("window-placement.txt");
            var culture = CultureInfo.InvariantCulture;
            try
            {
                if (File.Exists(path))
                {
                    var saved = File.ReadAllLines(path);
                    if (saved.Length == 3)
                    {
                        double width, height;
                        if (Double.TryParse(saved[1], NumberStyles.Float, culture, out width) &&
                            Double.TryParse(saved[2], NumberStyles.Float, culture, out height) &&
                            width >= MinWidth && width <= Double.MaxValue &&
                            height >= MinHeight && height <= Double.MaxValue)
                        {
                            var workArea = SystemParameters.WorkArea;
                            Width = Math.Max(MinWidth, Math.Min(width, workArea.Width));
                            Height = Math.Max(MinHeight, Math.Min(height, workArea.Height));
                        }
                        if (saved[0] == "Maximized") WindowState = WindowState.Maximized;
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            var lastVisibleState = WindowState;
            StateChanged += (s, e) =>
            {
                if (WindowState != WindowState.Minimized) lastVisibleState = WindowState;
            };
            Closing += (s, e) =>
            {
                if (e.Cancel) return;
                var bounds = RestoreBounds;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllLines(path, new[] { lastVisibleState.ToString(),
                        (bounds.IsEmpty ? Width : bounds.Width).ToString("R", culture),
                        (bounds.IsEmpty ? Height : bounds.Height).ToString("R", culture) });
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            };
        }

        async void Start(string[] args)
        {
            StartSessionTracking();
            if (String.Equals(Path.GetFileName(typeof(Viewer).Assembly.Location), "EgorikaVehicleStudio.exe",
                StringComparison.OrdinalIgnoreCase))
                CheckForStudioUpdateOnStartup();
            if (args.Length != 0)
            {
                await OpenInitialModel(args);
                if (vehicle != null) await SaveSessionSnapshot();
                return;
            }
            if (sessionLock != null && File.Exists(SessionPath))
            {
                if (await OpenProjectAsync(SessionPath, false, true))
                {
                    status.Text = Ui.Text("Предыдущая сессия восстановлена.", "Previous session restored.");
                    return;
                }
                MessageBox.Show(this, status.Text, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            ShowProjectLauncher();
        }

        async Task OpenInitialModel(string[] args)
        {
            if (loading) return;
            loading = true;
            try
            {
                string normal = Path.GetFullPath(args[0]), high = args.Length > 1 ? args[1] : null;
                var paths = args.Length > 2 ? args.Skip(2).ToArray() :
                    DictionaryFiles(Path.GetDirectoryName(normal), Path.GetFileNameWithoutExtension(normal)).ToArray();
                await ConnectSavedArchives();
                var model = await Task.Run(() => new Vehicle(normal, high, paths, gameTextures));
                Load(normal, high, paths, model);
                await CacheVehicleThumbnail(normal, model);
            }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { loading = false; }
        }

        async void ConnectGame()
        {
            if (loading) return;
            string folder = ChooseGame();
            if (folder == null) return;
            if (gameTextures != null && String.Equals(folder, gameFolder, StringComparison.OrdinalIgnoreCase)) return;
            var toolbar = freeCamera.Parent as Panel;
            if (toolbar != null) toolbar.IsEnabled = false;
            status.Text = Ui.Text("Читаю оглавления архивов GTA V…", "Indexing GTA V archives…");
            try
            {
                gameTextures = await Task.Run(() => new GameTextures(folder));
                gameFolder = folder;
                SaveGameFolder(folder);
                if (currentNormal != null) Load(currentNormal, currentHigh, currentDictionaries, null, autoCarcols);
                else status.Text = Ui.Text("Архивы GTA V подключены. Откройте YFT.", "GTA V archives connected. Open a YFT.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Ui.Text("Не удалось подключить архивы GTA V", "Could not connect GTA V archives"));
                RefreshScene();
            }
            finally { if (toolbar != null) toolbar.IsEnabled = true; }
        }

        async void Open()
        {
            if (loading) return;
            var normal = Pick(Ui.Text("Основной YFT машины", "Main vehicle YFT"), Ui.Text("Модели GTA V (*.yft)|*.yft", "GTA V models (*.yft)|*.yft"));
            if (normal == null) return;
            var directory = Path.GetDirectoryName(normal);
            var name = Path.GetFileNameWithoutExtension(normal);
            string high = Path.Combine(directory, name + "_hi.yft");
            if (!File.Exists(high))
                high = Pick(Ui.Text("Дополнительный _hi.yft (можно пропустить)", "Optional _hi.yft"), Ui.Text("Модели GTA V (*.yft)|*.yft", "GTA V models (*.yft)|*.yft"));
            loading = true;
            status.Text = Ui.Text("Загружаю ", "Loading ") + Path.GetFileName(normal) + "…";
            try
            {
                var catalog = VehicleCatalog.FromFolder(directory, SearchOption.TopDirectoryOnly);
                var paths = PrioritizeTextures(DictionaryFiles(directory, name), catalog.TextureDictionaries(name));
                await ConnectSavedArchives();
                var loaded = await Task.Run(() => new Vehicle(normal, high, paths, gameTextures));
                projectParts.Clear(); projectVariations = null;
                sourceArchives = null;
                packageFolder = null;
                autoCarcols = null;
                Load(normal, high, paths, loaded);
                await CacheVehicleThumbnail(normal, loaded);
            }
            catch (Exception ex)
            {
                status.Text = Ui.Text("Ошибка загрузки: ", "Load error: ") + ex.Message;
            }
            finally { loading = false; }
        }

        static string[] PrioritizeTextures(IEnumerable<string> paths, IEnumerable<string> dictionaries)
        {
            var priority = dictionaries.SelectMany(name => new[] { name, name + "+hi" }).ToList();
            return paths.OrderBy(path => priority.FindIndex(name =>
                String.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase))).ToArray();
        }

        IEnumerable<string> DictionaryFiles(string directory, string name)
        {
            var shared = Directory.Exists(textureLibrary)
                ? Directory.EnumerateFiles(textureLibrary, "*.ytd", SearchOption.AllDirectories)
                : Enumerable.Empty<string>();
            var local = Directory.EnumerateFiles(directory, "*.ytd").OrderBy(path =>
            {
                string stem = Path.GetFileNameWithoutExtension(path);
                if (String.Equals(stem, name + "+hi", StringComparison.OrdinalIgnoreCase)) return 2;
                if (String.Equals(stem, name, StringComparison.OrdinalIgnoreCase)) return 1;
                return 0;
            });
            return shared.Concat(local).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        void Load(string normal, string high, IEnumerable<string> dictionaries, Vehicle prepared = null, string packageCarcols = null, string packageVariations = null)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var paths = dictionaries.ToArray();
                var loaded = prepared ?? new Vehicle(normal, high, paths, gameTextures, sourceArchives);
                if (tuningEditor != null && !String.Equals(currentNormal, normal, StringComparison.OrdinalIgnoreCase))
                    CloseTuningEditor();
                foreach (var choice in tuningChoices.Values) SetTuningCheck(choice.Part, false);
                tuningChoices.Clear();
                isolatedBone = null;
                isolatedTuning = null;
                isolatedCenter = null;
                previousLod = null;
                currentNormal = normal;
                manualLiveryPath = null;
                currentHigh = high;
                currentDictionaries = paths;
                spawnColors.ItemsSource = null;
                var variations = packageVariations ?? Path.Combine(Path.GetDirectoryName(normal), "carvariations.meta");
                if (File.Exists(variations))
                    try { ReadVariations(variations, Path.GetFileNameWithoutExtension(normal)); }
                    catch (XmlException) { }
                levels.Items.Clear();
                tree.Items.Clear();
                controls.Clear();
                boneParents.Clear();
                hidden.Clear();
                hideUnboundGeometry = false;
                vehicle = loaded;
                wireMeshes.Clear();
                vehicle.SetReflections(reflect.Value);
                UpdateEnvironment();
                string localCarcols = packageCarcols ?? Path.Combine(Path.GetDirectoryName(normal), "carcols.meta");
                try { PopulateLiveries(File.Exists(localCarcols) ? localCarcols : null); }
                catch (XmlException) { PopulateLiveries(null); }
                if (File.Exists(localCarcols))
                    try { EnsureTuningEditor(localCarcols); }
                    catch (XmlException) { }
                    catch (InvalidDataException) { }
                EnsureTuningEditor(null);
                UpdateTuningTree();
                Title = AppName + " · " + Path.GetFileName(normal);
                var nodes = new Dictionary<int, TreeViewItem>();
                var byIndex = vehicle.Bones.Values.GroupBy(b => b.Index).ToDictionary(g => g.Key, g => (int)g.First().Tag);
                foreach (var pair in vehicle.Bones.OrderBy(p => p.Value.Index))
                {
                    int tag = pair.Key;
                    var box = new CheckBox { Content = new TextBlock { Text = pair.Value.Name }, IsChecked = true };
                    var node = new TreeViewItem { Header = box, Tag = tag, IsExpanded = pair.Value.ParentIndex < 0 };
                    box.PreviewMouseLeftButtonDown += (sender, args) => node.IsSelected = true;
                    controls.Add(tag, box);
                    nodes.Add(tag, node);
                    box.Checked += (s, e) => { hidden.Remove(tag); if (!changingVisibility) RefreshScene(); };
                    box.Unchecked += (s, e) => { hidden.Add(tag); if (!changingVisibility) RefreshScene(); };
                }
                foreach (var pair in vehicle.Bones.OrderBy(p => p.Value.Index))
                {
                    int parentTag;
                    if (byIndex.TryGetValue(pair.Value.ParentIndex, out parentTag) && parentTag != pair.Key)
                    {
                        boneParents.Add(pair.Key, parentTag);
                        nodes[parentTag].Items.Add(nodes[pair.Key]);
                    }
                    else
                        tree.Items.Add(nodes[pair.Key]);
                }
                foreach (var level in vehicle.Levels.Keys) levels.Items.Add(level);
                levels.SelectedIndex = 0;
                ApplyPaint();
                ResetCamera();
            }
            catch (Exception ex)
            {
                if (Application.Current.ShutdownMode == ShutdownMode.OnExplicitShutdown) throw;
                MessageBox.Show(this, ex.Message, Ui.Text("Ошибка загрузки", "Load error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { Mouse.OverrideCursor = null; }
        }

        bool IsBoneHidden(int tag)
        {
            if (hidden.Contains(tag) || (hideUnboundGeometry && !controls.ContainsKey(tag))) return true;
            if (hideChildren.IsChecked != true) return false;
            int ancestor;
            for (int depth = 0; depth < boneParents.Count && boneParents.TryGetValue(tag, out ancestor); depth++)
            {
                if (hidden.Contains(ancestor)) return true;
                tag = ancestor;
            }
            return false;
        }

        void RefreshScene()
        {
            if (vehicle == null || levels.SelectedItem == null) return;
            var level = levels.SelectedItem.ToString();
            var pieces = vehicle.Levels[level];
            var counts = pieces.GroupBy(p => p.Bone).ToDictionary(g => g.Key, g => g.Count());
            var available = new HashSet<int>(counts.Keys);
            if (hideChildren.IsChecked == true)
                foreach (int tag in counts.Keys)
                {
                    int parent, current = tag;
                    for (int depth = 0; depth < boneParents.Count && boneParents.TryGetValue(current, out parent); depth++)
                    {
                        available.Add(parent);
                        current = parent;
                    }
                }
            foreach (var entry in controls)
            {
                bool present = available.Contains(entry.Key);
                entry.Value.IsEnabled = present;
                entry.Value.ToolTip = present ? null : Ui.Text("У кости нет отдельной геометрии на этом LOD", "This bone has no separate geometry at this LOD");
            }
            scene.Children.Clear();
            scene.Children.Add(lighting);
            if (!IsIsolated && renderMode.SelectedIndex == 3) scene.Children.Add(environment);
            UpdateViewportGuides();
            scene.Children.Add(viewportGuides);
            var attachments = new List<Vehicle.Piece>();
            var replacedBones = new HashSet<int>();
            var active = new List<string>();
            CodeWalker.GameFiles.Texture liveryOverride = null;
            foreach (var choice in tuningChoices.Values.Where(item => item.Enabled))
            {
                if (choice.Livery != null) liveryOverride = choice.Livery;
                else
                {
                    List<Vehicle.Piece> atLevel;
                    if (!choice.Pieces.TryGetValue(level, out atLevel) || atLevel.Count == 0) continue;
                    attachments.AddRange(atLevel);
                }
                replacedBones.UnionWith(choice.HiddenBones);
                active.Add(choice.Part.Model);
            }
            List<Vehicle.Piece> visible;
            if (IsIsolated)
            {
                if (isolatedTuning != null && isolatedTuning.Livery != null) liveryOverride = isolatedTuning.Livery;
                attachments.Clear();
                active.Clear();
                visible = IsolationPieces(level).ToList();
            }
            else visible = pieces.Where(p => !IsBoneHidden(p.Bone) && !replacedBones.Contains(p.Bone)).ToList();
            if (hideUnboundGeometry && !IsIsolated) attachments.Clear();
            vehicle.SetTuningLivery(liveryOverride);
            visibleWindows.Clear();
            visibleDecals.Clear();
            occlusion.Children.Clear();
            var translucentMaterials = xray.IsChecked == true ? new Dictionary<Material, Material>() : null;
            if (translucentMaterials == null && renderMode.SelectedIndex == 3 && aoEnabled.IsChecked == true)
                foreach (var piece in visible)
                {
                    var mask = vehicle.PreviewAo(piece);
                    if (mask != null) occlusion.Children.Add(mask);
                }
            foreach (var piece in visible.Concat(attachments.Where(p => !IsBoneHidden(p.Bone))))
            {
                var model = StudioPreview(piece);
                if (translucentMaterials != null) visibleWindows.Add(TranslucentModel(model, translucentMaterials));
                else if (renderMode.SelectedIndex < 2) scene.Children.Add(model);
                else if (piece.Glass) visibleWindows.Add(model);
                else if (piece.Alpha) visibleDecals.Add(model);
                else scene.Children.Add(model);
                var livery = renderMode.SelectedIndex >= 2 && (!IsIsolated || isolatedTuning != null) ? vehicle.PreviewLivery(piece) : null;
                if (livery != null)
                {
                    if (translucentMaterials != null) visibleWindows.Add(TranslucentModel(livery, translucentMaterials));
                    else visibleDecals.Add(livery);
                }
            }
            AddTuningHighlight(level, visible, attachments);
            scene.Children.Add(decals);
            scene.Children.Add(occlusion);
            scene.Children.Add(windows);
            SortWindows();
            status.Text = String.Format(Ui.Text("{0}   •   Треугольники {1:N0} из {2:N0}   •   Привязок к костям {3}", "{0}   •   Triangles {1:N0} of {2:N0}   •   Bone bindings {3}"),
                level, Triangles(visible), Triangles(pieces), counts.Keys.Count(tag => vehicle.Bones.ContainsKey(tag)));
            bool info = showViewportInfo.IsChecked == true, stats = showStatistics.IsChecked == true;
            viewportInfo.Visibility = info || stats ? Visibility.Visible : Visibility.Collapsed;
            viewportInfo.Text = (info ? Path.GetFileNameWithoutExtension(currentNormal ?? currentHigh) + "  ·  " + level : "") +
                (info && stats ? Environment.NewLine : "") + (stats ? status.Text : "");
            if (IsIsolated)
                status.Text += Ui.Text("   •   Изоляция: ", "   •   Local view: ") +
                    (isolatedBone.HasValue ? vehicle.Bones[isolatedBone.Value].Name : isolatedTuning.Part.Model) + " · NumPad Del";
            if (active.Count > 0) status.Text += Ui.Text("   •   Примерка: ", "   •   Preview: ") + String.Join(", ", active);
            if (vehicle.TextureErrors.Count > 0)
                status.Text += Ui.Text("   •   Ошибки текстур: ", "   •   Texture errors: ") + vehicle.TextureErrors.Count;
            if (vehicle.MissingTextures.Count > 0)
                status.Text += Ui.Text("   •   Не найдены текстуры: ", "   •   Missing textures: ") + vehicle.MissingTextures.Count;
            if (freeCamera.IsChecked == true)
                status.Text += Ui.Text("   •   WASD — полёт, Q/E — вниз/вверх, Shift — быстрее", "   •   WASD to fly, Q/E down/up, Shift to accelerate");
            status.ToolTip = String.Join(Environment.NewLine, vehicle.TextureErrors.Concat(vehicle.MissingTextures.OrderBy(name => name)));
        }

        void ResetCamera()
        {
            StopAxisTransition();
            orbitPan = new Vector3D();
            yaw = 0.85;
            pitch = 0.28;
            distance = isolatedCenter.HasValue ? isolatedRadius * 3.1 : vehicle == null ? 8 : vehicle.Radius * 2.9;
            freeCamera.IsChecked = false;
            MoveCamera();
        }

        void MoveCamera()
        {
            var center = (isolatedCenter ?? (vehicle == null ? new Point3D() : vehicle.Center)) + orbitPan;
            double aspect = viewport.ActualHeight > 0 ? viewport.ActualWidth / viewport.ActualHeight : 1.6;
            double framedDistance = distance * Math.Sqrt(Math.Max(1, aspect / 1.65));
            var offset = new Vector3D(Math.Cos(yaw) * Math.Cos(pitch), Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch)) * framedDistance;
            camera.Position = center + offset;
            camera.LookDirection = -offset;
            if (freeCamera.IsChecked == true)
            {
                camera.Position = freePosition;
                camera.LookDirection = FlightDirection();
            }
            camera.UpDirection = freeCamera.IsChecked == true ? new Vector3D(0, 0, 1) :
                new Vector3D(-Math.Cos(yaw) * Math.Sin(pitch), -Math.Sin(yaw) * Math.Sin(pitch), Math.Cos(pitch));
            orthographic.Position = camera.Position;
            orthographic.LookDirection = camera.LookDirection;
            orthographic.UpDirection = camera.UpDirection;
            if (updateAxisGizmo != null) updateAxisGizmo();
            orthographic.Width = Math.Max(0.1, 2 * framedDistance * Math.Tan(Math.PI * camera.FieldOfView / 360));
            SortWindows();
        }

        void SortWindows()
        {
            DrawTuningOutline();
            SetDrawOrder(decals, ByDepth(visibleDecals));
            SetDrawOrder(windows, ByDepth(visibleWindows));
        }

        static void SetDrawOrder(Model3DGroup target, IEnumerable<GeometryModel3D> models)
        {
            var ordered = models.ToArray();
            if (target.Children.SequenceEqual(ordered)) return;
            target.Children.Clear();
            foreach (var model in ordered) target.Children.Add(model);
        }

        IEnumerable<GeometryModel3D> ByDepth(IEnumerable<GeometryModel3D> models)
        {
            return models.OrderByDescending(model =>
            {
                var bounds = model.Bounds;
                var center = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
                return Vector3D.DotProduct(center - camera.Position, camera.LookDirection);
            });
        }

        static int Triangles(IEnumerable<Vehicle.Piece> pieces)
        {
            return pieces.Sum(p => ((MeshGeometry3D)p.Model.Geometry).TriangleIndices.Count / 3);
        }

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 4 && args[0] == "--apply-studio-update")
                return StudioUpdate.Apply(args[1], args[2], args[3]);
            if (args.Length == 0 && StudioUpdate.TryApplyOnRestart()) return 0;
            try
            {
                StudioDependencies.RegisterResolver();
                if (args.Length == 0) StudioDependencies.PrepareForLaunch();
                else StudioDependencies.EnsureCodeWalker();
            }
            catch (Exception ex)
            {
                string message = Ui.Text(
                    "Не удалось загрузить CodeWalker. Проверьте интернет и право записи в папку программы, затем запустите её снова.",
                    "Could not download CodeWalker. Check your internet connection and folder permissions, then launch the app again.");
                if (args.Length >= 2 && args[0] == "--check") File.WriteAllText(args[1], message + Environment.NewLine + ex);
                else MessageBox.Show(message + Environment.NewLine + ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
            return RunStudio(args);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static int RunStudio(string[] args)
        {
            if (args.Length >= 4 && args[0] == "--bake-thumbnails")
            {
                VehicleCatalogRow.Bake(new GameTextures(args[1]), args[2], args.Skip(3).ToArray());
                return 0;
            }
            if (args.Length >= 4 && args[0] == "--check")
            {
                try { return Check(args); }
                catch (Exception ex) { File.WriteAllText(args[1], ex.ToString()); return 1; }
            }
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var viewer = new Viewer();
            viewer.RememberWindowPlacement();
            viewer.Loaded += (s, e) => viewer.Start(args);
            return app.Run(viewer);
        }

        static int Check(string[] args)
        {
            if (Ui.Translate("es", "Файл", "File") != "Archivo" ||
                Ui.Translate("fr", "Файл", "File") != "Fichier" ||
                Ui.Translate("pt-BR", "Файл", "File") != "Arquivo")
                throw new InvalidOperationException("Interface translations were not loaded.");
            if (Environment.GetEnvironmentVariable("SENTINEL_CHECK_EN") == "1") Ui.UseEnglishForCheck();
            string gta = Environment.GetEnvironmentVariable("SENTINEL_CHECK_GTA_PATH");
            var game = String.IsNullOrEmpty(gta) ? null : new GameTextures(gta);
            var vehicle = new Vehicle(args[2], args[3], args.Skip(4), game);
            var lines = new List<string>();
            if (game != null)
            {
                var preset = game.ReadCommonStatMods();
                if (preset == null || !preset.Elements("Item").Any())
                    throw new InvalidOperationException("The GTA V statMods preset was not read from the archives.");
                lines.Add("Most common GTA V statMods preset: " + preset.Elements("Item").Count() + " entries.");
            }
            lines.Add("Bones: " + vehicle.Bones.Count + "; diffuse materials: " + vehicle.AppliedTextures);
            foreach (var pair in vehicle.Levels)
            {
                lines.Add(pair.Key + ": " + Triangles(pair.Value) + " triangles");
                foreach (var bone in vehicle.Bones.Where(b => b.Value.Name == "bonnet" ||
                    b.Value.Name == "door_dside_f" || b.Value.Name.StartsWith("wheel_")))
                    lines.Add("  " + bone.Value.Name + ": " + Triangles(pair.Value.Where(p => p.Bone == bone.Key)));
            }
            lines.AddRange(vehicle.TextureErrors);
            lines.Add("Missing textures: " + String.Join(", ", vehicle.MissingTextures.OrderBy(n => n)));
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var testWindow = new Viewer { Width = 1000, Height = 660, ShowInTaskbar = false };
            testWindow.gameTextures = game;
            testWindow.Load(args[2], args[3], args.Skip(4));
            int bonnet = vehicle.Bones.First(p => p.Value.Name == "bonnet").Key;
            testWindow.controls[bonnet].IsChecked = false;
            foreach (string level in vehicle.Levels.Keys)
            {
                testWindow.levels.SelectedItem = level;
                var models = testWindow.scene.Children.OfType<GeometryModel3D>().Concat(testWindow.windows.Children.OfType<GeometryModel3D>()).Concat(testWindow.decals.Children.OfType<GeometryModel3D>()).ToList();
                int expected = vehicle.Levels[level].Count(p => p.Bone != bonnet);
                if (models.Count != expected || testWindow.controls[bonnet].IsChecked != false)
                    throw new InvalidOperationException("Bone visibility failed at " + level);
            }
            testWindow.levels.SelectedIndex = 0;
            testWindow.controls[bonnet].IsChecked = true;
            testWindow.SetAllBones(false);
            if (testWindow.controls.Keys.Any(tag => !testWindow.IsBoneHidden(tag)))
                throw new InvalidOperationException("Hide all did not hide all bones.");
            testWindow.SetAllBones(true);
            if (testWindow.controls.Keys.Any(tag => testWindow.IsBoneHidden(tag)))
                throw new InvalidOperationException("Show all did not restore all bones.");
            int bumper = vehicle.Bones.First(p => p.Value.Name == "bumper_r").Key;
            int childBone = testWindow.boneParents.First(p => p.Value == bumper).Key;
            testWindow.controls[bumper].IsChecked = false;
            if (testWindow.IsBoneHidden(childBone))
                throw new InvalidOperationException("Child hidden when propagation is off.");
            testWindow.hideChildren.IsChecked = true;
            if (!testWindow.IsBoneHidden(childBone))
                throw new InvalidOperationException("Child remained visible with hidden parent.");
            testWindow.controls[childBone].IsChecked = false;
            testWindow.controls[bumper].IsChecked = true;
            if (!testWindow.IsBoneHidden(childBone))
                throw new InvalidOperationException("Child's own visibility was reset.");
            testWindow.controls[childBone].IsChecked = true;
            testWindow.hideChildren.IsChecked = false;
            if (testWindow.visibleWindows.Count == 0 || testWindow.visibleDecals.Count == 0)
                throw new InvalidOperationException("Transparent windows or decals were not detected.");
            if (testWindow.palette.Length != 161 || testWindow.palette[158][2] != "#47391B")
                throw new InvalidOperationException("Embedded palette is incomplete.");
            var primary = testWindow.vehicle.Levels["_hi.yft"].First(p => p.PaintLayer == 1);
            if (testWindow.spawnColors.Items.Count != 8)
                throw new InvalidOperationException("Sentinel5 spawn presets were not loaded.");
            testWindow.spawnColors.SelectedIndex = 5;
            if (testWindow.paintFields[1].Text != "#292C2E" ||
                !(testWindow.vehicle.Preview(primary).Material is MaterialGroup))
                throw new InvalidOperationException("Spawn paint or pearl highlight was not applied.");
            testWindow.paintFields[1].Text = "Chrome";
            testWindow.ApplyPaint();
            var chrome = testWindow.vehicle.Preview(primary).Material as MaterialGroup;
            if (chrome == null || !chrome.Children.OfType<SpecularMaterial>().Any(material => material.SpecularPower >= 80))
                throw new InvalidOperationException("Chrome preset was rendered as flat paint.");
            testWindow.paintFields[1].Text = "#E13529";
            testWindow.ApplyPaint();
            if (Object.ReferenceEquals(primary.Model.Material, testWindow.vehicle.Preview(primary).Material))
                throw new InvalidOperationException("Primary paint did not update.");
            lines.Add("Paint layers on Sentinel5: " + String.Join(", ", testWindow.vehicle.Levels.Values
                .SelectMany(pieces => pieces).Select(piece => piece.PaintLayer).Where(layer => layer != 0)
                .Distinct().OrderBy(layer => layer)));
            testWindow.freeCamera.IsChecked = true;
            var beforeFlight = testWindow.camera.Position;
            testWindow.freePosition += testWindow.FlightDirection();
            testWindow.MoveCamera();
            if ((testWindow.camera.Position - beforeFlight).Length < 0.99)
                throw new InvalidOperationException("Free camera position did not update.");
            testWindow.freeCamera.IsChecked = false;
            if (game != null)
            {
                var modelList = game.Models().ToArray();
                var variants = modelList.Where(item => item.Name == "sentinel5").ToArray();
                if (!modelList.Any(item => item.Name == "adder") ||
                    modelList.Any(item => item.Name == "player_zero" || item.Name.StartsWith("prop_")))
                    throw new InvalidOperationException("Vehicle list filtering failed.");
                if (variants.Length < 2 || !variants[0].ToString().Contains("patch2026_01"))
                    throw new InvalidOperationException("Sentinel5 patch ordering failed.");
                string bundle = Path.Combine(Path.GetTempPath(), "EgorikaVehicle-" + Guid.NewGuid().ToString("N"));
                testWindow.previewFolders.Add(bundle);
                string archivedModel = game.ExportPreview(variants[0], bundle);
                if (!File.Exists(archivedModel) || !File.Exists(Path.Combine(bundle, "carcols.meta")) ||
                    !File.Exists(Path.Combine(bundle, "carvariations.meta")))
                    throw new InvalidOperationException("Game model bundle has missing files.");
                if (!File.Exists(Path.Combine(bundle, "vehicles_jugular_interior.ytd")))
                    throw new InvalidOperationException("Shared interior dictionary was not resolved from vehicles.meta.");
                var archived = new Vehicle(archivedModel, Path.Combine(bundle, "sentinel5_hi.yft"),
                    Directory.GetFiles(bundle, "*.ytd"), game);
                if (archived.MissingTextures.Any(name => name.StartsWith("jugular_", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Sentinel5 interior textures did not resolve.");
                if (!archived.Levels.ContainsKey("_hi.yft"))
                    throw new InvalidOperationException("Patched archive YFT was not loaded.");
                var externalPath = Path.Combine(gta, "update", "x64", "dlcpacks", "mp2025_01", "dlc.rpf");
                var external = GameTextures.OpenRpf(externalPath);
                var externalModel = external.Models().First(item => item.Name == "sentinel5");
                string externalFolder = Path.Combine(Path.GetTempPath(), "EgorikaVehicle-" + Guid.NewGuid().ToString("N"));
                testWindow.previewFolders.Add(externalFolder);
                string extracted = external.ExportPreview(externalModel, externalFolder);
                if (!File.Exists(extracted) || !File.Exists(Path.Combine(externalFolder, "carcols.meta")))
                    throw new InvalidOperationException("External RPF source failed.");
                if (FindPackageMetadata(externalFolder, "carcols", "sentinel5") == null)
                    throw new InvalidOperationException("Folder metadata discovery failed.");
                if (!PackageVehicles(externalFolder).Any(path => Path.GetFileName(path) == "sentinel5.yft"))
                    throw new InvalidOperationException("Vehicle skeleton fallback did not detect the package model.");
                var everon = modelList.First(item => item.Name == "everon3");
                string everonFolder = Path.Combine(Path.GetTempPath(), "EgorikaVehicle-" + Guid.NewGuid().ToString("N"));
                testWindow.previewFolders.Add(everonFolder);
                game.ExportPreview(everon, everonFolder);
                if (!File.Exists(Path.Combine(everonFolder, "vehicles_cav_interior.ytd")))
                    throw new InvalidOperationException("Everon3 shared interior dictionary did not load.");
                lines.Add("Vehicle-only lists, standalone RPF and TXD inheritance for Sentinel5 and Everon3 passed.");
                if (testWindow.liveries.Items.Count != 12)
                    throw new InvalidOperationException("Sentinel5 livery list is incomplete.");
                foreach (var option in testWindow.liveries.Items.Cast<LiveryOption>().Skip(1))
                    if (game.ReadLivery(option.Model) == null)
                        throw new InvalidOperationException("Missing livery: " + option.Model);
                testWindow.liveries.SelectedIndex = 1;
                int meshes = testWindow.vehicle.Levels["_hi.yft"].Count(p => testWindow.vehicle.PreviewLivery(p) != null);
                if (meshes == 0) throw new InvalidOperationException("No UV meshes for livery.");
                var highPieces = testWindow.vehicle.Levels["_hi.yft"];
                if (!highPieces.Any(p => testWindow.vehicle.PreviewLivery(p) != null &&
                    testWindow.vehicle.PreviewAo(p) != null))
                    throw new InvalidOperationException("AO was not applied over any livery geometry.");
                if (highPieces.Any(p => (p.Alpha || p.TextureHasAlpha) && testWindow.vehicle.PreviewAo(p) != null))
                    throw new InvalidOperationException("AO was applied over a transparent base material.");
                lines.Add("All 11 livery textures found; first livery meshes: " + meshes);
                var liveryPiece = new Vehicle.Piece { LiveryMesh = highPieces.First(piece => piece.LiveryMesh != null).LiveryMesh };
                var liveryCases = new[] {
                    new { Name = "comet6_livery6", IsMask = true },
                    new { Name = "comet7_livery1", IsMask = true },
                    new { Name = "comet6_livery7", IsMask = false }
                };
                foreach (var sample in liveryCases)
                {
                    var texture = game.ReadLivery(sample.Name);
                    var sourcePixels = CodeWalker.Utils.DDSIO.GetPixels(texture, 0);
                    testWindow.vehicle.SetTuningLivery(texture);
                    liveryPiece.LiveryMultiplier = SharpDX.Vector4.One;
                    var brush = (ImageBrush)((DiffuseMaterial)testWindow.vehicle.PreviewLivery(liveryPiece).Material).Brush;
                    var image = (System.Windows.Media.Imaging.BitmapSource)brush.ImageSource;
                    var previewPixels = new byte[sourcePixels.Length];
                    image.CopyPixels(previewPixels, image.PixelWidth * 4, 0);
                    if (sample.IsMask)
                    {
                        for (int i = 0; i < previewPixels.Length; i += 4)
                            if (previewPixels[i] != 0 || previewPixels[i + 1] != 0 || previewPixels[i + 2] != 0 ||
                                previewPixels[i + 3] != sourcePixels[i])
                                throw new InvalidOperationException("Livery A8 did not render as black with the original coverage: " + sample.Name);
                    }
                    else if (!previewPixels.SequenceEqual(sourcePixels))
                        throw new InvalidOperationException("Color livery pixels changed: " + sample.Name);

                    liveryPiece.LiveryMultiplier = new SharpDX.Vector4(1, 0.5f, 0.25f, 0.5f);
                    var adjusted = (DiffuseMaterial)testWindow.vehicle.PreviewLivery(liveryPiece).Material;
                    image = (System.Windows.Media.Imaging.BitmapSource)((ImageBrush)adjusted.Brush).ImageSource;
                    image.CopyPixels(previewPixels, image.PixelWidth * 4, 0);
                    for (int i = 0; i < previewPixels.Length; i += 4)
                    {
                        if (previewPixels[i] != (sample.IsMask ? 0 : (int)Math.Round(sourcePixels[i] / 4.0)) ||
                            previewPixels[i + 1] != (sample.IsMask ? 0 : (int)Math.Round(sourcePixels[i + 1] / 2.0)) ||
                            previewPixels[i + 2] != (sample.IsMask ? 0 : sourcePixels[i + 2]) ||
                            previewPixels[i + 3] != (int)Math.Round(sourcePixels[i + (sample.IsMask ? 0 : 3)] / 2.0))
                            throw new InvalidOperationException("matDiffuseColor2 did not modulate the livery: " + sample.Name);
                    }
                    liveryPiece.LiveryMultiplier.W = 0;
                    if (testWindow.vehicle.PreviewLivery(liveryPiece) != null)
                        throw new InvalidOperationException("Zero livery opacity should suppress the overlay.");
                }
                testWindow.vehicle.SetTuningLivery(null);
                lines.Add("A8 and color liveries honor material RGB and alpha modulation.");
            }
            if (ParseSceneTime("18:45") != 1125 || ParseSceneTime("24:00").HasValue ||
                ParseReflection("45,5 %") != 0.455 || ParseReflection("101").HasValue)
                throw new InvalidOperationException("Scene value input parsing failed.");
            testWindow.clock.Value = 0;
            if (testWindow.clockLabel.Text != "00:00" || testWindow.lighting.Children.Count != 2)
                throw new InvalidOperationException("Night lighting failed.");
            var aoPiece = testWindow.vehicle.Levels["_hi.yft"].First(piece => testWindow.vehicle.PreviewAo(piece) != null);
            var aoModel = testWindow.vehicle.PreviewAo(aoPiece);
            if (testWindow.vehicle.Levels.Values.SelectMany(pieces => pieces).Any(piece =>
                piece.Model.BackMaterial != null || testWindow.vehicle.Preview(piece).BackMaterial != null) ||
                (aoModel != null && aoModel.BackMaterial != null))
                throw new InvalidOperationException("Vehicle materials must render on the front face only.");
            if (aoModel == null || !(aoModel.Material is DiffuseMaterial) ||
                testWindow.occlusion.Children.Count == 0)
                throw new InvalidOperationException("AO preview does not preserve mesh triangles.");
            if (testWindow.tuningTree.Items.Count < 10 || testWindow.tuningEditor.IsVisible)
                throw new InvalidOperationException("Tuning tree should populate without opening the editor.");
            testWindow.CloseTuningEditor();
            var editor = new TuningEditor("sentinel5", testWindow.vehicle.Bones.Values.Select(bone => bone.Name));
            if (!editor.ImportPart(args[2]) || editor.ImportPart(args[2]))
                throw new InvalidOperationException("YFT import or duplicate detection failed.");
            string metadata = Path.Combine(Path.GetDirectoryName(args[2]), "carcols.meta");
            if (editor.LoadCarcols(metadata) < 70) throw new InvalidOperationException("Sentinel5 carcols import failed.");
            var roundTrip = editor.ExportDocument(606, "606_sentinel5_modkit");
            var currentKit = editor.ExportVehicleDocument(606, "606_sentinel5_modkit");
            if (currentKit.Root.Element("Kits").Elements("Item").Count() != 1 ||
                roundTrip.Root.Element("Kits").Elements("Item").Count() <= 1 ||
                (string)currentKit.Root.Element("Kits").Element("Item").Element("kitName") != "606_sentinel5_modkit")
                throw new InvalidOperationException("Vehicle-only and full carcols exports did not preserve their scopes.");
            var original = System.Xml.Linq.XDocument.Load(metadata, System.Xml.Linq.LoadOptions.PreserveWhitespace);
            if (!System.Xml.Linq.XNode.DeepEquals(original.Root, roundTrip.Root))
            {
                throw new InvalidOperationException("Unmodified carcols did not round-trip.");
            }
            using (var formatted = new MemoryStream())
            {
                TuningEditor.WriteCarcols(roundTrip, formatted);
                string xml = System.Text.Encoding.UTF8.GetString(formatted.ToArray());
                if (!xml.Contains("        <Item>" + Environment.NewLine + "          <modelName>") ||
                    xml.Contains("<Item><modelName>"))
                    throw new InvalidOperationException("Exported carcols entries were not indented.");
                formatted.Position = 0;
                var parsed = System.Xml.Linq.XDocument.Load(formatted);
                if (!System.Xml.Linq.XNode.DeepEquals(System.Xml.Linq.XDocument.Load(metadata).Root, parsed.Root))
                    throw new InvalidOperationException("Formatting changed carcols data.");
            }
            var withAddedPart = new System.Xml.Linq.XDocument(roundTrip);
            var added = TuningEditor.CreateDocument(606, "606_sentinel5_modkit",
                new[] { new TuningEditor.Part { Model = "astrone_roof_1", Label = "ASTRONE_ROOF_1",
                    Type = "VMT_ROOF", Bone = "chassis", CollisionBone = "chassis", Linked = "roof_link" } });
            withAddedPart.Descendants("visibleMods").First().Add(added.Descendants("visibleMods").Single().Element("Item"));
            using (var formatted = new MemoryStream())
            {
                TuningEditor.WriteCarcols(withAddedPart, formatted);
                string xml = System.Text.Encoding.UTF8.GetString(formatted.ToArray());
                if (!xml.Contains("        <Item>" + Environment.NewLine + "          <modelName>astrone_roof_1</modelName>") ||
                    !xml.Contains("          <linkedModels>" + Environment.NewLine + "            <Item>roof_link</Item>"))
                    throw new InvalidOperationException("New entries in imported carcols were not indented.");
            }
            var cleanEditor = new TuningEditor("sentinel5", testWindow.vehicle.Bones.Values.Select(bone => bone.Name));
            var mixedCasePart = added.Descendants("visibleMods").Single().Element("Item");
            mixedCasePart.Element("linkedModels").Element("Item").Name = "item";
            mixedCasePart.Element("turnOffBones").Add(new System.Xml.Linq.XElement("Item", "misc_c"),
                new System.Xml.Linq.XElement("item", "misc_d"));
            string mixedCaseFile = Path.GetTempFileName();
            try
            {
                added.Save(mixedCaseFile);
                cleanEditor.LoadCarcols(mixedCaseFile, false);
                var importedPart = cleanEditor.PreviewParts.Single();
                if (importedPart.Linked != "roof_link" || importedPart.TurnOff != "misc_c, misc_d")
                    throw new InvalidOperationException("Lowercase carcols list entries were not imported.");
                importedPart.TurnOff = "misc_d, misc_e";
                var editedPart = cleanEditor.ExportVehicleDocument(606, "606_sentinel5_modkit")
                    .Descendants("visibleMods").Single().Element("Item");
                if ((string)editedPart.Element("linkedModels").Element("item") != "roof_link" ||
                    (string)editedPart.Element("turnOffBones").Element("item") != "misc_d" ||
                    (string)editedPart.Element("turnOffBones").Element("Item") != "misc_e")
                    throw new InvalidOperationException("Edited carcols lists lost their entry casing.");
            }
            finally { File.Delete(mixedCaseFile); }
            cleanEditor.LoadCarcols(metadata);
            cleanEditor.ResetKit(null);
            var fresh = cleanEditor.ExportDocument(702, "702_sentinel5_modkit");
            var freshKit = fresh.Root.Element("Kits").Elements("Item").Single();
            if (fresh.Root.Elements().Count() != 1 || freshKit.Element("visibleMods").HasElements ||
                freshKit.Element("statMods").Elements("Item").Count() != 77 || freshKit.Element("linkMods").HasElements)
                throw new InvalidOperationException("New carcols retained imported kit data.");
            if (!System.Xml.Linq.XNode.DeepEquals(freshKit.Element("statMods"),
                cleanEditor.ExportVehicleDocument(702, "702_sentinel5_modkit").Descendants("statMods").Single()))
                throw new InvalidOperationException("New carcols exported different statMods in the two modes.");
            cleanEditor.SaveAdvancedSections(new System.Xml.Linq.XElement("statMods"), new System.Xml.Linq.XElement("slotNames"));
            if (cleanEditor.ExportVehicleDocument(702, "702_sentinel5_modkit").Descendants("statMods").Single().HasElements)
                throw new InvalidOperationException("An explicitly cleared statMods section was replaced by the default.");
            var example = original.Root.Element("Kits").Elements("Item").First(item => item.Element("statMods") != null &&
                item.Element("statMods").HasElements);
            cleanEditor.ResetKit(example);
            var seeded = cleanEditor.ExportDocument(703, "703_sentinel5_modkit").Root.Element("Kits").Elements("Item").Single();
            if (!System.Xml.Linq.XNode.DeepEquals(example.Element("statMods"), seeded.Element("statMods")) ||
                seeded.Element("visibleMods").HasElements || seeded.Element("slotNames").HasElements)
                throw new InvalidOperationException("New kit template copied unexpected sections.");
            cleanEditor.Close();
            testWindow.tuningEditor = editor;
            testWindow.UpdateTuningTree();
            if (testWindow.tuningTree.Items.Count < 10)
                throw new InvalidOperationException("VMT category grouping failed.");
            if (game != null)
            {
                var part = editor.PreviewParts.First(p => p.Model == "sent5_splt_0");
                testWindow.PreviewTuning(part, editor.PreviewParts, metadata);
                if (testWindow.tuningChoices[Category(part)].Pieces["_hi.yft"].Count == 0)
                    throw new InvalidOperationException("Tuning attachment was not rendered.");
                var wrap = editor.PreviewParts.First(p => p.Type == "VMT_LIVERY_MOD");
                testWindow.PreviewTuning(wrap, editor.PreviewParts, metadata);
                if (!testWindow.tuningChoices[Category(wrap)].Enabled || !testWindow.tuningChoices[Category(part)].Enabled)
                    throw new InvalidOperationException("Tuning livery preview failed.");
                int stockTag = testWindow.vehicle.Bones.Values.First(b => b.Name == "misc_d").Tag;
                var stock = testWindow.vehicle.Levels["_hi.yft"].First(p => p.Bone == stockTag).Model.Geometry;
                Func<bool> stockVisible = () => testWindow.scene.Children.OfType<GeometryModel3D>()
                    .Concat(testWindow.decals.Children.OfType<GeometryModel3D>())
                    .Concat(testWindow.windows.Children.OfType<GeometryModel3D>()).Any(model => model.Geometry == stock);
                if (stockVisible()) throw new InvalidOperationException("turnOffBones did not hide stock geometry.");
                testWindow.tuningChecks[part].IsChecked = false;
                if (!stockVisible()) throw new InvalidOperationException("Stock geometry did not return.");
                if (testWindow.tuningChoices[Category(part)].Enabled || !testWindow.tuningChoices[Category(wrap)].Enabled)
                    throw new InvalidOperationException("Disabling a tuning category changed another category.");
                testWindow.tuningChecks[part].IsChecked = true;
                if (!testWindow.tuningChoices[Category(part)].Enabled)
                    throw new InvalidOperationException("Tuning checkbox did not restore the part.");
                var alternate = editor.PreviewParts.First(p => p.Model == "sent5_splt_1");
                testWindow.tuningChecks[alternate].IsChecked = true;
                if (testWindow.tuningChecks[part].IsChecked == true ||
                    testWindow.tuningChecks[wrap].IsChecked != true ||
                    testWindow.tuningChoices[Category(part)].Part != alternate)
                    throw new InvalidOperationException("Only one part in each VMT category should remain enabled.");
            }
            testWindow.tuningEditor = null;
            editor.Close();
            lines.Add("carcols round-trip, VMT categories and tuning preview checks passed.");
            var tuning = TuningEditor.CreateDocument(606, "606_sentinel5_modkit",
                new[] { new TuningEditor.Part { Model = "sample_mirror", Label = "SAMPLE&TEST",
                    Linked = "sample_mirror_right", TurnOff = "misc_t, misc_c", Type = "VMT_CHASSIS",
                    Bone = "chassis", CollisionBone = "chassis" } });
            var tuningItem = tuning.Descendants("visibleMods").Single().Element("Item");
            if (tuningItem.Element("turnOffBones").Elements().Count() != 2 ||
                tuningItem.Element("linkedModels").Element("Item").Value != "sample_mirror_right" ||
                !tuning.ToString().Contains("SAMPLE&amp;TEST"))
                throw new InvalidOperationException("Tuning XML output is invalid.");
            testWindow.aoEnabled.IsChecked = true;
            lines.Add("AO preview and carcols XML checks passed.");
            testWindow.environmentChoice.SelectedItem = Ui.Text("Дорога", "Road");
            if (((Model3DGroup)testWindow.environment.Children[0]).Children.Count < 6)
                throw new InvalidOperationException("Road environment was not created.");
            testWindow.clock.Value = 780;
            if (testWindow.lighting.Children.Count != 3) throw new InvalidOperationException("Day lighting failed.");
            testWindow.yaw = 0.85;
            testWindow.MoveCamera();
            if (Environment.GetEnvironmentVariable("SENTINEL_CHECK_NO_IMAGE") != "1")
            {
                testWindow.EnsureTuningEditor(metadata);
                var category = testWindow.tuningTree.Items.OfType<TreeViewItem>().First(item => item.Items.Count > 0);
                category.IsExpanded = true;
                testWindow.Show();
                testWindow.UpdateLayout();
                if (testWindow.pendingTuning.ActualWidth < 40 || testWindow.tuningTableSize.Width < 200)
                    throw new InvalidOperationException("Tuning columns do not fill the editor pane.");
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1000, 660, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render((Visual)testWindow.Content);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var output = File.Create(Path.ChangeExtension(args[1], ".png"))) encoder.Save(output);
                double xmlFont = testWindow.xmlPreview.FontSize;
                double paintWidth = testWindow.paintFields[1].Width;
                var paint = (StackPanel)((StackPanel)testWindow.paintFields[1].Parent).Parent;
                var apply = paint.Children.OfType<Button>().Single();
                Func<bool> singlePaintRow = () => apply.TranslatePoint(new Point(apply.ActualWidth, 0), paint).X <= paint.ActualWidth + 1 &&
                    Math.Abs(apply.TranslatePoint(new Point(0, apply.ActualHeight / 2), paint).Y -
                        testWindow.paintFields[7].TranslatePoint(new Point(0, testWindow.paintFields[7].ActualHeight / 2), paint).Y) < 1;
                if (!singlePaintRow()) throw new InvalidOperationException("Apply colors did not fit on the color toolbar.");
                testWindow.Width = 1350;
                testWindow.UpdateLayout();
                if (!singlePaintRow() || testWindow.xmlPreview.FontSize <= xmlFont || testWindow.paintFields[1].Width <= paintWidth)
                    throw new InvalidOperationException("XML preview and paint controls did not scale with the workspace.");
            }
            testWindow.Close();
            if (game != null)
            {
                var woodlander = game.Models().First(model => model.Name == "woodlander");
                string folder = Path.Combine(Path.GetTempPath(), "EgorikaVehicle-" + Guid.NewGuid().ToString("N"));
                var woodView = new Viewer { gameTextures = game, sourceArchives = game };
                woodView.previewFolders.Add(folder);
                var woodModel = game.ExportPreview(woodlander, folder);
                woodView.Load(woodModel, Path.Combine(folder, "woodlander_hi.yft"), Directory.GetFiles(folder, "*.ytd"));
                var mirrors = woodView.tuningEditor.PreviewParts.First(p => p.Model == "wood_mirr_df2");
                if (woodView.tuningEditor.LinkedBone("wood_mirr_pf2") != "door_pside_f")
                    throw new InvalidOperationException("linkMods attachment bone was not read.");
                woodView.PreviewTuning(mirrors, woodView.tuningEditor.PreviewParts, Path.Combine(folder, "carcols.meta"));
                var mirrorMeshes = woodView.tuningChoices[Category(mirrors)].Pieces["_hi.yft"];
                int driver = woodView.vehicle.Bones.Values.First(b => b.Name == "door_dside_f").Tag;
                int passenger = woodView.vehicle.Bones.Values.First(b => b.Name == "door_pside_f").Tag;
                if (!mirrorMeshes.Any(p => p.Bone == driver && p.Model.Bounds.X + p.Model.Bounds.SizeX < 0) ||
                    !mirrorMeshes.Any(p => p.Bone == passenger && p.Model.Bounds.X > 0))
                    throw new InvalidOperationException("Linked mirrors were not placed on opposite doors.");
                var link = woodView.tuningEditor.LinkMods.First(item => item.Model == "wood_mirr_pf2");
                link.Bone = "chassis";
                woodView.RefreshLinkedTuning(link.Model);
                var editedPieces = woodView.tuningChoices[Category(mirrors)].Pieces["_hi.yft"];
                int chassis = woodView.vehicle.Bones.Values.First(b => b.Name == "chassis").Tag;
                if (!editedPieces.Any(p => p.Bone == chassis) || editedPieces.Any(p => p.Bone == passenger))
                    throw new InvalidOperationException("Edited linkMods bone was not used in preview.");
                var edited = woodView.tuningEditor.ExportDocument(613, "613_woodlander_modkit");
                var exportedLink = edited.Root.Element("Kits").Elements("Item").First(k =>
                    (string)k.Element("kitName") == "613_woodlander_modkit").Element("linkMods").Elements("Item")
                    .First(item => (string)item.Element("modelName") == "wood_mirr_pf2");
                if ((string)exportedLink.Element("bone") != "chassis" || exportedLink.Element("turnOffExtra") == null)
                    throw new InvalidOperationException("Edited linkMods bone did not round-trip.");
                woodView.Close();
                lines.Add("Woodlander linked mirrors use separate door bones and render on opposite sides.");
                var astron = game.Models().First(model => model.Name == "astron");
                string astronFolder = Path.Combine(Path.GetTempPath(), "EgorikaVehicle-" + Guid.NewGuid().ToString("N"));
                var astronView = new Viewer { gameTextures = game, sourceArchives = game };
                astronView.previewFolders.Add(astronFolder);
                var astronModel = game.ExportPreview(astron, astronFolder);
                astronView.Load(astronModel, Path.Combine(astronFolder, "astron_hi.yft"), Directory.GetFiles(astronFolder, "*.ytd"));
                var bonnetTag = astronView.vehicle.Bones.Values.First(b => b.Name == "bonnet").Tag;
                var paintLayers = new List<string>();
                foreach (string hood in new[] { "astron_bon_3", "astron_bon_3a", "astron_bon_3b" })
                {
                    var mod = astronView.tuningEditor.PreviewParts.Single(part => part.Model == hood);
                    astronView.PreviewTuning(mod, astronView.tuningEditor.PreviewParts, Path.Combine(astronFolder, "carcols.meta"));
                    var choice = astronView.tuningChoices[Category(mod)];
                    var mesh = choice.Pieces["_hi.yft"];
                    if (!choice.HiddenBones.Contains(bonnetTag) || mesh.Count == 0 || mesh.Any(piece => piece.Bone != bonnetTag))
                        throw new InvalidOperationException("Astron bonnet attachment or stock hiding failed: " + hood);
                    if (hood.EndsWith("3a", StringComparison.Ordinal) && !mesh.Any(piece => piece.PaintLayer == 2))
                        throw new InvalidOperationException("Astron secondary-color bonnet insert was not detected.");
                    if (hood.EndsWith("3b", StringComparison.Ordinal) && !mesh.Any(piece =>
                        piece.Model.Material is DiffuseMaterial &&
                        ((DiffuseMaterial)piece.Model.Material).Brush is ImageBrush && piece.PaintLayer == 0))
                        throw new InvalidOperationException("Astron fixed-texture bonnet insert was not detected.");
                    var sourceLevels = game.ReadModel(hood).Fragment.Drawable.DrawableModels;
                    var expected = new[] { sourceLevels.High, sourceLevels.Med, sourceLevels.Low }
                        .Select(lod => lod.SelectMany(model => model.Geometries).Sum(geometry => (int)geometry.IndicesCount / 3)).ToArray();
                    var actual = new[] { "_hi.yft", "High", "Medium" }.Select(lod => Triangles(choice.Pieces[lod])).ToArray();
                    if (!actual.SequenceEqual(expected) || choice.Pieces["Low"].Count != 0 || choice.Pieces["Very Low"].Count != 0)
                        throw new InvalidOperationException("Astron tuning LOD mapping does not match High / Med / Low.");
                    var shutPanel = mesh.Single(piece => ((MeshGeometry3D)piece.Model.Geometry).TriangleIndices.Count == 1090 * 3);
                    var panelAo = astronView.vehicle.PreviewAo(shutPanel);
                    if (astronView.vehicle.Preview(shutPanel).BackMaterial != null ||
                        (panelAo != null && panelAo.BackMaterial != null))
                        throw new InvalidOperationException("The Astron shut panel must not render its back face.");
                    paintLayers.Add(hood + ": " + String.Join(",", mesh.Select(p => p.PaintLayer).Distinct()) +
                        ", triangles: " + Triangles(mesh) + ", height: " + mesh.Min(p => p.Model.Bounds.Z).ToString("0.000"));
                }
                var astronLivery = astronView.tuningEditor.PreviewParts.First(part => part.Type == "VMT_LIVERY_MOD");
                astronView.PreviewTuning(astronLivery, astronView.tuningEditor.PreviewParts, Path.Combine(astronFolder, "carcols.meta"));
                var installedHood = astronView.tuningChoices["VMT_BONNET"].Pieces["_hi.yft"];
                var carbon = installedHood.Single(piece => piece.PaintLayer == 0 &&
                    ((MeshGeometry3D)piece.Model.Geometry).TriangleIndices.Count == 1435 * 3);
                if (carbon.LiveryMesh == null || !astronView.visibleDecals.Any(model => model.Geometry == carbon.LiveryMesh))
                    throw new InvalidOperationException("The fixed-texture Astron carbon insert did not receive the livery.");
                var hoodDecals = installedHood.Select(piece => piece.LiveryMesh).Where(mesh => mesh != null).ToArray();
                if (hoodDecals.Length == 0 || hoodDecals.Any(mesh => !astronView.visibleDecals.Any(model => model.Geometry == mesh)))
                    throw new InvalidOperationException("Active livery was not rendered on the Astron tuning hood.");
                astronView.isolationTreeIsTuning = true;
                var hoodNode = astronView.tuningTree.Items.OfType<TreeViewItem>().Single(node => (string)node.Tag == "VMT_BONNET");
                hoodNode.IsExpanded = true;
                var isolatedNode = hoodNode.Items.OfType<TreeViewItem>().Single(node =>
                    ((TuningEditor.Part)node.Tag).Model == "astron_bon_3a");
                isolatedNode.IsSelected = true;
                astronView.levels.SelectedItem = "Low";
                bool handled = false;
                astronView.NumpadHook(IntPtr.Zero, 0x0100, new IntPtr(0x2E), new IntPtr(1 << 24), ref handled);
                if (handled || astronView.IsIsolated) throw new InvalidOperationException("Main Delete toggled local view.");
                astronView.NumpadHook(IntPtr.Zero, 0x0100, new IntPtr(0x2E), IntPtr.Zero, ref handled);
                if (!handled || astronView.isolatedTuning == null || astronView.isolatedTuning.Part.Model != "astron_bon_3a" ||
                    (string)astronView.levels.SelectedItem != "_hi.yft" ||
                    astronView.scene.Children.Contains(astronView.environment))
                    throw new InvalidOperationException("Local view did not isolate the selected uninstalled hood.");
                if (!astronView.IsolationPieces("_hi.yft").Any(piece => piece.LiveryMesh != null &&
                    astronView.visibleDecals.Any(model => model.Geometry == piece.LiveryMesh)))
                    throw new InvalidOperationException("Isolated tuning hood lost the active livery.");
                astronView.NumpadHook(IntPtr.Zero, 0x0100, new IntPtr(0x6E), IntPtr.Zero, ref handled);
                if (astronView.IsIsolated || (string)astronView.levels.SelectedItem != "Low" ||
                    astronView.tuningChoices["VMT_BONNET"].Part.Model != "astron_bon_3b")
                    throw new InvalidOperationException("Local view did not restore the LOD and installed hood.");
                lines.Add("Astron tuning LOD mapping and NumPad Del isolation passed.");
                lines.AddRange(paintLayers);
                lines.Add("Astron unresolved textures: " + String.Join(", ", astronView.vehicle.MissingTextures));
                astronView.Close();
            }
            CheckProjectRoundTrip(args[2], args[3], args.Skip(4).ToArray(), game);
            lines.Add("Project asset, carcols, layout and viewport round-trip passed.");
            lines.Add("Visibility toggle and all LOD selections passed.");
            File.WriteAllLines(args[1], lines);
            return 0;
        }
    }
}
