using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class OverlayWindowMarkupTests
{
    [Fact]
    public void Header_shows_total_time_instead_of_top_categories_label()
    {
        var document = XDocument.Load(GetOverlayXamlPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var header = document
            .Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "{Binding HeaderText}");

        Assert.NotNull(header);
        Assert.DoesNotContain(
            document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "TODAY · TOP CATEGORIES");
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
        Assert.Equal(3, styledTextBlocks);
    }

    private static string GetOverlayXamlPath(
        [CallerFilePath] string testFilePath = "")
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(testFilePath)!,
                "../../src/ActivityWatch.CategoryOverlay.Windows/Views/OverlayWindow.xaml"));
    }
}
