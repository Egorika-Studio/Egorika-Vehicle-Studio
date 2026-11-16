using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace SentinelViewer
{
    sealed partial class TuningEditor
    {
        static readonly string[] statFields = { "type", "identifier", "modifier", "audioApply", "weight" };
        static readonly string[] slotFields = { "slot", "name" };
        static readonly string[] knownCategoryLabels = (
            "CMOD_ROOF_ACC CMOD_SLOU_N TOP_AIR TOP_ARCHCOVER TOP_ARM_PLAT TOP_BACKREST TOP_BED TOP_BODY TOP_BOOT " +
            "TOP_BRACE TOP_BTRIM TOP_BULL TOP_BUMF TOP_BUMR TOP_CAGE TOP_CANARDS TOP_CATCH TOP_COUNTERM TOP_DIFF " +
            "TOP_ENGHD TOP_ENGINE TOP_FARI TOP_FDIFF TOP_FOGL TOP_FRAME TOP_HEADCO TOP_HL_CV TOP_HLT TOP_HOOD_ACC " +
            "TOP_HOODLIP TOP_HSWTURBO TOP_HSWUPGRADE TOP_IMANI_TECH TOP_INTERCOOL TOP_INTERTYPE TOP_LOUV TOP_MINE " +
            "TOP_MIR TOP_MUD TOP_MUDF TOP_MUDR TOP_ORN TOP_PANN TOP_PBOMBS TOP_PINS TOP_PLAT TOP_PUSH TOP_PWEAPON " +
            "TOP_RACK TOP_RAILCOV TOP_RDIFF TOP_REAR_DOOR TOP_ROLLBAR TOP_ROOF_LIGHT TOP_ROOFFIN TOP_ROOFPNL " +
            "TOP_ROOFSC TOP_RPNL TOP_RUFSPOILER TOP_RWING TOP_SEARCHLIGHT TOP_SEAT TOP_SEATF TOP_SIDE_PAN TOP_SKID " +
            "TOP_SNORKEL TOP_SPARE TOP_SPLIT TOP_SSHADES TOP_SSTEP TOP_STORAGEBOX TOP_SUNST TOP_SWEAPON " +
            "TOP_SWINGARM TOP_TAILFIN TOP_TALIGHT TOP_TANKS TOP_TGATE TOP_THRUST TOP_TNK TOP_TRUNK TOP_VALHD " +
            "TOP_VALVE TOP_VENT TOP_VENTBRAC TOP_WIND_DF TOP_WIND_PA TOP_WINDS_ACC TOP_WINDSH").Split(' ');
        static readonly XElement standardStatMods = CreateStandardStatMods();

        static XElement Stat(string type, int modifier, int weight, string identifier = null)
        {
            return new XElement("Item", new XElement("identifier", identifier), Value("modifier", modifier),
                Value("audioApply", 1), Value("weight", weight), new XElement("type", type));
        }

        static XElement Horn(string hash, int? modifier = null)
        {
            int audio = modifier ?? unchecked((int)UInt32.Parse(hash, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            return Stat("VMT_HORN", audio, 0, "hash_" + hash);
        }

        internal static XElement CreateStandardStatMods()
        {
            // Preserve the order of the standard Legacy preset, including the suspension block between horn groups.
            const string extraHorns =
                "D8470A11 E803A98A F525C3CE 06F76771 A00999A7 B1C83D24 BEEE5770 2DF8DE27 9A2CB52D 10E42116 " +
                "E788CD80 0FD69CCB 681F5467 726EE7E6 40B00395 1A7F4A28 5F8CD442 0E53B1D1 393A879E D4EA9A96 " +
                "E294B5EA A97EC3BF C72FFF21 BEB6E57D ACE041D0 EBCEBFB0 285006C6 7D2505B8 51DC2F23 ADDA777E " +
                "DD69EE08 9BADD325 BF236EB7 12824D0B B0C2FDDB 034BAE9E 39859163 0354642A 808D4A0A 17D78D30 " +
                "357E85FA 216DA05C 12BACFAB E58C583C 0EAC739E CB6C23FC 6B9022F1 C91F1F62 A6C6C5DE";
            return new XElement("statMods", new XAttribute("itemType", "CVehicleModStat"),
                new[] { 25, 50, 75, 100 }.Select(value => Stat("VMT_ENGINE", value, 20)),
                new[] { 25, 50, 100 }.Select(value => Stat("VMT_BRAKES", value, 5)),
                new[] { 25, 50, 100 }.Select(value => Stat("VMT_GEARBOX", value, 5)),
                Enumerable.Range(1, 5).Select(level => Stat("VMT_ARMOUR", level * 20, (level - 1) * 10)),
                Horn("622480DC", 1766676233), Horn("F39227BC", -1390777827), Horn("4DF196D6", -1751761149),
                Horn("0305BDAE", 1732399718), Horn("104C583B", 2046162893), Horn("5EE9F579", -2099967605),
                Horn("ECF4918C", -1786663196), Horn("3B3D2E20", -587743761), Horn("25BA0D65"),
                new[] { 5, 10, 15, 20 }.Select(value => Stat("VMT_SUSPENSION", value, 0)),
                extraHorns.Split(' ').Select(hash => Horn(hash)));
        }

        public sealed class AdvancedRow
        {
            readonly XElement original;
            readonly Dictionary<string, string> values;
            readonly bool newSlot;

            internal AdvancedRow(XElement item, IEnumerable<string> fields, bool newSlot = false)
            {
                original = item;
                values = fields.ToDictionary(field => field, field => ReadField(item, field) ?? "");
                this.newSlot = newSlot;
            }

            public string this[string field] { get { return values[field]; } set { values[field] = value ?? ""; } }

            static bool IsNumeric(string field) { return field == "modifier" || field == "audioApply" || field == "weight"; }

            static string ReadField(XElement item, string field)
            {
                var element = item.Element(field);
                return IsNumeric(field) ? (string)(element == null ? null : element.Attribute("value")) : (string)element;
            }

            internal XElement ToXml()
            {
                if (newSlot && values.Values.Any(String.IsNullOrWhiteSpace))
                    throw new InvalidOperationException(Ui.Text("Укажите категорию и подпись для каждой строки slotNames.",
                        "Enter a category and label for every slotNames row."));
                var item = new XElement(original);
                foreach (var field in values)
                {
                    string oldValue = ReadField(original, field.Key) ?? "";
                    if (field.Value == oldValue) continue;
                    var value = field.Value.Trim();
                    if (IsNumeric(field.Key))
                    {
                        decimal number;
                        if (!Decimal.TryParse(value.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture, out number) ||
                            (field.Key != "audioApply" && Decimal.Truncate(number) != number))
                            throw new InvalidOperationException(field.Key + ": " + value);
                        var element = item.Element(field.Key);
                        if (element == null) item.Add(element = new XElement(field.Key));
                        element.SetAttributeValue("value", field.Key == "audioApply"
                            ? number.ToString("0.000000", CultureInfo.InvariantCulture)
                            : number.ToString("0", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        if (field.Key != "identifier" && value.Length == 0)
                            throw new InvalidOperationException(field.Key + ": " +
                                Ui.Text("укажите значение", "Enter a value"));
                        item.SetElementValue(field.Key, value);
                    }
                }
                return item;
            }
        }

        XElement AdvancedSection(XElement edited, string name)
        {
            return edited ?? (importedKit == null ? null : importedKit.Element(name)) ??
                (importedKit == null && name == "statMods" ? standardStatMods : new XElement(name));
        }

        void ApplyAdvancedSections(XElement kit)
        {
            var stats = templateStats ?? (importedKit == null ? standardStatMods : null);
            foreach (var section in new[] { stats, editedSlots })
            {
                if (section == null) continue;
                var existing = kit.Element(section.Name);
                if (existing == null) kit.Add(new XElement(section));
                else existing.ReplaceWith(new XElement(section));
            }
        }

        internal void EditAdvancedSettings(Func<Task<XElement>> getCommonStats,
            Func<string[], Task<Dictionary<string, string>>> getCategoryLabels)
        {
            var currentStats = AdvancedSection(templateStats, "statMods");
            var currentSlots = AdvancedSection(editedSlots, "slotNames");
            var stats = new ObservableCollection<AdvancedRow>(currentStats.Elements("Item").Select(item => new AdvancedRow(item, statFields)));
            var slots = new ObservableCollection<AdvancedRow>(currentSlots.Elements("Item").Select(item => new AdvancedRow(item, slotFields)));
            var dialog = new Window { Owner = DialogOwner, Title = Ui.Text("Расширенные настройки carcols", "Advanced carcols settings"),
                Width = 760, Height = 660, MinWidth = 500, MinHeight = 450, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            StudioTheme.Apply(dialog);
            var layout = new DockPanel { Margin = new Thickness(10) };
            dialog.Content = layout;
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0) };
            DockPanel.SetDock(footer, Dock.Bottom);
            layout.Children.Add(footer);
            var sections = new Grid();
            sections.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
            sections.RowDefinitions.Add(new RowDefinition());
            layout.Children.Add(sections);

            var statGrid = AdvancedGrid(stats, statFields);
            var slotGrid = AdvancedGrid(slots, new string[0]);
            var originalSlots = imported == null ? Enumerable.Empty<XElement>() : imported.Descendants("slotNames").Elements("Item");
            var labels = originalSlots.Concat(currentSlots.Elements("Item")).ToArray();
            var labelKeys = knownCategoryLabels.Concat(labels.Select(item => (string)item.Element("name")))
                .Where(value => !String.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            Dictionary<string, string> localizedLabels = null;
            bool loadingLabels = true;
            dialog.Loaded += async (s, e) =>
            {
                try { localizedLabels = await getCategoryLabels(labelKeys); }
                catch (Exception ex) { System.Diagnostics.Trace.TraceWarning(ex.Message); }
                finally { loadingLabels = false; }
            };
            slotGrid.Columns.Add(new SlotSuggestionColumn("slot", Types
                .Concat(parts.Select(part => part.Type))
                .Concat(currentStats.Elements("Item").Select(item => (string)item.Element("type")))
                .Concat(labels.Select(item => (string)item.Element("slot")))));
            slotGrid.Columns.Add(new SlotSuggestionColumn("name", labelKeys, key =>
            {
                string title;
                if (localizedLabels != null && localizedLabels.TryGetValue(key, out title)) return title;
                if (loadingLabels) return Ui.Text("Загрузка названия из GTA V…", "Loading the GTA V label…");
                return localizedLabels == null
                    ? Ui.Text("Подключите архивы GTA V Legacy через настройки.", "Connect the GTA V Legacy archives in Settings.")
                    : Ui.Text("Название отсутствует в игровых архивах.", "This label is missing from the game archives.");
            }));
            var statTools = AddAdvancedPanel(sections, 0, "statMods", statGrid, stats, () => new AdvancedRow(new XElement("Item",
                new XElement("identifier"), Value("modifier", 0), Value("audioApply", "1.000000"), Value("weight", 20),
                new XElement("type", "VMT_ENGINE")), statFields));
            AddAdvancedPanel(sections, 1, Ui.Text("Подписи категорий", "Category labels") + " · slotNames", slotGrid, slots,
                () => new AdvancedRow(new XElement("Item", new XElement("slot"), new XElement("name")), slotFields, true));

            var preset = new Button { Content = Ui.Text("Частый пресет GTA V Legacy", "Most common GTA V Legacy preset"),
                Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 4, 8, 4) };
            statTools.Children.Add(preset);
            preset.Click += async (s, e) =>
            {
                preset.IsEnabled = false;
                object caption = preset.Content;
                preset.Content = Ui.Text("Читаю архивы GTA V…", "Indexing GTA V archives…");
                try
                {
                    var common = await getCommonStats();
                    if (!dialog.IsVisible) return;
                    if (common == null)
                    {
                        MessageBox.Show(dialog, Ui.Text("Подключите архивы GTA V Legacy через настройки.",
                            "Connect the GTA V Legacy archives in Settings."), dialog.Title);
                        return;
                    }
                    statGrid.CancelEdit();
                    stats.Clear();
                    foreach (var item in common.Elements("Item")) stats.Add(new AdvancedRow(item, statFields));
                }
                catch (Exception ex) { if (dialog.IsVisible) MessageBox.Show(dialog, ex.Message, dialog.Title); }
                finally { preset.Content = caption; preset.IsEnabled = true; }
            };

            XElement savedStats = null, savedSlots = null;
            AddButton(footer, Ui.Text("Применить", "Apply"), () =>
            {
                if (!CommitGrid(statGrid) || !CommitGrid(slotGrid)) return;
                try
                {
                    savedStats = BuildAdvancedSection(currentStats, stats);
                    savedSlots = BuildAdvancedSection(currentSlots, slots);
                    dialog.DialogResult = true;
                }
                catch (InvalidOperationException ex) { MessageBox.Show(dialog, ex.Message, dialog.Title); }
            });
            AddButton(footer, Ui.Text("Отмена", "Cancel"), dialog.Close);
            if (dialog.ShowDialog() != true) return;
            SaveAdvancedSections(savedStats, savedSlots);
        }

        internal void SaveAdvancedSections(XElement stats, XElement slots)
        {
            if (!XNode.DeepEquals(AdvancedSection(templateStats, "statMods"), stats)) templateStats = new XElement(stats);
            if (!XNode.DeepEquals(AdvancedSection(editedSlots, "slotNames"), slots)) editedSlots = new XElement(slots);
            NotifyPartsChanged();
        }

        internal static XElement BuildAdvancedSection(XElement original, IEnumerable<AdvancedRow> rows)
        {
            var section = new XElement(original);
            var previous = section.Elements("Item").ToArray();
            var updates = rows.Select(row => row.ToXml()).ToArray();
            int common = Math.Min(previous.Length, updates.Length);
            for (int i = 0; i < common; i++)
                if (!XNode.DeepEquals(previous[i], updates[i])) previous[i].ReplaceWith(updates[i]);
            foreach (var item in previous.Skip(common)) item.Remove();
            section.Add(updates.Skip(common));
            return section;
        }

        static DataGrid AdvancedGrid(ObservableCollection<AdvancedRow> rows, IEnumerable<string> fields)
        {
            var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, CanUserAddRows = false,
                CanUserSortColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column,
                SelectionMode = DataGridSelectionMode.Single };
            foreach (var field in fields)
                grid.Columns.Add(new DataGridTextColumn { Header = field, Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    Binding = new Binding("[" + field + "]") { Mode = BindingMode.TwoWay } });
            return grid;
        }

        static bool CommitGrid(DataGrid grid)
        {
            return grid.CommitEdit(DataGridEditingUnit.Cell, true) && grid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        sealed class SlotSuggestionColumn : DataGridColumn
        {
            readonly string field;
            readonly string[] suggestions;
            readonly Func<string, string> suggestionHint;

            internal SlotSuggestionColumn(string field, IEnumerable<string> suggestions, Func<string, string> suggestionHint = null)
            {
                this.field = field;
                this.suggestionHint = suggestionHint;
                this.suggestions = suggestions.Where(value => !String.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
                Header = field;
                Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                IsReadOnly = true; // The always-visible textbox owns editing inside the cell.
            }

            protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
            {
                var panel = new DockPanel();
                var box = new TextBox { BorderThickness = new Thickness(0), Padding = new Thickness(4, 0, 1, 0),
                    VerticalContentAlignment = VerticalAlignment.Center };
                box.SetBinding(TextBox.TextProperty, new Binding("[" + field + "]") { Source = dataItem,
                    Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                var arrow = new Button { Content = "▾", Width = 24, Padding = new Thickness(0), Focusable = false };
                System.Windows.Automation.AutomationProperties.SetName(arrow, field);
                DockPanel.SetDock(arrow, Dock.Right);
                panel.Children.Add(arrow);
                panel.Children.Add(box);

                var list = new ListBox { Background = StudioTheme.Panel, Foreground = Brushes.WhiteSmoke,
                    MaxHeight = 210, MinWidth = 160, Focusable = false };
                if (suggestionHint != null) list.ItemContainerStyle = SuggestionToolTips(suggestionHint);
                var popup = new Popup { PlacementTarget = panel, Placement = PlacementMode.Bottom, Child = list };
                popup.SetBinding(Popup.WidthProperty, new Binding("ActualWidth") { Source = panel });
                panel.Children.Add(popup);
                Action<bool> show = all =>
                {
                    string query = all ? "" : box.Text.Trim();
                    list.ItemsSource = FilterSlotSuggestions(suggestions, query);
                    list.SelectedIndex = -1;
                    popup.IsOpen = list.Items.Count > 0;
                };
                Action select = () =>
                {
                    string value = list.SelectedItem as string;
                    if (value != null) box.SetCurrentValue(TextBox.TextProperty, value);
                    popup.IsOpen = false;
                };
                arrow.Click += (s, e) =>
                {
                    if (popup.IsOpen) popup.IsOpen = false;
                    else { box.Focus(); show(true); }
                };
                box.TextChanged += (s, e) => { if (box.IsKeyboardFocused) show(false); };
                box.LostKeyboardFocus += (s, e) => { if (!list.IsKeyboardFocusWithin) popup.IsOpen = false; };
                panel.Unloaded += (s, e) => popup.IsOpen = false;
                list.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    var target = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) as ListBoxItem;
                    if (target == null) return;
                    list.SelectedItem = target.Content;
                    select();
                    e.Handled = true;
                };
                box.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Down || e.Key == Key.Up)
                    {
                        if (!popup.IsOpen) show(true);
                        if (!popup.IsOpen) return;
                        int index = list.SelectedIndex + (e.Key == Key.Down ? 1 : -1);
                        list.SelectedIndex = Math.Max(0, Math.Min(list.Items.Count - 1, index));
                        list.ScrollIntoView(list.SelectedItem);
                        e.Handled = true;
                    }
                    else if (popup.IsOpen && e.Key == Key.Enter) { select(); e.Handled = true; }
                    else if (popup.IsOpen && e.Key == Key.Escape) { popup.IsOpen = false; e.Handled = true; }
                };
                return panel;
            }

            static Style SuggestionToolTips(Func<string, string> hint)
            {
                var style = new Style(typeof(ListBoxItem));
                style.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 350));
                style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, ""));
                style.Setters.Add(new EventSetter(FrameworkElement.ToolTipOpeningEvent, new ToolTipEventHandler((s, e) =>
                {
                    var item = (ListBoxItem)s;
                    var key = item.Content as string;
                    if (key == null) { e.Handled = true; return; }
                    item.ToolTip = hint(key);
                })));
                return style;
            }

            protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
            {
                return GenerateElement(cell, dataItem);
            }
        }

        internal static string[] FilterSlotSuggestions(IEnumerable<string> suggestions, string query)
        {
            return suggestions.Where(value => value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(value => value.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        static Panel AddAdvancedPanel(Grid sections, int row, string title, DataGrid grid,
            ObservableCollection<AdvancedRow> rows, Func<AdvancedRow> newRow)
        {
            var section = new DockPanel { Margin = new Thickness(0, row == 0 ? 0 : 8, 0, 0) };
            Grid.SetRow(section, row);
            sections.Children.Add(section);
            var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 5) };
            DockPanel.SetDock(tools, Dock.Top);
            section.Children.Add(tools);
            section.Children.Add(grid);
            tools.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0) });
            AddButton(tools, "+", () =>
            {
                if (!CommitGrid(grid)) return;
                var item = newRow();
                rows.Add(item);
                grid.SelectedItem = item;
                grid.ScrollIntoView(item);
            });
            AddButton(tools, "−", () =>
            {
                if (CommitGrid(grid) && grid.SelectedItem is AdvancedRow) rows.Remove((AdvancedRow)grid.SelectedItem);
            });
            foreach (int direction in new[] { -1, 1 })
            {
                int step = direction;
                AddButton(tools, step < 0 ? "↑" : "↓", () =>
                {
                    if (!CommitGrid(grid)) return;
                    int index = grid.SelectedIndex, next = index + step;
                    if (index < 0 || next < 0 || next >= rows.Count) return;
                    rows.Move(index, next);
                    grid.SelectedIndex = next;
                });
            }
            return tools;
        }
    }
}
