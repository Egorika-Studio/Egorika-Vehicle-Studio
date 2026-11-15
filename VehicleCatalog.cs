using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using CodeWalker.GameFiles;

namespace SentinelViewer
{
    sealed class VehicleCatalog
    {
        readonly Dictionary<string, string> dictionaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public int Count { get { return dictionaries.Count; } }

        public void Read(Stream input)
        {
            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
            {
                var root = XDocument.Load(reader).Root;
                if (root == null) return;
                var init = root.Element("InitDatas");
                if (init != null)
                    foreach (var item in init.Elements("Item"))
                    {
                        string name = ((string)item.Element("modelName") ?? "").Trim();
                        string txd = ((string)item.Element("txdName") ?? "").Trim();
                        if (name.Length > 0) dictionaries[name] = txd.Length == 0 ? name : txd;
                    }
                var relationships = root.Element("txdRelationships");
                if (relationships != null)
                    foreach (var item in relationships.Elements("Item"))
                    {
                        string child = ((string)item.Element("child") ?? "").Trim();
                        string parent = ((string)item.Element("parent") ?? "").Trim();
                        if (child.Length > 0 && parent.Length > 0) parents[child] = parent;
                    }
            }
        }

        public bool Contains(string model) { return dictionaries.ContainsKey(model); }

        public IEnumerable<string> TextureDictionaries(string model)
        {
            string txd;
            if (!dictionaries.TryGetValue(model, out txd)) txd = model;
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (string current = txd; current != null && seen.Add(current);)
            {
                names.Add(current);
                string parent;
                current = parents.TryGetValue(current, out parent) ? parent : null;
            }
            names.Reverse();
            if (seen.Add(model)) names.Add(model);
            return names;
        }

        public static VehicleCatalog FromFolder(string folder, SearchOption search)
        {
            var result = new VehicleCatalog();
            foreach (var path in Directory.EnumerateFiles(folder, "*.meta", search).Where(file =>
                Path.GetFileName(file).Equals("vehicles.meta", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(file).Equals("gtxd.meta", StringComparison.OrdinalIgnoreCase)))
            {
                try { using (var stream = File.OpenRead(path)) result.Read(stream); }
                catch (XmlException) { }
            }
            return result;
        }

        public static bool HasVehicleSkeleton(YftFile file)
        {
            var drawable = file == null || file.Fragment == null ? null : file.Fragment.Drawable;
            var skeleton = drawable == null ? null : drawable.Skeleton;
            return skeleton != null && skeleton.Bones != null && skeleton.Bones.Items != null &&
                skeleton.Bones.Items.Any(bone => bone != null && (String.Equals(bone.Name, "chassis", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(bone.Name, "bodyshell", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
