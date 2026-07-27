using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class OverlayWindowMarkupTests
{
    [Fact]
    public void Header_and_last_refresh_use_unified_16px_text()
    {
        var document = XDocument.Load(GetOverlayXamlPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var header = document
            .Descendants(presentation + "TextBlock")
            .Single(element =>
                (string?)element.Attribute("Text") == "{Binding HeaderText}");
        var lastRefresh = document
            .Descendants(presentation + "TextBlock")
            .Single(element =>
                (string?)element.Attribute("Text") == "{Binding LastRefreshText}");

        Assert.Equal("16", header.Attribute("FontSize")?.Value);
        Assert.Equal("16", lastRefresh.Attribute("FontSize")?.Value);
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

        Assert.Contains("HeaderText", source);
        Assert.Contains("RequirementText", source);
        Assert.DoesNotContain("TotalText", source);
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
