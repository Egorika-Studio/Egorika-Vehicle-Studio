using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SentinelViewer
{
    static class EnvironmentScene
    {
        public static Model3DGroup Create(string preset, Point3D center, double ground)
        {
            var group = new Model3DGroup();
            if (preset == Ui.Text("Дорога", "Road"))
            {
                group.Children.Add(Strip(center, ground, -80, 80, -11, 11, Color.FromRgb(65, 66, 69)));
                group.Children.Add(Strip(center, ground + 0.12, -80, 80, 7, 11, Color.FromRgb(162, 155, 140)));
                group.Children.Add(Strip(center, ground + 0.12, -80, 80, -11, -7, Color.FromRgb(162, 155, 140)));
                group.Children.Add(Strip(center, ground + 0.001, -80, 80, 6.55, 6.62, Color.FromRgb(213, 208, 192)));
                group.Children.Add(Strip(center, ground + 0.001, -80, 80, -6.62, -6.55, Color.FromRgb(213, 208, 192)));
                for (int x = -72; x < 74; x += 9)
                    group.Children.Add(Strip(center, ground + 0.001, x, x + 4.5, -3.55, -3.45, Color.FromRgb(214, 192, 122)));
            }
            else if (preset == Ui.Text("Площадка", "Studio floor"))
                group.Children.Add(Strip(center, ground, -75, 75, -75, 75, Color.FromRgb(120, 126, 132)));

            if (group.Children.Count > 0)
            {
                var shadow = new RadialGradientBrush();
                shadow.GradientStops.Add(new GradientStop(Color.FromArgb(130, 5, 8, 14), 0.0));
                shadow.GradientStops.Add(new GradientStop(Color.FromArgb(95, 5, 8, 14), 0.56));
                shadow.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
                group.Children.Add(Quad(center, ground + 0.003, -2.85, 2.85, -1.4, 1.4, new DiffuseMaterial(shadow)));
            }
            if (group.Children.Count > 0)
                group.Transform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 90), center);
            group.Freeze();
            return group;
        }

        static GeometryModel3D Strip(Point3D center, double z, double x1, double x2, double y1, double y2, Color color)
        {
            return Quad(center, z, x1, x2, y1, y2, new DiffuseMaterial(new SolidColorBrush(color)));
        }

        static GeometryModel3D Quad(Point3D center, double z, double x1, double x2, double y1, double y2, Material material)
        {
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection
                {
                    new Point3D(center.X + x1, center.Y + y1, z),
                    new Point3D(center.X + x2, center.Y + y1, z),
                    new Point3D(center.X + x2, center.Y + y2, z),
                    new Point3D(center.X + x1, center.Y + y2, z)
                },
                TextureCoordinates = new PointCollection { new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1) },
                TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
            };
            return new GeometryModel3D(mesh, material);
        }
    }
}
