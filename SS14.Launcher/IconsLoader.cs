using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SS14.Launcher;

public static class IconsLoader
{
    private static readonly (string path, string resource)[] Icons =
    {
        ("info-icons/discord.png", "InfoIcon-discord"),
        ("info-icons/forum.png", "InfoIcon-forum"),
        ("info-icons/github.png", "InfoIcon-github"),
        ("info-icons/web.png", "InfoIcon-web"),
        ("info-icons/wiki.png", "InfoIcon-wiki"),
        ("info-icons/telegram.png", "InfoIcon-telegram"),
        ("button-icons/refresh.png", "ButtonIcon-refresh"),
        ("button-icons/plus.png", "ButtonIcon-plus"),
        ("button-icons/star.png", "ButtonIcon-star"),
        ("button-icons/star-outline.png", "ButtonIcon-star-outline"),
    };

    public static void Load(App app)
    {
        foreach (var (path, resource) in Icons)
        {
            using var file = AssetLoader.Open(new Uri($"avares://SS14.Launcher/Assets/{path}"));
            var bitmap = new Bitmap(file);
            app.Resources.Add(resource, bitmap);
        }
    }
}
