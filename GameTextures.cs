using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeWalker.GameFiles;

namespace SentinelViewer
{
    sealed class GameTextures
    {
        readonly string thumbnailScope = Guid.NewGuid().ToString("N");
        readonly RpfManager archives = new RpfManager();
        readonly Dictionary<string, RpfResourceFileEntry> entries =
            new Dictionary<string, RpfResourceFileEntry>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, TextureDictionary> loaded =
            new Dictionary<string, TextureDictionary>(StringComparer.OrdinalIgnoreCase);

        readonly Dictionary<string, List<RpfResourceFileEntry>> versions =
            new Dictionary<string, List<RpfResourceFileEntry>>(StringComparer.OrdinalIgnoreCase);
        readonly List<RpfFileEntry> metadata = new List<RpfFileEntry>();
        readonly Dictionary<RpfFileEntry, string> metaCache = new Dictionary<RpfFileEntry, string>();
        readonly Dictionary<string, int> packOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        RpfResourceFileEntry selectedModel;
        public string OriginRpf { get; private set; }
        public string SelectedPath { get { return selectedModel == null ? null : selectedModel.Path; } }

        public void SelectVersion(string path)
        {
            selectedModel = String.IsNullOrEmpty(path) ? null : versions.Values.SelectMany(items => items)
                .FirstOrDefault(entry => String.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
        }
        readonly VehicleCatalog catalog = new VehicleCatalog();

        readonly Dictionary<uint, Texture> resolved = new Dictionary<uint, Texture>();

        public GameTextures(string folder)
        {
            InitializeLegacyArchives(folder);
            var all = archives.AllNoModRpfs.SelectMany(archive => archive.AllEntries.OfType<RpfFileEntry>()).ToArray();
            var list = all.FirstOrDefault(entry => entry.Path.Equals(@"update\update.rpf\common\data\dlclist.xml",
                StringComparison.OrdinalIgnoreCase));
            if (list != null)
            {
                var data = list.File.ExtractFile(list);
                if (data != null)
                {
                    int priority = 0;
                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                        System.Text.Encoding.UTF8.GetString(data), @"dlcpacks\s*:[/\\]+([^/\\<]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        packOrder[match.Groups[1].Value.Trim()] = ++priority;
                }
            }
            Index(all);
        }

        void InitializeLegacyArchives(string folder)
        {
            // dev46 predates the explicit Legacy/Enhanced argument added in dev48.
            var loadKeys = typeof(GTA5Keys).GetMethod("LoadFromPath", new[] { typeof(string), typeof(bool), typeof(string) });
            if (loadKeys != null) loadKeys.Invoke(null, new object[] { folder, false, null });
            else typeof(GTA5Keys).GetMethod("LoadFromPath", new[] { typeof(string), typeof(string) })
                .Invoke(null, new object[] { folder, null });

            Action<string> ignore = message => { };
            var init = typeof(RpfManager).GetMethod("Init", new[]
                { typeof(string), typeof(bool), typeof(Action<string>), typeof(Action<string>), typeof(bool), typeof(bool) });
            if (init != null) init.Invoke(archives, new object[] { folder, false, ignore, ignore, false, false });
            else typeof(RpfManager).GetMethod("Init", new[]
                { typeof(string), typeof(Action<string>), typeof(Action<string>), typeof(bool), typeof(bool) })
                .Invoke(archives, new object[] { folder, ignore, ignore, false, false });
        }

        GameTextures(RpfFile file)
        {
            file.ScanStructure(message => { }, error => { });
            if (file.AllEntries == null || file.AllEntries.Count == 0)
                throw new InvalidDataException(file.LastError ?? "RPF has no readable entries.");
            Index(Flatten(file).SelectMany(archive => archive.AllEntries.OfType<RpfFileEntry>()));
        }

        public static GameTextures OpenRpf(string path)
        {
            return new GameTextures(new RpfFile(path, Path.GetFileName(path))) { OriginRpf = path };
        }

        static IEnumerable<RpfFile> Flatten(RpfFile file)
        {
            yield return file;
            if (file.Children == null) yield break;
            foreach (var child in file.Children)
                foreach (var archive in Flatten(child)) yield return archive;
        }

        void Index(IEnumerable<RpfFileEntry> files)
        {
            var all = files.ToArray();
            foreach (var entry in all.Where(entry => entry.Name.Equals("vehicles.meta", StringComparison.OrdinalIgnoreCase) ||
                entry.Name.Equals("gtxd.meta", StringComparison.OrdinalIgnoreCase)).OrderBy(Priority))
            {
                try
                {
                    byte[] data = entry.File.ExtractFile(entry);
                    if (data != null)
                        using (var stream = new MemoryStream(data)) catalog.Read(stream);
                }
                catch (System.Xml.XmlException) { }
                catch (IOException) { }
            }

            foreach (var entry in all)
            {
                if (entry.Name.Equals("carcols.meta", StringComparison.OrdinalIgnoreCase) ||
                    entry.Name.Equals("carcols.ymt", StringComparison.OrdinalIgnoreCase) ||
                    entry.Name.Equals("carvariations.meta", StringComparison.OrdinalIgnoreCase) ||
                    entry.Name.Equals("carvariations.ymt", StringComparison.OrdinalIgnoreCase))
                    metadata.Add(entry);
                var resource = entry as RpfResourceFileEntry;
                if (resource == null || (!entry.Name.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase) &&
                    !entry.Name.EndsWith(".yft", StringComparison.OrdinalIgnoreCase))) continue;
                List<RpfResourceFileEntry> entriesForName;
                if (!versions.TryGetValue(entry.Name, out entriesForName))
                    versions.Add(entry.Name, entriesForName = new List<RpfResourceFileEntry>());
                entriesForName.Add(resource);
            }
            foreach (var pair in versions)
            {
                pair.Value.Sort((a, b) => Priority(b).CompareTo(Priority(a)));
                entries[pair.Key] = pair.Value[0];
            }
        }

        static string Pack(RpfFileEntry entry)
        {
            var match = System.Text.RegularExpressions.Regex.Match(entry.Path, @"(?:^|\\)(?:dlcpacks|dlc_patch)\\([^\\]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : "";
        }

        int Priority(RpfFileEntry entry)
        {
            int order;
            if (entry.Path.IndexOf(@"\dlc_patch\", StringComparison.OrdinalIgnoreCase) >= 0) return Int32.MaxValue;
            if (packOrder.TryGetValue(Pack(entry), out order)) return order + 2;
            return entry.Path.StartsWith(@"update\", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        RpfResourceFileEntry Preferred(string name, RpfResourceFileEntry context = null)
        {
            List<RpfResourceFileEntry> options;
            if (!versions.TryGetValue(name, out options)) return null;
            if (context != null)
            {
                var sameArchive = options.FirstOrDefault(entry => entry.File == context.File);
                if (sameArchive != null) return sameArchive;
                string pack = Pack(context);
                if (!String.IsNullOrEmpty(pack))
                {
                    var samePack = options.FirstOrDefault(entry => Pack(entry).Equals(pack, StringComparison.OrdinalIgnoreCase));
                    if (samePack != null) return samePack;
                }
            }
            return options[0];
        }

        public sealed class ModelVersion
        {
            internal RpfResourceFileEntry Entry;
            internal GameTextures Source;
            internal string ThumbnailKey { get { return Source.thumbnailScope + Entry.Path; } }
            internal List<Vehicle.Piece> ReadThumbnail()
            {
                var file = RpfFile.GetFile<YftFile>(Entry);
                return Vehicle.ThumbnailPieces(file, Source, Source.ThumbnailDictionaries(Name, Entry));
            }
            public string Name { get { return Entry.GetShortName(); } }
            public override string ToString() { return Name + "  —  " + Entry.Path; }
        }

        public IEnumerable<ModelVersion> Models()
        {
            return versions.OrderBy(pair => pair.Key).Where(pair => pair.Key.EndsWith(".yft", StringComparison.OrdinalIgnoreCase) &&
                !pair.Key.EndsWith("_hi.yft", StringComparison.OrdinalIgnoreCase))
                .SelectMany(pair => pair.Value).Where(entry => entry.Path.IndexOf("vehiclemods", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(IsVehicle)
                .Select(entry => new ModelVersion { Entry = entry, Source = this });
        }

        bool IsVehicle(RpfResourceFileEntry entry)
        {
            if (catalog.Contains(entry.GetShortName())) return true;
            if (catalog.Count > 0) return false;
            try { return VehicleCatalog.HasVehicleSkeleton(RpfFile.GetFile<YftFile>(entry)); }
            catch (Exception) { return false; }
        }

        public string ExportPreview(ModelVersion version, string destination)
        {
            var entry = version.Entry;
            selectedModel = entry;
            Directory.CreateDirectory(destination);
            WriteResource(entry, destination);
            var dictionaries = catalog.TextureDictionaries(version.Name)
                .SelectMany(name => new[] { name + ".ytd", name + "+hi.ytd" });
            foreach (var name in new[] { version.Name + "_hi.yft" }.Concat(dictionaries))
            {
                var companion = Preferred(name, entry);
                if (companion != null) WriteResource(companion, destination);
            }
            SaveMetadata(destination, "carcols", version.Name, entry);
            SaveMetadata(destination, "carvariations", version.Name, entry);
            return Path.Combine(destination, entry.Name);
        }

        static void WriteResource(RpfResourceFileEntry entry, string folder)
        {
            var data = entry.File.ExtractFile(entry);
            if (data == null) throw new IOException("Unable to read " + entry.Path);
            File.WriteAllBytes(Path.Combine(folder, Path.GetFileName(entry.Name)),
                ResourceBuilder.AddResourceHeader(entry, CompressForPreview(data)));
        }

        static byte[] CompressForPreview(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var compressor = new System.IO.Compression.DeflateStream(output,
                    System.IO.Compression.CompressionLevel.Fastest, true))
                    compressor.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        void SaveMetadata(string folder, string kind, string model, RpfResourceFileEntry entry)
        {
            var packs = new HashSet<string>(versions[entry.Name].Select(Pack), StringComparer.OrdinalIgnoreCase);
            bool originalVersion = versions[entry.Name][0] != entry;
            var candidates = metadata.Where(item => item.Name.StartsWith(kind + ".", StringComparison.OrdinalIgnoreCase))
                .Where(item => packs.Contains(Pack(item)) || Pack(item).Length == 0 || selectedModel.File == item.File)
                .OrderByDescending(item => originalVersion && Pack(item) == Pack(entry) &&
                    item.Path.IndexOf(@"\dlc_patch\", StringComparison.OrdinalIgnoreCase) < 0)
                .ThenByDescending(Priority);
            foreach (var candidate in candidates)
            {
                string xml;
                if (!metaCache.TryGetValue(candidate, out xml))
                {
                    var data = candidate.File.ExtractFile(candidate);
                    if (data != null && candidate.Name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        xml = System.Text.Encoding.UTF8.GetString(data).TrimStart('\uFEFF', '\0');
                    else if (data != null && kind == "carcols")
                    {
                        var file = new CarColsFile();
                        file.Load(data, candidate);
                        xml = file.Xml;
                    }
                    else if (data != null)
                    {
                        var file = new CarVariationsFile();
                        file.Load(data, candidate);
                        xml = file.Xml;
                    }
                    metaCache[candidate] = xml;
                }
                if (String.IsNullOrEmpty(xml)) continue;
                var pattern = kind == "carcols" ? @"<kitName>[^<]*_" +
                    System.Text.RegularExpressions.Regex.Escape(model) + @"_modkit\s*</kitName>" :
                    @"<modelName>\s*" + System.Text.RegularExpressions.Regex.Escape(model) + @"\s*</modelName>";
                if (!System.Text.RegularExpressions.Regex.IsMatch(xml, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                File.WriteAllText(Path.Combine(folder, kind + ".meta"), xml);
                return;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        TextureDictionary Read(string name)
        {
            TextureDictionary dictionary;
            if (loaded.TryGetValue(name, out dictionary)) return dictionary;
            RpfResourceFileEntry entry;
            if (!entries.TryGetValue(name, out entry)) return null;
            var file = RpfFile.GetFile<YtdFile>(entry);
            dictionary = file == null ? null : file.TextureDict;
            loaded.Add(name, dictionary);
            return dictionary;
        }

        IEnumerable<TextureDictionary> ThumbnailDictionaries(string name, RpfResourceFileEntry entry)
        {
            foreach (var shared in entries.Keys.Where(key => key.StartsWith("vehshare", StringComparison.OrdinalIgnoreCase)
                && key.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase)))
                yield return Read(shared);
            foreach (var dictionary in catalog.TextureDictionaries(name))
                foreach (var suffix in new[] { ".ytd", "+hi.ytd" })
                {
                    var version = Preferred(dictionary + suffix, entry);
                    if (version != null)
                    {
                        var file = RpfFile.GetFile<YtdFile>(version);
                        if (file != null) yield return file.TextureDict;
                    }
                }
        }

        public IEnumerable<TextureDictionary> ForVehicle(string name)
        {
            foreach (string shared in entries.Keys.Where(key => key.StartsWith("vehshare", StringComparison.OrdinalIgnoreCase) && key.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase)))
                yield return Read(shared);
            foreach (var dictionary in catalog.TextureDictionaries(name))
            {
                yield return Read(dictionary + ".ytd");
                yield return Read(dictionary + "+hi.ytd");
            }
        }

        public YftFile ReadModel(string name)
        {
            RpfResourceFileEntry entry;
            entry = Preferred(name + ".yft", selectedModel);
            return entry == null ? null : RpfFile.GetFile<YftFile>(entry);
        }

        public Texture ReadLivery(string name) { return Vehicle.LiveryTexture(ReadModel(name)); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public Texture Find(string name, uint hash)
        {
            Texture texture;
            if (!resolved.TryGetValue(hash, out texture))
            {
                texture = Resolve(name, hash);
                resolved.Add(hash, texture);
            }
            return texture;
        }

        Texture Resolve(string name, uint hash)
        {
            if (String.IsNullOrEmpty(name) || name.StartsWith("script_rt_", StringComparison.OrdinalIgnoreCase))
                return null;
            int firstSeparator = name.IndexOf('_');
            string prefix = firstSeparator < 0 ? name : name.Substring(0, firstSeparator);
            var sharedInterior = entries.Keys.Where(key => key.StartsWith("vehicles_" + prefix + "_", StringComparison.OrdinalIgnoreCase) && key.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase))
                .OrderBy(key => key.Equals("vehicles_" + prefix + "_interior.ytd", StringComparison.OrdinalIgnoreCase) ? 0 : 1);
            foreach (string candidate in sharedInterior)
            {
                var dictionary = Read(candidate);
                Texture texture;
                if (dictionary != null && dictionary.Dict != null && dictionary.Dict.TryGetValue(hash, out texture))
                    return texture;
            }
            string stem = name;
            do
            {
                foreach (var suffix in new[] { ".ytd", "+hi.ytd" })
                {
                    var dictionary = Read(stem + suffix);
                    Texture texture;
                    if (dictionary != null && dictionary.Dict != null && dictionary.Dict.TryGetValue(hash, out texture))
                        return texture;
                }
                int separator = stem.LastIndexOf('_');
                if (separator < 0) break;
                stem = stem.Substring(0, separator);
            } while (stem.Length > 0);
            return null;
        }
    }
}
