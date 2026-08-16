using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Cordi.Services.Chatbox;

public static class OpenGraphParser
{
    private static readonly RegexOptions Options =
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled;

    private static readonly Regex MetaTag = new("<meta\\b[^>]*>", Options);
    private static readonly Regex LinkTag = new("<link\\b[^>]*>", Options);
    private static readonly Regex TitleTag = new("<title[^>]*>(.*?)</title>", Options);
    private static readonly Regex Attribute =
        new("(?<name>[a-zA-Z:_-]+)\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s\"'>]+))", Options);

    public static ChatboxLinkEmbed? Parse(string html, string url, int maxTitle, int maxDescription)
    {
        var embed = new ChatboxLinkEmbed { Url = url, SiteName = ChatboxLinkEmbed.HostOf(url) };

        var title = string.Empty;
        var fallbackTitle = string.Empty;
        var description = string.Empty;
        var fallbackDescription = string.Empty;
        var image = string.Empty;
        var icon = string.Empty;
        var site = string.Empty;
        var video = string.Empty;
        var pageType = string.Empty;
        var card = string.Empty;

        foreach (Match tag in MetaTag.Matches(html))
        {
            var (key, content) = ReadMeta(tag.Value);
            if (key.Length == 0 || content.Length == 0) continue;

            switch (key)
            {
                case "og:title":
                case "twitter:title":
                    if (title.Length == 0) title = content;
                    break;
                case "og:description":
                case "twitter:description":
                    if (description.Length == 0) description = content;
                    break;
                case "description":
                    if (fallbackDescription.Length == 0) fallbackDescription = content;
                    break;
                case "og:image":
                case "og:image:secure_url":
                case "twitter:image":
                    if (image.Length == 0) image = content;
                    break;
                case "og:site_name":
                    if (site.Length == 0) site = content;
                    break;
                case "og:video":
                case "og:video:url":
                case "og:video:secure_url":
                case "twitter:player:stream":
                    if (video.Length == 0) video = content;
                    break;
                case "og:type":
                    if (pageType.Length == 0) pageType = content.ToLowerInvariant();
                    break;
                case "twitter:card":
                    if (card.Length == 0) card = content.ToLowerInvariant();
                    break;
            }
        }

        foreach (Match tag in LinkTag.Matches(html))
        {
            var (rel, href) = ReadLink(tag.Value);
            if (href.Length == 0) continue;
            if (!rel.Contains("icon", StringComparison.OrdinalIgnoreCase)) continue;
            if (rel.Contains("mask-icon", StringComparison.OrdinalIgnoreCase)) continue;

            icon = href;
            break;
        }

        var titleMatch = TitleTag.Match(html);
        if (titleMatch.Success) fallbackTitle = Clean(titleMatch.Groups[1].Value);

        embed.Title = Truncate(title.Length > 0 ? title : fallbackTitle, maxTitle);
        embed.Description = Truncate(description.Length > 0 ? description : fallbackDescription, maxDescription);
        var animated = Absolute(video, url);
        embed.ImageUrl = animated != null && IsGif(animated) ? animated : Absolute(image, url);
        embed.IconUrl = Absolute(icon.Length > 0 ? icon : "/favicon.ico", url);
        embed.MediaOnly = embed.ImageUrl != null && IsMediaPage(pageType, card, embed.ImageUrl);
        if (site.Length > 0) embed.SiteName = Truncate(site, 48);

        return embed.IsMedia || embed.HasCard ? embed : null;
    }

    private static bool IsMediaPage(string pageType, string card, string image)
    {
        if (IsGif(image)) return true;
        if (card is "photo" or "player") return true;

        return pageType.StartsWith("video.", StringComparison.Ordinal)
               || pageType is "video" or "image" or "gif" or "photo";
    }

    private static bool IsGif(string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;
        return path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
    }

    private static (string Key, string Content) ReadMeta(string tag)
    {
        var key = string.Empty;
        var content = string.Empty;

        foreach (Match attribute in Attribute.Matches(tag))
        {
            var name = attribute.Groups["name"].Value.ToLowerInvariant();
            var value = attribute.Groups["value"].Value;

            switch (name)
            {
                case "property":
                case "name":
                case "itemprop":
                    if (key.Length == 0) key = value.Trim().ToLowerInvariant();
                    break;
                case "content":
                    content = Clean(value);
                    break;
            }
        }

        return (key, content);
    }

    private static (string Rel, string Href) ReadLink(string tag)
    {
        var rel = string.Empty;
        var href = string.Empty;

        foreach (Match attribute in Attribute.Matches(tag))
        {
            var name = attribute.Groups["name"].Value.ToLowerInvariant();
            var value = attribute.Groups["value"].Value;

            if (name == "rel") rel = value;
            else if (name == "href") href = Clean(value);
        }

        return (rel, href);
    }

    private static string Clean(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var decoded = WebUtility.HtmlDecode(value);
        return Regex.Replace(decoded, "\\s+", " ").Trim();
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max) return value;
        return value[..Math.Max(0, max - 1)].TrimEnd() + "…";
    }

    private static string? Absolute(string candidate, string baseUrl)
    {
        if (string.IsNullOrEmpty(candidate)) return null;
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var absolute))
            return absolute.Scheme is "http" or "https" ? absolute.AbsoluteUri : null;

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var root)
               && Uri.TryCreate(root, candidate, out var combined)
               && combined.Scheme is "http" or "https"
            ? combined.AbsoluteUri
            : null;
    }
}
