using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Cordi.Configuration.QoLBar;
using Newtonsoft.Json;

namespace Cordi.Services.QoLBar;

public class BarImportExportService
{
    public string ExportBar(BarCfg bar, bool saveAllValues)
    {
        var settings = new JsonSerializerSettings
        {
            DefaultValueHandling = saveAllValues ? DefaultValueHandling.Include : DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };
        var json = JsonConvert.SerializeObject(bar, settings);
        return CompressString(json);
    }

    public string ExportShortcut(ShCfg shortcut, bool saveAllValues)
    {
        var settings = new JsonSerializerSettings
        {
            DefaultValueHandling = saveAllValues ? DefaultValueHandling.Include : DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };
        var json = JsonConvert.SerializeObject(shortcut, settings);
        return CompressString(json);
    }

    public string ExportConditionSet(CndSetCfg conditionSet)
    {
        var json = JsonConvert.SerializeObject(conditionSet);
        return CompressString(json);
    }

    public (BarCfg? bar, ShCfg? shortcut, CndSetCfg? conditionSet) TryImport(string importStr)
    {
        if (string.IsNullOrWhiteSpace(importStr))
            return (null, null, null);

        try
        {
            var json = DecompressString(importStr);

            try
            {
                var bar = JsonConvert.DeserializeObject<BarCfg>(json);
                if (bar?.ShortcutList != null)
                    return (bar, null, null);
            }
            catch { }

            try
            {
                var shortcut = JsonConvert.DeserializeObject<ShCfg>(json);
                if (shortcut?.Name != null)
                    return (null, shortcut, null);
            }
            catch { }

            try
            {
                var conditionSet = JsonConvert.DeserializeObject<CndSetCfg>(json);
                if (conditionSet?.Conditions != null)
                    return (null, null, conditionSet);
            }
            catch { }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Failed to import QoLBar data");
        }

        return (null, null, null);
    }

    public static string CompressString(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var msi = new MemoryStream(bytes);
        using var mso = new MemoryStream();
        using (var gs = new GZipStream(mso, CompressionMode.Compress))
        {
            msi.CopyTo(gs);
        }
        return Convert.ToBase64String(mso.ToArray());
    }

    public static string DecompressString(string compressedText)
    {
        var bytes = Convert.FromBase64String(compressedText);
        using var msi = new MemoryStream(bytes);
        using var mso = new MemoryStream();
        using (var gs = new GZipStream(msi, CompressionMode.Decompress))
        {
            gs.CopyTo(mso);
        }
        return Encoding.UTF8.GetString(mso.ToArray());
    }
}
