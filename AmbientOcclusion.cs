using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace SentinelViewer
{
    static class AmbientOcclusion
    {
        static Material material;

        public static GeometryModel3D Create(MeshGeometry3D source, byte[] channel)
        {
            if (channel == null) return null;
            var triangles = new Int32Collection();
            var indices = source.TriangleIndices;
            for (int i = 0; i < indices.Count; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (channel[a] == 255 && channel[b] == 255 && channel[c] == 255) continue;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
            if (triangles.Count == 0) return null;

            var positions = new Point3DCollection(source.Positions.Count);
            var uv = new PointCollection(source.Positions.Count);
            bool hasNormals = source.Normals.Count == source.Positions.Count;
            for (int i = 0; i < source.Positions.Count; i++)
            {
                positions.Add(source.Positions[i] + (hasNormals ? source.Normals[i] * 0.0015 : new Vector3D()));
                uv.Add(new Point((channel[i] + 0.5) / 256.0, 0.5));
            }
            var mesh = new MeshGeometry3D { Positions = positions, TextureCoordinates = uv, TriangleIndices = triangles };
            if (material == null)
            {
                var pixels = new byte[256 * 4];
                for (int i = 0; i < 256; i++) pixels[i * 4 + 3] = (byte)((255 - i) * 0.7);
                var bitmap = BitmapSource.Create(256, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 256 * 4);
                material = new DiffuseMaterial(new ImageBrush(bitmap) { Stretch = Stretch.Fill,
                    ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1) });
                material.Freeze();
            }
            var model = new GeometryModel3D(mesh, material);
            model.Freeze();
            return model;
        }
    }
}
