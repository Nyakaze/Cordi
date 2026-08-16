using System;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxLinkEmbed
{
    public string Url { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? IconUrl { get; set; }
    public bool IsImage { get; set; }
    public bool MediaOnly { get; set; }

    public bool IsMedia => (IsImage || MediaOnly) && !string.IsNullOrEmpty(ImageUrl);

    public bool HasCard =>
        !IsMedia && (Title.Length > 0 || Description.Length > 0 || !string.IsNullOrEmpty(ImageUrl));

    public static ChatboxLinkEmbed ForImage(string url) => new()
    {
        Url = url,
        ImageUrl = url,
        IsImage = true,
        SiteName = HostOf(url),
    };

    public static string HostOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
}
