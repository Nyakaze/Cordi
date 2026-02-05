using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Cordi.Core;

namespace Cordi.Services.Features;

public record GearInfo(int ItemLevel, string? ClassIconUrl = null);

public record RaidEncounter(
    string RaidName,
    double? BestPercent,
    int ClearCount
);

public record RaidActivity(
    IReadOnlyList<RaidEncounter> Encounters
);

public class TomestoneService : IDisposable
{
    private static readonly IPluginLog Logger = Service.Log;
    private readonly CordiPlugin _plugin;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, GearInfo> _gearLevelCache = new();
    private readonly ConcurrentDictionary<string, string> _lodestoneIdCache = new();
    private readonly ConcurrentDictionary<string, RaidActivity> _raidActivityCache = new();

    // Mapping from FFXIV class/job abbreviations to Tomestone API format
    private static readonly Dictionary<string, string> ClassJobMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // Tanks
        ["PLD"] = "Paladin",
        ["WAR"] = "Warrior",
        ["DRK"] = "DarkKnight",
        ["GNB"] = "Gunbreaker",
        // Healers
        ["WHM"] = "WhiteMage",
        ["SCH"] = "Scholar",
        ["AST"] = "Astrologian",
        ["SGE"] = "Sage",
        // Melee DPS
        ["MNK"] = "Monk",
        ["DRG"] = "Dragoon",
        ["NIN"] = "Ninja",
        ["SAM"] = "Samurai",
        ["RPR"] = "Reaper",
        ["VPR"] = "Viper",
        // Physical Ranged DPS
        ["BRD"] = "Bard",
        ["MCH"] = "Machinist",
        ["DNC"] = "Dancer",
        // Magical Ranged DPS
        ["BLM"] = "BlackMage",
        ["SMN"] = "Summoner",
        ["RDM"] = "RedMage",
        ["PCT"] = "Pictomancer",
        ["BLU"] = "BlueMage"
    };

    public TomestoneService(CordiPlugin plugin)
    {
        _plugin = plugin;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Cordi-FFXIV-Plugin/1.0");
    }

    public Task InitializeAsync()
    {
        Logger.Info("TomestoneService initialized (using JSON API for gear levels).");
        return Task.CompletedTask;
    }



    public async Task<GearInfo> GetAverageItemLevelAsync(string name, string world, string? classJob = null)
    {
        var key = $"{name}@{world}";

        if (_gearLevelCache.TryGetValue(key, out var cachedLevel))
        {
            return cachedLevel;
        }

        try
        {
            // Try tomestone.gg JSON API first
            var gearInfo = await GetItemLevelFromTomestoneAsync(name, world, classJob);
            if (gearInfo.ItemLevel > 0)
            {
                _gearLevelCache[key] = gearInfo;
                Logger.Info($"[Tomestone] Got gear level from JSON API for {name}@{world}: {gearInfo.ItemLevel}");
                return gearInfo;
            }

            // Fall back to Lodestone if tomestone.gg doesn't have data
            Logger.Debug($"Tomestone.gg unavailable for {name}@{world}, trying Lodestone");
            var lodestoneLevel = await _plugin.Lodestone.GetAverageItemLevelAsync(name, world);
            if (lodestoneLevel > 0)
            {
                var lodestoneGearInfo = new GearInfo(lodestoneLevel);
                _gearLevelCache[key] = lodestoneGearInfo;
                Logger.Debug($"Got gear level from Lodestone for {name}@{world}: {lodestoneLevel}");
                return lodestoneGearInfo;
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Failed to get gear level for {name}@{world}: {ex.Message}");
        }

        return new GearInfo(0);
    }

    private async Task<GearInfo> GetItemLevelFromTomestoneAsync(string name, string world, string? classJob)
    {
        var key = $"{name}@{world}";

        // Try to get cached Lodestone ID first
        if (!_lodestoneIdCache.TryGetValue(key, out var lodestoneId))
        {
            // Get Lodestone ID from LodestoneService
            lodestoneId = await _plugin.Lodestone.GetLodestoneIdAsync(name, world);

            if (string.IsNullOrEmpty(lodestoneId))
            {
                Logger.Debug($"Cannot fetch from tomestone.gg - no Lodestone ID for {name}@{world}");
                return new GearInfo(0);
            }

            // Cache the ID
            _lodestoneIdCache[key] = lodestoneId;
        }

        // Create character slug (lowercase name with spaces->hyphens, no apostrophes)
        var slug = name.ToLower().Replace(" ", "-").Replace("'", "");

        // Construct tomestone.gg JSON API URL
        var url = $"https://tomestone.gg/character-contents/{lodestoneId}/{slug}";

        Logger.Info($"[Tomestone] Fetching JSON: {url}");

        try
        {
            var response = await _httpClient.GetAsync(url);

            Logger.Info($"[Tomestone] Response: {response.StatusCode} for {name}@{world}");

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Logger.Info($"[Tomestone] 404 Not Found - Character {name}@{world} not on tomestone.gg (ID: {lodestoneId})");
                }
                else
                {
                    Logger.Warning($"[Tomestone] HTTP {response.StatusCode} for {url}");
                }
                return new GearInfo(0);
            }

            var json = await response.Content.ReadAsStringAsync();
            Logger.Info($"[Tomestone] Successfully fetched JSON ({json.Length} bytes) for {name}@{world}");

            // Parse JSON to get item level for specified class
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("profile", out var profile))
            {
                // If classJob specified, look in gearSetList
                if (!string.IsNullOrEmpty(classJob) && profile.TryGetProperty("gearSetList", out var gearSetList))
                {
                    // Convert abbreviation to full name (e.g., DRK → DarkKnight)
                    var classJobFullName = ClassJobMapping.TryGetValue(classJob, out var fullName)
                        ? fullName
                        : classJob; // Fall back to original if not found

                    Logger.Debug($"[Tomestone] Looking for class {classJob} (mapped to: {classJobFullName})");

                    foreach (var gearSet in gearSetList.EnumerateArray())
                    {
                        if (gearSet.TryGetProperty("id", out var id) &&
                            id.GetString()?.Equals(classJobFullName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (gearSet.TryGetProperty("name", out var nameField))
                            {
                                var nameStr = nameField.GetString();
                                if (!string.IsNullOrEmpty(nameStr))
                                {
                                    var parts = nameStr.Split(' ');
                                    if (parts.Length > 0 && int.TryParse(parts[0], out var level))
                                    {
                                        // Extract icon URL if available
                                        string? iconUrl = null;
                                        if (gearSet.TryGetProperty("icon", out var iconField))
                                        {
                                            iconUrl = iconField.GetString();
                                        }

                                        Logger.Info($"[Tomestone] Extracted item level: {level} for {classJobFullName} ({name}@{world})");
                                        return new GearInfo(level, iconUrl);
                                    }
                                }
                            }
                        }
                    }
                    Logger.Debug($"[Tomestone] Could not find {classJobFullName} in gearSetList for {name}@{world}");
                }

                // Fallback to currently equipped gear if no class specified
                if (profile.TryGetProperty("gearSetAndAttributes", out var gearSetAndAttributes))
                {
                    if (gearSetAndAttributes.TryGetProperty("gearSet", out var gearSet))
                    {
                        if (gearSet.TryGetProperty("itemLevel", out var itemLevel))
                        {
                            var level = itemLevel.GetInt32();
                            Logger.Info($"[Tomestone] Extracted currently equipped item level: {level} for {name}@{world}");
                            return new GearInfo(level); // No class icon for fallback
                        }
                    }
                }
            }

            Logger.Debug($"[Tomestone] Could not find itemLevel in JSON for {name}@{world}");
            return new GearInfo(0);
        }
        catch (Exception ex)
        {
            Logger.Debug($"Failed to fetch tomestone.gg JSON for {name}@{world}: {ex.Message}");
            return new GearInfo(0);
        }
    }

    public async Task<RaidActivity> GetRaidActivityAsync(string name, string world, string lodestoneId, string slug)
    {
        var key = $"{name}@{world}";
        var expansion = "dawntrail";
        var league = "all";
        var zone = "aac-heavyweight-savage";

        // Check cache first
        if (_raidActivityCache.TryGetValue(key, out var cachedActivity))
        {
            Logger.Debug($"[Tomestone] Using cached raid activity for {name}@{world}");
            return cachedActivity;
        }

        try
        {
            // Aggregate raid data across all pages
            var raidData = new Dictionary<string, (double? bestPercent, int clearCount)>();
            const int maxPages = 50;
            int currentPage = 1;

            while (currentPage <= maxPages)
            {
                // Build URL - page 1 has no page param, page 2+ includes &page=X
                var url = currentPage == 1
                    ? $"https://tomestone.gg/character-contents/{lodestoneId}/{slug}/activity?category=raids&expansion={expansion}&league={league}&zone={zone}"
                    : $"https://tomestone.gg/character-contents/{lodestoneId}/{slug}/activity?category=raids&expansion={expansion}&league={league}&page={currentPage}&zone={zone}";

                Logger.Debug($"[Tomestone] Fetching raid activity page {currentPage} for {name}@{world}: {url}");

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Logger.Debug($"[Tomestone] No raid activity found for {name}@{world}");
                    }
                    else
                    {
                        Logger.Warning($"[Tomestone] HTTP {response.StatusCode} for raid activity: {url}");
                    }
                    break;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // Navigate to the data array: activities.activities.activities.paginator.data
                if (!root.TryGetProperty("activities", out var activities1) ||
                    !activities1.TryGetProperty("activities", out var activities2) ||
                    !activities2.TryGetProperty("activities", out var activities3) ||
                    !activities3.TryGetProperty("paginator", out var paginator) ||
                    !paginator.TryGetProperty("data", out var data) ||
                    data.GetArrayLength() == 0)
                {
                    Logger.Debug($"[Tomestone] No more data on page {currentPage}, stopping pagination");
                    break;
                }

                // Process each activity
                foreach (var item in data.EnumerateArray())
                {
                    if (!item.TryGetProperty("activity", out var activityData))
                        continue;

                    // Get raid name from encounter.instanceContentLocalizedName
                    if (!activityData.TryGetProperty("encounter", out var encounter) ||
                        !encounter.TryGetProperty("instanceContentLocalizedName", out var raidNameProp))
                        continue;

                    var raidName = raidNameProp.GetString();
                    if (string.IsNullOrEmpty(raidName))
                        continue;

                    // Initialize raid data if not exists
                    if (!raidData.ContainsKey(raidName))
                    {
                        raidData[raidName] = (null, 0);
                    }

                    var currentData = raidData[raidName];

                    // Parse bestPercent (it's a string like "56.61%")
                    if (activityData.TryGetProperty("bestPercent", out var bestPercentProp))
                    {
                        var bestPercentStr = bestPercentProp.GetString();
                        if (!string.IsNullOrEmpty(bestPercentStr) && bestPercentStr.EndsWith("%"))
                        {
                            var percentValue = bestPercentStr.TrimEnd('%');
                            if (double.TryParse(percentValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bestPercent))
                            {
                                if (currentData.bestPercent == null || bestPercent < currentData.bestPercent.Value)
                                {
                                    currentData.bestPercent = bestPercent;
                                }
                            }
                        }
                    }

                    // Get kill count
                    if (activityData.TryGetProperty("killsCount", out var killsCountProp))
                    {
                        var killsCount = killsCountProp.GetInt32();
                        currentData.clearCount += killsCount;
                    }

                    raidData[raidName] = currentData;
                }

                Logger.Debug($"[Tomestone] Processed page {currentPage} with {data.GetArrayLength()} activities");
                currentPage++;
            }

            // Convert to RaidEncounter list
            var raidEncounters = raidData
                .Select(kvp => new RaidEncounter(kvp.Key, kvp.Value.bestPercent, kvp.Value.clearCount))
                .ToList();

            Logger.Info($"[Tomestone] Found {raidEncounters.Count} raids for {name}@{world}");

            var activity = new RaidActivity(raidEncounters);

            // Cache the result
            _raidActivityCache[key] = activity;

            return activity;
        }
        catch (Exception ex)
        {
            Logger.Error($"[Tomestone] Failed to get raid activity for {name}@{world}: {ex.Message}");
            return new RaidActivity(Array.Empty<RaidEncounter>());
        }
    }

    public void ClearCache()
    {
        _gearLevelCache.Clear();
        _lodestoneIdCache.Clear();
        _raidActivityCache.Clear();
        Logger.Info("Cleared Tomestone cache.");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
