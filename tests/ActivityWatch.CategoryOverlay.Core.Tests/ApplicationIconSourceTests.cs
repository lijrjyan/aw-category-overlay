using System.Runtime.CompilerServices;

namespace ActivityWatch.CategoryOverlay.Core.Tests;

public sealed class ApplicationIconSourceTests
{
    [Fact]
    public void Windows_project_embeds_the_category_overlay_icon()
    {
        var project = File.ReadAllText(GetWindowsProjectPath(
            "ActivityWatch.CategoryOverlay.Windows.csproj"));

        Assert.Contains(
            "<ApplicationIcon>Assets\\category-overlay.ico</ApplicationIcon>",
            project);
    }

    [Fact]
    public void Tray_uses_the_running_executables_icon()
    {
        var source = File.ReadAllText(GetWindowsProjectPath(
            "Services/TrayService.cs"));

        Assert.Contains(
            "Icon.ExtractAssociatedIcon(Environment.ProcessPath!)",
            source);
        Assert.Contains("_ownedIcon?.Dispose();", source);
    }

    [Fact]
    public void Icon_asset_contains_multiple_sizes()
    {
        var iconPath = GetWindowsProjectPath("Assets/category-overlay.ico");
        var header = File.ReadAllBytes(iconPath);

        Assert.True(header.Length > 6);
        Assert.Equal(0, BitConverter.ToUInt16(header, 0));
        Assert.Equal(1, BitConverter.ToUInt16(header, 2));
        Assert.True(BitConverter.ToUInt16(header, 4) >= 8);
    }

    private static string GetWindowsProjectPath(
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
