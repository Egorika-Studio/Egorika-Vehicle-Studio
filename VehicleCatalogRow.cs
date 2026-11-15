using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace SentinelViewer
{
    public sealed class VehicleCatalogRow : DockPanel
    {
        readonly Image image = new Image { Width = 224, Height = 126 };
        readonly TextBlock title = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold };
        readonly TextBlock path = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.72 };
        readonly TextBlock placeholder = new TextBlock { Text = "YFT", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        public VehicleCatalogRow()
        {
            Margin = new Thickness(3);
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            var tile = new Grid(); tile.Children.Add(image); tile.Children.Add(placeholder);
            var preview = new Border { Background = StudioTheme.Background, Child = tile, Margin = new Thickness(0, 0, 12, 0) };
            SetDock(preview, Dock.Left);
            Children.Add(preview);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(title);
            text.Children.Add(path);
            Children.Add(text);
            Loaded += (s, e) => Update();
            DataContextChanged += (s, e) => { if (IsLoaded) Update(); };
        }

        void Update()
        {
            var item = DataContext as GameTextures.ModelVersion;
            image.Source = null;
            placeholder.Visibility = Visibility.Visible;
            var local = DataContext as string;
            if (item == null && local == null) return;
            title.Text = item == null ? System.IO.Path.GetFileNameWithoutExtension(local) : item.Name;
            path.Text = item == null ? local : item.ToString().Substring(item.Name.Length + 5);
            string file = item == null ? CustomPath(local) : item.Source.OriginRpf == null
                ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "thumbnails", item.Name + ".png")
                : CustomPath(item.Source.OriginRpf + "|" + item.ToString());
            if (!System.IO.File.Exists(file)) return;
            try
            {
                using (var stream = System.IO.File.OpenRead(file))
                    image.Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                placeholder.Visibility = Visibility.Collapsed;
            }
            catch (System.IO.IOException) { }
            catch (NotSupportedException) { }
        }

        internal static void Bake(GameTextures source, string folder, string[] models)
        {
            System.IO.Directory.CreateDirectory(folder);
            var names = new HashSet<string>(models, StringComparer.OrdinalIgnoreCase);
            int processed = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in System.Linq.Enumerable.Where(source.Models(), item => seen.Add(item.Name) && (names.Contains("*") || names.Contains(item.Name))))
            {
                string path = System.IO.Path.Combine(folder, item.Name + ".png");
                if (System.IO.File.Exists(path)) continue;
                try { Save(path, item.ReadThumbnail()); }
                catch (Exception ex) { System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "errors.log"), item.Name + ": " + ex.Message + Environment.NewLine); }
                if (++processed % 20 == 0) { GC.Collect(); GC.WaitForPendingFinalizers(); }
            }
        }

        static string CustomPath(string key)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SentinelViewer", "thumbnails", BitConverter.ToString(hash.ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes(key.ToUpperInvariant()))).Replace("-", "") + ".png");
        }

        internal static void CacheOpened(string key, List<Vehicle.Piece> pieces)
        {
            string path = CustomPath(key);
            if (System.IO.File.Exists(path)) return;
            try { Save(path, pieces); }
            catch (System.IO.IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (InvalidOperationException) { }
        }

        static void Save(string path, List<Vehicle.Piece> pieces)
        {
            var bitmap = Draw(pieces);
            if (bitmap == null) return;
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = System.IO.File.Create(temporary)) png.Save(output);
                if (!System.IO.File.Exists(path)) System.IO.File.Move(temporary, path);
            }
            finally { if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
        }

        static BitmapSource Draw(List<Vehicle.Piece> pieces)
        {
            var bounds = Rect3D.Empty;
            var scene = new Model3DGroup();
            foreach (var piece in pieces) bounds.Union(piece.Model.Bounds);
            if (bounds.IsEmpty) return null;
            scene.Children.Add(new AmbientLight(Color.FromRgb(112, 112, 112)));
            scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-2, -2, -3)));
            var center = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
            double radius = new Vector3D(bounds.SizeX, bounds.SizeY, bounds.SizeZ).Length / 2;
            var direction = new Vector3D(0.72, 0.9, 0.42);
            direction.Normalize();
            foreach (var piece in System.Linq.Enumerable.OrderBy(pieces, piece => piece.Glass ? 2 : piece.Alpha ? 1 : 0))
                scene.Children.Add(piece.Model);
            var view = new Viewport3D { Camera = new OrthographicCamera(center + direction * (radius * 3),
                -direction, new Vector3D(0, 0, 1), Math.Max(0.1, radius * 2.3)) };
            view.Children.Add(new ModelVisual3D { Content = scene });
            view.Measure(new Size(224, 126));
            view.Arrange(new Rect(0, 0, 224, 126));
            var bitmap = new RenderTargetBitmap(448, 252, 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(view);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
