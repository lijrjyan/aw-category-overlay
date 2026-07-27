using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class OverlayWindowMarkupTests
{
    [Fact]
    public void Category_bar_text_style_uses_approved_font_size()
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
        var fontSize = style
            .Elements(presentation + "Setter")
            .Single(element =>
                (string?)element.Attribute("Property") == "FontSize")
            .Attribute("Value")?.Value;
        var styledTextBlocks = document
            .Descendants(presentation + "TextBlock")
            .Count(element =>
                (string?)element.Attribute("Style") ==
                "{StaticResource CategoryBarText}");

        Assert.Equal("13", fontSize);
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
