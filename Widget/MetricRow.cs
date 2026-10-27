using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace CyborgVitals
{
    // Small code-native icons: no font, image, AI, or third-party icon dependency.
    public static class MetricIcons
    {
        private const string Chip = "M6,6 H18 V18 H6 Z M9,9 H15 V15 H9 Z M9,2 V6 M15,2 V6 M9,18 V22 M15,18 V22 M2,9 H6 M2,15 H6 M18,9 H22 M18,15 H22";
        private const string Gpu = "M3,5 H21 V18 H3 Z M6,18 V21 H17 V18 M1,5 V20 M21,8 H23 M21,15 H23 M16,11 A4,4 0 1 1 8,11 A4,4 0 1 1 16,11 M10,8 L14,14 M14,8 L10,14";
        private const string Ram = "M2,6 H22 V17 H2 Z M5,9 H9 V14 H5 Z M12,9 H16 V14 H12 Z M19,9 V14 M5,17 V21 M9,17 V21 M13,17 V21 M17,17 V21 M21,17 V21";
        private const string Thermometer = "M10,4 A2,2 0 0 1 14,4 V14 A5,5 0 1 1 10,14 Z M12,7 V17 M12,17 A1.5,1.5 0 1 1 12,20 A1.5,1.5 0 1 1 12,17 M17,6 H21 M17,10 H20";
        private const string Fan = "M12,10 A2,2 0 1 1 12,14 A2,2 0 1 1 12,10 M11,10 C2,7 7,0 12,3 C15,5 13,8 12,10 M14,11 C17,2 24,7 21,12 C19,15 16,13 14,12 M13,14 C22,17 17,24 12,21 C9,19 11,16 12,14 M10,13 C7,22 0,17 3,12 C5,9 8,11 10,12";
        public static Viewbox Create(string metric)
        {
            string path = metric == "ram" ? Ram : metric == "gpu" ? Gpu
                : VitalsLogic.IsTemperature(metric) ? Thermometer : VitalsLogic.IsFan(metric) ? Fan : Chip;
            var canvas = new Canvas { Width = 24, Height = 24 };
            var geometry = XamlReader.Load("<Path xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Data=\"" + path + "\" />") as Path;
            geometry.Stroke = new SolidColorBrush(Color.FromArgb(255, 178, 198, 214));
            geometry.StrokeThickness = 1.5;
            geometry.StrokeLineJoin = PenLineJoin.Round;
            geometry.StrokeStartLineCap = PenLineCap.Round;
            geometry.StrokeEndLineCap = PenLineCap.Round;
            canvas.Children.Add(geometry);
            return new Viewbox { Child = canvas, Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        }
    }

    public sealed class AlertBadge : Grid
    {
        public TextBlock ValueText { get; private set; }
        private readonly Ellipse dot;
        private Storyboard blink;
        private bool active, animated;
        public AlertBadge(double size)
        {
            Padding = new Thickness(0, 1, 7, 0);
            ValueText = new TextBlock { Text = "N/A", FontSize = size, Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            dot = new Ellipse { Width = 4, Height = 4, Fill = new SolidColorBrush(Color.FromArgb(255, 255, 76, 91)),
                VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed, IsHitTestVisible = false };
            Children.Add(ValueText); Children.Add(dot);
            Unloaded += (s, e) => Stop();
        }
        public void Update(string value, bool alert, bool animate, string tooltip)
        {
            ValueText.Text = value;
            ToolTipService.SetToolTip(this, tooltip);
            AutomationProperties.SetName(this, value + (alert ? ", above configured threshold" : ""));
            if (active == alert && animated == animate) return;
            Stop(); active = alert; animated = animate;
            dot.Visibility = alert ? Visibility.Visible : Visibility.Collapsed;
            dot.Opacity = 1;
            if (!alert || !animate) return;
            var animation = new DoubleAnimation { From = 1, To = 0.15, Duration = TimeSpan.FromSeconds(0.8),
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            Storyboard.SetTarget(animation, dot); Storyboard.SetTargetProperty(animation, "Opacity");
            blink = new Storyboard(); blink.Children.Add(animation); blink.Begin();
        }
        public void Stop()
        {
            if (blink != null) { blink.Stop(); blink = null; }
            active = false; dot.Visibility = Visibility.Collapsed; dot.Opacity = 1;
        }
    }

    public sealed class MetricRow : Button
    {
        public string Metric { get; private set; }
        public AlertBadge Badge { get; private set; }
        private readonly TextBlock label;
        private readonly Grid rowGrid;
        private readonly Viewbox rowIcon;
        public MetricRow(string metric, string caption)
        {
            Metric = metric; Tag = metric;
            Padding = new Thickness(2, 1, 0, 1); BorderThickness = new Thickness(0);
            Background = new SolidColorBrush(Colors.Transparent);
            // Default Button visual states can add theme-filled hover/pressed surfaces.
            // Keep the entire reading surface transparent in every pointer state.
            Template = (ControlTemplate)XamlReader.Load(
                "<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TargetType=\"Button\">"
                + "<Grid Background=\"Transparent\"><ContentPresenter Content=\"{TemplateBinding Content}\" ContentTemplate=\"{TemplateBinding ContentTemplate}\" "
                + "Margin=\"{TemplateBinding Padding}\" HorizontalContentAlignment=\"Stretch\" VerticalContentAlignment=\"Center\"/></Grid></ControlTemplate>");
            UseSystemFocusVisuals = true;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            MinHeight = 24;
            var content = new Grid(); rowGrid = content;
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(21) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowIcon = MetricIcons.Create(metric); content.Children.Add(rowIcon);
            label = new TextBlock { Text = caption, FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(1, 0, 5, 0) };
            Grid.SetColumn(label, 1); content.Children.Add(label);
            Badge = new AlertBadge(11); Grid.SetColumn(Badge, 2); content.Children.Add(Badge);
            Content = content;
            AutomationProperties.SetName(this, caption);
        }
        public void ShowIcons(bool show)
        {
            rowIcon.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            rowGrid.ColumnDefinitions[0].Width = new GridLength(show ? 21 : 0);
        }
        public void Update(string caption, string value, bool selected, bool alert, bool animate, string tooltip)
        {
            label.Text = caption;
            label.Foreground = new SolidColorBrush(selected ? Color.FromArgb(255, 93, 208, 244) : Colors.White);
            Background = new SolidColorBrush(Colors.Transparent);
            Badge.Update(value, alert, animate, tooltip);
            ToolTipService.SetToolTip(this, tooltip);
            AutomationProperties.SetName(this, caption + ", " + value + (alert ? ", above configured threshold" : ""));
        }
    }
}
