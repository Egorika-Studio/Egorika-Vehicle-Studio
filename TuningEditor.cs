using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using CodeWalker.GameFiles;
using Microsoft.Win32;

namespace SentinelViewer
{
    sealed class TuningEditor : Window
    {
        public sealed class Part
        {
            internal XElement Original;
            internal string FilePath;
            public string Model { get; set; }
            public string Label { get; set; }
            public string Linked { get; set; }
            public string TurnOff { get; set; }
            public string Type { get; set; }
            public string Bone { get; set; }
            public string CollisionBone { get; set; }
        }

        internal static readonly string[] Types = (
            "BONNET BUMPER_F BUMPER_R CHASSIS CHASSIS2 CHASSIS3 CHASSIS4 CHASSIS5 DOOR_L ENGINEBAY1 ENGINEBAY2 ENGINEBAY3 " +
            "EXHAUST GRILL HYDRO ICE INTERIOR1 INTERIOR2 INTERIOR3 INTERIOR4 INTERIOR5 KNOB LIVERY_MOD PLAQUE PLTHOLDER " +
            "PLTVANITY ROOF SEATS SKIRT SPOILER STEERING TRUNK WING_L WING_R").Split(' ').Select(t => "VMT_" + t).ToArray();

        readonly ObservableCollection<Part> parts = new ObservableCollection<Part>();
        readonly TextBox kitId = new TextBox { Width = 70, Margin = new Thickness(5, 0, 14, 0) };
        static readonly string[] KitTypes = { "MKT_SPECIAL", "MKT_STANDARD", "MKT_SPORT", "MKT_SUV" };
        static string KitTypeLabel(string code)
        {
            switch (code)
            {
                case "MKT_STANDARD": return code + Ui.Text(" · Стандартный", " · Standard");
                case "MKT_SPORT": return code + Ui.Text(" · Спортивный", " · Sport");
                case "MKT_SUV": return code + Ui.Text(" · Для SUV", " · SUV");
                case "MKT_SPECIAL": return code + Ui.Text(" · Специальный", " · Special");
                default: return code;
            }
        }

        void SelectKitType(string code)
        {
            kitType.ItemsSource = KitTypes.Concat(String.IsNullOrEmpty(code) ? new string[0] : new[] { code })
                .Distinct().Select(value => new KeyValuePair<string, string>(value, KitTypeLabel(value))).ToArray();
            kitType.SelectedValue = code;
        }
        readonly ComboBox kitType = new ComboBox { Width = 225, Margin = new Thickness(5, 0, 14, 0), DisplayMemberPath = "Value", SelectedValuePath = "Key" };
        XElement templateStats;
        string generatedName;
        readonly TextBox kitName = new TextBox { Width = 230, Margin = new Thickness(5, 0, 14, 0) };

        internal event Action<string> StatusChanged;
        void ReportStatus(string message) { if (StatusChanged != null) StatusChanged(message); }
        internal readonly string[] bones;
        readonly string modelName;
        readonly Action<Part, Part[], string> preview;
        XDocument imported;
        XElement importedKit;
        string importedPath;
        bool loadingParts;
        public sealed class LinkMod
        {
            internal XElement Original;
            public string Model { get; set; }
            public string Bone { get; set; }
        }

        readonly ObservableCollection<LinkMod> linkMods = new ObservableCollection<LinkMod>();
        internal readonly ContentControl Workspace = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        public event Action<string> LinkChanged;

        internal string LinkedBone(string model)
        {
            var link = linkMods.FirstOrDefault(item => String.Equals(item.Model, model, StringComparison.OrdinalIgnoreCase));
            return link == null ? null : link.Bone;
        }

        internal IEnumerable<LinkMod> LinkMods { get { return linkMods; } }
        public event Action PartsChanged;
        public Part[] PreviewParts { get { return parts.ToArray(); } }
        public string ImportedPath { get { return importedPath; } }

        public TuningEditor(string model, IEnumerable<string> vehicleBones, Action<Part, Part[], string> preview = null)
        {
            modelName = model;
            this.preview = preview;
            StudioTheme.Apply(this);
            Title = "Egorika Vehicle Studio · carcols.meta · " + model;
            Width = 1150;
            Height = 580;
            MinWidth = 820;
            MinHeight = 350;
            bones = vehicleBones.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(b => b).ToArray();
            SelectKitType(KitTypes[0]);
            generatedName = model + "_modkit";
            kitName.Text = generatedName;
            kitId.TextChanged += (sender, args) =>
            {
                if (imported != null || kitName.Text != generatedName) return;
                ushort number;
                generatedName = UInt16.TryParse(kitId.Text, NumberStyles.None, CultureInfo.InvariantCulture, out number)
                    ? number.ToString(CultureInfo.InvariantCulture) + "_" + modelName + "_modkit" : modelName + "_modkit";
                kitName.Text = generatedName;
            };
            Content = Workspace;
            kitId.TextChanged += (s, e) => NotifyPartsChanged();
            kitName.TextChanged += (s, e) => NotifyPartsChanged();
            kitType.SelectionChanged += (s, e) => NotifyPartsChanged();
            linkMods.CollectionChanged += (s, e) => NotifyPartsChanged();
            parts.CollectionChanged += (sender, args) => NotifyPartsChanged();
        }

        internal void EditKitSettings()
        {
            var dialog = new Window { Owner = DialogOwner, Title = Ui.Text("Параметры комплекта carcols", "Carcols kit settings"),
                SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner };
            StudioTheme.Apply(dialog);
            var panel = new StackPanel { Margin = new Thickness(12), MinWidth = 260 };
            var labels = new[] { Ui.Text("ID комплекта", "Kit ID"), Ui.Text("Имя комплекта", "Kit name"), "kitType" };
            var fields = new Control[] { kitId, kitName, kitType };
            for (int i = 0; i < fields.Length; i++)
            {
                panel.Children.Add(new TextBlock { Text = labels[i], Margin = new Thickness(0, 5, 0, 3) });
                panel.Children.Add(fields[i]);
            }
            AddButton(panel, Ui.Text("Готово", "Done"), dialog.Close);
            dialog.Content = panel;
            try { dialog.ShowDialog(); }
            finally { foreach (var field in fields) panel.Children.Remove(field); }
        }

        internal void NotifyPartsChanged()
        {
            if (!loadingParts && PartsChanged != null) PartsChanged();
        }

        internal bool Unassigned(Part part)
        {
            return String.IsNullOrWhiteSpace(part.Type) && !parts.Any(parent =>
                Names(parent.Linked).Contains(part.Model, StringComparer.OrdinalIgnoreCase));
        }

        internal void Assign(Part part, string type)
        {
            if (!parts.Contains(part)) return;
            part.Type = type;
            NotifyPartsChanged();
        }

        internal void Link(Part parent, Part child)
        {
            if (!parts.Contains(parent) || String.IsNullOrWhiteSpace(parent.Type) ||
                String.IsNullOrWhiteSpace(child.Model) ||
                String.Equals(parent.Model, child.Model, StringComparison.OrdinalIgnoreCase) ||
                Names(parent.Linked).Contains(child.Model, StringComparer.OrdinalIgnoreCase) ||
                (!parts.Contains(child) && !parts.Any(part => Names(part.Linked)
                    .Contains(child.Model, StringComparer.OrdinalIgnoreCase))) ||
                Names(child.Linked).Contains(parent.Model, StringComparer.OrdinalIgnoreCase)) return;
            parent.Linked = String.Join(", ", Names(parent.Linked).Concat(new[] { child.Model }));
            if (!linkMods.Any(link => String.Equals(link.Model, child.Model, StringComparison.OrdinalIgnoreCase)))
                SetLinkBone(child.Model, child.Bone ?? parent.Bone);
            else if (LinkChanged != null) LinkChanged(child.Model);
            NotifyPartsChanged();
        }

        internal void Unlink(Part parent, string model)
        {
            parent.Linked = String.Join(", ", Names(parent.Linked).Where(name =>
                !String.Equals(name, model, StringComparison.OrdinalIgnoreCase)));
            if (!parts.Any(part => Names(part.Linked).Contains(model, StringComparer.OrdinalIgnoreCase)))
            {
                var link = linkMods.FirstOrDefault(item => String.Equals(item.Model, model, StringComparison.OrdinalIgnoreCase));
                if (link != null) linkMods.Remove(link);
                if (!parts.Any(part => String.Equals(part.Model, model, StringComparison.OrdinalIgnoreCase)))
                {
                    string bone = (link == null ? null : link.Bone) ?? parent.Bone;
                    parts.Add(new Part { Model = model, Label = model.ToUpperInvariant(),
                        Bone = bone, CollisionBone = parent.CollisionBone });
                }
            }
            if (LinkChanged != null) LinkChanged(null);
            NotifyPartsChanged();
        }

        internal void SetLinkBone(string model, string bone)
        {
            var link = linkMods.FirstOrDefault(item => String.Equals(item.Model, model, StringComparison.OrdinalIgnoreCase));
            if (link == null) linkMods.Add(new LinkMod { Model = model, Bone = bone });
            else link.Bone = bone;
            if (LinkChanged != null) LinkChanged(model);
        }

        static void AddButton(Panel panel, string label, Action action)
        {
            var button = new Button { Content = label, Margin = new Thickness(0, 0, 7, 0),
                Padding = new Thickness(8, 4, 8, 4) };
            button.Click += (s, e) => action();
            panel.Children.Add(button);
        }

        Window hostWindow;
        Window DialogOwner { get { return hostWindow ?? this; } }

        internal FrameworkElement DetachEditorContent(Window owner)
        {
            var element = (FrameworkElement)Content;
            Content = null;
            hostWindow = owner;
            return element;
        }

        internal void Import()
        {
            var dialog = new OpenFileDialog { Multiselect = true, Filter = Ui.Text("Тюнинг GTA V (*.yft)|*.yft", "GTA V tuning (*.yft)|*.yft") };
            if (dialog.ShowDialog(DialogOwner) != true) return;
            var failures = new List<string>();
            int added = 0;
            foreach (var path in dialog.FileNames)
            {
                try { if (ImportPart(path)) added++; }
                catch (Exception ex) { failures.Add(Path.GetFileName(path) + ": " + ex.Message); }
            }
            ReportStatus(Ui.Text("Добавлено: ", "Added: ") + added +
                Ui.Text(". Перетащите детали в категории VMT.", ". Drag parts into VMT categories."));
            if (failures.Count > 0) MessageBox.Show(DialogOwner, String.Join(Environment.NewLine, failures), Ui.Text("Ошибки импорта", "Import errors"));
        }

        internal bool ImportPart(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (parts.Any(p => String.Equals(p.Model, name, StringComparison.OrdinalIgnoreCase))) return false;
            var yft = new YftFile();
            yft.Load(File.ReadAllBytes(path));
            if (yft.Fragment == null || yft.Fragment.Drawable == null)
                throw new InvalidDataException(Ui.Text("В файле нет модели.", "The file contains no model."));
            var chassis = bones.FirstOrDefault(b => b == "chassis") ?? bones.FirstOrDefault();
            parts.Add(new Part { FilePath = path, Model = name, Label = name.ToUpperInvariant(), Bone = chassis, CollisionBone = chassis });
            return true;
        }

        internal void OpenCarcols()
        {
            var dialog = new OpenFileDialog { Filter = "GTA V (*.meta;*.xml)|*.meta;*.xml" };
            if (dialog.ShowDialog(DialogOwner) != true) return;
            if (parts.Count > 0 && MessageBox.Show(DialogOwner, Ui.Text("Заменить таблицу открываемым комплектом?", "Replace the table with the selected kit?"), Ui.Text("Открыть carcols", "Open carcols"),
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try
            {
                LoadCarcols(dialog.FileName);
                if (preview != null) preview(null, null, null);
            }
            catch (Exception ex) { MessageBox.Show(DialogOwner, ex.Message, Ui.Text("Чтение carcols", "Reading carcols")); }
        }

        static XDocument ReadDocument(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }

        static XElement[] ReadKits(XDocument document)
        {
            var kits = document.Root == null ? null : document.Root.Element("Kits");
            var choices = kits == null ? new XElement[0] : kits.Elements("Item").Where(k => k.Element("kitName") != null).ToArray();
            if (choices.Length == 0)
                throw new InvalidDataException(Ui.Text("В файле не найдены комплекты Kits.", "No Kits were found in this file."));
            return choices;
        }

        XElement ChooseKit(XElement[] choices)
        {
            var picker = new Window { Owner = DialogOwner, Title = Ui.Text("Выберите комплект", "Select a kit"), Width = 440, Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner };
            StudioTheme.Apply(picker);
            var list = new ListBox { ItemsSource = choices.Select(k => (string)k.Element("kitName")).ToArray(), SelectedIndex = 0 };
            var layout = new DockPanel { Margin = new Thickness(10) };
            var row = new StackPanel();
            DockPanel.SetDock(row, Dock.Bottom);
            layout.Children.Add(row);
            AddButton(row, Ui.Text("Открыть комплект", "Open kit"), () => { if (list.SelectedIndex >= 0) picker.DialogResult = true; });
            layout.Children.Add(list);
            picker.Content = layout;
            if (picker.ShowDialog() != true) return null;
            return choices[list.SelectedIndex];
        }

        bool ConfirmNew()
        {
            return (parts.Count == 0 && linkMods.Count == 0 && templateStats == null) ||
                MessageBox.Show(DialogOwner, Ui.Text("Очистить текущий комплект и создать новый?",
                    "Clear the current kit and create a new one?"), Ui.Text("Новый carcols", "New carcols"),
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        }

        internal void NewKit()
        {
            if (ConfirmNew()) ResetKit(null);
        }

        internal void NewFromExample()
        {
            var dialog = new OpenFileDialog { Filter = "GTA V (*.meta;*.xml)|*.meta;*.xml" };
            if (dialog.ShowDialog(DialogOwner) != true) return;
            try
            {
                var choices = ReadKits(ReadDocument(dialog.FileName));
                var example = choices.Length == 1 ? choices[0] : ChooseKit(choices);
                if (example != null && ConfirmNew()) ResetKit(example);
            }
            catch (Exception ex) { MessageBox.Show(DialogOwner, ex.Message, Ui.Text("Чтение carcols", "Reading carcols")); }
        }

        internal void ResetKit(XElement example)
        {
            imported = null;
            importedKit = null;
            importedPath = null;
            var stats = example == null ? null : example.Element("statMods");
            templateStats = stats == null ? null : new XElement(stats);
            string type = example == null ? null : (string)example.Element("kitType");
            SelectKitType(KitTypes.Contains(type) ? type : KitTypes[0]);
            kitId.Clear();
            generatedName = modelName + "_modkit";
            kitName.Text = generatedName;
            linkMods.Clear();
            parts.Clear();

            if (preview != null) preview(null, null, null);
            ReportStatus(templateStats == null
                ? Ui.Text("Новый комплект. Укажите ID и импортируйте YFT.", "New kit. Enter an ID and import YFT files.")
                : Ui.Text("Новый комплект. statMods взяты из образца.", "New kit. statMods copied from the example."));
        }

        internal int LoadCarcols(string path, bool allowPicker = true, string exactKit = null)
        {
            var document = ReadDocument(path);
            var choices = ReadKits(document);
            var kit = choices.FirstOrDefault(k => exactKit != null
                ? String.Equals((string)k.Element("kitName"), exactKit, StringComparison.Ordinal)
                : ((string)k.Element("kitName")).EndsWith("_" + modelName + "_modkit", StringComparison.OrdinalIgnoreCase));
            if (kit == null)
            {
                if (choices.Length == 1) kit = choices[0];
                else if (allowPicker) kit = ChooseKit(choices);
                if (kit == null) return 0;
            }
            var visible = kit.Element("visibleMods");
            var loaded = visible == null ? new Part[0] : visible.Elements("Item").Select(item => new Part
            {
                Original = item,
                Model = (string)item.Element("modelName"),
                Label = (string)item.Element("modShopLabel"),
                Linked = ReadNames(item.Element("linkedModels")),
                TurnOff = ReadNames(item.Element("turnOffBones")),
                Type = (string)item.Element("type"),
                Bone = (string)item.Element("bone"),
                CollisionBone = (string)item.Element("collisionBone")
            }).ToArray();

            linkMods.Clear();
            var links = kit.Element("linkMods");
            if (links != null)
                foreach (var link in links.Elements("Item"))
                    linkMods.Add(new LinkMod { Original = link, Model = (string)link.Element("modelName"), Bone = (string)link.Element("bone") });
            templateStats = null;
            imported = document;
            string type = (string)kit.Element("kitType");
            SelectKitType(type);
            importedKit = kit;
            importedPath = path;
            kitId.Text = kit.Element("id") == null ? "" : (string)kit.Element("id").Attribute("value");
            kitName.Text = (string)kit.Element("kitName");
            loadingParts = true;
            try
            {
                parts.Clear();
                foreach (var part in loaded) parts.Add(part);
            }
            finally { loadingParts = false; }
            NotifyPartsChanged();
            ReportStatus(Ui.Text("Загружен ", "Loaded ") + kitName.Text + Ui.Text(" · деталей: ", " · parts: ") + parts.Count);
            return parts.Count;
        }

        static string ReadNames(XElement element)
        {
            return element == null ? "" : String.Join(", ", element.Elements("Item").Select(item => item.Value));
        }

        internal string PreviewXml()
        {
            ushort id;
            UInt16.TryParse(kitId.Text, NumberStyles.None, CultureInfo.InvariantCulture, out id);
            using (var output = new MemoryStream())
            {
                WriteCarcols(ExportVehicleDocument(id, kitName.Text), output);
                return System.Text.Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF');
            }
        }

        internal XElement CaptureProject()
        {

            ushort id;
            UInt16.TryParse(kitId.Text, NumberStyles.None, CultureInfo.InvariantCulture, out id);
            var document = ExportDocument(id, kitName.Text, true);
            return new XElement("editor", new XAttribute("id", kitId.Text),
                new XAttribute("name", kitName.Text), new XElement(document.Root));
        }

        internal void RestoreProject(XElement state, string[] partPaths, string directory)
        {
            if (state == null) return;
            var root = state.Element("CVehicleModelInfoVarGlobal");
            if (root == null) throw new InvalidDataException("Missing project carcols data.");
            var path = Path.Combine(directory, "carcols.meta");
            new XDocument(new XElement(root)).Save(path);
            LoadCarcols(path, false, (string)state.Attribute("name"));
            kitId.Text = (string)state.Attribute("id") ?? "";
            for (int i = 0; i < Math.Min(parts.Count, partPaths.Length); i++)
                parts[i].FilePath = partPaths[i];
        }

        internal XDocument ExportVehicleDocument(ushort id, string name)
        {
            var kit = BuildEditedKit(id, name);
            return new XDocument(new XElement(imported == null ? XName.Get("CVehicleModelInfoVarGlobal") : imported.Root.Name, new XElement("Kits", kit)));
        }

        internal void Export(bool vehicleOnly)
        {

            try
            {
                ushort id;
                if (!UInt16.TryParse(kitId.Text, NumberStyles.None, CultureInfo.InvariantCulture, out id))
                    throw new InvalidDataException(Ui.Text("Введите числовой ID комплекта от 0 до 65535.", "Enter a numeric kit ID from 0 to 65535."));
                string name = kitName.Text.Trim();
                if (name.Length == 0) throw new InvalidDataException(Ui.Text("Введите имя комплекта.", "Enter a kit name."));
                if (!ExportParts(false).Any()) throw new InvalidDataException(Ui.Text("Импортируйте хотя бы одну деталь.", "Import at least one part."));
                foreach (var part in ExportParts(false).Where(p => p.Original == null))
                {
                    if (String.IsNullOrWhiteSpace(part.Label) || !Types.Contains(part.Type) ||
                        String.IsNullOrWhiteSpace(part.Bone) || String.IsNullOrWhiteSpace(part.CollisionBone))
                        throw new InvalidDataException(part.Model + Ui.Text(": заполните метку, тип и обе кости.", ": fill in the label, type and both bones."));
                }
                if (linkMods.Any(link => String.IsNullOrWhiteSpace(link.Model) ||
                    (link.Original == null && String.IsNullOrWhiteSpace(link.Bone))))
                    throw new InvalidDataException(Ui.Text("Заполните имя и кость каждой связанной модели.",
                        "Enter a model name and bone for each linked model."));
                var document = vehicleOnly ? ExportVehicleDocument(id, name) : ExportDocument(id, name);
                var dialog = new SaveFileDialog { Title = vehicleOnly ? Ui.Text("Экспорт текущей модели", "Export current vehicle") : Ui.Text("Экспорт всего carcols", "Export entire carcols"), FileName = "carcols.meta", Filter = "GTA V (*.meta)|*.meta", DefaultExt = ".meta" };
                if (dialog.ShowDialog(DialogOwner) != true) return;
                string temporary = Path.Combine(Path.GetDirectoryName(dialog.FileName), Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var stream = File.Create(temporary)) WriteCarcols(document, stream);
                    if (File.Exists(dialog.FileName)) File.Replace(temporary, dialog.FileName, null);
                    else File.Move(temporary, dialog.FileName);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                ReportStatus(Ui.Text("Сохранён ", "Saved ") + dialog.FileName);
            }
            catch (Exception ex) { MessageBox.Show(DialogOwner, ex.Message, Ui.Text("Экспорт carcols.meta", "Export carcols.meta")); }
        }

        internal static IEnumerable<string> Names(string text)
        {
            return (text ?? "").Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        static XElement Entries(string element, string text)
        {
            return new XElement(element, Names(text).Select(value => new XElement("Item", value)));
        }

        static XElement Value(string name, object value)
        {
            return new XElement(name, new XAttribute("value", value));
        }

        internal static void WriteCarcols(XDocument document, Stream output)
        {
            var formatted = new XDocument(document);
            foreach (var element in formatted.Descendants())
            {
                if (!element.HasElements) continue;
                var space = element.AncestorsAndSelf().Select(node => (string)node.Attribute(XNamespace.Xml + "space"))
                    .FirstOrDefault(value => value != null);
                var text = element.Nodes().OfType<XText>().ToArray();
                if (space == "preserve" || text.Any(node => node is XCData || !String.IsNullOrWhiteSpace(node.Value)))
                    continue;
                foreach (var whitespace in text) whitespace.Remove();
            }
            foreach (var whitespace in formatted.Nodes().OfType<XText>().Where(node => String.IsNullOrWhiteSpace(node.Value)).ToArray())
                whitespace.Remove();

            var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ",
                NewLineChars = Environment.NewLine, NewLineHandling = NewLineHandling.Entitize };
            using (var writer = XmlWriter.Create(output, settings)) formatted.Save(writer);
        }

        internal XDocument ExportDocument(ushort id, string name, bool drafts = false)
        {
            if (imported == null)
            {
                var created = CreateDocument(id, name, ExportParts(drafts));
                var newKit = created.Root.Element("Kits").Element("Item");
                newKit.SetElementValue("kitType", kitType.SelectedValue as string ?? KitTypes[0]);
                newKit.Element("linkMods").Add(linkMods.Select(CreateLink));
                if (templateStats != null) newKit.Element("statMods").ReplaceWith(new XElement(templateStats));
                return created;
            }
            var copy = new XDocument(imported);
            var index = importedKit.ElementsBeforeSelf().Count();
            var kit = copy.Root.Element("Kits").Elements().ElementAt(index);
            kit.ReplaceWith(BuildEditedKit(id, name, drafts));
            return copy;
        }

        IEnumerable<Part> ExportParts(bool drafts) { return parts.Where(part => drafts || !String.IsNullOrWhiteSpace(part.Type)); }

        XElement BuildEditedKit(ushort id, string name, bool drafts = false)
        {
            if (importedKit == null)
            {
                var kitOnly = CreateDocument(id, name, ExportParts(drafts)).Root.Element("Kits").Element("Item");
                kitOnly.SetElementValue("kitType", kitType.SelectedValue as string ?? KitTypes[0]);
                kitOnly.Element("linkMods").Add(linkMods.Select(CreateLink));
                if (templateStats != null) kitOnly.Element("statMods").ReplaceWith(new XElement(templateStats));
                return kitOnly;
            }
            var kit = new XElement(importedKit);
            kit.SetElementValue("kitName", name);
            string chosenType = kitType.SelectedValue as string;
            if (chosenType != null && (string)kit.Element("kitType") != chosenType) kit.SetElementValue("kitType", chosenType);
            var idNode = kit.Element("id");
            if (idNode == null) kit.Add(Value("id", id));
            else idNode.SetAttributeValue("value", id);
            var visible = kit.Element("visibleMods");
            if (visible == null) kit.Add(new XElement("visibleMods", ExportParts(drafts).Select(CreateItem)));
            else
            {
                var originalItems = importedKit.Element("visibleMods").Elements("Item").ToArray();
                var copiedItems = visible.Elements("Item").ToArray();
                var retained = ExportParts(drafts).Where(p => p.Original != null).ToDictionary(p => p.Original);
                for (int i = 0; i < copiedItems.Length; i++)
                {
                    Part part;
                    if (retained.TryGetValue(originalItems[i], out part))
                        copiedItems[i].ReplaceWith(CreateItem(part));
                    else copiedItems[i].Remove();
                }
                visible.Add(ExportParts(drafts).Where(p => p.Original == null).Select(CreateItem));
            }
            var linkSection = kit.Element("linkMods");
            if (linkSection == null)
            {
                if (linkMods.Count > 0) kit.Add(new XElement("linkMods", linkMods.Select(CreateLink)));
            }
            else
            {
                var originals = importedKit.Element("linkMods").Elements("Item").ToArray();
                var copies = linkSection.Elements("Item").ToArray();
                var kept = linkMods.Where(link => link.Original != null).ToDictionary(link => link.Original);
                for (int i = 0; i < copies.Length; i++)
                {
                    LinkMod link;
                    if (kept.TryGetValue(originals[i], out link)) copies[i].ReplaceWith(CreateLink(link));
                    else copies[i].Remove();
                }
                linkSection.Add(linkMods.Where(link => link.Original == null).Select(CreateLink));
            }
            return kit;
        }

        static XElement CreateLink(LinkMod link)
        {
            if (link.Original == null)
                return new XElement("Item", new XElement("modelName", link.Model), new XElement("bone", link.Bone),
                    Value("turnOffExtra", "false"));
            var item = new XElement(link.Original);
            if ((string)item.Element("modelName") != link.Model) item.SetElementValue("modelName", link.Model);
            if ((string)item.Element("bone") != link.Bone) item.SetElementValue("bone", link.Bone);
            return item;
        }

        static XElement CreateItem(Part part)
        {
            if (part.Original == null) return NewItem(part);
            var item = new XElement(part.Original);
            foreach (var field in new[] {
                new KeyValuePair<string, string>("modelName", part.Model),
                new KeyValuePair<string, string>("modShopLabel", part.Label),
                new KeyValuePair<string, string>("type", part.Type),
                new KeyValuePair<string, string>("bone", part.Bone),
                new KeyValuePair<string, string>("collisionBone", part.CollisionBone) })
                if ((string)item.Element(field.Key) != field.Value) item.SetElementValue(field.Key, field.Value);
            foreach (var field in new[] {
                new KeyValuePair<string, string>("linkedModels", part.Linked),
                new KeyValuePair<string, string>("turnOffBones", part.TurnOff) })
            {
                var node = item.Element(field.Key);
                if (ReadNames(node) == field.Value) continue;
                if (node == null) item.Add(Entries(field.Key, field.Value));
                else node.ReplaceNodes(Names(field.Value).Select(value => new XElement("Item", value)));
            }
            return item;
        }

        static XElement NewItem(Part part)
        {
            return new XElement("Item",
                new XElement("modelName", part.Model), new XElement("modShopLabel", (part.Label ?? "").Trim()),
                Entries("linkedModels", part.Linked), Entries("turnOffBones", part.TurnOff),
                new XElement("type", part.Type), new XElement("bone", part.Bone),
                new XElement("collisionBone", part.CollisionBone), new XElement("cameraPos", "VMCP_DEFAULT"),
                Value("audioApply", "1.000000"), Value("weight", 20), Value("turnOffExtra", "false"),
                Value("disableBonnetCamera", "false"), Value("allowBonnetSlide", "true"));
        }

        public static XDocument CreateDocument(ushort id, string name, IEnumerable<Part> parts)
        {
            var items = parts.Select(CreateItem);
            var kit = new XElement("Item", new XElement("kitName", name), Value("id", id),
                new XElement("kitType", "MKT_SPECIAL"), new XElement("visibleMods", items),
                new XElement("linkMods"), new XElement("statMods"), new XElement("slotNames"), new XElement("liveryNames"));
            return new XDocument(new XDeclaration("1.0", "UTF-8", null),
                new XElement("CVehicleModelInfoVarGlobal", new XElement("Kits", kit)));
        }
    }
}
