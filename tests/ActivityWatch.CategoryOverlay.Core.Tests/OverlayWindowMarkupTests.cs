using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class OverlayWindowMarkupTests
{
    [Fact]
    public void Total_duration_and_last_refresh_use_identical_typography()
    {
        var document = XDocument.Load(GetOverlayXamlPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var totalDuration = document
            .Descendants(presentation + "TextBlock")
            .Single(element =>
                (string?)element.Attribute("Text") == "{Binding TotalDurationText}");
        var lastRefresh = document
            .Descendants(presentation + "TextBlock")
            .Single(element =>
                (string?)element.Attribute("Text") == "{Binding LastRefreshText}");

        foreach (var text in new[] { totalDuration, lastRefresh })
        {
            Assert.Equal("Segoe UI", text.Attribute("FontFamily")?.Value);
            Assert.Equal("16", text.Attribute("FontSize")?.Value);
            Assert.Equal("Normal", text.Attribute("FontWeight")?.Value);
        }
    }

    [Fact]
    public void Header_labels_last_update_and_places_total_on_the_right()
    {
        var document = XDocument.Load(GetOverlayXamlPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var textBlocks = document
            .Descendants(presentation + "TextBlock")
            .ToList();
        var lastRefresh = textBlocks.Single(element =>
            (string?)element.Attribute("Text") == "{Binding LastRefreshText}");
        var totalDuration = textBlocks.Single(element =>
            (string?)element.Attribute("Text") == "{Binding TotalDurationText}");

        Assert.Contains(
            textBlocks,
            element => (string?)element.Attribute("Text") == "LAST UPDATE · ");
        Assert.DoesNotContain(
            textBlocks,
            element => (string?)element.Attribute("Text") == "TODAY · ");
        Assert.Equal(
            lastRefresh.Parent,
            textBlocks.Single(element =>
                (string?)element.Attribute("Text") == "LAST UPDATE · ").Parent);
        Assert.Equal(
            "Right",
            totalDuration.Attribute("HorizontalAlignment")?.Value);
    }

    [Fact]
    public void Requirement_label_appears_after_the_divider()
    {
        var document = XDocument.Load(GetOverlayXamlPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var requirement = document
            .Descendants(presentation + "TextBlock")
            .Single(element =>
                (string?)element.Attribute("Text") == "{Binding RequirementText}");
        var rightColumn = requirement
            .Ancestors(presentation + "Grid")
            .First(element => element.Attribute("Grid.Column") is not null);

        Assert.Equal("1", rightColumn.Attribute("Grid.Column")?.Value);
        Assert.Equal(
            "{StaticResource CategoryBarText}",
            requirement.Attribute("Style")?.Value);
        Assert.Equal("Bold", requirement.Attribute("FontWeight")?.Value);
    }

    [Fact]
    public void View_model_exposes_total_and_requirement_text()
    {
        var source = File.ReadAllText(GetOverlayViewModelPath());

        Assert.Contains("TotalDurationText", source);
        Assert.Contains("RequirementText", source);
        Assert.Contains(
            "TotalDurationText = OverlayText.FormatDuration(state.TotalActiveTime)",
            source);
        Assert.Contains(
            "ActualText = OverlayText.FormatDuration(row.Actual)",
            source);
        Assert.Contains(
            "ThresholdText = OverlayText.FormatDuration(row.Target.Threshold)",
            source);
        Assert.DoesNotContain("HeaderText", source);
        Assert.DoesNotContain("TotalText", source);
    }

    [Fact]
    public void Apply_updates_existing_rows_without_clearing_the_collection()
    {
        var source = File.ReadAllText(GetOverlayViewModelPath());

        Assert.DoesNotContain("Rows.Clear()", source);
        Assert.Contains("Rows[index].Apply(state.Rows[index])", source);
        Assert.Contains("Rows.RemoveAt(Rows.Count - 1)", source);
    }

    [Fact]
    public void Progress_value_reads_the_view_model_without_writing_back()
    {
        var document = XDocument.Load(GetOverlayXamlPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var progress = document
            .Descendants(presentation + "ProgressBar")
            .Single();

        Assert.Equal(
            "{Binding FillFraction, Mode=OneWay}",
            progress.Attribute("Value")?.Value);
    }

    [Fact]
    public void Overlay_uses_approved_width_and_configurable_bar_font_size()
    {
        var document = XDocument.Load(GetOverlayXamlPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        var style = document
            .Descendants(presentation + "Style")
            .Single(element =>
                (string?)element.Attribute(x + "Key") == "CategoryBarText");
        var fontSizeSetter = style
            .Elements(presentation + "Setter")
            .Single(element =>
                (string?)element.Attribute("Property") == "FontSize");
        var fontSizeBinding = fontSizeSetter
            .Descendants(presentation + "Binding")
            .Single();
        var styledTextBlocks = document
            .Descendants(presentation + "TextBlock")
            .Count(element =>
                (string?)element.Attribute("Style") ==
                "{StaticResource CategoryBarText}");

        Assert.Equal("440", document.Root?.Attribute("Width")?.Value);
        Assert.Equal(
            "DataContext.BarFontSize",
            fontSizeBinding.Attribute("Path")?.Value);
        Assert.Equal(4, styledTextBlocks);
    }

    private static string GetOverlayXamlPath(
        [CallerFilePath] string testFilePath = "")
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(testFilePath)!,
                "../../src/ActivityWatch.CategoryOverlay.Windows/Views/OverlayWindow.xaml"));
    }

    private static string GetOverlayViewModelPath(
        [CallerFilePath] string testFilePath = "")
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(testFilePath)!,
                "../../src/ActivityWatch.CategoryOverlay.Windows/ViewModels/OverlayViewModel.cs"));
    }
}
