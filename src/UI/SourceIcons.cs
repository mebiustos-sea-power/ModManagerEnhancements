using System.IO;
using Noesis;

namespace ModManagerEnhancements;

internal static class SourceIcons
{
    internal const string SteamResource = "ModManagerEnhancements.SteamSource";
    internal const string SizeResource = "ModManagerEnhancements.SourceIconSize";
    private static BitmapSource? steam;
    private static DataTemplate? steamTemplate, localTemplate;
    internal static BitmapSource Steam => steam ??= LoadSteam();

    private static BitmapSource LoadSteam()
    {
        using var stream = typeof(SourceIcons).Assembly.GetManifestResourceStream("ModManagerEnhancements.Steam.pbgra")
            ?? throw new FileNotFoundException("The embedded Steam source icon is missing.");
        using var data = new MemoryStream();
        stream.CopyTo(data);
        var pixels = data.ToArray();
        if (pixels.Length != 48 * 48 * 4) throw new InvalidDataException("The embedded Steam source icon has an unexpected size.");
        return BitmapSource.Create(48, 48, 96, 96, pixels, 48 * 4, BitmapSource.Format.BGRA8);
    }

    internal static DataTemplate Template(bool isSteam)
    {
        if (isSteam)
            return steamTemplate ??= Parse("<Image Source='{DynamicResource ModManagerEnhancements.SteamSource}' " +
                "Width='{DynamicResource ModManagerEnhancements.SourceIconSize}' Height='{DynamicResource ModManagerEnhancements.SourceIconSize}' Stretch='Uniform'/>");
        return localTemplate ??= Parse("<Viewbox Width='{DynamicResource ModManagerEnhancements.SourceIconSize}' " +
            "Height='{DynamicResource ModManagerEnhancements.SourceIconSize}' Stretch='Uniform'><Canvas Width='16' Height='16' Background='Transparent'>" +
            "<Path Data='M 1,4 L 6,4 L 8,6 L 15,6 L 15,13 L 1,13 Z M 1,6 L 8,6' Stroke='#949EA6' StrokeThickness='1.25'/>" +
            "</Canvas></Viewbox>");
    }

    private static DataTemplate Parse(string content) => (DataTemplate)GUI.ParseXaml(
        "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" + content + "</DataTemplate>");
}
