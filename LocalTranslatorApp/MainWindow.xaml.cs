using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LocalTranslatorApp.Models;
using LocalTranslatorApp.Services;
using Forms = System.Windows.Forms;

namespace LocalTranslatorApp;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly LmStudioClient _lmStudioClient = new();
    private readonly GitHubUpdateService _updateService = new();
    private readonly KeyboardShortcutService _keyboardShortcutService = new();
    private readonly DispatcherTimer _autoTranslateTimer = new();
    private readonly DispatcherTimer _clipboardTimer = new();
    private readonly DispatcherTimer _updateTimer = new();
    private readonly List<string> _clipboardHistory = new();
    private readonly Forms.NotifyIcon _notifyIcon;
    private FloatingTranslationWindow? _floatingWindow;
    private NativeInput.FocusTarget _lastSourceTarget;
    private System.Drawing.Point _lastFloatingAnchorPoint;
    private bool _hasLastFloatingAnchorPoint;
    private AppSettings _settings;
    private bool _exitRequested;
    private bool _suppressAutoTranslate;
    private int _translationVersion;
    private DateTimeOffset _lastModelCheckAt = DateTimeOffset.MinValue;
    private UpdateInfo? _pendingUpdate;
    private bool _isCheckingUpdates;

    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += Window_PreviewKeyDown;

        _settings = _settingsStore.Load();
        ApplySettingsToUi();

        _keyboardShortcutService.TranslateSelectedTextRequested += (_, _) =>
            Dispatcher.BeginInvoke(TranslateSelectedTextFromShortcutAsync);
        _keyboardShortcutService.InsertTranslationRequested += (_, _) =>
            Dispatcher.BeginInvoke(InsertResultAsync);
        _keyboardShortcutService.Start(_settings);

        _notifyIcon = CreateNotifyIcon();

        SourceTextBox.TextChanged += SourceTextBox_TextChanged;
        _autoTranslateTimer.Interval = TimeSpan.FromMilliseconds(650);
        _autoTranslateTimer.Tick += AutoTranslateTimer_Tick;

        _clipboardTimer.Interval = TimeSpan.FromSeconds(1.5);
        _clipboardTimer.Tick += (_, _) => AddClipboardSnapshot();
        _clipboardTimer.Start();
        AddClipboardSnapshot();

        _updateTimer.Interval = TimeSpan.FromHours(6);
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync(manual: false);
        _updateTimer.Start();

        Loaded += async (_, _) =>
        {
            await CheckConnectionAsync();
            await CheckForUpdatesAsync(manual: false);
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exitRequested && _settings.CloseBehavior == CloseBehavior.KeepInBackground)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon.ShowBalloonTip(1400, "Local Translator", "諛깃렇?쇱슫?쒖뿉??怨꾩냽 ?ㅽ뻾 以묒엯?덈떎.", Forms.ToolTipIcon.Info);
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _keyboardShortcutService.Dispose();
        _floatingWindow?.Close();
        _notifyIcon.Dispose();
        base.OnClosed(e);
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("?닿린", null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        menu.Items.Add("?ㅼ젙", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("?낅뜲?댄듃 ?뺤씤", null, (_, _) =>
            Dispatcher.BeginInvoke(async () => await CheckForUpdatesAsync(manual: true)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("醫낅즺", null, (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                _exitRequested = true;
                Close();
            });
        });

        var icon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Local Translator",
            Visible = true,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        icon.BalloonTipClicked += (_, _) =>
            Dispatcher.BeginInvoke(async () =>
            {
                if (_pendingUpdate is not null)
                {
                    await DownloadAndInstallUpdateAsync(_pendingUpdate);
                }
            });
        return icon;
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Close();
    }

    private async Task TranslateSelectedTextFromShortcutAsync()
    {
        _lastFloatingAnchorPoint = Forms.Cursor.Position;
        _hasLastFloatingAnchorPoint = true;
        _lastSourceTarget = NativeInput.CaptureForegroundTarget();
        NativeInput.SendCtrlC();
        await Task.Delay(180);
        var text = TryGetClipboardText();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("?좏깮???띿뒪?멸? ?놁뒿?덈떎");
            return;
        }

        if (LmStudioClient.LooksLikeMetaResponse(text))
        {
            SetStatus("Selected text looks like commentary, not source text. Translation stopped.");
            return;
        }

        AddClipboardText(text);
        SetSourceText(text);

        if (_settings.QuickAccessMode == QuickAccessMode.FloatingWindow)
        {
            Hide();
            await TranslateInFloatingWindowAsync(text);
            return;
        }

        ShowMainWindow();
        await TranslateAsync(text);
    }

    private async Task TranslateAsync(string text)
    {
        var version = ++_translationVersion;
        if (string.IsNullOrWhiteSpace(text))
        {
            ResultTextBox.Clear();
            SetStatus("踰덉뿭???띿뒪?멸? ?놁뒿?덈떎");
            return;
        }

        try
        {
            SetStatus("踰덉뿭 以?..");
            ResultTextBox.Text = "";
            var result = await TranslateTextOnlyAsync(text);
            if (version == _translationVersion)
            {
                ResultTextBox.Text = result;
                SetStatus("?꾨즺");
            }
        }
        catch (Exception ex)
        {
            if (version == _translationVersion)
            {
                SetStatus("踰덉뿭 ?ㅽ뙣");
                ResultTextBox.Text = ex.Message;
            }
        }
    }

    private async Task TranslateInFloatingWindowAsync(string text)
    {
        var version = ++_translationVersion;
        var floatingWindow = EnsureFloatingWindow();
        floatingWindow.SetTargetLanguage(_settings.TargetLanguage);
        floatingWindow.SetLoading(text);
        floatingWindow.Show();
        floatingWindow.PositionNearScreenPoint(_hasLastFloatingAnchorPoint
            ? _lastFloatingAnchorPoint
            : NativeInput.GetFloatingAnchorPoint(_lastSourceTarget));

        try
        {
            SetStatus("踰덉뿭 以?..");
            ResultTextBox.Clear();
            var result = await TranslateTextOnlyAsync(text);
            if (version != _translationVersion)
            {
                return;
            }

            ResultTextBox.Text = result;
            SetStatus("?꾨즺");
            floatingWindow.SetResult(result);
        }
        catch (Exception ex)
        {
            if (version != _translationVersion)
            {
                return;
            }

            SetStatus("踰덉뿭 ?ㅽ뙣");
            ResultTextBox.Text = ex.Message;
            floatingWindow.SetError(ex.Message);
        }
    }

    private async Task<string> TranslateTextOnlyAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(_settings.Model) ||
            DateTimeOffset.UtcNow - _lastModelCheckAt > TimeSpan.FromMinutes(5))
        {
            var check = await _lmStudioClient.CheckAsync(_settings);
            _lastModelCheckAt = DateTimeOffset.UtcNow;
            if (check.IsConnected)
            {
                _settings.Model = check.Model;
                _settingsStore.Save(_settings);
            }
        }

        return await _lmStudioClient.TranslateAsync(text, _settings);
    }

    private FloatingTranslationWindow EnsureFloatingWindow()
    {
        if (_floatingWindow is { IsLoaded: true })
        {
            return _floatingWindow;
        }

        _floatingWindow = new FloatingTranslationWindow();
        _floatingWindow.InsertRequested += async (_, _) => await InsertResultAsync();
        _floatingWindow.OpenMainRequested += (_, _) =>
        {
            ShowMainWindow();
            _floatingWindow?.Hide();
        };
        _floatingWindow.TargetLanguageChanged += (_, language) =>
        {
            _settings.TargetLanguage = language;
            _settingsStore.Save(_settings);
            SetLanguageButtonText(TargetLanguageButton, language);
            if (_floatingWindow is { IsVisible: true } && !string.IsNullOrWhiteSpace(SourceTextBox.Text))
            {
                _ = TranslateInFloatingWindowAsync(SourceTextBox.Text);
            }
        };
        _floatingWindow.Closed += (_, _) => _floatingWindow = null;
        return _floatingWindow;
    }

    private async Task CheckConnectionAsync()
    {
        var result = await _lmStudioClient.CheckAsync(_settings);
        if (result.IsConnected)
        {
            _settings.Model = result.Model;
            _settingsStore.Save(_settings);
            ConnectionBadge.Background = BrushFrom("#EAF8F0");
            ConnectionText.Foreground = BrushFrom("#18A058");
            ConnectionText.Text = string.IsNullOrWhiteSpace(result.Model)
                ? "LM Studio connected"
                : $"Connected · {result.Model}";
        }
        else
        {
            ConnectionBadge.Background = BrushFrom("#FFF7E6");
            ConnectionText.Foreground = BrushFrom("#835B14");
            ConnectionText.Text = result.Message;
        }
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_isCheckingUpdates)
        {
            return;
        }

        if (!_settings.CheckForUpdates || string.IsNullOrWhiteSpace(_settings.UpdateRepository))
        {
            if (manual)
            {
                System.Windows.MessageBox.Show(this, "Enable update checks and enter a GitHub repository in Settings.", "Updates", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return;
        }

        try
        {
            _isCheckingUpdates = true;
            var update = await _updateService.CheckLatestAsync(_settings);
            if (update is null)
            {
                if (manual)
                {
                    System.Windows.MessageBox.Show(this, "You are using the latest version.", "Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return;
            }

            _pendingUpdate = update;
            if (manual)
            {
                var answer = System.Windows.MessageBox.Show(
                    this,
                    $"Version {update.TagName} is available.\n\nDownload and install it now?",
                    "Updates",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (answer == MessageBoxResult.Yes)
                {
                    await DownloadAndInstallUpdateAsync(update);
                }

                return;
            }

            _notifyIcon.ShowBalloonTip(
                8000,
                "Local Translator update",
                $"Version {update.TagName} is available. Click to install.",
                Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            if (manual)
            {
                System.Windows.MessageBox.Show(this, ex.Message, "Update check failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            _isCheckingUpdates = false;
        }
    }

    private async Task DownloadAndInstallUpdateAsync(UpdateInfo update)
    {
        try
        {
            SetStatus("Downloading update...");
            var installerPath = await _updateService.DownloadInstallerAsync(update);
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            _exitRequested = true;
            Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Update install failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    private void ApplySettingsToUi()
    {
        SetLanguageButtonText(SourceLanguageButton, _settings.SourceLanguage);
        SetLanguageButtonText(TargetLanguageButton, _settings.TargetLanguage);
        _floatingWindow?.SetTargetLanguage(_settings.TargetLanguage);
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_settings)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            _settings = window.Settings;
            _settingsStore.Save(_settings);
            _keyboardShortcutService.Configure(_settings);
            ApplySettingsToUi();
            _ = CheckConnectionAsync();
            QueueAutoTranslate();
        }
    }

    private async void CheckConnection_Click(object sender, RoutedEventArgs e)
    {
        await CheckConnectionAsync();
    }

    private async void Translate_Click(object sender, RoutedEventArgs e)
    {
        _autoTranslateTimer.Stop();
        await TranslateAsync(SourceTextBox.Text);
    }

    private void OpenClipboard_Click(object sender, RoutedEventArgs e)
    {
        AddClipboardSnapshot();
        var window = new ClipboardHistoryWindow(_clipboardHistory)
        {
            Owner = this
        };

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedText))
        {
            SetSourceText(window.SelectedText);
            _autoTranslateTimer.Stop();
            _ = TranslateAsync(window.SelectedText);
        }
    }

    private void ClearSource_Click(object sender, RoutedEventArgs e)
    {
        _autoTranslateTimer.Stop();
        _translationVersion++;
        SourceTextBox.Clear();
        ResultTextBox.Clear();
        SetStatus("대기 중");
    }

    private void CopyResult_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ResultTextBox.Text))
        {
            System.Windows.Clipboard.SetText(ResultTextBox.Text);
            AddClipboardText(ResultTextBox.Text);
            SetStatus("대기 중");
        }
    }

    private async void InsertResult_Click(object sender, RoutedEventArgs e)
    {
        await InsertResultAsync();
    }

    private async Task InsertResultAsync()
    {
        var resultText = _floatingWindow is { IsVisible: true } && !string.IsNullOrWhiteSpace(_floatingWindow.ResultText)
            ? _floatingWindow.ResultText
            : ResultTextBox.Text;

        if (string.IsNullOrWhiteSpace(resultText))
        {
            SetStatus("?쎌엯??踰덉뿭臾몄씠 ?놁뒿?덈떎");
            return;
        }

        var previousClipboard = TryGetClipboardText();
        System.Windows.Clipboard.SetText(resultText);
        AddClipboardText(resultText);
        _floatingWindow?.Hide();
        await Task.Delay(80);

        await WaitForInsertShortcutReleaseAsync();

        var focused = true;
        if (_lastSourceTarget.WindowHandle != IntPtr.Zero)
        {
            focused = NativeInput.FocusWindow(_lastSourceTarget);
        }

        if (!focused)
        {
            SetStatus("대기 중");
            _floatingWindow?.SetError("?먮Ц 李쎌쑝濡??ъ빱?ㅻ? ?섎룎由ъ? 紐삵뻽?듬땲?? ?먮Ц 李쎌쓣 ?대┃?????ㅼ떆 ?쎌엯?섏꽭??");
            return;
        }

        await Task.Delay(160);
        System.Windows.Forms.SendKeys.SendWait("^v");
        await Task.Delay(260);

        if (!string.IsNullOrEmpty(previousClipboard))
        {
            System.Windows.Clipboard.SetText(previousClipboard);
        }

        SetStatus("대기 중");
    }

    private static async Task WaitForInsertShortcutReleaseAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(900);
        while (DateTimeOffset.UtcNow < deadline &&
               (NativeInput.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
                NativeInput.IsKeyDown(System.Windows.Input.Key.RightCtrl) ||
                NativeInput.IsKeyDown(System.Windows.Input.Key.Enter) ||
                NativeInput.IsKeyDown(System.Windows.Input.Key.Return)))
        {
            await Task.Delay(25);
        }
    }

    private void SwapLanguages_Click(object sender, RoutedEventArgs e)
    {
        var source = GetLanguageButtonText(SourceLanguageButton);
        var target = GetLanguageButtonText(TargetLanguageButton);
        if (source == "Auto")
        {
            source = "English";
        }

        SetLanguageButtonText(SourceLanguageButton, target);
        SetLanguageButtonText(TargetLanguageButton, source);
        SaveLanguagesFromUi();
    }

    private void SourceLanguage_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickLanguage(GetLanguageButtonText(SourceLanguageButton), includeAutoDetect: true, out var language))
        {
            SetLanguageButtonText(SourceLanguageButton, language);
            SaveLanguagesFromUi();
        }
    }

    private void TargetLanguage_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickLanguage(GetLanguageButtonText(TargetLanguageButton), includeAutoDetect: false, out var language))
        {
            SetLanguageButtonText(TargetLanguageButton, language);
            SaveLanguagesFromUi();
        }
    }

    private void SaveLanguagesFromUi()
    {
        _settings.SourceLanguage = GetLanguageButtonText(SourceLanguageButton);
        _settings.TargetLanguage = GetLanguageButtonText(TargetLanguageButton);
        _settingsStore.Save(_settings);
        _floatingWindow?.SetTargetLanguage(_settings.TargetLanguage);
        QueueAutoTranslate();
    }

    private void SourceTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressAutoTranslate)
        {
            return;
        }

        QueueAutoTranslate();
    }

    private void QueueAutoTranslate()
    {
        _autoTranslateTimer.Stop();
        if (string.IsNullOrWhiteSpace(SourceTextBox.Text))
        {
            ResultTextBox.Clear();
            SetStatus("대기 중");
            return;
        }

        SetStatus("대기 중");
        _autoTranslateTimer.Start();
    }

    private async void AutoTranslateTimer_Tick(object? sender, EventArgs e)
    {
        _autoTranslateTimer.Stop();
        await TranslateAsync(SourceTextBox.Text);
    }

    private void SetSourceText(string text)
    {
        _suppressAutoTranslate = true;
        SourceTextBox.Text = text;
        _suppressAutoTranslate = false;
    }

    private void AddClipboardSnapshot()
    {
        AddClipboardText(TryGetClipboardText());
    }

    private void AddClipboardText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        text = text.Trim();
        var existingIndex = _clipboardHistory.FindIndex(item => string.Equals(item, text, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            _clipboardHistory.RemoveAt(existingIndex);
        }

        _clipboardHistory.Insert(0, text);
        if (_clipboardHistory.Count > 30)
        {
            _clipboardHistory.RemoveRange(30, _clipboardHistory.Count - 30);
        }
    }

    private static string TryGetClipboardText()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : "";
        }
        catch
        {
            return "";
        }
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
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

    private static SolidColorBrush BrushFrom(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }
}
