using System.Windows;
using System.Windows.Input;
using LocalTranslatorApp.Services;

namespace LocalTranslatorApp;

public partial class FloatingTranslationWindow : Window
{
    private string _sourceText = "";

    public FloatingTranslationWindow()
    {
        InitializeComponent();
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    public event EventHandler? InsertRequested;
    public event EventHandler? OpenMainRequested;
    public event EventHandler<string>? TargetLanguageChanged;

    public string ResultText
    {
        get => ResultTextBox.Text;
        set => ResultTextBox.Text = value;
    }

    public void SetTargetLanguage(string language)
    {
        TargetLanguageButton.Content = string.IsNullOrWhiteSpace(language) ? "Korean" : language;
    }

    public void SetLoading(string sourceText)
    {
        _sourceText = sourceText;
        SourcePreviewText.Text = sourceText;
        ResultTextBox.Text = "";
        FloatingStatusText.Text = "번역 중...";
    }

    public void SetResult(string translatedText)
    {
        ResultTextBox.Text = translatedText;
        FloatingStatusText.Text = "완료";
    }

    public void SetError(string message)
    {
        ResultTextBox.Text = message;
        FloatingStatusText.Text = "번역 실패";
    }

    public void PositionNearCursor()
    {
        PositionNearScreenPoint(System.Windows.Forms.Cursor.Position);
    }

    public void PositionNearScreenPoint(System.Drawing.Point screenPoint)
    {
        var point = DeviceToDip(screenPoint);
        var workArea = SystemParameters.WorkArea;
        var position = FloatingWindowPositioner.Calculate(point, Width, Height, workArea);
        Left = position.X;
        Top = position.Y;
    }

    private System.Windows.Point DeviceToDip(System.Drawing.Point screenPoint)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return new System.Windows.Point(screenPoint.X, screenPoint.Y);
        }

        return source.CompositionTarget.TransformFromDevice.Transform(new System.Windows.Point(screenPoint.X, screenPoint.Y));
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ResultTextBox.Text))
        {
            System.Windows.Clipboard.SetText(ResultTextBox.Text);
            FloatingStatusText.Text = "복사됨";
        }
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        InsertRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenMain_Click(object sender, RoutedEventArgs e)
    {
        OpenMainRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Hide();
    }

    private void TargetLanguage_Click(object sender, RoutedEventArgs e)
    {
        var picker = new LanguagePickerWindow(TargetLanguageButton.Content?.ToString() ?? "Korean", includeAutoDetect: false)
        {
            Owner = this
        };

        if (picker.ShowDialog() == true && !string.IsNullOrWhiteSpace(picker.SelectedLanguage))
        {
            SetTargetLanguage(picker.SelectedLanguage);
            TargetLanguageChanged?.Invoke(this, picker.SelectedLanguage);
        }
    }

    private void WindowChrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
