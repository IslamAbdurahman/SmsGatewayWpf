using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmsGatewayApp.Helpers
{
    public static class TextBoxHelper
    {
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached("Placeholder", typeof(string), typeof(TextBoxHelper), new PropertyMetadata(string.Empty, OnPlaceholderChanged));

        public static string GetPlaceholder(DependencyObject obj) => (string)obj.GetValue(PlaceholderProperty);
        public static void SetPlaceholder(DependencyObject obj, string value) => obj.SetValue(PlaceholderProperty, value);

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                textBox.Loaded += (s, _) => UpdatePlaceholderVisibility(textBox);
                textBox.TextChanged += (s, _) => UpdatePlaceholderVisibility(textBox);
                textBox.GotFocus += (s, _) => UpdatePlaceholderVisibility(textBox);
                textBox.LostFocus += (s, _) => UpdatePlaceholderVisibility(textBox);
            }
            else if (d is ComboBox comboBox)
            {
                comboBox.Loaded += (s, _) => UpdateComboBoxPlaceholder(comboBox);
                comboBox.SelectionChanged += (s, _) => UpdateComboBoxPlaceholder(comboBox);
                comboBox.GotFocus += (s, _) => UpdateComboBoxPlaceholder(comboBox);
                comboBox.LostFocus += (s, _) => UpdateComboBoxPlaceholder(comboBox);
            }
        }

        private static void UpdateComboBoxPlaceholder(ComboBox comboBox)
        {
            if (comboBox.SelectedItem == null && !comboBox.IsFocused)
            {
                var placeholder = GetPlaceholder(comboBox);
                var visualBrush = new VisualBrush
                {
                    Visual = new TextBlock
                    {
                        Text = placeholder,
                        Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                        Margin = new Thickness(20, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = comboBox.FontSize,
                        FontWeight = FontWeights.Normal
                    },
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Center,
                    TileMode = TileMode.None
                };
                comboBox.Background = visualBrush;
            }
            else
            {
                comboBox.Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
            }
        }

        private static void UpdatePlaceholderVisibility(TextBox textBox)
        {
            if (string.IsNullOrEmpty(textBox.Text) && !textBox.IsFocused)
            {
                var placeholder = GetPlaceholder(textBox);
                var visualBrush = new VisualBrush
                {
                    Visual = new TextBlock
                    {
                        Text = placeholder,
                        Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                        Margin = new Thickness(20, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = textBox.FontSize,
                        FontWeight = FontWeights.Normal
                    },
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Center,
                    TileMode = TileMode.None
                };
                textBox.Background = visualBrush;
            }
            else
            {
                textBox.Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
            }
        }
    }
}
