using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using CodeWalker.GameFiles;
using CodeWalker.Utils;
using DX = SharpDX;

namespace SentinelViewer
{
    sealed class Vehicle
    {
        public sealed class Piece
        {
            public int Bone;
            public bool Glass;
            public bool Alpha;
            public int PaintLayer;
            public MeshGeometry3D LiveryMesh;
            public byte[] Ao;
            public GeometryModel3D AoModel;
            public bool AoPrepared;
            public bool TextureHasAlpha;
            public GeometryModel3D Model;
        }

        public readonly Dictionary<int, Bone> Bones = new Dictionary<int, Bone>();
        public readonly Dictionary<string, List<Piece>> Levels = new Dictionary<string, List<Piece>>();
        public readonly List<string> TextureErrors = new List<string>();
        public int AppliedTextures;
        double reflections = 0.45;
        Material liveryMaterial;
        Material tuningLiveryMaterial;
        Texture tuningLiveryTexture;
        FragType baseFragment;
        FragType highFragment;
        readonly HashSet<ImageSource> transparentImages = new HashSet<ImageSource>();
        public readonly HashSet<string> MissingTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<Material, Material> tintedMaterials = new Dictionary<Material, Material>();
        readonly Dictionary<int, Color> paint = new Dictionary<int, Color>();
        public double Radius = 3;
        public Point3D Center;
        readonly GameTextures gameTextures;
        readonly GameTextures sourceTextures;
        readonly Dictionary<uint, Texture> textures = new Dictionary<uint, Texture>();
        readonly Dictionary<Texture, ImageSource> images = new Dictionary<Texture, ImageSource>();
        readonly Dictionary<Texture, ImageSource> litImages = new Dictionary<Texture, ImageSource>();

        public Vehicle(string normal, string high, IEnumerable<string> dictionaries, GameTextures gameTextures = null, GameTextures sourceTextures = null)
        {
            this.gameTextures = gameTextures;
            this.sourceTextures = sourceTextures == gameTextures ? null : sourceTextures;
            if (gameTextures != null)
                foreach (var dict in gameTextures.ForVehicle(Path.GetFileNameWithoutExtension(normal)))
                    AddTextures(dict);
            if (this.sourceTextures != null)
                foreach (var dict in this.sourceTextures.ForVehicle(Path.GetFileNameWithoutExtension(normal)))
                    AddTextures(dict);
            foreach (string path in dictionaries)
            {
                try
                {
                    var file = new YtdFile();
                    file.Load(File.ReadAllBytes(path));
                    AddTextures(file.TextureDict);
                }
                catch (Exception ex) { TextureErrors.Add(Path.GetFileName(path) + ": " + ex.Message); }
            }

            var baseModel = Read(normal);
            baseFragment = baseModel;
            Radius = Math.Max(1, baseModel.Drawable.BoundingSphereRadius);
            Center = Point(baseModel.Drawable.BoundingCenter);
            if (!String.IsNullOrWhiteSpace(high))
            {
                highFragment = Read(high);
                AddLevels(highFragment, true);
            }
            AddLevels(baseModel, false);
            if (Levels.Count == 0)
                throw new InvalidDataException(Ui.Text("В YFT нет поддерживаемой геометрии.", "The YFT has no supported geometry."));
        }

        Vehicle(GameTextures archives) { gameTextures = archives; }

        internal static List<Piece> ThumbnailPieces(YftFile file, GameTextures archives, IEnumerable<TextureDictionary> dictionaries)
        {
            var root = file == null || file.Fragment == null ? null : file.Fragment.Drawable;
            if (root == null || root.DrawableModels == null) return new List<Piece>();
            var builder = new Vehicle(archives);
            foreach (var dictionary in dictionaries) builder.AddTextures(dictionary);
            var embedded = root.ShaderGroup == null ? null : root.ShaderGroup.TextureDictionary;
            builder.AddTextures(embedded);
            var lods = root.DrawableModels;
            var levels = new[] { lods.High, lods.Med, lods.Low, lods.VLow };
            int level = Array.FindIndex(levels, models => models != null && models.Length > 0);
            if (levels[1] != null && levels[1].Length > 0) level = 1;
            if (level < 0) return new List<Piece>();
            builder.AddLevels(file.Fragment, false, level);
            return builder.Levels.Values.FirstOrDefault() ?? new List<Piece>();
        }

        void AddTextures(TextureDictionary dictionary)
        {
            if (dictionary == null || dictionary.Dict == null) return;
            foreach (var item in dictionary.Dict) textures[item.Key] = item.Value;
        }

        static FragType Read(string path)
        {
            var file = new YftFile();
            file.Load(File.ReadAllBytes(path));
            if (file.Fragment == null || file.Fragment.Drawable == null)
                throw new InvalidDataException(Ui.Text("Не удалось прочитать геометрию: ", "Could not read geometry: ") + Path.GetFileName(path));
            return file.Fragment;
        }

        void AddLevels(FragType fragment, bool highFile, int? onlyLevel = null)
        {
            var root = fragment.Drawable;
            var skeleton = root.Skeleton;
            var bones = skeleton == null || skeleton.Bones == null
                ? new Bone[0] : skeleton.Bones.Items;
            foreach (var bone in bones)
                Bones[bone.Tag] = bone;

            var physics = fragment.PhysicsLODGroup == null ? null : fragment.PhysicsLODGroup.PhysicsLOD1;
            var children = physics == null || physics.Children == null
                ? new FragPhysTypeChild[0] : physics.Children.data_items;
            var wheelSource = children.FirstOrDefault(c => c != null && c.BoneTag == 27922 && c.Drawable1 != null);
            var wheelBounds = physics == null ? null : physics.Bound as BoundComposite;
            var sourceWheelSize = WheelDiscSize(wheelBounds, wheelSource);
            for (int level = onlyLevel ?? 0; level < (onlyLevel.HasValue ? onlyLevel.Value + 1 : 4); level++)
            {
                var pieces = new List<Piece>();
                AddDrawable(root, level, bones, -1, DX.Matrix.Identity, pieces);
                if (fragment.DrawableArray != null)
                    foreach (var drawable in fragment.DrawableArray.data_items)
                        AddDrawable(drawable, level, bones, -1, DX.Matrix.Identity, pieces);

                foreach (var child in children)
                {
                    if (child == null) continue;
                    var bone = bones.FirstOrDefault(b => b.Tag == child.BoneTag);
                    bool wheel = bone != null && bone.Name.StartsWith("wheel_", StringComparison.Ordinal);
                    var drawable = child.Drawable1;
                    bool reusedWheel = wheel && (drawable == null || drawable.AllModels == null || drawable.AllModels.Length == 0)
                        && wheelSource != null;
                    if (reusedWheel)
                        drawable = wheelSource.Drawable1;
                    if (drawable == null) continue;

                    var transform = DX.Matrix.Identity;
                    var matrices = physics.FragTransforms == null ? null : physics.FragTransforms.Matrices;
                    if (matrices != null && child.OwnerFragPhysIndex >= 0 && child.OwnerFragPhysIndex < matrices.Length)
                    {
                        transform = matrices[child.OwnerFragPhysIndex];
                        transform.Column4 = DX.Vector4.UnitW;
                        transform.Row4 += new DX.Vector4(physics.PositionOffset, 0);
                    }
                    else if (bone != null)
                        transform = BoneMatrix(skeleton, bone.Index);
                    if (wheel && bone.Name.StartsWith("wheel_r", StringComparison.Ordinal))
                    {
                        var turn = DX.Matrix.RotationY((float)Math.PI);
                        turn.TranslationVector = transform.TranslationVector;
                        transform = turn;
                    }
                    bool scaledWheel = false;
                    if (reusedWheel && sourceWheelSize.HasValue)
                    {
                        var wheelSize = WheelDiscSize(wheelBounds, child);
                        if (wheelSize.HasValue)
                        {
                            var from = sourceWheelSize.Value;
                            var to = wheelSize.Value;
                            // Scale the cloned mesh around its local axle before placing it at the destination bone.
                            transform = DX.Matrix.Scaling(to.X / from.X, to.Y / from.Y, to.Z / from.Z) * transform;
                            scaledWheel = from != to;
                        }
                    }
                    AddDrawable(drawable, level, bones, child.BoneTag, transform, pieces, correctScaledNormals: scaledWheel);
                }
                if (pieces.Count == 0) continue;
                string name = new[] { "High", "Medium", "Low", "Very Low" }[level];
                if (highFile) name = level == 0 ? "_hi.yft" : "_hi.yft / " + name;
                Levels.Add(name, pieces);
            }
        }

        static DX.Vector3? WheelDiscSize(BoundComposite composite, FragPhysTypeChild wheel)
        {
            var bounds = composite == null || composite.Children == null ? null : composite.Children.data_items;
            if (wheel == null || bounds == null || wheel.OwnerFragPhysIndex < 0 || wheel.OwnerFragPhysIndex >= bounds.Length)
                return null;
            var bound = bounds[wheel.OwnerFragPhysIndex] as BoundDisc;
            if (bound == null) return null;
            var size = bound.BoxMax - bound.BoxMin;
            // GTA vehicle wheel discs in this layout have their axle on X and equal radial extents.
            if (!(size.X > 0.001f && size.Y > 0.001f && size.Z > 0.001f &&
                Math.Abs(size.Y - size.Z) <= 0.02f * Math.Max(size.Y, size.Z))) return null;
            var axis = DX.Vector3.TransformNormal(DX.Vector3.UnitX, bound.Transform);
            if (!(Math.Abs(axis.X) > 0.99f * axis.Length())) return null;
            var scale = bound.Scale;
            if (!(scale.X > 0.001f && scale.Y > 0.001f && scale.Z > 0.001f)) return null;
            return new DX.Vector3(size.X * scale.X, size.Y * scale.Y, size.Z * scale.Z);
        }

        public Dictionary<string, List<Piece>> BuildTuning(YftFile model, string anchor)
        {
            if (model == null || model.Fragment == null || model.Fragment.Drawable == null)
                throw new InvalidDataException(Ui.Text("У детали нет геометрии.", "The part has no geometry."));
            var bone = Bones.Values.FirstOrDefault(b => String.Equals(b.Name, anchor, StringComparison.OrdinalIgnoreCase));
            if (bone == null) throw new InvalidDataException(Ui.Text("Кость крепления не найдена: ", "Mount bone not found: ") + anchor);
            var root = model.Fragment.Drawable;
            var result = new Dictionary<string, List<Piece>>();
            foreach (string name in Levels.Keys)
            {
                var host = name.StartsWith("_hi.yft", StringComparison.Ordinal) ? highFragment ?? baseFragment : baseFragment;
                var hostSkeleton = host.Drawable.Skeleton;
                var anchorBone = hostSkeleton == null || hostSkeleton.Bones == null ? null :
                    hostSkeleton.Bones.Items.FirstOrDefault(b => b.Tag == bone.Tag);
                if (anchorBone == null)
                {
                    host = baseFragment;
                    hostSkeleton = host.Drawable.Skeleton;
                    anchorBone = hostSkeleton == null || hostSkeleton.Bones == null ? null :
                        hostSkeleton.Bones.Items.FirstOrDefault(b => b.Tag == bone.Tag);
                }
                var transform = anchorBone == null ? DX.Matrix.Identity : BoneMatrix(hostSkeleton, anchorBone.Index);
                int level;
                switch (name)
                {
                    case "_hi.yft": level = 0; break;
                    case "_hi.yft / Medium":
                    case "High": level = 1; break;
                    case "_hi.yft / Low":
                    case "Medium": level = 2; break;
                    default: result.Add(name, new List<Piece>()); continue;
                }
                var pieces = new List<Piece>();
                AddDrawable(root, level, new Bone[0], bone.Tag, transform, pieces, model.Fragment);
                if (model.Fragment.DrawableArray != null)
                    foreach (var drawable in model.Fragment.DrawableArray.data_items)
                        AddDrawable(drawable, level, new Bone[0], bone.Tag, transform, pieces, model.Fragment);
                result.Add(name, pieces);
            }
            return result;
        }

        static DX.Matrix FragmentPose(FragType fragment, Skeleton skeleton, int index)
        {
            var pose = fragment.BoneTransforms == null ? null : fragment.BoneTransforms.Items;
            if (pose == null || index < 0 || index >= pose.Length) return BoneMatrix(skeleton, index);
            var bone = pose[index];
            var x = bone.Row1;
            var y = bone.Row2;
            var z = bone.Row3;
            return new DX.Matrix(x.X, y.X, z.X, 0, x.Y, y.Y, z.Y, 0,
                x.Z, y.Z, z.Z, 0, x.W, y.W, z.W, 1);
        }

        static DX.Matrix BoneMatrix(Skeleton skeleton, int index)
        {
            var result = DX.Matrix.Identity;
            var matrices = skeleton == null ? null : skeleton.Transformations;
            for (int depth = 0; matrices != null && index >= 0 && index < matrices.Length && depth < matrices.Length; depth++)
            {
                var transform = matrices[index];
                transform.Column4 = DX.Vector4.UnitW;
                result *= transform;
                index = skeleton.ParentIndices == null || index >= skeleton.ParentIndices.Length
                    ? -1 : skeleton.ParentIndices[index];
            }
            return result;
        }

        static Point3D Point(DX.Vector3 p) { return new Point3D(p.X, p.Y, p.Z); }

        void AddDrawable(FragDrawable drawable, int level, Bone[] rootBones, int forcedBone,
            DX.Matrix fragmentTransform, List<Piece> pieces, FragType attachment = null, bool correctScaledNormals = false)
        {
            if (drawable == null || drawable.DrawableModels == null) return;
            var lod = drawable.DrawableModels;
            var models = new[] { lod.High, lod.Med, lod.Low, lod.VLow }[level];
            if (models == null) return;
            var skeleton = drawable.Skeleton;
            var bones = skeleton == null || skeleton.Bones == null ? rootBones : skeleton.Bones.Items;
            foreach (var model in models)
            {
                int boneIndex = model.BoneIndex;
                int owner = forcedBone >= 0 ? forcedBone : boneIndex < bones.Length ? bones[boneIndex].Tag : -1;
                bool skinned = model.HasSkin != 0 && forcedBone < 0;
                var transform = forcedBone >= 0 ? fragmentTransform
                    : skinned ? DX.Matrix.Identity : BoneMatrix(skeleton, boneIndex);
                if (attachment != null && model.HasSkin == 0)
                    transform = (skeleton == null ? FragmentPose(attachment, null, boneIndex) : BoneMatrix(skeleton, boneIndex)) * transform;
                var normalTransform = correctScaledNormals ? DX.Matrix.Transpose(DX.Matrix.Invert(transform)) : transform;
                foreach (var geo in model.Geometries)
                    AddGeometry(geo, bones, owner, skinned, transform, normalTransform, pieces);
            }
        }

        void AddGeometry(DrawableGeometry geo, Bone[] bones, int owner, bool skinned,
            DX.Matrix transform, DX.Matrix normalTransform, List<Piece> pieces)
        {
            var data = geo.VertexData;
            var indices = geo.IndexBuffer == null ? null : geo.IndexBuffer.Indices;
            if (data == null || data.Info == null || indices == null) return;
            int count = data.VertexCount;
            var positions = new Point3D[count];
            var normals = new Vector3D[count];
            var uv = new Point[count];
            var bindings = new int[count];
            bool weights = skinned && (data.Info.Flags & 6) == 6;
            bool hasNormals = (data.Info.Flags & 8) != 0;
            bool hasUv = (data.Info.Flags & 64) != 0;
            int colorIndex = (int)VertexSemantics.Colour0;
            bool hasAo = (data.Info.Flags & (1 << colorIndex)) != 0;
            int weightOffset = data.Info.GetComponentOffset(1);
            int indexOffset = data.Info.GetComponentOffset(2);

            bool glass = IsWindowGlass(geo.Shader);
            bool alpha = glass || (geo.Shader != null && geo.Shader.RenderBucket != 0);
            int paintLayer = PaintLayer(geo.Shader);
            var diffuse = DiffuseTexture(geo.Shader);
            float tiling = DiffuseTiling(geo.Shader, diffuse);
            // A paint4 surface can take a livery even when its base is a fixed texture such as carbon.
            bool supportsLivery = paintLayer == 1 || paintLayer == 2 ||
                (geo.Shader != null && (geo.Shader.Name.Hash == JenkHash.GenHash("vehicle_paint3") ||
                    geo.Shader.Name.Hash == JenkHash.GenHash("vehicle_paint4")));
            var overlayUv = (data.Info.Flags & 128) != 0 && supportsLivery &&
                (ShaderParameter(geo.Shader, "diffusesampler2") is TextureBase ||
                 ShaderParameter(geo.Shader, "diffuse2sampler") is TextureBase ||
                 ShaderParameter(geo.Shader, "diffusesampler3") is TextureBase)
                ? new Point[count] : null;

            for (int i = 0; i < count; i++)
            {
                positions[i] = Point(DX.Vector3.TransformCoordinate(data.GetVector3(i, 0), transform));
                if (hasNormals)
                {
                    var n = DX.Vector3.TransformNormal(data.GetVector3(i, 3), normalTransform);
                    normals[i] = new Vector3D(n.X, n.Y, n.Z);
                }
                if (hasUv)
                {
                    var t = data.GetVector2(i, 6);
                    uv[i] = new Point(t.X * tiling, t.Y * tiling);
                }
                if (overlayUv != null)
                {
                    var t = data.GetVector2(i, 7);
                    overlayUv[i] = new Point(t.X, t.Y);
                }
                bindings[i] = owner;
                if (!weights) continue;
                int start = i * data.VertexStride;
                int best = 0;
                for (int j = 1; j < 4; j++)
                    if (data.VertexBytes[start + weightOffset + j] > data.VertexBytes[start + weightOffset + best])
                        best = j;
                int id = data.VertexBytes[start + indexOffset + best];
                if (geo.BoneIds != null && geo.BoneIds.Length != bones.Length && id < geo.BoneIds.Length)
                    id = geo.BoneIds[id];
                if (id < bones.Length) bindings[i] = bones[id].Tag;
            }

            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (a >= count || b >= count || c >= count) continue;
                int tag = bindings[b] == bindings[c] ? bindings[b] : bindings[a];
                List<int> group;
                if (!groups.TryGetValue(tag, out group)) groups[tag] = group = new List<int>();
                group.Add(a); group.Add(b); group.Add(c);
            }
            // Some paint shaders use an empty diffuse placeholder and put the livery in another sampler.
            // The placeholder's alpha must not make the painted panel disappear.
            bool paintPlaceholder = supportsLivery && diffuse != null &&
                String.Equals(diffuse.Name, "blank", StringComparison.OrdinalIgnoreCase);
            var material = paintPlaceholder
                ? new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(185, 185, 185)))
                : MaterialFor(geo.Shader, hasUv, glass, diffuse);
            if (paintPlaceholder && !glass) alpha = false;
            var diffuseMaterial = material as DiffuseMaterial;
            var imageBrush = diffuseMaterial == null ? null : diffuseMaterial.Brush as ImageBrush;
            bool textureHasAlpha = imageBrush != null && transparentImages.Contains(imageBrush.ImageSource);
            foreach (var entry in groups)
            {
                var mesh = new MeshGeometry3D();
                var remap = new Dictionary<int, int>();
                var ao = hasAo ? new List<byte>() : null;
                var decal = overlayUv == null ? null : new MeshGeometry3D();
                foreach (int old in entry.Value)
                {
                    int current;
                    if (!remap.TryGetValue(old, out current))
                    {
                        remap[old] = current = mesh.Positions.Count;
                        mesh.Positions.Add(positions[old]);
                        if (hasAo) ao.Add(data.GetUByte4(old, colorIndex).R);
                        if (hasNormals) mesh.Normals.Add(normals[old]);
                        if (hasUv) mesh.TextureCoordinates.Add(uv[old]);
                        if (decal != null)
                        {
                            decal.Positions.Add(positions[old] + normals[old] * 0.0008);
                            if (hasNormals) decal.Normals.Add(normals[old]);
                            decal.TextureCoordinates.Add(overlayUv[old]);
                        }
                    }
                    mesh.TriangleIndices.Add(current);
                    if (decal != null) decal.TriangleIndices.Add(current);
                }
                var model = new GeometryModel3D(mesh, material);
                model.Freeze();
                if (decal != null) decal.Freeze();
                pieces.Add(new Piece { Bone = entry.Key, Model = model, Glass = glass, Alpha = alpha,
                    PaintLayer = paintLayer, LiveryMesh = decal, TextureHasAlpha = textureHasAlpha, Ao = ao == null ? null : ao.ToArray() });
            }
        }

        static bool IsWindowGlass(ShaderFX shader)
        {
            if (shader == null) return false;
            if (shader.FileName.Hash == JenkHash.GenHash("vehicle_glass.sps")) return true;
            var parameters = shader.ParametersList == null ? null : shader.ParametersList.Parameters;
            return parameters != null && parameters.Select(p => p.Data).OfType<TextureBase>()
                .Any(texture => texture.Name != null &&
                    texture.Name.StartsWith("vehicle_generic_glasswindows", StringComparison.OrdinalIgnoreCase));
        }

        static object ShaderParameter(ShaderFX shader, string parameter)
        {
            var parameters = shader == null ? null : shader.ParametersList;
            if (parameters == null || parameters.Hashes == null || parameters.Parameters == null) return null;
            uint hash = JenkHash.GenHash(parameter);
            for (int i = 0; i < Math.Min(parameters.Hashes.Length, parameters.Parameters.Length); i++)
                if ((uint)parameters.Hashes[i] == hash) return parameters.Parameters[i].Data;
            return null;
        }

        static TextureBase DiffuseTexture(ShaderFX shader)
        {
            return ShaderParameter(shader, "diffusesampler") as TextureBase;
        }

        static int PaintLayer(ShaderFX shader)
        {
            var parameter = ShaderParameter(shader, "matdiffusecolor");
            if (!(parameter is DX.Vector4)) return 0;
            var value = (DX.Vector4)parameter;
            if (value.X != 2 || value.Y != value.Z || value.W != 0) return 0;
            int layer = (int)value.Y;
            return value.Y == layer && (layer == 1 || layer == 2 || layer == 3 || layer == 4 || layer == 6 || layer == 7) ? layer : 0;
        }

        static float DiffuseTiling(ShaderFX shader, TextureBase texture)
        {
            if (texture == null || texture.Name == null ||
                texture.Name.IndexOf("carbon", StringComparison.OrdinalIgnoreCase) < 0) return 1;
            var parameter = ShaderParameter(shader, "diffusetextileuv");
            if (!(parameter is DX.Vector4)) return 1;
            float repeat = ((DX.Vector4)parameter).X;
            return repeat > 0 && repeat <= 128 ? repeat : 1;
        }

        Material MaterialFor(ShaderFX shader, bool hasUv, bool glass, TextureBase reference)
        {
            bool light = reference != null && reference.Name != null &&
                (reference.Name.EndsWith("_lights", StringComparison.OrdinalIgnoreCase) ||
                 (shader != null && shader.Name.Hash == JenkHash.GenHash("vehicle_lightsemissive")));
            if (hasUv && reference != null)
            {
                var texture = reference as Texture;
                if (texture == null) textures.TryGetValue(reference.NameHash, out texture);
                if (texture == null && sourceTextures != null)
                    texture = sourceTextures.Find(reference.Name, reference.NameHash);
                if (texture == null && gameTextures != null)
                    texture = gameTextures.Find(reference.Name, reference.NameHash);
                if (texture == null) MissingTextures.Add(reference.Name ?? reference.NameHash.ToString("X8"));
                else
                {
                    ImageSource image;
                    var cache = light ? litImages : images;
                    if (!cache.TryGetValue(texture, out image))
                    {
                        try
                        {
                            byte[] pixels = DDSIO.GetPixels(texture, 0);
                            if (light)
                                for (int i = 0; i < pixels.Length; i += 4)
                                    for (int c = 0; c < 3; c++)
                                        pixels[i + c] = (byte)Math.Round(255 * Math.Pow(pixels[i + c] / 255.0, 0.56));
                            var bitmap = TextureBitmap(texture, pixels);
                            bitmap.Freeze();
                            image = bitmap;
                            for (int i = 3; i < pixels.Length; i += 4)
                                if (pixels[i] != 255)
                                {
                                    transparentImages.Add(image);
                                    break;
                                }
                        }
                        catch (Exception ex) { TextureErrors.Add(texture.Name + ": " + ex.Message); }
                        cache[texture] = image;
                    }
                    if (image != null)
                    {
                        AppliedTextures++;
                        var brush = new ImageBrush(image)
                        {
                            Opacity = glass ? 0.24 : 1.0,
                            TileMode = TileMode.Tile,
                            ViewportUnits = BrushMappingMode.Absolute,
                            Viewport = new Rect(0, 0, 1, 1)
                        };
                        var diffuse = new DiffuseMaterial(brush);
                        if (!light || glass) return diffuse;
                        var glow = brush.Clone();
                        glow.Opacity = 0.45;
                        var result = new MaterialGroup();
                        result.Children.Add(diffuse);
                        result.Children.Add(new EmissiveMaterial(glow));
                        return result;
                    }
                }
            }
            return new DiffuseMaterial(new SolidColorBrush(glass ? Color.FromArgb(52, 120, 153, 174) : Color.FromRgb(135, 151, 170)));
        }

        // These properties moved from Texture to TextureBase between CodeWalker builds.
        static readonly System.Reflection.PropertyInfo textureWidth = typeof(Texture).GetProperty("Width");
        static readonly System.Reflection.PropertyInfo textureHeight = typeof(Texture).GetProperty("Height");

        static BitmapSource TextureBitmap(Texture texture, byte[] pixels)
        {
            int width = Convert.ToInt32(textureWidth.GetValue(texture, null));
            int height = Convert.ToInt32(textureHeight.GetValue(texture, null));
            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        }

        public static Texture LiveryTexture(YftFile file)
        {
            var drawable = file == null || file.Fragment == null ? null : file.Fragment.Drawable;
            var group = drawable == null ? null : drawable.ShaderGroup;
            var dictionary = group == null ? null : group.TextureDictionary;
            if (dictionary == null || dictionary.Dict == null) return null;
            foreach (var geometry in drawable.AllModels.SelectMany(model => model.Geometries))
                foreach (string slot in new[] { "diffusesampler2", "diffuse2sampler", "diffusesampler3" })
                {
                    var reference = ShaderParameter(geometry.Shader, slot) as TextureBase;
                    if (reference == null) continue;
                    Texture texture;
                    if (dictionary.Dict.TryGetValue(reference.NameHash, out texture)) return texture;
                }
            return dictionary.Dict.Values.FirstOrDefault(t => t.Name != null &&
                (t.Name.IndexOf("livery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 t.Name.IndexOf("_sign", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public void SetLivery(Texture texture) { liveryMaterial = CreateLiveryMaterial(texture); }

        public void SetTuningLivery(Texture texture)
        {
            if (tuningLiveryTexture == texture) return;
            tuningLiveryMaterial = CreateLiveryMaterial(texture);
            tuningLiveryTexture = texture;
        }

        static Material CreateLiveryMaterial(Texture texture)
        {
            if (texture == null) return null;
            var bitmap = TextureBitmap(texture, DDSIO.GetPixels(texture, 0));
            var material = new DiffuseMaterial(new ImageBrush(bitmap) { Stretch = Stretch.Fill,
                ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1) });
            material.Freeze();
            return material;
        }

        public GeometryModel3D PreviewLivery(Piece piece)
        {
            var active = tuningLiveryMaterial ?? liveryMaterial;
            if (piece.LiveryMesh == null || active == null) return null;
            var model = new GeometryModel3D(piece.LiveryMesh, active);
            model.Freeze();
            return model;
        }

        public void SetReflections(double strength)
        {
            if (reflections == strength) return;
            reflections = strength;
            tintedMaterials.Clear();
        }

        readonly Dictionary<Material, Material> flatMaterials = new Dictionary<Material, Material>();
        readonly HashSet<int> chromeLayers = new HashSet<int>();

        public void SetPaint(int layer, Color? color, bool chrome = false)
        {
            if (chrome) chromeLayers.Add(layer);
            else chromeLayers.Remove(layer);
            if (color.HasValue) paint[layer] = color.Value;
            else paint.Remove(layer);
            tintedMaterials.Clear();
            flatMaterials.Clear();
        }

        public GeometryModel3D PreviewAo(Piece piece)
        {
            // WPF's separate AO pass writes depth even through transparent texels.
            if (piece.Alpha || piece.TextureHasAlpha)
                return null;
            if (!piece.AoPrepared)
            {
                piece.AoPrepared = true;
                piece.AoModel = AmbientOcclusion.Create((MeshGeometry3D)piece.Model.Geometry, piece.Ao);
            }
            return piece.AoModel;
        }

        public GeometryModel3D Preview(Piece piece, bool highlights = true)
        {
            Color color, pearl;
            var source = piece.Model.Material as DiffuseMaterial;
            bool chrome = chromeLayers.Contains(piece.PaintLayer);
            bool hasColor = paint.TryGetValue(piece.PaintLayer, out color);
            bool hasPearl = paint.TryGetValue(3, out pearl) && highlights && (piece.PaintLayer == 1 || piece.PaintLayer == 2);
            bool reflective = highlights && reflections > 0 && (piece.PaintLayer == 1 || piece.PaintLayer == 2 || piece.Glass);
            if (source == null || (!hasColor && !hasPearl && !reflective && !chrome)) return piece.Model;
            Material material;
            var cache = highlights ? tintedMaterials : flatMaterials;
            if (!cache.TryGetValue(source, out material))
            {
                var imageBrush = source.Brush as ImageBrush;
                var bitmap = imageBrush == null ? null : imageBrush.ImageSource as BitmapSource;
                if (chrome)
                {
                    var chromeMaterial = new MaterialGroup();
                    chromeMaterial.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(99, 111, 125))));
                    chromeMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(26, 31, 38))));
                    if (highlights) chromeMaterial.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(223, 238, 255))
                        { Opacity = 0.5 + 0.5 * reflections }, 84));
                    material = chromeMaterial;
                }
                else if (!hasColor) material = source;
                else if (bitmap == null)
                    material = new DiffuseMaterial(new SolidColorBrush(color));
                else
                {
                    int stride = bitmap.PixelWidth * 4;
                    byte[] pixels = new byte[stride * bitmap.PixelHeight];
                    bitmap.CopyPixels(pixels, stride, 0);
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        pixels[i] = (byte)(pixels[i] * color.B / 255);
                        pixels[i + 1] = (byte)(pixels[i + 1] * color.G / 255);
                        pixels[i + 2] = (byte)(pixels[i + 2] * color.R / 255);
                    }
                    var tinted = BitmapSource.Create(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96,
                        PixelFormats.Bgra32, null, pixels, stride);
                    var brush = imageBrush.Clone();
                    brush.ImageSource = tinted;
                    material = new DiffuseMaterial(brush);
                }
                if (!chrome && (hasPearl || reflective))
                {
                    var combined = new MaterialGroup();
                    combined.Children.Add(material);
                    if (reflective)
                        combined.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(210, 225, 255))
                            { Opacity = reflections }, piece.Glass ? 60 : 46));
                    if (hasPearl)
                        combined.Children.Add(new SpecularMaterial(new SolidColorBrush(pearl) { Opacity = 0.6 }, 24));
                    material = combined;
                }
                material.Freeze();
                cache.Add(source, material);
            }
            var model = new GeometryModel3D(piece.Model.Geometry, material);
            model.Freeze();
            return model;
        }
    }
}
