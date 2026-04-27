using System.Windows;
using System.Windows.Input;

namespace LocalTranslatorApp;

public partial class ClipboardHistoryWindow : Window
{
    public string SelectedText { get; private set; } = "";

    public ClipboardHistoryWindow(IEnumerable<string> items)
    {
        InitializeComponent();
        PreviewKeyDown += Window_PreviewKeyDown;
        ClipboardList.ItemsSource = items.Select(text => new ClipboardItem(text)).ToList();
        if (ClipboardList.Items.Count > 0)
        {
            ClipboardList.SelectedIndex = 0;
        }
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        SelectCurrent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        DialogResult = false;
    }

    private void ClipboardList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        SelectCurrent();
    }

    private void SelectCurrent()
    {
        if (ClipboardList.SelectedItem is not ClipboardItem item)
        {
            return;
        }

        SelectedText = item.Text;
        DialogResult = true;
    }

    private sealed class ClipboardItem
    {
        public ClipboardItem(string text)
        {
            Text = text;
            Preview = text.Length > 260 ? text[..260] + "..." : text;
            LengthText = $"{text.Length:N0} characters";
        }

        public string Text { get; }
        public string Preview { get; }
        public string LengthText { get; }
    }
}
