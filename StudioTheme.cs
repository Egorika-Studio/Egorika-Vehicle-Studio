using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;

namespace SentinelViewer
{
    static class StudioTheme
    {
        public static readonly Brush Background = Brush(31, 31, 31);
        public static readonly Brush Panel = Brush(40, 40, 40);
        static readonly Brush Field = Brush(52, 52, 52);
        static readonly Brush Line = Brush(74, 74, 74);
        static readonly Brush Text = Brush(225, 225, 225);
        static readonly Brush Accent = Brush(56, 100, 153);

        public static Style MainMenuItemStyle()
        {
            return (Style)XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='MenuItem'>
  <Setter Property='Foreground' Value='#E1E1E1'/>
  <Setter Property='Padding' Value='9,3'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='MenuItem'>
        <Border x:Name='Row' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                Background='Transparent' CornerRadius='3' Padding='{TemplateBinding Padding}'>
          <Grid>
            <ContentPresenter ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center'/>
            <Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='True'
                   IsOpen='{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'
                   PlacementTarget='{Binding RelativeSource={RelativeSource TemplatedParent}}'>
              <Border Background='#252525' CornerRadius='4' Padding='4'>
                <ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle'/>
              </Border>
            </Popup>
          </Grid>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsHighlighted' Value='True'>
            <Setter TargetName='Row' Property='Background' Value='#414141'/>
          </Trigger>
          <Trigger Property='IsSubmenuOpen' Value='True'>
            <Setter TargetName='Row' Property='Background' Value='#414141'/>
          </Trigger>
          <Trigger Property='IsEnabled' Value='False'>
            <Setter Property='Opacity' Value='0.45'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>");
        }

        static Brush Brush(byte red, byte green, byte blue)
        {
            var value = new SolidColorBrush(Color.FromRgb(red, green, blue));
            value.Freeze();
            return value;
        }

        public static void Apply(Window window)
        {
            window.Background = Background;
            window.Foreground = Text;
            window.FontFamily = new FontFamily("Segoe UI");
            var resources = window.Resources;
            resources[SystemColors.WindowBrushKey] = Panel;
            resources[SystemColors.WindowTextBrushKey] = Text;
            resources[SystemColors.ControlBrushKey] = Field;
            resources[SystemColors.ControlTextBrushKey] = Text;
            resources[SystemColors.ControlDarkBrushKey] = Line;
            resources[SystemColors.HighlightBrushKey] = Accent;
            resources[SystemColors.HighlightTextBrushKey] = Brushes.White;
            Add(resources, typeof(TextBox), Field, Text);
            Add(resources, typeof(TreeView), Panel, Text);
            Add(resources, typeof(ListBox), Panel, Text);
            Add(resources, typeof(DataGrid), Panel, Text);
            Add(resources, typeof(DataGridRow), Panel, Text);
            Add(resources, typeof(System.Windows.Controls.Primitives.DataGridColumnHeader), Field, Text);
            Add(resources, typeof(ComboBox), Field, Brushes.Black);
            Add(resources, typeof(ComboBoxItem), Field, Text);
            Add(resources, typeof(ContextMenu), Panel, Text);
            Add(resources, typeof(MenuItem), Panel, Text);
            Add(resources, typeof(Button), Field, Text, true);
        }

        static void Add(ResourceDictionary resources, System.Type type, Brush background, Brush text, bool button = false)
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(Control.BackgroundProperty, background));
            style.Setters.Add(new Setter(Control.ForegroundProperty, text));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
            if (button)
            {
                var template = new ControlTemplate(type);
                var frame = new FrameworkElementFactory(typeof(Border));
                frame.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
                frame.SetValue(Border.BorderBrushProperty, Line);
                frame.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
                var content = new FrameworkElementFactory(typeof(ContentPresenter));
                content.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
                content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                frame.AppendChild(content);
                template.VisualTree = frame;
                style.Setters.Add(new Setter(Control.TemplateProperty, template));
                var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                hover.Setters.Add(new Setter(Control.BackgroundProperty, Accent));
                style.Triggers.Add(hover);
            }
            resources[type] = style;
        }
    }
}
