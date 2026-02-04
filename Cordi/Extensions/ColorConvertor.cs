namespace Cordi.Extensions;

public static class ColorConvertor
{
    public static System.Numerics.Vector4 ToVector4(this string hex)
    {
        if (hex.StartsWith("#"))
            hex = hex[1..];

        if (hex.Length != 6 && hex.Length != 8)
            throw new System.ArgumentException("Hex string must be 6 or 8 characters long.");

        byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
        byte a = 255;

        if (hex.Length == 8)
            a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

        return new System.Numerics.Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}
