using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SentinelViewer
{
    sealed partial class Viewer
    {
        readonly ListBox pendingTuning = new ListBox();
        TuningEditor.Part selectedTuningPart;
        TuningEditor.Part hoveredTuningPart;
        TuningChoice highlightedTuning;
        Point tuningDragStart;
        readonly Border tuningTableSize = new Border();
        static readonly Brush TuningDropBrush = new SolidColorBrush(Color.FromRgb(56, 100, 153));
        readonly List<GeometryModel3D> outlineModels = new List<GeometryModel3D>();
        readonly System.Windows.Shapes.Path outlineHalo = new System.Windows.Shapes.Path
            { Stroke = Brushes.Black, StrokeThickness = 1.6, IsHitTestVisible = false, Stretch = Stretch.None };
        readonly System.Windows.Shapes.Path outlineStroke = new System.Windows.Shapes.Path
            { Stroke = Brushes.Orange, StrokeThickness = 0.8, IsHitTestVisible = false, Stretch = Stretch.None };
        readonly Dictionary<MeshGeometry3D, OutlineMesh> outlineMeshes = new Dictionary<MeshGeometry3D, OutlineMesh>();

        Grid CreateTuningWorkspace()
        {
            var columns = new Grid { Margin = new Thickness(5, 0, 5, 4) };
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.55, GridUnitType.Star), MinWidth = 75 });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
            AddTuningColumn(columns, 0, Ui.Text("Без категории", "Unassigned"), pendingTuning);
            AddTuningColumn(columns, 2, Ui.Text("Категории VMT", "VMT categories"), tuningTree);
            var split = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch, Background = StudioTheme.Background };
            Grid.SetColumn(split, 1);
            columns.Children.Add(split);
            pendingTuning.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new Binding("Model"));
            name.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Model"));
            name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            pendingTuning.ItemTemplate = new DataTemplate { VisualTree = name };
            ScrollViewer.SetHorizontalScrollBarVisibility(pendingTuning, ScrollBarVisibility.Disabled);
            pendingTuning.PreviewMouseLeftButtonDown -= BeginTuningDrag;
            pendingTuning.PreviewMouseLeftButtonDown += BeginTuningDrag;
            pendingTuning.MouseMove -= DragPendingTuning;
            pendingTuning.MouseMove += DragPendingTuning;
            pendingTuning.SelectionChanged -= SelectPendingTuning;
            pendingTuning.SelectionChanged += SelectPendingTuning;
            pendingTuning.MouseMove -= HoverPendingTuning;
            pendingTuning.MouseMove += HoverPendingTuning;
            pendingTuning.MouseLeave -= LeaveTuning;
            pendingTuning.MouseLeave += LeaveTuning;
            return columns;
        }

        void AddTuningColumn(Grid columns, int index, string title, FrameworkElement content)
        {
            var column = new DockPanel { Margin = new Thickness(2, 0, 2, 0) };
            var label = new TextBlock { Text = title, Margin = new Thickness(3, 2, 3, 5) };
            DockPanel.SetDock(label, Dock.Top);
            column.Children.Add(label);
            if (content is TreeView)
            {
                var headings = TuningTableRow();
                foreach (string name in new[] { "YFT", "modShopLabel", "bone", "collisionBone", "turnOffBones", "linkedModels" })
                    AddTuningCell(headings, new TextBlock { Text = name, FontWeight = FontWeights.SemiBold });
                headings.HorizontalAlignment = HorizontalAlignment.Left;
                headings.Margin = new Thickness(38, 0, 0, 3);
                DockPanel.SetDock(headings, Dock.Top);
                column.Children.Add(headings);
                ScrollViewer.SetHorizontalScrollBarVisibility(content, ScrollBarVisibility.Disabled);
            }
            column.ClipToBounds = true;
            column.SizeChanged += (s, e) =>
            {
                double width = e.NewSize.Width;
                TextElement.SetFontSize(column, Math.Max(9, Math.Min(13, content is TreeView ? width / 67 : width / 12)));
                if (content is TreeView) tuningTableSize.Width = Math.Max(0, width - 60);
            };
            column.Children.Add(content);
            Grid.SetColumn(column, index);
            columns.Children.Add(column);
        }

        void BeginTuningDrag(object sender, MouseButtonEventArgs e)
        {
            tuningDragStart = e.GetPosition(pendingTuning);
            var row = ItemsControl.ContainerFromElement(pendingTuning, e.OriginalSource as DependencyObject) as ListBoxItem;
            pendingDragPart = row == null ? null : row.Content as TuningEditor.Part;
        }

        TuningEditor.Part pendingDragPart;
        void DragPendingTuning(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || pendingDragPart == null) return;
            Vector delta = e.GetPosition(pendingTuning) - tuningDragStart;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            var part = pendingDragPart;
            pendingDragPart = null;
            DragDrop.DoDragDrop(pendingTuning, part, DragDropEffects.Move);
        }

        void SelectPendingTuning(object sender, SelectionChangedEventArgs e)
        {
            var part = pendingTuning.SelectedItem as TuningEditor.Part;
            if (part != null) SelectTuningPart(part);
        }

        void SelectTuningTreeItem(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var node = e.NewValue as TreeViewItem;
            var part = node == null ? null : node.Tag as TuningEditor.Part;
            if (part != null) SelectTuningPart(part);
        }

        void SelectTuningPart(TuningEditor.Part part)
        {
            isolationTreeIsTuning = true;
            selectedTuningPart = part;
            SelectTuningXml();
        }

        void HoverPendingTuning(object sender, MouseEventArgs e)
        {
            var row = ItemsControl.ContainerFromElement(pendingTuning, e.OriginalSource as DependencyObject) as ListBoxItem;
            HoverTuning(row == null ? null : row.Content as TuningEditor.Part);
        }

        void LeaveTuning(object sender, MouseEventArgs e) { HoverTuning(null); }

        void HoverTuning(TuningEditor.Part part)
        {
            if (hoveredTuningPart == part) return;
            hoveredTuningPart = part;
            highlightedTuning = null;
            outlineMeshes.Clear();
            RefreshScene();
        }

        void SelectTuningXml()
        {
            if (selectedTuningPart == null || String.IsNullOrEmpty(selectedTuningPart.Model)) return;
            string xml = xmlPreview.Text;
            string name = new System.Xml.Linq.XElement("modelName", selectedTuningPart.Model).ToString();
            int sectionStart = xml.IndexOf("<visibleMods>", StringComparison.Ordinal);
            int sectionEnd = xml.IndexOf("</visibleMods>", StringComparison.Ordinal);
            int index = sectionStart < 0 ? -1 : xml.IndexOf(name, sectionStart, StringComparison.Ordinal);
            if (index < 0 || index >= sectionEnd)
            {
                sectionStart = xml.IndexOf("<linkMods>", StringComparison.Ordinal);
                sectionEnd = xml.IndexOf("</linkMods>", StringComparison.Ordinal);
                index = sectionStart < 0 ? -1 : xml.IndexOf(name, sectionStart, StringComparison.Ordinal);
                if (index < 0 || index >= sectionEnd)
                {
                    xmlPreview.Select(0, 0);
                    return;
                }
            }
            int start = xml.LastIndexOf("<Item>", index, StringComparison.Ordinal);
            int end = xml.IndexOf(Environment.NewLine + "        </Item>", index, StringComparison.Ordinal);
            if (start < sectionStart || end < 0 || end >= sectionEnd) return;
            end += Environment.NewLine.Length + "        </Item>".Length;
            xmlPreview.Select(start, end - start);
            xmlPreview.ScrollToLine(xmlPreview.GetLineIndexFromCharacterIndex(start));
        }

        void UpdateTuningTree()
        {
            var categories = tuningTree.Items.OfType<TreeViewItem>().ToArray();
            var expanded = new HashSet<string>(categories.Where(node => node.IsExpanded).Select(node => (string)node.Tag));

            tuningTree.Items.Clear();
            hoveredTuningPart = null;
            highlightedTuning = null;
            tuningChecks.Clear();
            var parts = tuningEditor == null ? new TuningEditor.Part[0] : tuningEditor.PreviewParts;
            if (!parts.Contains(selectedTuningPart)) { selectedTuningPart = null; highlightedTuning = null; }
            foreach (string key in tuningChoices.Keys.Where(key =>
                !parts.Contains(tuningChoices[key].Part) || Category(tuningChoices[key].Part) != key).ToArray())
                tuningChoices.Remove(key);
            pendingTuning.ItemsSource = tuningEditor == null ? null : parts.Where(tuningEditor.Unassigned).OrderBy(part => part.Model).ToArray();
            if (tuningEditor != null)
            {
                foreach (string type in TuningEditor.Types.Concat(parts.Select(part => part.Type))
                    .Where(type => !String.IsNullOrWhiteSpace(type)).Distinct().OrderBy(type => type))
                {
                    TuningChoice choice;
                    tuningChoices.TryGetValue(type, out choice);
                    var header = new TextBlock { Text = type, Padding = new Thickness(2, 1, 12, 1) };
                    DropTuning(header, false, part =>
                    {
                        if (!tuningEditor.Unassigned(part)) return;
                        var categoryNode = tuningTree.Items.OfType<TreeViewItem>().First(node => Equals(node.Tag, type));
                        categoryNode.IsExpanded = true;
                        tuningEditor.Assign(part, type);
                        SelectTuningPart(part);
                    });
                    var category = new TreeViewItem { Header = header, Tag = type, IsExpanded = expanded.Contains(type) };
                    foreach (var part in parts.Where(part => part.Type == type))
                    {
                        var row = TuningTableRow();
                        var check = new CheckBox { IsChecked = choice != null && choice.Enabled && choice.Part == part,
                            ToolTip = Ui.Text("Примерить / вернуть сток", "Fit / restore stock"), VerticalAlignment = VerticalAlignment.Center };
                        tuningChecks.Add(part, check);
                        var label = new TextBlock { Text = part.Model, Padding = new Thickness(4, 1, 12, 1),
                            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
                            ToolTip = Ui.Text("Задержите здесь деталь для linkedModels", "Hold a part here to add it to linkedModels") };
                        var modelCell = new DockPanel();
                        modelCell.Children.Add(check);
                        modelCell.Children.Add(label);
                        AddTuningCell(row, modelCell);
                        var shopLabel = new TextBox { Text = part.Label ?? "" };
                        shopLabel.TextChanged += (s, e) => { part.Label = shopLabel.Text; RefreshCarcolsPreview(); };
                        AddTuningCell(row, shopLabel);
                        AddTuningCell(row, BonePicker(part.Bone, bone => { part.Bone = bone; TuningGeometryEdited(part); }));
                        AddTuningCell(row, BonePicker(part.CollisionBone, bone => { part.CollisionBone = bone; RefreshCarcolsPreview(); }));
                        row.MouseEnter += (s, e) => HoverTuning(part);
                        row.MouseLeave += LeaveTuning;
                        var node = new TreeViewItem { Header = row, Tag = part };
                        check.PreviewMouseLeftButtonDown += (s, e) => node.IsSelected = true;
                        check.Checked += (s, e) => SetTuningPart(part, true);
                        check.Unchecked += (s, e) => SetTuningPart(part, false);
                        label.MouseLeftButtonDown += (s, e) => SelectTuningPart(part);
                        DropTuning(label, true, child =>
                        {

                            tuningEditor.Link(part, child);
                            SelectTuningPart(part);
                        });
                        AddPartLists(row, part);
                        category.Items.Add(node);
                    }
                    tuningTree.Items.Add(category);
                }
            }
            RefreshScene();
        }

        static void DropTuning(FrameworkElement target, bool hold, Action<TuningEditor.Part> drop)
        {
            target.AllowDrop = true;
            var text = target as TextBlock;
            var panel = target as Panel;
            Brush normal = text != null ? text.Background : panel.Background;
            Action<Brush> setBackground = brush => { if (text != null) text.Background = brush; else panel.Background = brush; };
            bool ready = false;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (s, e) => { timer.Stop(); ready = true; setBackground(TuningDropBrush); };
            target.DragEnter += (s, e) =>
            {
                if (!e.Data.GetDataPresent(typeof(TuningEditor.Part))) return;
                if (hold) timer.Start(); else { ready = true; setBackground(TuningDropBrush); }
            };
            target.DragOver += (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent(typeof(TuningEditor.Part)) && ready
                    ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            target.DragLeave += (s, e) => { timer.Stop(); ready = false; setBackground(normal); };
            target.Drop += (s, e) =>
            {
                timer.Stop();
                var part = e.Data.GetData(typeof(TuningEditor.Part)) as TuningEditor.Part;
                if (ready && part != null) drop(part);
                ready = false; setBackground(normal);
                e.Handled = true;
            };
        }

        void AddPartLists(Grid row, TuningEditor.Part part)
        {
            var turnOff = new StackPanel();
            var linked = new StackPanel { Background = Brushes.Transparent, MinHeight = 22,
                ToolTip = Ui.Text("Перетащите сюда связанную деталь", "Drop a linked part here") };
            AddTuningCell(row, turnOff);
            AddTuningCell(row, linked);
            DropTuning(linked, true, child => tuningEditor.Link(part, child));
            var bones = new StackPanel();
            turnOff.Children.Add(bones);
            Action updateBones = null;
            updateBones = () =>
            {
                bones.Children.Clear();
                foreach (string bone in TuningEditor.Names(part.TurnOff))
                    bones.Children.Add(Removable(bone, null, () =>
                    {
                        part.TurnOff = String.Join(", ", TuningEditor.Names(part.TurnOff).Where(value => value != bone));
                        updateBones();
                        TuningGeometryEdited(part);
                    }));
            };
            updateBones();
            var addBone = BonePicker(null, null);
            var add = Button("+", () =>
            {
                var bone = addBone.Text.Trim();
                if (bone.Length == 0) return;
                part.TurnOff = String.Join(", ", TuningEditor.Names(part.TurnOff).Concat(new[] { bone }).Distinct());
                addBone.Clear();
                updateBones();
                TuningGeometryEdited(part);
            });
            turnOff.Children.Add(Field(null, addBone, add));

            foreach (string model in TuningEditor.Names(part.Linked))
            {
                var linkedRow = Removable(model, BonePicker(tuningEditor.LinkedBone(model) ?? part.Bone,
                    bone => tuningEditor.SetLinkBone(model, bone)), () =>
                    {
                        tuningEditor.Unlink(part, model);
                        TuningGeometryEdited(part);
                    });
                var label = linkedRow.Children.OfType<TextBlock>().Single();
                label.ToolTip = Ui.Text("Перетащите к другой детали, чтобы добавить общую ссылку",
                    "Drag to another part to add a shared reference");
                Point? dragStart = null;
                label.MouseLeftButtonUp += (s, e) => dragStart = null;
                label.MouseLeftButtonDown += (s, e) =>
                {
                    dragStart = e.GetPosition(label);
                    var importedPart = tuningEditor.PreviewParts.FirstOrDefault(item =>
                        String.Equals(item.Model, model, StringComparison.OrdinalIgnoreCase));
                    SelectTuningPart(new TuningEditor.Part
                    {
                        Model = model,
                        FilePath = importedPart == null ? null : importedPart.FilePath,
                        Bone = tuningEditor.LinkedBone(model) ?? part.Bone
                    });
                };
                label.MouseMove += (s, e) =>
                {
                    if (e.LeftButton != MouseButtonState.Pressed || !dragStart.HasValue) return;
                    Vector delta = e.GetPosition(label) - dragStart.Value;
                    if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                        Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                    var child = tuningEditor.PreviewParts.FirstOrDefault(item =>
                        String.Equals(item.Model, model, StringComparison.OrdinalIgnoreCase)) ??
                        new TuningEditor.Part { Model = model, Bone = tuningEditor.LinkedBone(model) ?? part.Bone };
                    dragStart = null;
                    DragDrop.DoDragDrop(label, child, DragDropEffects.Move);
                };
                linked.Children.Add(linkedRow);
            }

        }

        Grid TuningTableRow()
        {
            var row = new Grid();
            row.SetBinding(FrameworkElement.WidthProperty, new Binding("Width") { Source = tuningTableSize });
            foreach (double width in new[] { 170.0, 140, 120, 122, 145, 270 })
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
            return row;
        }

        static void AddTuningCell(Grid row, FrameworkElement cell)
        {
            cell.Margin = new Thickness(2, 0, 2, 1);
            cell.VerticalAlignment = VerticalAlignment.Center;
            var text = cell as TextBlock;
            if (text != null) { text.TextTrimming = TextTrimming.CharacterEllipsis; text.ToolTip = text.Text; }
            var field = cell as TextBox;
            if (field != null) { field.VerticalContentAlignment = VerticalAlignment.Center; field.Padding = new Thickness(3, 1, 3, 1); }
            Grid.SetColumn(cell, row.Children.Count);
            row.Children.Add(cell);
        }

        TextBox BonePicker(string selected, Action<string> changed)
        {
            var field = new TextBox { Text = selected ?? "", VerticalContentAlignment = VerticalAlignment.Center };
            var matches = new ListBox { Background = StudioTheme.Panel, Foreground = Brushes.WhiteSmoke,
                MaxHeight = 190, MinWidth = 165 };
            var popup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = field, StaysOpen = false,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, Child = matches
            };
            Action filter = () =>
            {
                string query = field.Text.Trim();
                matches.ItemsSource = tuningEditor.bones
                    .Where(bone => bone.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(bone => bone.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToArray();
                matches.SelectedIndex = matches.Items.Count > 0 ? 0 : -1;
                popup.IsOpen = matches.Items.Count > 0 && field.IsKeyboardFocusWithin;
            };
            Action commit = () =>
            {
                popup.IsOpen = false;
                string value = field.Text.Trim();
                if (changed != null && value != selected) { selected = value; changed(value); }
            };
            Action pick = () =>
            {
                var choice = matches.SelectedItem as string;
                if (choice != null) field.Text = choice;
                commit();
            };
            field.GotKeyboardFocus += (s, e) => filter();
            field.TextChanged += (s, e) => { if (field.IsKeyboardFocusWithin) filter(); };
            field.LostKeyboardFocus += (s, e) => { if (!matches.IsKeyboardFocusWithin) commit(); };
            matches.PreviewMouseLeftButtonDown += (s, e) =>
            {
                var row = ItemsControl.ContainerFromElement(matches, e.OriginalSource as DependencyObject) as ListBoxItem;
                if (row == null) return;
                matches.SelectedItem = row.Content;
                pick();
                e.Handled = true;
            };
            field.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { popup.IsOpen = false; e.Handled = true; }
                else if (e.Key == Key.Enter) { commit(); e.Handled = true; }
                else if (e.Key == Key.Down && popup.IsOpen)
                {
                    matches.Focus();
                    var item = matches.ItemContainerGenerator.ContainerFromIndex(matches.SelectedIndex) as ListBoxItem;
                    if (item != null) item.Focus();
                    e.Handled = true;
                }
            };
            matches.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Enter && e.Key != Key.Escape) return;
                if (e.Key == Key.Enter) pick(); else popup.IsOpen = false;
                e.Handled = true;
            };
            field.Unloaded += (s, e) => popup.IsOpen = false;
            return field;
        }

        static DockPanel Field(string label, UIElement editor, Button action = null)
        {
            var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
            if (action != null)
            {
                action.Margin = new Thickness(3, 0, 0, 0);
                action.Padding = new Thickness(4, 0, 4, 0);
                DockPanel.SetDock(action, Dock.Right);
                row.Children.Add(action);
            }
            if (label != null)
                row.Children.Add(new TextBlock { Text = label, MaxWidth = 112, Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            if (editor != null) row.Children.Add(editor);
            return row;
        }

        static DockPanel Removable(string label, UIElement editor, Action remove)
        {
            var close = Button("×", remove);
            close.ToolTip = Ui.Text("Удалить из списка", "Remove from list");
            return Field(label, editor, close);
        }

        void TuningGeometryEdited(TuningEditor.Part part)
        {
            highlightedTuning = null;
            RefreshCarcolsPreview();
            TuningChoice choice;
            if (tuningChoices.TryGetValue(Category(part), out choice) && choice.Part == part && choice.Enabled)
                SetTuningPart(part, true);
            else RefreshScene();
        }

        void AddTuningHighlight(string level, List<Vehicle.Piece> visible, List<Vehicle.Piece> attachments)
        {
            outlineModels.Clear();
            var part = hoveredTuningPart;
            if (part == null || tuningEditor == null || loading || hideUnboundGeometry || showHoverOutline.IsChecked != true) return;
            TuningChoice choice;
            bool active = tuningChoices.TryGetValue(Category(part), out choice) &&
                choice.Part == part && choice.Enabled;
            if (IsIsolated && isolatedTuning != null && isolatedTuning.Part == part) choice = isolatedTuning;
            else if (!active)
            {
                if (highlightedTuning == null)
                {
                    try { highlightedTuning = PrepareTuning(part, tuningEditor.PreviewParts, tuningEditor.ImportedPath); }
                    catch (Exception ex) { status.Text = ex.Message; return; }
                }
                choice = highlightedTuning;
            }
            if (choice.Livery != null)
            {
                foreach (var piece in visible.Concat(attachments).Where(piece => piece.LiveryMesh != null))
                    outlineModels.Add(new GeometryModel3D(piece.LiveryMesh, null));
                return;
            }
            List<Vehicle.Piece> pieces;
            if (!choice.Pieces.TryGetValue(level, out pieces) || pieces.Count == 0)
            {
                if (active || IsIsolated) return;
                pieces = choice.Pieces.Values.FirstOrDefault(items => items.Count > 0);
                if (pieces == null) return;
            }
            foreach (var piece in pieces)
            {
                if (active && !IsIsolated && IsBoneHidden(piece.Bone)) continue;
                if (IsIsolated && !visible.Contains(piece)) continue;
                outlineModels.Add(piece.Model);
            }
        }
        sealed class OutlineMesh
        {
            internal readonly Point3D[] Points;
            internal readonly int[] Triangles;
            internal readonly Dictionary<Tuple<int, int>, List<int>> Edges = new Dictionary<Tuple<int, int>, List<int>>();

            internal OutlineMesh(MeshGeometry3D mesh)
            {
                // Merge coincident vertices so UV seams do not become outlines.
                var pointIds = new Dictionary<Point3D, int>();
                var ids = new int[mesh.Positions.Count];
                for (int i = 0; i < ids.Length; i++)
                {
                    Point3D p = mesh.Positions[i];
                    int id;
                    if (!pointIds.TryGetValue(p, out id)) pointIds.Add(p, id = pointIds.Count);
                    ids[i] = id;
                }
                Points = new Point3D[pointIds.Count];
                foreach (var point in pointIds) Points[point.Value] = point.Key;
                Triangles = mesh.TriangleIndices.Select(index => ids[index]).ToArray();
                for (int i = 0; i < Triangles.Length; i += 3)
                    for (int side = 0; side < 3; side++)
                    {
                        int a = Triangles[i + side], b = Triangles[i + (side + 1) % 3];
                        if (a == b) continue;
                        var key = Tuple.Create(Math.Min(a, b), Math.Max(a, b));
                        List<int> faces;
                        if (!Edges.TryGetValue(key, out faces)) Edges.Add(key, faces = new List<int>(2));
                        faces.Add(i / 3);
                    }
            }
        }

        void DrawTuningOutline()
        {
            if (outlineModels.Count == 0 || viewport.ActualWidth <= 0 || viewport.ActualHeight <= 0)
            {
                outlineHalo.Data = outlineStroke.Data = null;
                return;
            }
            var forward = camera.LookDirection;
            forward.Normalize();
            var right = Vector3D.CrossProduct(forward, camera.UpDirection);
            right.Normalize();
            var up = Vector3D.CrossProduct(right, forward);
            double width = viewport.ActualWidth, height = viewport.ActualHeight;
            bool ortho = viewport.Camera == orthographic;
            double scale = width / (ortho ? orthographic.Width : 2 * Math.Tan(camera.FieldOfView * Math.PI / 360));
            var geometry = new StreamGeometry();
            using (var draw = geometry.Open())
                foreach (var model in outlineModels)
                {
                    var source = (MeshGeometry3D)model.Geometry;
                    OutlineMesh mesh;
                    if (!outlineMeshes.TryGetValue(source, out mesh))
                        outlineMeshes.Add(source, mesh = new OutlineMesh(source));
                    var world = mesh.Points.Select(p => model.Transform.Transform(p)).ToArray();
                    var screen = new Point[world.Length];
                    var depth = new double[world.Length];
                    for (int i = 0; i < world.Length; i++)
                    {
                        var offset = world[i] - camera.Position;
                        depth[i] = Vector3D.DotProduct(offset, forward);
                        double factor = ortho ? scale : scale / Math.Max(camera.NearPlaneDistance, depth[i]);
                        screen[i] = new Point(width / 2 + Vector3D.DotProduct(offset, right) * factor,
                            height / 2 - Vector3D.DotProduct(offset, up) * factor);
                    }
                    var front = new bool[mesh.Triangles.Length / 3];
                    for (int i = 0; i < front.Length; i++)
                    {
                        var a = world[mesh.Triangles[3 * i]];
                        var b = world[mesh.Triangles[3 * i + 1]];
                        var c = world[mesh.Triangles[3 * i + 2]];
                        front[i] = Vector3D.DotProduct(Vector3D.CrossProduct(b - a, c - a),
                            ortho ? -forward : camera.Position - a) > 0;
                    }
                    foreach (var edge in mesh.Edges)
                    {
                        int a = edge.Key.Item1, b = edge.Key.Item2;
                        if (depth[a] < camera.NearPlaneDistance || depth[b] < camera.NearPlaneDistance ||
                            !edge.Value.Any(face => front[face]) ||
                            (edge.Value.Count > 1 && edge.Value.All(face => front[face]))) continue;
                        draw.BeginFigure(screen[a], false, false);
                        draw.LineTo(screen[b], true, false);
                    }
                }
            geometry.Freeze();
            outlineHalo.Data = outlineStroke.Data = geometry;
        }

    }
}
