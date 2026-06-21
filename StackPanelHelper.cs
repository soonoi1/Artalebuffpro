using System.Windows;
using System.Windows.Controls;

namespace ArtaleProBuff
{
    public static class StackPanelHelper
    {
        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.RegisterAttached(
                "Spacing",
                typeof(double),
                typeof(StackPanelHelper),
                new PropertyMetadata(0.0, OnSpacingChanged));

        public static double GetSpacing(DependencyObject obj)
        {
            return (double)obj.GetValue(SpacingProperty);
        }

        public static void SetSpacing(DependencyObject obj, double value)
        {
            obj.SetValue(SpacingProperty, value);
        }

        private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StackPanel panel)
            {
                panel.Loaded += (s, args) => ApplySpacing(panel);
                // Also apply immediately if already loaded
                if (panel.IsLoaded)
                {
                    ApplySpacing(panel);
                }
            }
        }

        private static void ApplySpacing(StackPanel panel)
        {
            double spacing = GetSpacing(panel);
            if (spacing <= 0) return;

            var margin = panel.Orientation == Orientation.Horizontal
                ? new Thickness(0, 0, spacing, 0)
                : new Thickness(0, 0, 0, spacing);

            int count = panel.Children.Count;
            for (int i = 0; i < count - 1; i++)
            {
                if (panel.Children[i] is FrameworkElement child)
                {
                    child.Margin = margin;
                }
            }
        }
    }
}
