using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using DSharpPlus.Entities;
using Lumina.Excel.Sheets;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Cordi.Services.Features;

public class PartyService : IDisposable
{
    private readonly CordiPlugin plugin;
    private readonly NotificationManager notificationService;
    private readonly HashSet<ulong> currentPartyMembers = new();
    private DateTime _lastPartyCheck = DateTime.MinValue;
    private readonly TimeSpan _partyCheckInterval = TimeSpan.FromMilliseconds(500);

    public record PartyMemberInfo(string Name, string World, uint JobId)
    {
        public int? ItemLevel { get; set; }
        public RaidActivity? RaidActivity { get; set; }
        public string? LodestoneId { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
    private List<PartyMemberInfo> _partyMembers = new();
    public IReadOnlyList<PartyMemberInfo> PartyMembers => _partyMembers;

    public PartyService(CordiPlugin plugin, NotificationManager notificationService)
    {
        this.plugin = plugin;
        this.notificationService = notificationService;
        Service.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!plugin.Config.Party.Enabled)
        {
            if (currentPartyMembers.Count > 0) currentPartyMembers.Clear();
            if (_partyMembers.Count > 0) _partyMembers.Clear();
            return;
        }

        if (DateTime.Now - _lastPartyCheck < _partyCheckInterval)
            return;

        if (Service.Condition[ConditionFlag.BetweenAreas])
            return;

        _lastPartyCheck = DateTime.Now;

        if (Service.ClientState.LocalPlayer == null) return;

        var partyList = Service.PartyList;

        var newMembers = new HashSet<ulong>();
        var memberData = new Dictionary<ulong, (string Name, string World, uint JobId)>();

        // 1. Check Standard Party List
        if (partyList.Length > 0 && !partyList.IsAlliance)
        {
            for (int i = 0; i < partyList.Length; i++)
            {
                var member = partyList[i];
                ulong id = member.ContentId != 0 ? (ulong)member.ContentId : (ulong)member.ObjectId;
                newMembers.Add(id);

                var wName = "Unknown";
                if (member.World.RowId != 0) wName = member.World.Value.Name.ToString();
                memberData[id] = (member.Name.ToString(), wName, member.ClassJob.RowId);
            }
        }

        // 2. Check Cross Realm Party if needed
        // We always check this if we can, to capture members that PartyList might miss in cross-world scenarios
        unsafe
        {
            var crossRealm = InfoProxyCrossRealm.Instance();
            if (crossRealm != null)
            {
                // Iterate fixed size (8 is max party) as Count is unavailable/unreliable or named differently
                for (uint i = 0; i < 8; i++)
                {
                    var member = InfoProxyCrossRealm.GetGroupMember(i);
                    if (member == null) continue;

                    var id = (ulong)member->ContentId;
                    if (id == 0) continue;

                    newMembers.Add(id);

                    // Read name safely byte by byte until null or max 30
                    var nameBytes = new List<byte>();
                    for (int k = 0; k < 30; k++)
                    {
                        var b = member->Name[k];
                        if (b == 0) break;
                        nameBytes.Add(b);
                    }
                    var name = System.Text.Encoding.UTF8.GetString(nameBytes.ToArray());

                    // Get World Name from Sheet
                    var worldSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
                    // member->HomeWorld is likely int or uint
                    // Fix nullable access for GetRow
                    var worldRow = worldSheet?.GetRow((uint)member->HomeWorld);
                    var worldName = worldRow != null ? worldRow.Value.Name.ToString() : "Unknown";

                    memberData[id] = (name, worldName, member->ClassJobId);
                }
                // Service.Log.Debug($"[PartyService] Checked CrossRealm InfoProxy: found valid members.");
            }
        }


        // Exclude Alliance Config Check
        if (plugin.Config.Party.ExcludeAlliance && partyList.IsAlliance)
        {
            if (currentPartyMembers.Count > 0) currentPartyMembers.Clear();
            if (_partyMembers.Count > 0) _partyMembers.Clear();
            return;
        }

        // Service.Log.Debug($"[PartyService] Final Member Count: {newMembers.Count}");

        // 1. Calculate Joins and Leaves
        var joins = new List<(ulong Id, string Name, string World, uint JobId)>();
        foreach (var id in newMembers)
        {
            if (!currentPartyMembers.Contains(id))
            {
                if (memberData.TryGetValue(id, out var info))
                    joins.Add((id, info.Name, info.World, info.JobId));
            }
        }

        var leaves = new List<ulong>();
        foreach (var id in currentPartyMembers)
        {
            if (!newMembers.Contains(id))
                leaves.Add(id);
        }

        // 2. Update the public list and preserve cached data BEFORE notifications
        var currentList = _partyMembers;
        var newList = new List<PartyMemberInfo>();
        foreach (var id in newMembers)
        {
            if (memberData.TryGetValue(id, out var info))
            {
                var existing = currentList.FirstOrDefault(m => m.Name == info.Name && m.World == info.World);
                if (existing != null)
                {
                    if (existing.JobId != info.JobId)
                    {
                        var updated = existing with { JobId = info.JobId };
                        updated.ItemLevel = null;
                        updated.RaidActivity = null;
                        updated.LastUpdated = null;
                        newList.Add(updated);
                        _ = EnsureMemberDataFetchedAsync(updated);
                    }
                    else
                    {
                        newList.Add(existing);
                        if (existing.ItemLevel == null) _ = EnsureMemberDataFetchedAsync(existing);
                    }
                }
                else
                {
                    var @new = new PartyMemberInfo(info.Name, info.World, info.JobId);
                    newList.Add(@new);
                    _ = EnsureMemberDataFetchedAsync(@new);
                }
            }
        }
        _partyMembers = newList;

        // 3. Process Notifications AFTER the list is updated
        foreach (var join in joins)
        {
            _ = NotifyJoin(join.Name, join.World, join.JobId, newMembers.Count);
        }

        foreach (var id in leaves)
        {
            NotifyLeave(id, newMembers.Count);
        }

        if (currentPartyMembers.Count < 8 && newMembers.Count == 8)
        {
            if (plugin.Config.Party.NotifyFull)
            {
                _ = SendDiscordNotificationAsync("Party Full", "The party is now full (8/8)!", DiscordColor.Blurple);
            }

            if (plugin.Config.Party.AutoSendSummary)
            {
                _ = SendPartySummary();
            }
        }

        // 4. Final Maintenance
        currentPartyMembers.Clear();
        foreach (var id in newMembers) currentPartyMembers.Add(id);
        UpdateNameCache(memberData);
    }

    private Dictionary<ulong, (string Name, string World)> memberCache = new();

    private void UpdateNameCache(Dictionary<ulong, (string Name, string World, uint JobId)> currentData)
    {
        foreach (var kvp in currentData)
        {
            if (!memberCache.ContainsKey(kvp.Key))
            {
                memberCache[kvp.Key] = (kvp.Value.Name, kvp.Value.World);
            }
        }
    }

    private async Task NotifyJoin(string name, string world, uint classJobId, int count)
    {
        try
        {
            if (!plugin.Config.Party.NotifyJoin) return;

            if (!plugin.Config.Party.IncludeSelf)
            {
                var localPlayer = Service.ClientState.LocalPlayer;
                if (localPlayer != null && name == localPlayer.Name.TextValue && (world == "Unknown" || world == localPlayer.HomeWorld.Value.Name.ToString()))
                {
                    Service.Log.Debug($"[PartyService] Skipping notification for self: {name}@{world}");
                    return;
                }
            }

            var classJobSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
            var classJob = classJobSheet?.GetRow(classJobId);
            var classJobAbbr = classJob?.Abbreviation.ToString() ?? string.Empty;

            Service.Log.Info($"[PartyService] NotifyJoin: {name}@{world} (Job: {classJobAbbr}).");

            var message = $"**{name}@{world}** has joined the party. ({count}/8)";
            var msgId = await SendDiscordNotificationAsync("Party Join", message, DiscordColor.Green, name, world);

            if (plugin.Config.Party.ShowGearLevel && msgId > 0)
            {
                // Wait up to 10 seconds for data to be fetched
                var member = _partyMembers.FirstOrDefault(m => m.Name == name && m.World == world);
                int attempts = 0;
                while (attempts < 20 && member != null)
                {
                    bool hasILvl = member.ItemLevel != null;
                    bool needRaid = plugin.Config.Party.ShowSavageProgress;
                    bool hasRaid = member.RaidActivity != null;

                    if (hasILvl && (!needRaid || hasRaid)) break;

                    await Task.Delay(500);
                    attempts++;
                }

                if (member != null && member.ItemLevel != null)
                {
                    var updatedMessage = message + $"\nItem Level: [{classJobAbbr}] {member.ItemLevel}";

                    if (plugin.Config.Party.ShowSavageProgress)
                    {
                        updatedMessage += "\n\n**Savage Progress:**";

                        if (member.RaidActivity != null && member.RaidActivity.Encounters.Count > 0)
                        {
                            var raidLines = new List<string>();
                            foreach (var encounter in member.RaidActivity.Encounters.OrderByDescending(e => e.RaidName).Take(4))
                            {
                                var cleanName = encounter.RaidName;
                                if (encounter.ClearCount > 0)
                                    raidLines.Add($"{cleanName}: {encounter.ClearCount} clears");
                                else if (encounter.BestPercent.HasValue)
                                    raidLines.Add($"{cleanName}: {encounter.BestPercent.Value:F1}%");
                            }

                            if (raidLines.Count > 0)
                            {
                                updatedMessage += "\n• " + string.Join("\n• ", raidLines);
                            }
                            else
                            {
                                updatedMessage += "\n• No Raid Data";
                            }
                        }
                        else
                        {
                            updatedMessage += "\n• No Raid Data";
                        }
                    }

                    if (plugin.Config.RememberMe.Enabled)
                    {
                        var rememberedPlayer = plugin.RememberMe.FindPlayer(name, world);
                        if (rememberedPlayer != null && !string.IsNullOrWhiteSpace(rememberedPlayer.Notes))
                        {
                            updatedMessage += $"\n\n**Note:** {rememberedPlayer.Notes}";
                        }
                    }

                    await UpdateDiscordNotificationAsync(msgId, "Party Join", updatedMessage, DiscordColor.Green, name, world);
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Error in NotifyJoin");
        }
    }

    private HashSet<string> _fetchingSet = new();

    private async Task EnsureMemberDataFetchedAsync(PartyMemberInfo member)
    {
        var key = $"{member.Name}@{member.World}@{member.JobId}";
        lock (_fetchingSet)
        {
            if (_fetchingSet.Contains(key)) return;
            _fetchingSet.Add(key);
        }

        try
        {
            var classJobSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
            var classJob = classJobSheet?.GetRow(member.JobId);
            var classJobAbbr = classJob?.Abbreviation.ToString() ?? string.Empty;

            var gearInfo = await plugin.Tomestone.GetAverageItemLevelAsync(member.Name, member.World, classJobAbbr);
            member.ItemLevel = gearInfo.ItemLevel;
            member.LastUpdated = DateTime.Now;

            if (plugin.Config.Party.ShowSavageProgress)
            {
                var lodestoneId = await plugin.Lodestone.GetLodestoneIdAsync(member.Name, member.World);
                if (!string.IsNullOrEmpty(lodestoneId))
                {
                    member.LodestoneId = lodestoneId;
                    var slug = member.Name.ToLower().Replace(" ", "-").Replace("'", "");
                    var raidActivity = await plugin.Tomestone.GetRaidActivityAsync(member.Name, member.World, lodestoneId, slug);
                    member.RaidActivity = raidActivity;
                }
                else
                {
                    // Mark as empty if we can't find them to stop the wait loop
                    member.RaidActivity = new RaidActivity(Array.Empty<RaidEncounter>());
                }
            }

            if (plugin.Config.RememberMe.Enabled)
            {
                // Ensure LodestoneId is cached for RememberMe as well
                if (string.IsNullOrEmpty(member.LodestoneId))
                {
                    member.LodestoneId = await plugin.Lodestone.GetLodestoneIdAsync(member.Name, member.World);
                }
                if (!string.IsNullOrEmpty(member.LodestoneId))
                {
                    plugin.RememberMe.AddOrUpdatePlayer(member.Name, member.World, member.LodestoneId);
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"Error fetching data for {member.Name}@{member.World}");
            // Ensure properties are set to stop any wait loops
            member.ItemLevel ??= 0;
            member.RaidActivity ??= new RaidActivity(Array.Empty<RaidEncounter>());
        }
        finally
        {
            lock (_fetchingSet)
            {
                _fetchingSet.Remove(key);
            }
        }
    }

    private async void NotifyLeave(ulong id, int count)
    {
        try
        {
            if (!plugin.Config.Party.NotifyLeave) return;

            string name = "Unknown";
            string world = "Unknown";
            if (memberCache.TryGetValue(id, out var info))
            {
                name = info.Name;
                world = info.World;
                memberCache.Remove(id);
            }

            if (!plugin.Config.Party.IncludeSelf)
            {
                var localPlayer = Service.ClientState.LocalPlayer;
                if (localPlayer != null && name == localPlayer.Name.TextValue && (world == "Unknown" || world == localPlayer.HomeWorld.Value.Name.ToString()))
                {
                    Service.Log.Debug($"[PartyService] Skipping leave notification for self: {name}@{world}");
                    return;
                }
            }

            var message = $"**{name}@{world}** has left the party. ({count}/8)";
            await SendDiscordNotificationAsync("Party Leave", message, DiscordColor.Orange, name, world);
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Error in NotifyLeave");
        }
    }

    public async Task SendPartySummary(bool ignoreConfig = false)
    {
        try
        {
            if (!ignoreConfig && !plugin.Config.Party.NotifyFull) return;

            var channelIdStr = plugin.Config.Party.DiscordChannelId;
            if (!ulong.TryParse(channelIdStr, out var channelId)) return;

            var members = _partyMembers.ToList();
            if (members.Count == 0) return;

            // Wait until the party is settled and data is fetched
            int attempts = 0;
            while (attempts < 10 && members.Any(m => m.ItemLevel == null))
            {
                await Task.Delay(500);
                attempts++;
                members = _partyMembers.ToList(); // Refresh list in case it changes
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"Party Summary ({members.Count}/8)")
                .WithDescription(members.Count == 8 ? "The party is now full! Here is a summary:" : "Current party summary:")
                .WithColor(DiscordColor.Blurple)
                .WithTimestamp(DateTime.Now);

            var classJobSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();

            // Use the already created copy of the list to build the embed
            members = _partyMembers.ToList();

            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                var classJob = classJobSheet?.GetRow(member.JobId);
                var jobAbbr = classJob?.Abbreviation.ToString() ?? "??";

                var iLvlText = member.ItemLevel.HasValue && member.ItemLevel.Value > 0 ? member.ItemLevel.Value.ToString() : "??";

                var raidSummary = "No Raid Data";
                if (member.RaidActivity != null && member.RaidActivity.Encounters.Count > 0)
                {
                    var raidLines = new List<string>();
                    foreach (var encounter in member.RaidActivity.Encounters.OrderByDescending(e => e.RaidName).Take(2))
                    {
                        var cleanName = CleanRaidName(encounter.RaidName);
                        if (encounter.ClearCount > 0)
                            raidLines.Add($"{cleanName}: {encounter.ClearCount}");
                        else if (encounter.BestPercent.HasValue)
                            raidLines.Add($"{cleanName}: {encounter.BestPercent.Value:F0}%");
                    }
                    if (raidLines.Count > 0) raidSummary = string.Join("\n", raidLines);
                }

                // Title: [JOB] Name
                // Body: iLvl + Raid
                embed.AddField($"`[{jobAbbr}]` {member.Name}", $"**iLvl:** {iLvlText}\n{raidSummary}", inline: true);

                // Try to force a row break after 4 people if possible
                // Note: Discord often forces 3 columns, but we'll try to groups them.
                if ((i + 1) % 4 == 0 && i < members.Count - 1)
                {
                    // Adding an empty field with inline: false sometimes forces a break or at least separates rows
                    // But Discord embed fields are tricky. We'll stick to 8 inline fields for now.
                }
            }

            await plugin.Discord.SendWebhookMessageRaw(channelId, embed.Build(), "Party Full Summary", null);
            Service.Log.Info("[PartyService] Sent party full summary to Discord.");
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Error in SendPartySummary");
        }
    }

    private async Task<ulong> SendDiscordNotificationAsync(string title, string description, DiscordColor color, string? characterName = null, string? characterWorld = null)
    {
        if (!plugin.Config.Party.DiscordEnabled) return 0;

        var channelIdStr = plugin.Config.Party.DiscordChannelId;
        if (!ulong.TryParse(channelIdStr, out var channelId)) return 0;

        string? avatarUrl = null;
        if (!string.IsNullOrEmpty(characterName) && !string.IsNullOrEmpty(characterWorld))
        {
            avatarUrl = await plugin.Lodestone.GetAvatarUrlAsync(characterName, characterWorld);
        }

        var embed = new DiscordEmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(color)
            .WithTimestamp(DateTime.Now);

        if (avatarUrl != null)
        {
            embed.WithThumbnail(avatarUrl);
        }

        string username = "Party Notification";
        if (!string.IsNullOrEmpty(characterName) && !string.IsNullOrEmpty(characterWorld))
        {
            username = $"{characterName}@{characterWorld}";
        }

        return await plugin.Discord.SendWebhookMessageRaw(channelId, embed.Build(), username, avatarUrl);
    }

    private async Task UpdateDiscordNotificationAsync(ulong msgId, string title, string description, DiscordColor color, string? characterName = null, string? characterWorld = null)
    {
        if (!plugin.Config.Party.DiscordEnabled) return;

        var channelIdStr = plugin.Config.Party.DiscordChannelId;
        if (!ulong.TryParse(channelIdStr, out var channelId)) return;

        var embed = new DiscordEmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(color)
            .WithTimestamp(DateTime.Now);

        if (!string.IsNullOrEmpty(characterName) && !string.IsNullOrEmpty(characterWorld))
        {
            var avatarUrl = await plugin.Lodestone.GetAvatarUrlAsync(characterName, characterWorld);
            if (avatarUrl != null) embed.WithThumbnail(avatarUrl);
        }

        await plugin.Discord.EditWebhookMessage(channelId, msgId, embed.Build());
    }

    public async void DebugTriggerLeave(string name, string world)
    {
        var id = (ulong)(name + world).GetHashCode();
        memberCache[id] = (name, world);
        NotifyLeave(id, Math.Max(0, currentPartyMembers.Count - 1));
    }
    public void DebugTriggerFull() => _ = SendPartySummary();

    public void Dispose()
    {
        Service.Framework.Update -= OnFrameworkUpdate;
    }

    private string CleanRaidName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // Common patterns to remove to leave only the tier (e.g., M1, P1S, etc.)
        var patterns = new[]
        {
            "AAC Heavyweight",
            "Eden's Promise",
            "Anabaseios",
            "Abyssos",
            "Asphodelos",
            "(Savage)",
            " - "
        };

        var result = name;
        foreach (var pattern in patterns)
        {
            result = result.Replace(pattern, "", StringComparison.OrdinalIgnoreCase);
        }

        return result.Trim();
    }
}
