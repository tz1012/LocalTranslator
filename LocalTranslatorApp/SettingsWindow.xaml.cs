using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LocalTranslatorApp.Models;
using Microsoft.Win32;

namespace LocalTranslatorApp;

public partial class SettingsWindow : Window
{
    private string _lastTranslateCapture = "";
    private DateTimeOffset _lastTranslateCaptureAt = DateTimeOffset.MinValue;

    public AppSettings Settings { get; private set; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        PreviewKeyDown += Window_PreviewKeyDown;
        Settings = Clone(settings);
        LoadSettings();
        ShowPanel(GeneralPanel, GeneralNav);
    }

    private static AppSettings Clone(AppSettings settings)
    {
        return new AppSettings
        {
            LaunchAtStartup = settings.LaunchAtStartup,
            FloatingIconEnabled = settings.FloatingIconEnabled,
            QuickAccessMode = settings.QuickAccessMode,
            CloseBehavior = settings.CloseBehavior,
            SourceLanguage = settings.SourceLanguage,
            TargetLanguage = settings.TargetLanguage,
            Theme = settings.Theme,
            TranslateShortcut = settings.TranslateShortcut,
            InsertShortcut = settings.InsertShortcut,
            DoubleCopyIntervalMs = settings.DoubleCopyIntervalMs,
            LmStudioEndpoint = settings.LmStudioEndpoint,
            Model = settings.Model,
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
            CheckForUpdates = settings.CheckForUpdates,
            UpdateRepository = settings.UpdateRepository,
            UpdateAssetPattern = settings.UpdateAssetPattern,
            Instruction = settings.Instruction
        };
    }

    private void LoadSettings()
    {
        LaunchAtStartupCheck.IsChecked = Settings.LaunchAtStartup;
        FloatingIconCheck.IsChecked = Settings.FloatingIconEnabled;
        QuickFloatingRadio.IsChecked = Settings.QuickAccessMode == QuickAccessMode.FloatingWindow;
        QuickMainRadio.IsChecked = Settings.QuickAccessMode == QuickAccessMode.MainApp;
        CloseBackgroundRadio.IsChecked = Settings.CloseBehavior == CloseBehavior.KeepInBackground;
        CloseExitRadio.IsChecked = Settings.CloseBehavior == CloseBehavior.Exit;
        CloseAskRadio.IsChecked = Settings.CloseBehavior == CloseBehavior.AskEveryTime;
        TranslateShortcutText.Text = Settings.TranslateShortcut;
        InsertShortcutText.Text = Settings.InsertShortcut;
        DoubleCopyIntervalText.Text = Settings.DoubleCopyIntervalMs.ToString();
        SetLanguageButtonText(SourceLanguageButton, Settings.SourceLanguage);
        SetLanguageButtonText(TargetLanguageButton, Settings.TargetLanguage);
        SelectComboItem(ThemeCombo, Settings.Theme);
        EndpointText.Text = Settings.LmStudioEndpoint;
        ModelText.Text = Settings.Model;
        TemperatureText.Text = Settings.Temperature.ToString("0.##");
        MaxTokensText.Text = Settings.MaxTokens.ToString();
        CheckForUpdatesCheck.IsChecked = Settings.CheckForUpdates;
        UpdateRepositoryText.Text = Settings.UpdateRepository;
        UpdateAssetPatternText.Text = Settings.UpdateAssetPattern;
        InstructionText.Text = Settings.Instruction;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Settings.LaunchAtStartup = LaunchAtStartupCheck.IsChecked == true;
        Settings.FloatingIconEnabled = FloatingIconCheck.IsChecked == true;
        Settings.QuickAccessMode = QuickFloatingRadio.IsChecked == true
            ? QuickAccessMode.FloatingWindow
            : QuickAccessMode.MainApp;
        Settings.CloseBehavior = CloseExitRadio.IsChecked == true
            ? CloseBehavior.Exit
            : CloseAskRadio.IsChecked == true
                ? CloseBehavior.AskEveryTime
                : CloseBehavior.KeepInBackground;
        Settings.TranslateShortcut = string.IsNullOrWhiteSpace(TranslateShortcutText.Text)
            ? "Ctrl+C,C"
            : TranslateShortcutText.Text.Trim();
        Settings.InsertShortcut = string.IsNullOrWhiteSpace(InsertShortcutText.Text)
            ? "Ctrl+Enter"
            : InsertShortcutText.Text.Trim();
        Settings.SourceLanguage = GetLanguageButtonText(SourceLanguageButton);
        Settings.TargetLanguage = GetLanguageButtonText(TargetLanguageButton);
        Settings.Theme = GetSelectedComboText(ThemeCombo);
        Settings.LmStudioEndpoint = EndpointText.Text.Trim();
        Settings.Model = ModelText.Text.Trim();
        Settings.CheckForUpdates = CheckForUpdatesCheck.IsChecked == true;
        Settings.UpdateRepository = UpdateRepositoryText.Text.Trim();
        Settings.UpdateAssetPattern = string.IsNullOrWhiteSpace(UpdateAssetPatternText.Text)
            ? "Setup.exe"
            : UpdateAssetPatternText.Text.Trim();
        Settings.Instruction = InstructionText.Text.Trim();

        if (int.TryParse(DoubleCopyIntervalText.Text, out var interval))
        {
            Settings.DoubleCopyIntervalMs = Math.Clamp(interval, 250, 1500);
        }

        if (double.TryParse(TemperatureText.Text, out var temperature))
        {
            Settings.Temperature = Math.Clamp(temperature, 0, 2);
        }

        if (int.TryParse(MaxTokensText.Text, out var maxTokens))
        {
            Settings.MaxTokens = Math.Clamp(maxTokens, 256, 8192);
        }

        ApplyStartupSetting(Settings.LaunchAtStartup);
        DialogResult = true;
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

    private void ShortcutText_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.Text = "키를 누르세요...";
            textBox.SelectAll();
        }
    }

    private void TranslateShortcutText_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var shortcut = BuildShortcut(e);
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (string.Equals(_lastTranslateCapture, shortcut, StringComparison.OrdinalIgnoreCase) &&
            (now - _lastTranslateCaptureAt).TotalMilliseconds <= 1500)
        {
            TranslateShortcutText.Text = $"{shortcut},{LastKeyName(shortcut)}";
            _lastTranslateCapture = "";
            Keyboard.ClearFocus();
            return;
        }

        _lastTranslateCapture = shortcut;
        _lastTranslateCaptureAt = now;
        TranslateShortcutText.Text = shortcut;
    }

    private void InsertShortcutText_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var shortcut = BuildShortcut(e);
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return;
        }

        InsertShortcutText.Text = shortcut;
        Keyboard.ClearFocus();
    }

    private static string BuildShortcut(System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return "";
        }

        var parts = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            parts.Add("Ctrl");
        }

        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            parts.Add("Alt");
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            parts.Add("Shift");
        }

        if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(key));
        return string.Join("+", parts);
    }

    private static string LastKeyName(string shortcut)
    {
        var plus = shortcut.LastIndexOf('+');
        return plus >= 0 ? shortcut[(plus + 1)..] : shortcut;
    }

    private static string KeyName(Key key)
    {
        return key switch
        {
            Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Space => "Space",
            >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => $"Num{(int)(key - Key.NumPad0)}",
            _ => key.ToString()
        };
    }

    private void GeneralNav_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(GeneralPanel, GeneralNav);
    }

    private void ShortcutNav_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(ShortcutPanel, ShortcutNav);
    }

    private void LanguageNav_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(LanguagePanel, LanguageNav);
    }

    private void LmStudioNav_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(LmStudioPanel, LmStudioNav);
    }

    private void UpdateNav_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(UpdatePanel, UpdateNav);
    }

    private void SourceLanguage_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickLanguage(GetLanguageButtonText(SourceLanguageButton), includeAutoDetect: true, out var language))
        {
            SetLanguageButtonText(SourceLanguageButton, language);
        }
    }

    private void TargetLanguage_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickLanguage(GetLanguageButtonText(TargetLanguageButton), includeAutoDetect: false, out var language))
        {
            SetLanguageButtonText(TargetLanguageButton, language);
        }
    }

    private void ShowPanel(StackPanel panel, System.Windows.Controls.Button activeButton)
    {
        GeneralPanel.Visibility = Visibility.Collapsed;
        ShortcutPanel.Visibility = Visibility.Collapsed;
        LanguagePanel.Visibility = Visibility.Collapsed;
        LmStudioPanel.Visibility = Visibility.Collapsed;
        UpdatePanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;

        foreach (var button in new[] { GeneralNav, ShortcutNav, LanguageNav, LmStudioNav, UpdateNav })
        {
            button.Background = System.Windows.Media.Brushes.White;
            button.BorderBrush = BrushFrom("#D7DEE8");
            button.Foreground = BrushFrom("#172033");
        }

        activeButton.Background = BrushFrom("#E8F2FF");
        activeButton.BorderBrush = BrushFrom("#1F7AE0");
        activeButton.Foreground = BrushFrom("#1F7AE0");
    }

    private static void ApplyStartupSetting(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key is null)
            {
                return;
            }

            const string valueName = "LocalTranslator";
            if (enabled && !string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                key.SetValue(valueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(valueName, false);
            }
        }
        catch
        {
            // Startup registration is best-effort; settings are still saved.
        }
    }

    private static string GetSelectedComboText(System.Windows.Controls.ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
    }

    private bool TryPickLanguage(string currentLanguage, bool includeAutoDetect, out string language)
    {
        var picker = new LanguagePickerWindow(currentLanguage, includeAutoDetect)
        {
            Owner = this
        };

        if (picker.ShowDialog() == true && !string.IsNullOrWhiteSpace(picker.SelectedLanguage))
        {
            language = picker.SelectedLanguage;
            return true;
        }

        language = "";
        return false;
    }

    private static string GetLanguageButtonText(System.Windows.Controls.Button button)
    {
        return button.Content?.ToString() ?? "";
    }

    private static void SetLanguageButtonText(System.Windows.Controls.Button button, string value)
    {
        button.Content = string.IsNullOrWhiteSpace(value) ? "Korean" : value;
    }

    private static void SelectComboItem(System.Windows.Controls.ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static System.Windows.Media.Brush BrushFrom(string hex)
    {
        return (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
    }
}
