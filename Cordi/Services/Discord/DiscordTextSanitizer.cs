using System.Text;

namespace Cordi.Services.Discord;

public static class DiscordTextSanitizer
{
    /// <summary>
    /// Sanitizes FFXIV special characters for Discord display.
    /// FFXIV uses Unicode Private Use Area characters for its custom font.
    /// The encoding adds 0xE010 to the ASCII character code.
    /// Converted characters are uppercased to make them stand out.
    /// For example: 's' (0x73) becomes 0xE083, 'k' (0x6B) becomes 0xE07B.
    /// </summary>
    public static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length);

        foreach (char c in input)
        {
            int codePoint = c;

            // FFXIV special character range: characters encoded with +0xE010 offset
            // This covers printable ASCII characters (space through ~)
            if (codePoint >= 0xE020 && codePoint <= 0xE0FF)
            {
                // Subtract 0xE010 to get the original ASCII character
                // Example: 0xE083 - 0xE010 = 0x73 ('s')
                char normalChar = (char)(codePoint - 0xE010);

                // Convert to uppercase to make it stand out
                sb.Append(char.ToUpper(normalChar));
            }
            else
            {
                // Normal character, keep as-is
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
