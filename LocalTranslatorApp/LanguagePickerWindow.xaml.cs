using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LocalTranslatorApp.Models;
using LocalTranslatorApp.Services;

namespace LocalTranslatorApp;

public partial class LanguagePickerWindow : Window
{
    private readonly IReadOnlyList<LanguageOption> _languages;
    private readonly string _initialLanguage;

    public LanguagePickerWindow(string currentLanguage, bool includeAutoDetect)
    {
        InitializeComponent();
        PreviewKeyDown += Window_PreviewKeyDown;
        _initialLanguage = string.IsNullOrWhiteSpace(currentLanguage) ? "Korean" : currentLanguage;
        _languages = includeAutoDetect ? LanguageCatalog.SourceLanguages : LanguageCatalog.TargetLanguages;
        Loaded += (_, _) =>
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        };
        ApplyFilter();
    }

    public string SelectedLanguage { get; private set; } = "";

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Clear();
        SearchTextBox.Focus();
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        CommitSelection();
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

    private void LanguageListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        CommitSelection();
    }

    private void LanguageListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            CommitSelection();
            e.Handled = true;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchTextBox.Text.Trim();
        var filtered = _languages
            .Where(language => language.Matches(query))
            .OrderBy(language => language.Name == "Auto" ? "" : language.Name)
            .ToArray();

        LanguageListBox.ItemsSource = filtered;
        CountTextBlock.Text = $"{filtered.Length} languages";

        var selected = filtered.FirstOrDefault(language =>
            string.Equals(language.Name, _initialLanguage, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            LanguageListBox.SelectedItem = selected;
            LanguageListBox.ScrollIntoView(selected);
        }
        else if (filtered.Length > 0)
        {
            LanguageListBox.SelectedIndex = 0;
        }
    }

    private void CommitSelection()
    {
        if (LanguageListBox.SelectedItem is not LanguageOption language)
        {
            return;
        }

        SelectedLanguage = language.Name;
        DialogResult = true;
    }
}
