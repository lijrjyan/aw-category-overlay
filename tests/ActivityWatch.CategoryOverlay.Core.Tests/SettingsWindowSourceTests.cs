using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class SettingsWindowSourceTests
{
    [Fact]
    public void Non_modal_settings_window_closes_without_dialog_result()
    {
        var source = File.ReadAllText(GetSourcePath(
            "Views/SettingsWindow.xaml.cs"));

        Assert.DoesNotContain("DialogResult", source);
        Assert.Equal(2, CountOccurrences(source, "Close();"));
        Assert.Contains("SetValidationMessage", source);
    }

    [Fact]
    public void Settings_markup_exposes_font_size_and_opacity_values()
    {
        var markup = File.ReadAllText(GetSourcePath(
            "Views/SettingsWindow.xaml"));

        Assert.Contains("Bar font size", markup);
        Assert.Contains("BarFontSize", markup);
        Assert.Contains("StringFormat={}{0:F2}", markup);
    }

    [Fact]
    public void Threshold_editor_shows_minutes_unit_after_minutes_value()
    {
        var document = XDocument.Load(GetSourcePath(
            "Views/SettingsWindow.xaml"));
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var minutesUnit = document
            .Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "m");

        Assert.Equal("5", minutesUnit.Attribute("Grid.Column")?.Value);
    }

    [Fact]
    public void Applying_settings_does_not_await_category_refresh()
    {
        var source = File.ReadAllText(GetSourcePath("App.xaml.cs"));
        var start = source.IndexOf(
            "private async Task ApplySettingsAsync",
            StringComparison.Ordinal);
        var end = source.IndexOf(
            "private async Task RefreshAfterSettingsAsync",
            start,
            StringComparison.Ordinal);
        var method = source[start..end];

        Assert.DoesNotContain("await RefreshWithRetryAsync(", method);
        Assert.Contains("_ = RefreshAfterSettingsAsync(", method);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string GetSourcePath(
        string relativePath,
        [CallerFilePath] string testFilePath = "")
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(testFilePath)!,
                "../../src/ActivityWatch.CategoryOverlay.Windows",
                relativePath));
    }
}
