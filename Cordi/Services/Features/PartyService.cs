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

namespace Cordi.Services.Features;

public class PartyService : IDisposable
{
    private readonly CordiPlugin plugin;
    private readonly NotificationManager notificationService;
    private readonly HashSet<ulong> currentPartyMembers = new();
    private DateTime _lastPartyCheck = DateTime.MinValue;
    private readonly TimeSpan _partyCheckInterval = TimeSpan.FromMilliseconds(500);

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
            return;
        }

        if (DateTime.Now - _lastPartyCheck < _partyCheckInterval)
            return;

        if (Service.Condition[ConditionFlag.BetweenAreas])
            return;

        _lastPartyCheck = DateTime.Now;

        if (Service.ClientState.LocalPlayer == null) return;

        var partyList = Service.PartyList;

        if (plugin.Config.Party.ExcludeAlliance && partyList.IsAlliance)
        {
            if (currentPartyMembers.Count > 0) currentPartyMembers.Clear();
            return;
        }
        var newMembers = new HashSet<ulong>();

        for (int i = 0; i < partyList.Length; i++)
        {
            var member = partyList[i];
            if (member.ContentId != 0)
                newMembers.Add((ulong)member.ContentId);
            else
                newMembers.Add((ulong)member.ObjectId);
        }

        foreach (var id in newMembers)
        {
            if (!currentPartyMembers.Contains(id))
            {
                var member = partyList.FirstOrDefault(m => (m.ContentId != 0 && (ulong)m.ContentId == id) || (ulong)m.ObjectId == id);
                if (member != null)
                {
                    NotifyJoin(member, newMembers.Count);
                }
            }
        }

        foreach (var id in currentPartyMembers)
        {
            if (!newMembers.Contains(id))
            {
                NotifyLeave(id, newMembers.Count);
            }
        }

        if (plugin.Config.Party.NotifyFull && currentPartyMembers.Count < 8 && newMembers.Count == 8)
        {
            NotifyFull();
        }

        currentPartyMembers.Clear();
        foreach (var id in newMembers) currentPartyMembers.Add(id);

        UpdateNameCache(partyList);
    }

    private Dictionary<ulong, (string Name, string World)> memberCache = new();

    private void UpdateNameCache(IPartyList partyList)
    {
        for (int i = 0; i < partyList.Length; i++)
        {
            var member = partyList[i];
            ulong id = member.ContentId != 0 ? (ulong)member.ContentId : (ulong)member.ObjectId;
            if (!memberCache.ContainsKey(id))
            {
                memberCache[id] = (member.Name.ToString(), member.World.Value.Name.ToString());
            }
        }
    }

    private async void NotifyJoin(IPartyMember member, int count)
    {
        if (!plugin.Config.Party.NotifyJoin) return;

        var name = member.Name.ToString();
        var world = member.World.Value.Name.ToString();

        if (!plugin.Config.Party.IncludeSelf)
        {
            var localPlayer = Service.ClientState.LocalPlayer;
            if (localPlayer != null && name == localPlayer.Name.TextValue && world == localPlayer.HomeWorld.Value.Name.ToString())
            {
                Service.Log.Debug($"[PartyService] Skipping notification for self: {name}@{world}");
                return;
            }
        }

        // Get the class/job from the party member
        var classJobId = member.ClassJob.RowId;
        var classJobSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
        var classJob = classJobSheet?.GetRow(classJobId);
        var classJobAbbr = classJob?.Abbreviation.ToString() ?? string.Empty;

        Service.Log.Info($"[PartyService] NotifyJoin: {name}@{world} (Job: {classJobAbbr}). Fetching gear level: {plugin.Config.Party.ShowGearLevel}");

        var message = $"**{name}@{world}** has joined the party. ({count}/8)";
        var msgId = await SendDiscordNotificationAsync("Party Join", message, DiscordColor.Green, name, world);

        if (plugin.Config.Party.ShowGearLevel && msgId > 0)
        {
            _ = Task.Run(async () =>
            {
                string? lodestoneId = null;

                // Pass the classJob abbreviation to get class-specific item level
                var gearInfo = await plugin.Tomestone.GetAverageItemLevelAsync(name, world, classJobAbbr);
                if (gearInfo.ItemLevel > 0)
                {
                    // Build message with optional class icon
                    var itemLevelText = !string.IsNullOrEmpty(classJobAbbr)
                        ? $"Item Level: [{classJobAbbr}] {gearInfo.ItemLevel}"
                        : $"Item Level: {gearInfo.ItemLevel}";

                    var updatedMessage = message + $"\n{itemLevelText}";

                    // Build notification message
                    var notificationMessage = $"{name}@{world} has {gearInfo.ItemLevel} item level.";

                    // Fetch raid activity if we have a lodestone ID and ShowSavageProgress is enabled
                    if (plugin.Config.Party.ShowSavageProgress)
                    {
                        lodestoneId = await plugin.Lodestone.GetLodestoneIdAsync(name, world);
                        if (!string.IsNullOrEmpty(lodestoneId))
                        {
                            var slug = name.ToLower().Replace(" ", "-").Replace("'", "");
                            var raidActivity = await plugin.Tomestone.GetRaidActivityAsync(name, world, lodestoneId, slug);

                            if (raidActivity.Encounters.Count > 0)
                            {
                                updatedMessage += "\n\n**Savage Progress:**";
                                notificationMessage += "\n\nSavage Progress:";

                                foreach (var encounter in raidActivity.Encounters.OrderByDescending(e => e.RaidName).Take(4))
                                {
                                    // Show clears if any exist, otherwise show parse percent
                                    if (encounter.ClearCount > 0)
                                    {
                                        var clearText = $"{encounter.RaidName}: {encounter.ClearCount} clear{(encounter.ClearCount != 1 ? "s" : "")}";
                                        updatedMessage += $"\n• {clearText}";
                                        notificationMessage += $"\n• {clearText}";
                                    }
                                    else
                                    {
                                        var percentText = encounter.BestPercent.HasValue
                                            ? $"{encounter.BestPercent.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}%"
                                            : "No parse";

                                        var encounterText = $"{encounter.RaidName}: {percentText}";
                                        updatedMessage += $"\n• {encounterText}";
                                        notificationMessage += $"\n• {encounterText}";
                                    }
                                }
                            }
                            else
                            {
                                updatedMessage += "\n\n**Savage Progress:**\n• No Raid Data";
                                notificationMessage += "\n\nSavage Progress:\n• No Raid Data";
                            }
                        }
                    }

                    // Check for Remember Me notes
                    if (plugin.Config.RememberMe.Enabled)
                    {
                        var rememberedPlayer = plugin.RememberMe.FindPlayer(name, world);
                        if (rememberedPlayer != null && !string.IsNullOrWhiteSpace(rememberedPlayer.Notes))
                        {
                            updatedMessage += $"\n\n**Note:** {rememberedPlayer.Notes}";
                            notificationMessage += $"\n\nNote: {rememberedPlayer.Notes}";
                        }

                        // Automatically track this player
                        if (string.IsNullOrEmpty(lodestoneId))
                        {
                            lodestoneId = await plugin.Lodestone.GetLodestoneIdAsync(name, world);
                        }
                        plugin.RememberMe.AddOrUpdatePlayer(name, world, lodestoneId);
                    }

                    notificationService.Add("Party Member Info", notificationMessage, CordiNotificationType.Success);
                    await UpdateDiscordNotificationAsync(msgId, "Party Join", updatedMessage, DiscordColor.Green, name, world);
                }
            });
        }
    }

    private async void NotifyLeave(ulong id, int count)
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
            if (localPlayer != null && name == localPlayer.Name.TextValue && world == localPlayer.HomeWorld.Value.Name.ToString())
            {
                Service.Log.Debug($"[PartyService] Skipping leave notification for self: {name}@{world}");
                return;
            }
        }

        var message = $"**{name}@{world}** has left the party. ({count}/8)";
        await SendDiscordNotificationAsync("Party Leave", message, DiscordColor.Orange, name, world);
    }

    private void NotifyFull()
    {
        _ = SendDiscordNotificationAsync("Party Full", "The party is now full (8/8)!", DiscordColor.Blurple);
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

    // Debug method disabled - NotifyJoin now requires IPartyMember parameter
    // public async void DebugTriggerJoin(string name, string world) => NotifyJoin(name, world, currentPartyMembers.Count + 1);
    public async void DebugTriggerLeave(string name, string world)
    {
        var id = (ulong)(name + world).GetHashCode();
        memberCache[id] = (name, world);
        NotifyLeave(id, Math.Max(0, currentPartyMembers.Count - 1));
    }
    public void DebugTriggerFull() => NotifyFull();

    public void Dispose()
    {
        Service.Framework.Update -= OnFrameworkUpdate;
    }
}
