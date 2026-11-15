using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace SentinelViewer
{
    sealed partial class Viewer
    {

        readonly TextBox xmlPreview = new TextBox { IsReadOnly = true, IsInactiveSelectionHighlightEnabled = true, FontFamily = new FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        readonly ContentControl editorSlot = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        readonly ComboBox renderMode = new ComboBox { Width = 110, SelectedIndex = 3 };
        readonly OrthographicCamera orthographic = new OrthographicCamera { NearPlaneDistance = 0.12, FarPlaneDistance = 260 };
        readonly CheckBox panMode = new CheckBox { Content = "✋", ToolTip = Ui.Text("Панорамирование", "Pan") };
        readonly CheckBox projection = new CheckBox { Content = "▱", ToolTip = Ui.Text("Ортографическая проекция", "Orthographic projection") };
        readonly ToggleButton xray = new ToggleButton();
        readonly CheckBox showFloorGrid = new CheckBox { Content = Ui.Text("Сетка пола", "Floor grid") };
        readonly CheckBox showSceneAxes = new CheckBox { Content = Ui.Text("Оси X / Y / Z", "X / Y / Z axes") };
        readonly CheckBox showViewportInfo = new CheckBox { Content = Ui.Text("Информация о модели", "Model info") };
        readonly CheckBox showStatistics = new CheckBox { Content = Ui.Text("Статистика", "Statistics") };
        readonly CheckBox showOrigin = new CheckBox { Content = Ui.Text("Начало координат", "Origin") };
        readonly CheckBox showHoverOutline = new CheckBox { Content = Ui.Text("Контур тюнинга", "Tuning outline"), IsChecked = true };
        readonly CheckBox showSky = new CheckBox { Content = Ui.Text("Небо сцены", "Scene sky") };
        readonly CheckBox[] visibleAxes = { new CheckBox { Content = "X", IsChecked = true },
            new CheckBox { Content = "Y", IsChecked = true }, new CheckBox { Content = "Z", IsChecked = true } };
        readonly Slider gridScale = new Slider { Minimum = 0.25, Maximum = 4, Value = 1, Width = 110,
            TickFrequency = 0.25, IsSnapToTickEnabled = true };
        readonly Slider gridExtent = new Slider { Minimum = 2, Maximum = 20, Value = 8, Width = 110,
            TickFrequency = 1, IsSnapToTickEnabled = true };
        static readonly Brush ViewportBackground = new SolidColorBrush(Color.FromRgb(43, 43, 43));
        readonly TextBlock viewportInfo = new TextBlock { Foreground = Brushes.WhiteSmoke, Background = StudioTheme.Background,
            Padding = new Thickness(6, 3, 6, 3), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        readonly Model3DGroup viewportGuides = new Model3DGroup();
        Action updateAxisGizmo;
        bool middleDrag, isolationTreeIsTuning;
        Vector3D orbitPan;
        readonly Dictionary<MeshGeometry3D, GeometryModel3D> wireMeshes = new Dictionary<MeshGeometry3D, GeometryModel3D>();
        readonly List<WorkspacePane> panes = new List<WorkspacePane>();
        readonly List<Window> floatingPanes = new List<Window>();
        Grid workspace;
        Material wireMaterial;
        readonly Material solidMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(159, 163, 171)));

        bool xmlRefreshPending;
        void RefreshCarcolsPreview()
        {
            if (xmlRefreshPending) return;
            xmlRefreshPending = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                xmlRefreshPending = false;
                xmlPreview.Text = tuningEditor == null ? "" : tuningEditor.PreviewXml();
                SelectTuningXml();
            }), DispatcherPriority.Background);
        }

        void FindXml(string query, bool next)
        {
            if (String.IsNullOrWhiteSpace(query)) return;
            string xml = xmlPreview.Text;
            int start = next ? Math.Min(xml.Length, xmlPreview.SelectionStart + xmlPreview.SelectionLength) : 0;
            int index = xml.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0 && start > 0) index = xml.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return;
            xmlPreview.Select(index, query.Length);
            xmlPreview.ScrollToLine(xmlPreview.GetLineIndexFromCharacterIndex(index));
        }

        void AddStudioMenu(DockPanel layout)
        {
            var menu = new Menu { Background = StudioTheme.Background, Margin = new Thickness(3, 1, 3, 2) };
            menu.Resources[typeof(MenuItem)] = StudioTheme.MainMenuItemStyle();
            DockPanel.SetDock(menu, Dock.Top);
            layout.Children.Add(menu);
            var file = AddMenu(menu, Ui.Text("Файл", "File"));
            AddMenu(file, Ui.Text("Новый проект…", "New project…"), NewProject);
            AddMenu(file, Ui.Text("Открыть проект…", "Open project…"), PickProject);
            AddMenu(file, Ui.Text("Сохранить проект", "Save project"), () => SaveProject(false));
            AddMenu(file, Ui.Text("Сохранить как…", "Save project as…"), () => SaveProject(true));
            var import = AddMenu(menu, Ui.Text("Импорт", "Import"));
            AddMenu(import, Ui.Text("Модель YFT…", "Vehicle YFT…"), Open);
            AddMenu(import, Ui.Text("Модель из GTA V…", "Model from GTA V…"), OpenGameVehicle);
            AddMenu(import, Ui.Text("Открыть RPF…", "Open RPF…"), OpenRpfVehicle);
            AddMenu(import, Ui.Text("Папка FiveM…", "FiveM folder…"), OpenPackage);
            AddMenu(import, Ui.Text("Тюнинг YFT…", "Tuning YFT…"), () => { if (ReadyEditor()) tuningEditor.Import(); });
            AddMenu(import, Ui.Text("Готовый carcols…", "Existing carcols…"), () => { if (ReadyEditor()) tuningEditor.OpenCarcols(); });
            var export = AddMenu(menu, Ui.Text("Экспорт", "Export"));
            AddMenu(export, Ui.Text("Новый carcols…", "New carcols…"), () => { if (ReadyEditor()) tuningEditor.NewKit(); });
            AddMenu(export, Ui.Text("Новый со statMods…", "New with statMods…"), () => { if (ReadyEditor()) tuningEditor.NewFromExample(); });
            AddMenu(export, Ui.Text("Параметры комплекта…", "Kit settings…"), () => { if (ReadyEditor()) tuningEditor.EditKitSettings(); });
            export.Items.Add(new Separator());
            AddMenu(export, Ui.Text("Только текущая модель…", "Current vehicle only…"), () => { if (ReadyEditor()) tuningEditor.Export(true); });
            AddMenu(export, Ui.Text("Весь файл…", "Entire file…"), () => { if (ReadyEditor()) tuningEditor.Export(false); });
            var settings = AddMenu(menu, Ui.Text("Настройки", "Settings"));
            AddMenu(settings, Ui.Text("Путь GTA V…", "GTA V path…"), ConnectGame);
            var languageChoices = AddMenu(settings, Ui.Text("Язык", "Language"));
            foreach (var language in Ui.Languages)
            {
                var option = AddMenu(languageChoices, (Ui.Language == language.Key ? "✓ " : "") + language.Value,
                    () => Ui.ChangeLanguage(language.Key));
                option.IsEnabled = Ui.Language != language.Key;
            }
            AddMenu(settings, Ui.Text("Вернуть раскладку", "Reset layout"), ResetWorkspace);
            var help = AddMenu(menu, Ui.Text("Справка", "Help"));
            AddMenu(help, Ui.Text("Проверить обновления…", "Check for updates…"), CheckForStudioUpdate);
            InputBindings.Add(new KeyBinding(new Shortcut(() => SaveProject(false)), Key.S, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new Shortcut(PickProject), Key.O, ModifierKeys.Control));
        }

        sealed class Shortcut : ICommand
        {
            readonly Action action;
            public Shortcut(Action action) { this.action = action; }
            public bool CanExecute(object value) { return true; }
            public void Execute(object value) { action(); }
            public event EventHandler CanExecuteChanged { add { } remove { } }
        }

        bool ReadyEditor()
        {
            if (vehicle == null || loading) return false;
            EnsureTuningEditor(null);
            return true;
        }

        static MenuItem AddMenu(ItemsControl parent, string text, Action action = null)
        {
            var item = new MenuItem { Header = text };
            if (action != null) item.Click += (sender, args) => action();
            parent.Items.Add(item);
            return item;
        }

        FrameworkElement CreateWorkspace(Border stage, Panel paint, FrameworkElement bones,
            CheckBox playback)
        {
            var viewportArea = new DockPanel { Background = StudioTheme.Panel };
            const double minimumToolbarWidth = 550;
            var header = new Grid { Width = minimumToolbarWidth };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var headerScroll = new ScrollViewer { Content = header, Margin = new Thickness(5, 3, 5, 3),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            headerScroll.SizeChanged += (s, e) => header.Width = Math.Max(minimumToolbarWidth, e.NewSize.Width);
            DockPanel.SetDock(headerScroll, Dock.Top);
            viewportArea.Children.Add(headerScroll);
            var modes = new[] { Ui.Text("Каркас", "Wireframe"), Ui.Text("Сплошной", "Solid"),
                Ui.Text("Материалы", "Material"), Ui.Text("Рендер", "Rendered") };
            var shading = new StackPanel { Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4, 0, 0, 0) };
            AddOverlayControls(shading);
            var shadingButtons = new Button[modes.Length];
            for (int i = 0; i < modes.Length; i++)
            {
                int mode = i;
                shadingButtons[i] = HeaderIcon(ShadingGlyph(mode), modes[mode], () => renderMode.SelectedIndex = mode);
                shading.Children.Add(shadingButtons[i]);
            }
            renderMode.ItemsSource = modes;
            renderMode.SelectedIndex = 3;
            Action updateModes = () =>
            {
                for (int i = 0; i < shadingButtons.Length; i++)
                    if (i == renderMode.SelectedIndex) shadingButtons[i].Background =
                        new SolidColorBrush(Color.FromRgb(56, 100, 153));
                    else shadingButtons[i].ClearValue(Control.BackgroundProperty);
            };
            renderMode.SelectionChanged += (s, e) => { updateModes(); UpdateLighting(); RefreshScene(); };
            updateModes();
            Grid.SetColumn(shading, 2);
            header.Children.Add(shading);

            var variationTools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            var importVariations = HeaderIcon(VariationGlyph(), Ui.Text("Загрузить carvariations.meta", "Load carvariations.meta"), OpenVariations);
            variationTools.Children.Add(importVariations);
            var pickVariation = HeaderIcon(new TextBlock { Text = "▾", FontSize = 13 },
                Ui.Text("Цвета спавна", "Spawn colors"), () => { });
            pickVariation.Click += (s, e) =>
            {
                var menu = new ContextMenu { PlacementTarget = pickVariation, Placement = PlacementMode.Bottom };
                foreach (var preset in spawnColors.Items)
                {
                    object choice = preset;
                    var entry = AddMenu(menu, choice.ToString(), () => spawnColors.SelectedItem = choice);
                    entry.IsCheckable = true;
                    entry.IsChecked = Object.Equals(spawnColors.SelectedItem, choice);
                }
                if (menu.Items.Count == 0)
                    AddMenu(menu, Ui.Text("Сначала загрузите carvariations.meta", "Load carvariations.meta first")).IsEnabled = false;
                pickVariation.ContextMenu = menu;
                menu.IsOpen = true;
            };
            variationTools.Children.Add(pickVariation);
            var lodBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            var lodButtons = new Dictionary<string, Button>();
            var lodNames = new[] { "_hi.yft", "High", "Medium", "Low", "Very Low" };
            for (int i = 0; i < lodNames.Length; i++)
            {
                string level = lodNames[i];
                var button = HeaderIcon(new TextBlock { Text = "L" + i, FontSize = 11 }, level,
                    () => { if (levels.Items.Contains(level)) levels.SelectedItem = level; });
                button.Width = 26;
                button.Height = 21;
                lodBar.Children.Add(button);
                lodButtons.Add(level, button);
            }
            var extraLod = HeaderIcon(new TextBlock { Text = "⋯" }, Ui.Text("LOD из _hi.yft", "LOD from _hi.yft"), () => { });
            extraLod.Click += (s, e) =>
            {
                var choices = new ContextMenu { PlacementTarget = extraLod, Placement = PlacementMode.Bottom };
                foreach (string level in levels.Items.OfType<string>().Where(value => value.StartsWith("_hi.yft / ")))
                    AddMenu(choices, level, () => levels.SelectedItem = level);
                extraLod.ContextMenu = choices;
                choices.IsOpen = true;
            };
            lodBar.Children.Add(extraLod);
            Action updateLod = () =>
            {
                foreach (var pair in lodButtons)
                {
                    pair.Value.IsEnabled = levels.Items.Contains(pair.Key);
                    pair.Value.Opacity = pair.Value.IsEnabled ? 1 : 0.38;
                    pair.Value.Background = Equals(levels.SelectedItem, pair.Key)
                        ? new SolidColorBrush(Color.FromRgb(45, 105, 169)) : StudioTheme.Panel;
                }
                extraLod.Visibility = levels.Items.OfType<string>().Any(value => value.StartsWith("_hi.yft / "))
                    ? Visibility.Visible : Visibility.Collapsed;
                extraLod.Background = (levels.SelectedItem as string ?? "").StartsWith("_hi.yft / ")
                    ? new SolidColorBrush(Color.FromRgb(45, 105, 169)) : StudioTheme.Panel;
            };
            levels.SelectionChanged += (s, e) => updateLod();
            ((System.Collections.Specialized.INotifyCollectionChanged)levels.Items).CollectionChanged += (s, e) => updateLod();
            updateLod();
            ((Panel)((DockPanel)bones).Children[0]).Children.Insert(0, lodBar);
            header.Children.Add(variationTools);
            var paintHost = new Border { Margin = new Thickness(2, 0, 2, 0),
                Child = new Viewbox { Child = paint, StretchDirection = StretchDirection.DownOnly,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            Grid.SetColumn(paintHost, 1);
            header.Children.Add(paintHost);
            ScalePanel(paintHost, 9, 13, 250, 720, font =>
            {
                bool compact = paintHost.ActualWidth < 650;
                foreach (var label in paint.Children.OfType<StackPanel>().SelectMany(channel => channel.Children.OfType<TextBlock>()))
                    label.Text = (string)(compact ? label.Tag : label.ToolTip);
                foreach (var apply in paint.Children.OfType<Button>())
                {
                    apply.Content = compact ? Ui.Text("Применить", "Apply") : apply.ToolTip;
                    apply.Margin = new Thickness(1, 0, 0, 0);
                    apply.Padding = new Thickness(compact ? 4 : 8, 3, compact ? 4 : 8, 3);
                }
                foreach (var field in paintFields.Values) field.Width = Math.Ceiling(font * 4.75);
                foreach (var picker in paint.Children.OfType<StackPanel>().SelectMany(channel => channel.Children.OfType<Button>()))
                    picker.Width = Math.Ceiling(font * 1.55);
            });
            var surface = new Grid();
            viewportArea.Children.Add(surface);
            surface.Children.Add(stage);
            surface.ClipToBounds = true;
            var outlineLayer = new Canvas { IsHitTestVisible = false };
            outlineLayer.Children.Add(outlineHalo);
            outlineLayer.Children.Add(outlineStroke);
            surface.Children.Add(outlineLayer);
            viewportInfo.HorizontalAlignment = HorizontalAlignment.Left;
            viewportInfo.VerticalAlignment = VerticalAlignment.Bottom;
            viewportInfo.Margin = new Thickness(8);
            surface.Children.Add(viewportInfo);
            var controls = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 9, 0) };
            controls.Children.Add(CreateAxisGizmo());
            var zoom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            zoom.Children.Add(NavigationButton("M 4,11 A 5,5 0 1 1 14,11 A 5,5 0 1 1 4,11 M 13,14 L 17,18 M 6.5,11 L 11.5,11 M 9,8.5 L 9,13.5",
                Ui.Text("Приблизить", "Zoom in"), () => { distance = Math.Max(0.15, distance * 0.82); MoveCamera(); }));
            zoom.Children.Add(NavigationButton("M 4,11 A 5,5 0 1 1 14,11 A 5,5 0 1 1 4,11 M 13,14 L 17,18 M 6.5,11 L 11.5,11",
                Ui.Text("Отдалить", "Zoom out"), () => { distance *= 1.22; MoveCamera(); }));
            controls.Children.Add(zoom);
            ConfigureNavigation(panMode, NavigationGlyph("M 6,11 L 6,5 Q 7,3 8,5 L 8,10 L 8,3 Q 9,1 10,3 L 10,10 L 10,4 Q 11,2 12,4 L 12,10 L 12,6 Q 13,4 14,6 L 14,12 L 15,9 Q 17,8 17,11 L 15,16 L 13,18 L 8,18 L 4,13 Q 3,10 6,11 Z"),
                Ui.Text("Панорамирование", "Pan"));
            ConfigureNavigation(freeCamera, NavigationGlyph("M 3,6 L 13,6 13,15 3,15 Z M 13,9 L 17,7 17,14 13,12"),
                Ui.Text("Свободная камера / орбита", "Free camera / orbit"));
            ConfigureNavigation(projection, NavigationGlyph("M 3,4 L 17,4 17,16 3,16 Z M 8,4 L 8,16 M 13,4 L 13,16 M 3,8 L 17,8 M 3,12 L 17,12"),
                Ui.Text("Ортографическая / перспективная проекция", "Orthographic / perspective"));
            controls.Children.Add(panMode);
            controls.Children.Add(freeCamera);
            controls.Children.Add(projection);
            projection.Checked += (s, e) => { viewport.Camera = orthographic; MoveCamera(); };
            projection.Unchecked += (s, e) => { viewport.Camera = camera; MoveCamera(); };
            controls.Children.Add(NavigationButton("M 3,8 L 3,3 8,3 M 12,3 L 17,3 17,8 M 17,12 L 17,17 12,17 M 8,17 L 3,17 3,12 M 8,10 L 12,10",
                Ui.Text("Вписать модель", "Frame vehicle"), () => { projection.IsChecked = false; ResetCamera(); }));
            surface.Children.Add(controls);

            var sceneTools = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6) };
            var timeField = SceneValueField(clock, clockLabel, ParseSceneTime, 1, 5,
                Ui.Text("Время (ЧЧ:ММ)", "Time (HH:MM)"));
            var reflectionField = SceneValueField(reflect, new TextBlock(), ParseReflection, 0.01, 0.01,
                Ui.Text("Отражения (%)", "Reflections (%)"), value => (value * 100).ToString("0.#") + " %");
            AddScenePopup(sceneTools, "☀", Ui.Text("Время", "Time"), timeField, playback);
            AddScenePopup(sceneTools, "◐", Ui.Text("Отражения", "Reflections"), reflectionField, aoEnabled);
            AddScenePopup(sceneTools, "▧", Ui.Text("Фон", "Background"), environmentChoice);
            surface.Children.Add(sceneTools);

            workspace = new Grid { Margin = new Thickness(3) };
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star), MinWidth = 210 });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 145 });
            workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star), MinHeight = 140 });
            workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.5, GridUnitType.Star), MinHeight = 110 });
            AddPane(Ui.Text("3D-вид", "3D Viewport"), viewportArea, 0);
            AddPane(Ui.Text("Объекты", "Objects"), bones, 1);
            AddPane(Ui.Text("Редактор тюнинга", "Tuning editor"), editorSlot, 2);
            var xmlPanel = new DockPanel();
            var searchBar = new DockPanel { Margin = new Thickness(4) };
            var search = new TextBox { ToolTip = Ui.Text("Поиск по XML", "Search XML") };
            var next = Button("↓", () => FindXml(search.Text, true));
            DockPanel.SetDock(next, Dock.Right);
            searchBar.Children.Add(next);
            searchBar.Children.Add(search);
            DockPanel.SetDock(searchBar, Dock.Top);
            xmlPanel.Children.Add(searchBar);
            xmlPanel.Children.Add(xmlPreview);
            ScalePanel(xmlPanel, 9, 14, 145, 650, font =>
            {
                search.MinHeight = font + 7;
                next.Margin = new Thickness(2, 0, 0, 0);
                next.Padding = new Thickness(font * 0.45, 1, font * 0.45, 1);
            });
            search.TextChanged += (s, e) => FindXml(search.Text, false);
            search.KeyDown += (s, e) => { if (e.Key == Key.Enter) { FindXml(search.Text, true); e.Handled = true; } };
            AddPane(Ui.Text("Предпросмотр carcols", "Carcols preview"), xmlPanel, 3);
            var vertical = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch, ResizeDirection = GridResizeDirection.Columns, Background = StudioTheme.Background };
            Grid.SetColumn(vertical, 1); Grid.SetRowSpan(vertical, 3); workspace.Children.Add(vertical);
            var horizontal = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows, Background = StudioTheme.Background };
            Grid.SetRow(horizontal, 1); Grid.SetColumnSpan(horizontal, 3); workspace.Children.Add(horizontal);
            tree.SelectedItemChanged += (s, e) => { isolationTreeIsTuning = false; RefreshScene(); };
            tuningTree.SelectedItemChanged += SelectTuningTreeItem;
            tree.GotKeyboardFocus += (s, e) => { if (isolationTreeIsTuning) { isolationTreeIsTuning = false; RefreshScene(); } };
            tuningTree.GotKeyboardFocus += (s, e) => isolationTreeIsTuning = true;
            Closing += (s, e) => { StopAxisTransition(); foreach (var window in floatingPanes.ToArray()) window.Close(); };
            return workspace;
        }

        static void ScalePanel(FrameworkElement panel, double minFont, double maxFont, double minWidth,
            double maxWidth, Action<double> resizeControls)
        {
            panel.SizeChanged += (s, e) =>
            {
                if (!e.WidthChanged) return;
                double ratio = Math.Max(0, Math.Min(1, (e.NewSize.Width - minWidth) / (maxWidth - minWidth)));
                double font = Math.Round((minFont + (maxFont - minFont) * ratio) * 2) / 2;
                TextElement.SetFontSize(panel, font);
                resizeControls(font);
            };
        }

        void AddOverlayControls(Panel shading)
        {
            string title = Ui.Text("Наложения 3D-вида", "Viewport overlays");
            var overlays = HeaderIcon(NavigationGlyph("M 2,8 A 5,5 0 1 1 12,8 A 5,5 0 1 1 2,8 M 7,11 A 5,5 0 1 1 17,11 A 5,5 0 1 1 7,11 M 15,17 L 18,20 21,17"), title, () => { });
            var options = new StackPanel { Margin = new Thickness(12), MinWidth = 180 };
            options.Children.Add(new TextBlock { Text = title, Foreground = Brushes.WhiteSmoke,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            var axes = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var axis in visibleAxes)
            {
                axis.Margin = new Thickness(0, 0, 10, 0);
                axis.Foreground = Brushes.WhiteSmoke;
                axis.Checked += (s, e) => RefreshScene();
                axis.Unchecked += (s, e) => RefreshScene();
                axes.Children.Add(axis);
            }
            foreach (var option in new[] { showFloorGrid, showSceneAxes, showOrigin, showViewportInfo,
                showStatistics, showHoverOutline, showSky })
            {
                option.Margin = new Thickness(0, 4, 0, 4);
                option.Foreground = Brushes.WhiteSmoke;
                option.Checked += (s, e) => RefreshScene();
                option.Unchecked += (s, e) => RefreshScene();
                options.Children.Add(option);
                if (option == showFloorGrid)
                {
                    options.Children.Add(OverlaySlider(Ui.Text("Шаг сетки", "Grid scale"), gridScale));
                    options.Children.Add(OverlaySlider(Ui.Text("Размер сетки", "Grid extent"), gridExtent));
                }
                if (option == showSceneAxes) options.Children.Add(axes);
            }
            showSky.Checked += (s, e) => UpdateLighting();
            showSky.Unchecked += (s, e) => UpdateLighting();
            var popup = new Popup { PlacementTarget = overlays, Placement = PlacementMode.Bottom, StaysOpen = false,
                AllowsTransparency = true, Child = new Border { Background = StudioTheme.Panel,
                    CornerRadius = new CornerRadius(4), Child = options } };
            overlays.Click += (s, e) => popup.IsOpen = !popup.IsOpen;
            Closed += (s, e) => popup.IsOpen = false;
            shading.Children.Add(overlays);

            ConfigureNavigation(xray, NavigationGlyph("M 2,2 L 12,2 12,12 2,12 Z M 7,7 L 17,7 17,17 7,17 Z"),
                Ui.Text("Прозрачность всех материалов (X-Ray)", "X-Ray all materials"));
            xray.Height = 25;
            xray.Margin = new Thickness(0, 0, 4, 0);
            xray.Checked += (s, e) => RefreshScene();
            xray.Unchecked += (s, e) => RefreshScene();
            shading.Children.Add(xray);
        }

        Panel OverlaySlider(string caption, Slider slider)
        {
            var row = new DockPanel { Margin = new Thickness(12, 1, 0, 1) };
            var value = new TextBlock { Foreground = Brushes.WhiteSmoke, Width = 34,
                TextAlignment = TextAlignment.Right, Text = slider.Value.ToString("0.##") };
            DockPanel.SetDock(value, Dock.Right);
            row.Children.Add(value);
            DockPanel.SetDock(slider, Dock.Right);
            row.Children.Add(slider);
            row.Children.Add(new TextBlock { Text = caption, Foreground = Brushes.WhiteSmoke, Margin = new Thickness(0, 0, 8, 0) });
            slider.ValueChanged += (s, e) => { value.Text = slider.Value.ToString("0.##"); RefreshScene(); };
            return row;
        }

        FrameworkElement CreateAxisGizmo()
        {
            var gizmo = new Canvas { Width = 88, Height = 88, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 3) };
            var lines = new System.Windows.Shapes.Path {
                Stroke = new SolidColorBrush(Color.FromRgb(150, 156, 170)), StrokeThickness = 1.15, IsHitTestVisible = false };
            gizmo.Children.Add(lines);
            AddAxis(gizmo, "X", Color.FromRgb(213, 75, 90), Ui.Text("Вид справа · NumPad 3", "Right · NumPad 3"), () => NavigateNumpad(3, false));
            AddAxis(gizmo, "Y", Color.FromRgb(117, 179, 64), Ui.Text("Вид спереди · NumPad 1", "Front · NumPad 1"), () => NavigateNumpad(1, false));
            AddAxis(gizmo, "Z", Color.FromRgb(74, 145, 222), Ui.Text("Вид сверху · NumPad 7", "Top · NumPad 7"), () => NavigateNumpad(7, false));
            AddAxis(gizmo, "−X", Color.FromRgb(133, 48, 59), Ui.Text("Вид слева · Ctrl+NumPad 3", "Left · Ctrl+NumPad 3"), () => NavigateNumpad(3, true));
            AddAxis(gizmo, "−Y", Color.FromRgb(73, 111, 41), Ui.Text("Вид сзади · Ctrl+NumPad 1", "Rear · Ctrl+NumPad 1"), () => NavigateNumpad(1, true));
            AddAxis(gizmo, "−Z", Color.FromRgb(46, 89, 143), Ui.Text("Вид снизу · Ctrl+NumPad 7", "Bottom · Ctrl+NumPad 7"), () => NavigateNumpad(7, true));
            var axes = gizmo.Children.OfType<Button>().ToArray();
            Action update = () =>
            {
                var forward = camera.LookDirection;
                if (forward.LengthSquared == 0) return;
                forward.Normalize();
                var right = Vector3D.CrossProduct(forward, camera.UpDirection);
                right.Normalize();
                var up = Vector3D.CrossProduct(right, forward);
                var directions = new[] { new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), new Vector3D(0, 0, 1) };
                var geometry = new GeometryGroup();
                var center = new Point(44, 44);
                for (int i = 0; i < axes.Length; i++)
                {
                    var axis = directions[i % 3] * (i < 3 ? 1 : -1);
                    var end = center + new Vector(Vector3D.DotProduct(axis, right), -Vector3D.DotProduct(axis, up)) * 30;
                    double depth = Vector3D.DotProduct(axis, forward);
                    Canvas.SetLeft(axes[i], end.X - 10);
                    Canvas.SetTop(axes[i], end.Y - 10);
                    Panel.SetZIndex(axes[i], (int)(100 - 90 * depth));
                    axes[i].Opacity = depth > 0 ? 0.72 : 1;
                    if (i < 3) geometry.Children.Add(new LineGeometry(center, end));
                }
                lines.Data = geometry;
            };
            updateAxisGizmo = update;
            update();
            return gizmo;
        }

        void NavigateNumpad(int digit, bool opposite)
        {
            StopAxisTransition();
            const double step = Math.PI / 12;
            switch (digit)
            {
                case 1: SetAxisView(opposite ? -Math.PI / 2 : Math.PI / 2, 0); return;
                case 3: SetAxisView(opposite ? Math.PI : 0, 0); return;
                case 7: SetAxisView(0, opposite ? -Math.PI / 2 : Math.PI / 2); return;
                case 5: projection.IsChecked = projection.IsChecked != true; return;
                case 9: SetAxisView(yaw + Math.PI, -pitch); return;
            }
            freeCamera.IsChecked = false;
            yaw += digit == 4 ? -step : digit == 6 ? step : 0;
            pitch = ClampOrbitPitch(pitch + (digit == 8 ? step : digit == 2 ? -step : 0));
            MoveCamera();
        }

        System.Diagnostics.Stopwatch axisTransition;
        void StopAxisTransition()
        {
            if (axisTransition == null) return;
            CompositionTarget.Rendering -= AnimateAxisView;
            axisTransition = null;
        }

        double startYaw, startPitch, targetYaw, targetPitch;
        Vector3D startPan;
        void SetAxisView(double horizontal, double vertical)
        {
            StopAxisTransition();
            freeCamera.IsChecked = false;
            if (!IsVisible)
            {
                orbitPan = new Vector3D();
                yaw = horizontal;
                pitch = vertical;
                projection.IsChecked = true;
                MoveCamera();
                return;
            }
            startYaw = yaw;
            startPitch = pitch;
            targetYaw = yaw + Math.IEEERemainder(horizontal - yaw, Math.PI * 2);
            targetPitch = vertical;
            startPan = orbitPan;
            projection.IsChecked = true;
            axisTransition = System.Diagnostics.Stopwatch.StartNew();
            CompositionTarget.Rendering += AnimateAxisView;
        }

        void AnimateAxisView(object sender, EventArgs e)
        {
            double progress = Math.Min(1, axisTransition.Elapsed.TotalMilliseconds / 230);
            double ease = progress * progress * (3 - 2 * progress);
            yaw = startYaw + (targetYaw - startYaw) * ease;
            pitch = startPitch + (targetPitch - startPitch) * ease;
            orbitPan = startPan * (1 - ease);
            MoveCamera();
            if (progress >= 1) StopAxisTransition();
        }

        static void AddAxis(Canvas gizmo, string label, Color color, string tip, Action direct)
        {
            var axis = new Button();
            ConfigureNavigation(axis, new TextBlock { Text = label, Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold, FontSize = 11 }, tip);
            axis.Width = axis.Height = 20;
            axis.Margin = new Thickness(0);
            axis.Background = new SolidColorBrush(color);
            axis.Click += (s, e) => direct();
            gizmo.Children.Add(axis);
        }

        static Button NavigationButton(string geometry, string tip, Action action)
        {
            var button = new Button();
            ConfigureNavigation(button, NavigationGlyph(geometry), tip);
            button.Click += (s, e) => action();
            return button;
        }

        static System.Windows.Shapes.Path NavigationGlyph(string geometry)
        {
            return new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(geometry), Stroke = Brushes.WhiteSmoke, StrokeThickness = 1.4,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round, Width = 16, Height = 16, Stretch = Stretch.Uniform
            };
        }

        static void ConfigureNavigation(ContentControl control, UIElement glyph, string tip)
        {
            control.Content = glyph;
            control.ToolTip = tip;
            control.Width = control.Height = 27;
            control.Margin = new Thickness(2, 2, 0, 2);
            control.HorizontalAlignment = HorizontalAlignment.Right;
            control.Background = new SolidColorBrush(Color.FromArgb(215, 43, 45, 50));
            var template = new ControlTemplate(control.GetType());
            var circle = new FrameworkElementFactory(typeof(Border));
            circle.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            circle.SetValue(Border.CornerRadiusProperty, new CornerRadius(20));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            circle.AppendChild(content);
            template.VisualTree = circle;
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.OpacityProperty, 0.75));
            template.Triggers.Add(hover);
            if (control is ToggleButton)
            {
                var active = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
                active.Setters.Add(new Setter(Control.BackgroundProperty,
                    new SolidColorBrush(Color.FromRgb(56, 100, 153))));
                template.Triggers.Add(active);
                // Active state has to outrank the normal color, so set the normal color in the template.
                circle.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(215, 43, 45, 50)));
                circle.Name = "Circle";
                active.Setters.Clear();
                active.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(56, 100, 153)), "Circle"));
            }
            control.Template = template;
            ToolTipService.SetInitialShowDelay(control, 320);
            System.Windows.Automation.AutomationProperties.SetName(control, tip);
        }

        static Button HeaderIcon(UIElement glyph, string tip, Action action)
        {
            var button = new Button { Content = glyph, ToolTip = tip, Width = 28, Height = 25,
                Padding = new Thickness(3), Margin = new Thickness(0, 0, 1, 0) };
            ToolTipService.SetInitialShowDelay(button, 320);
            System.Windows.Automation.AutomationProperties.SetName(button, tip);
            button.Click += (s, e) => action();
            return button;
        }

        static Image ShadingGlyph(int mode)
        {
            var drawing = new DrawingGroup();
            var line = new Pen(new SolidColorBrush(Color.FromRgb(218, 219, 221)), 1.05);
            var center = new Point(10, 10);
            using (var dc = drawing.Open())
            {
                Brush fill = null;
                if (mode == 1) fill = new SolidColorBrush(Color.FromRgb(190, 191, 195));
                if (mode >= 2)
                {
                    var lit = mode == 2 ? Color.FromRgb(233, 234, 236) : Color.FromRgb(248, 247, 243);
                    var shade = mode == 2 ? Color.FromRgb(79, 81, 85) : Color.FromRgb(46, 48, 53);
                    fill = new RadialGradientBrush(lit, shade)
                    {
                        GradientOrigin = new Point(mode == 2 ? 0.33 : 0.25, mode == 2 ? 0.32 : 0.20)
                    };
                }
                dc.DrawEllipse(fill, line, center, 7.2, 7.2);
                if (mode == 0)
                {
                    dc.DrawEllipse(null, line, center, 3.3, 7.2);
                    dc.DrawEllipse(null, line, center, 7.2, 3.1);
                }
                if (mode == 3)
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(251, 250, 242)), null, new Point(7.3, 6.8), 1.6, 1.6);
            }
            drawing.Freeze();
            return new Image { Source = new DrawingImage(drawing), Width = 17, Height = 17 };
        }

        static Image VariationGlyph()
        {
            var drawing = new DrawingGroup();
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(218, 219, 221)), 1.3);
            using (var dc = drawing.Open())
            {
                dc.DrawGeometry(null, pen, Geometry.Parse("M 3,2 L 11,2 15,6 15,17 3,17 Z M 11,2 L 11,6 15,6"));
                dc.DrawGeometry(null, pen, Geometry.Parse("M 6,10 L 12,10 M 6,13 L 12,13"));
            }
            drawing.Freeze();
            return new Image { Source = new DrawingImage(drawing), Width = 16, Height = 16 };
        }

        static double? ParseSceneTime(string text)
        {
            var parts = text.Trim().Split(':');
            int hour, minute;
            if (parts.Length != 2 || !Int32.TryParse(parts[0], out hour) || !Int32.TryParse(parts[1], out minute) ||
                hour < 0 || hour > 23 || minute < 0 || minute > 59) return null;
            return hour * 60 + minute;
        }

        static double? ParseReflection(string text)
        {
            string number = text.Trim().TrimEnd('%').Trim().Replace(',', '.');
            double percent;
            if (!Double.TryParse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out percent) || Double.IsNaN(percent) || percent < 0 || percent > 100) return null;
            return percent / 100;
        }

        static FrameworkElement SceneValueField(Slider value, TextBlock label, Func<string, double?> parse,
            double arrowStep, double dragStep, string title, Func<double, string> format = null)
        {
            var row = new DockPanel { Width = 118, Height = 24, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 6), Background = StudioTheme.Background };
            foreach (int direction in new[] { -1, 1 })
            {
                int sign = direction;
                var arrow = Button(sign < 0 ? "‹" : "›", () => value.Value += sign * arrowStep);
                arrow.Width = 21;
                arrow.Margin = new Thickness(0);
                arrow.Padding = new Thickness(0);
                arrow.ToolTip = sign < 0 ? Ui.Text("Уменьшить", "Decrease") : Ui.Text("Увеличить", "Increase");
                DockPanel.SetDock(arrow, sign < 0 ? Dock.Left : Dock.Right);
                row.Children.Add(arrow);
            }
            var display = new Border { Background = StudioTheme.Panel, Cursor = Cursors.SizeWE,
                Focusable = true, Child = label, ToolTip = title + Ui.Text(" · щелчок для ввода, перетаскивание для изменения",
                    " · click to type, drag to adjust") };
            System.Windows.Automation.AutomationProperties.SetName(display, title);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Foreground = Brushes.WhiteSmoke;
            var entry = new TextBox { Visibility = Visibility.Collapsed, HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0) };
            var center = new Grid();
            center.Children.Add(display);
            center.Children.Add(entry);
            row.Children.Add(center);
            if (format != null)
            {
                label.Text = format(value.Value);
                value.ValueChanged += (s, e) => label.Text = format(e.NewValue);
            }
            Action beginEdit = () =>
            {
                display.Visibility = Visibility.Collapsed;
                entry.Visibility = Visibility.Visible;
                entry.Text = label.Text;
                entry.Focus();
                entry.SelectAll();
            };
            Action<bool> finishEdit = accept =>
            {
                if (entry.Visibility != Visibility.Visible) return;
                double? input = accept ? parse(entry.Text) : null;
                entry.Visibility = Visibility.Collapsed;
                display.Visibility = Visibility.Visible;
                if (input.HasValue) value.Value = input.Value;
            };
            entry.LostKeyboardFocus += (s, e) => finishEdit(true);
            entry.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Enter && e.Key != Key.Escape) return;
                finishEdit(e.Key == Key.Enter);
                display.Focus();
                e.Handled = true;
            };
            display.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Space) beginEdit();
                else if (e.Key == Key.Left || e.Key == Key.Right) value.Value += e.Key == Key.Left ? -arrowStep : arrowStep;
                else return;
                e.Handled = true;
            };
            Point start = new Point();
            double initial = 0;
            bool dragged = false;
            display.MouseLeftButtonDown += (s, e) =>
            {
                start = e.GetPosition(row);
                initial = value.Value;
                dragged = false;
                display.Focus();
                display.CaptureMouse();
                e.Handled = true;
            };
            display.MouseMove += (s, e) =>
            {
                if (!display.IsMouseCaptured) return;
                double offset = e.GetPosition(row).X - start.X;
                if (!dragged && Math.Abs(offset) < SystemParameters.MinimumHorizontalDragDistance) return;
                dragged = true;
                value.Value = initial + Math.Round(offset / 3) * dragStep;
            };
            display.MouseLeftButtonUp += (s, e) =>
            {
                if (!display.IsMouseCaptured) return;
                display.ReleaseMouseCapture();
                if (!dragged) beginEdit();
                e.Handled = true;
            };
            return row;
        }

        void AddScenePopup(Panel tools, string icon, string title, params UIElement[] fields)
        {
            var button = Button(icon, () => { });
            button.ToolTip = title;
            var content = new StackPanel { Background = StudioTheme.Panel, Margin = new Thickness(8) };
            KeyboardNavigation.SetTabNavigation(content, KeyboardNavigationMode.Cycle);
            content.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 0, 0, 6) });
            foreach (var field in fields) content.Children.Add(field);
            var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Right, StaysOpen = true,
                Child = new Border { Background = StudioTheme.Panel, Child = content } };
            var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(330) };
            delay.Tick += (s, e) => { delay.Stop(); if (!button.IsMouseOver && !content.IsMouseOver &&
                !content.IsKeyboardFocusWithin) popup.IsOpen = false; };
            button.MouseEnter += (s, e) => { delay.Stop(); popup.IsOpen = true; };
            button.Click += (s, e) => popup.IsOpen = true;
            button.MouseLeave += (s, e) => delay.Start();
            content.MouseEnter += (s, e) => delay.Stop();
            content.MouseLeave += (s, e) => delay.Start();
            content.LostKeyboardFocus += (s, e) => delay.Start();
            Closed += (s, e) => { delay.Stop(); popup.IsOpen = false; };
            tools.Children.Add(button);
        }

        sealed class WorkspacePane
        {
            public string Name;
            public int Position;
            public Border Frame;
            public ContentControl Body;
            public FrameworkElement Content;
            public Window Floating;
        }

        void AddPane(string title, FrameworkElement content, int position)
        {
            var layout = new DockPanel { Background = StudioTheme.Panel };
            var header = new DockPanel { Background = StudioTheme.Background };
            DockPanel.SetDock(header, Dock.Top);
            layout.Children.Add(header);
            var options = Button("⋯", () => { });
            DockPanel.SetDock(options, Dock.Right);
            header.Children.Add(options);
            var caption = new TextBlock { Text = title, Margin = new Thickness(7, 4, 0, 4) };
            header.Children.Add(caption);
            var body = new ContentControl { Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            layout.Children.Add(body);
            var pane = new WorkspacePane { Name = title, Position = position, Body = body, Content = content,
                Frame = new Border { Child = layout, BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)) } };
            panes.Add(pane);
            PlacePane(pane);
            workspace.Children.Add(pane.Frame);
            Point? start = null;
            caption.MouseLeftButtonDown += (s, e) => start = e.GetPosition(caption);
            caption.MouseLeftButtonUp += (s, e) => start = null;
            caption.MouseMove += (s, e) =>
            {
                if (!start.HasValue || e.LeftButton != MouseButtonState.Pressed) return;
                var delta = e.GetPosition(caption) - start.Value;
                if (delta.Length < 5) return;
                start = null;
                DragDrop.DoDragDrop(caption, new DataObject(typeof(WorkspacePane), pane), DragDropEffects.Move);
            };
            header.AllowDrop = true;
            header.Drop += (s, e) =>
            {
                var source = e.Data.GetData(typeof(WorkspacePane)) as WorkspacePane;
                if (source != null && source != pane && panes.Contains(source)) SwapPanes(source, pane);
            };
            options.Click += (s, e) =>
            {
                var menu = new ContextMenu { PlacementTarget = options };
                if (pane.Floating == null)
                    AddMenu(menu, Ui.Text("В отдельное окно", "Detach window"), () => FloatPane(pane));
                else AddMenu(menu, Ui.Text("Вернуть в рабочую область", "Dock window"), () => pane.Floating.Close());
                foreach (var other in panes)
                {
                    if (other == pane) continue;
                    var destination = other;
                    AddMenu(menu, Ui.Text("Поменять местами: ", "Swap with: ") + destination.Name, () =>
                    {
                        SwapPanes(pane, destination);
                    });
                }
                options.ContextMenu = menu; menu.IsOpen = true;
            };
        }

        static void SwapPanes(WorkspacePane first, WorkspacePane second)
        {
            int position = first.Position;
            first.Position = second.Position;
            second.Position = position;
            PlacePane(first);
            PlacePane(second);
        }

        static void PlacePane(WorkspacePane pane)
        {
            Grid.SetRow(pane.Frame, pane.Position / 2 * 2);
            Grid.SetColumn(pane.Frame, pane.Position % 2 * 2);
        }

        void FloatPane(WorkspacePane pane)
        {
            pane.Body.Content = null;
            var window = new Window { Title = AppName + " · " + pane.Name, Owner = this, Width = 790, Height = 510,
                Icon = Icon, Content = pane.Content };
            pane.Floating = window;
            floatingPanes.Add(window);
            StudioTheme.Apply(window);
            window.Closed += (s, e) =>
            {
                window.Content = null; pane.Body.Content = pane.Content;
                pane.Floating = null; floatingPanes.Remove(window);
            };
            window.SourceInitialized += (s, e) =>
            {
                var source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(window).Handle);
                if (source != null) source.AddHook(NumpadHook);
            };
            window.Show();
        }

        void ResetWorkspace()
        {
            foreach (var window in floatingPanes.ToArray()) window.Close();
            for (int i = 0; i < panes.Count; i++) { panes[i].Position = i; PlacePane(panes[i]); }
            workspace.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
            workspace.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            workspace.RowDefinitions[0].Height = new GridLength(3, GridUnitType.Star);
            workspace.RowDefinitions[2].Height = new GridLength(1.5, GridUnitType.Star);
        }

        GeometryModel3D StudioPreview(Vehicle.Piece piece)
        {
            if (renderMode.SelectedIndex >= 2) return vehicle.Preview(piece, renderMode.SelectedIndex == 3);
            if (renderMode.SelectedIndex == 1) return new GeometryModel3D(piece.Model.Geometry, solidMaterial);
            var source = (MeshGeometry3D)piece.Model.Geometry;
            GeometryModel3D cached;
            if (wireMeshes.TryGetValue(source, out cached)) return cached;
            if (wireMaterial == null)
            {
                const int size = 128, inset = 3, diagonal = size - 2;
                var pixels = new byte[size * size * 4];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int i = (y * size + x) * 4;
                        byte shade = x <= inset + 1 || y <= inset + 1 || x + y >= diagonal - 2 ? (byte)0 : (byte)168;
                        pixels[i] = pixels[i + 1] = pixels[i + 2] = shade;
                        pixels[i + 3] = 255;
                    }
                var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
                wireMaterial = new DiffuseMaterial(new ImageBrush(bitmap));
                wireMaterial.Freeze();
            }
            var mesh = new MeshGeometry3D();
            for (int i = 0; i < source.TriangleIndices.Count; i++)
            {
                mesh.Positions.Add(source.Positions[source.TriangleIndices[i]]);
                mesh.TriangleIndices.Add(i);
                const double near = 3.5 / 128, far = 123.5 / 128;
                mesh.TextureCoordinates.Add(i % 3 == 0 ? new Point(near, near) :
                    i % 3 == 1 ? new Point(far, near) : new Point(near, far));
            }
            cached = new GeometryModel3D(mesh, wireMaterial) { Transform = piece.Model.Transform };
            cached.Freeze();
            wireMeshes.Add(source, cached);
            return cached;
        }

        static GeometryModel3D TranslucentModel(GeometryModel3D model, Dictionary<Material, Material> cache)
        {
            return new GeometryModel3D(model.Geometry, TranslucentMaterial(model.Material, cache))
            {
                BackMaterial = TranslucentMaterial(model.BackMaterial, cache),
                Transform = model.Transform
            };
        }

        static Material TranslucentMaterial(Material material, Dictionary<Material, Material> cache)
        {
            if (material == null) return null;
            Material result;
            if (cache.TryGetValue(material, out result)) return result;
            var group = material as MaterialGroup;
            if (group != null)
            {
                var translucent = new MaterialGroup();
                foreach (var child in group.Children) translucent.Children.Add(TranslucentMaterial(child, cache));
                result = translucent;
            }
            else
            {
                result = material.Clone();
                var diffuse = result as DiffuseMaterial;
                var emissive = result as EmissiveMaterial;
                var specular = result as SpecularMaterial;
                var brush = diffuse != null ? diffuse.Brush : emissive != null ? emissive.Brush : specular != null ? specular.Brush : null;
                if (brush != null) brush.Opacity *= 0.32;
            }
            result.Freeze();
            cache.Add(material, result);
            return result;
        }

        void UpdateViewportGuides()
        {
            viewportGuides.Children.Clear();
            if (showFloorGrid.IsChecked != true && showSceneAxes.IsChecked != true && showOrigin.IsChecked != true) return;
            double floor = vehicle.Levels.Values.First().Min(piece => piece.Model.Bounds.Z) - 0.01;
            var origin = new Point3D(vehicle.Center.X, vehicle.Center.Y, floor);
            int divisions = (int)gridExtent.Value;
            double spacing = gridScale.Value, extent = divisions * spacing;
            const double width = 0.009;
            var strips = new MeshGeometry3D();
            if (showFloorGrid.IsChecked == true)
            {
                for (int i = -divisions; i <= divisions; i++)
                {
                    AddGuideStrip(strips, origin + new Vector3D(-extent, i * spacing, 0), new Vector3D(2 * extent, 0, 0), new Vector3D(0, width, 0));
                    AddGuideStrip(strips, origin + new Vector3D(i * spacing, -extent, 0), new Vector3D(0, 2 * extent, 0), new Vector3D(width, 0, 0));
                }
                AddGuide(strips, Color.FromRgb(99, 103, 111));
            }
            if (showOrigin.IsChecked == true)
            {
                var marker = new MeshGeometry3D();
                AddGuideStrip(marker, new Point3D(-0.13, 0, 0), new Vector3D(0.26, 0, 0), new Vector3D(0, 0.012, 0));
                AddGuideStrip(marker, new Point3D(0, -0.13, 0), new Vector3D(0, 0.26, 0), new Vector3D(0.012, 0, 0));
                AddGuideStrip(marker, new Point3D(0, 0, -0.13), new Vector3D(0, 0, 0.26), new Vector3D(0.012, 0.012, 0));
                AddGuide(marker, Color.FromRgb(250, 180, 90));
            }
            if (showSceneAxes.IsChecked != true) return;
            origin.Z += 0.002;
            var x = new MeshGeometry3D();
            AddGuideStrip(x, origin + new Vector3D(-extent, 0, 0), new Vector3D(2 * extent, 0, 0), new Vector3D(0, 2 * width, 0));
            if (visibleAxes[0].IsChecked == true) AddGuide(x, Color.FromRgb(198, 74, 88));
            var y = new MeshGeometry3D();
            AddGuideStrip(y, origin + new Vector3D(0, -extent, 0), new Vector3D(0, 2 * extent, 0), new Vector3D(2 * width, 0, 0));
            if (visibleAxes[1].IsChecked == true) AddGuide(y, Color.FromRgb(111, 174, 64));
            var z = new MeshGeometry3D();
            AddGuideStrip(z, origin, new Vector3D(0, 0, 3), new Vector3D(2 * width, 0, 0));
            AddGuideStrip(z, origin, new Vector3D(0, 0, 3), new Vector3D(0, 2 * width, 0));
            if (visibleAxes[2].IsChecked == true) AddGuide(z, Color.FromRgb(74, 145, 222));
        }

        void AddGuide(MeshGeometry3D mesh, Color color)
        {
            var material = new EmissiveMaterial(new SolidColorBrush(color));
            var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
            model.Freeze();
            viewportGuides.Children.Add(model);
        }

        static void AddGuideStrip(MeshGeometry3D mesh, Point3D origin, Vector3D length, Vector3D halfWidth)
        {
            int start = mesh.Positions.Count;
            mesh.Positions.Add(origin - halfWidth);
            mesh.Positions.Add(origin + halfWidth);
            mesh.Positions.Add(origin + length + halfWidth);
            mesh.Positions.Add(origin + length - halfWidth);
            foreach (int index in new[] { 0, 1, 2, 0, 2, 3 }) mesh.TriangleIndices.Add(start + index);
        }
    }
}
