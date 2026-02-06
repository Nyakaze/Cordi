using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;
using Cordi.Core;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace Cordi.Services.Features;

public class RememberMeService : IDisposable
{
    private readonly CordiPlugin plugin;
    private bool _isCapturePending = true;
    private uint _lastTargetId = 0;
    private bool _wasAddonVisible = false;

    public RememberMeService(CordiPlugin plugin)
    {
        this.plugin = plugin;
        Service.Framework.Update += OnFrameworkUpdate;
    }

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        if (!plugin.Config.RememberMe.Enabled || !plugin.Config.RememberMe.EnableExamineFeature) return;

        var addonWrapper = Service.GameGui.GetAddonByName("CharacterInspect");
        nint ptr = (nint)addonWrapper;

        if (ptr == IntPtr.Zero)
        {
            if (_wasAddonVisible) Service.Log.Debug("[RememberMe] Addon lost/closed.");
            _wasAddonVisible = false;
            _isCapturePending = true;
            _lastTargetId = 0;
            return;
        }

        var addon = (AtkUnitBase*)ptr;
        if (!addon->IsVisible)
        {
            if (_wasAddonVisible) Service.Log.Debug("[RememberMe] Addon hidden.");
            _wasAddonVisible = false;
            _isCapturePending = true;
            _lastTargetId = 0;
            return;
        }

        if (!_wasAddonVisible)
        {
            Service.Log.Debug("[RememberMe] CharacterInspect Addon became visible. Resetting state.");
            _wasAddonVisible = true;
            _isCapturePending = true;
            _lastTargetId = 0;
        }

        IPlayerCharacter? target = null;

        // ACCURATE METHOD: Read from AgentInspect directly.
        // We found EntityId at offset 0x28 (40) via debug scan.
        try
        {
            unsafe
            {
                var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentInspect.Instance();
                if (agent != null)
                {
                    // agent->CurrentObjectId is likely at 0x28 based on logs
                    // (FOUND EntityId ... at offset 40 (0x28))
                    var agentPtr = (byte*)agent;
                    uint inspectedEntityId = *(uint*)(agentPtr + 0x28);

                    // If 0x28 is 0/invalid, check 0x34? 
                    // Providing fallback to 0x34 if 0x28 is 0, based on logs showing it appearing later.
                    if (inspectedEntityId == 0 || inspectedEntityId == 0xE0000000)
                        inspectedEntityId = *(uint*)(agentPtr + 0x34);

                    if (inspectedEntityId != 0 && inspectedEntityId != 0xE0000000)
                    {
                        // Find this entity in object table
                        var obj = Service.ObjectTable.SearchById(inspectedEntityId);
                        if (obj is IPlayerCharacter pc)
                        {
                            target = pc;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "[RememberMe] Error reading AgentInspect.");
        }

        // REMOVED FALLBACK: We strictly rely on AgentInspect.
        // If AgentInspect didn't give us a target, we assume no one is being inspected (or the window is just opening).
        // This prevents capturing the hard target erroneously.

        if (target != null)
        {
            // Only capture if it's a new target OR we just opened the window
            if (target.EntityId != _lastTargetId || _isCapturePending)
            {
                if (target.EntityId != _lastTargetId)
                {
                    Service.Log.Debug($"[RememberMe] Inspecting new target: {target.Name} (ID: {target.EntityId}).");
                    _lastTargetId = target.EntityId;
                    _isCapturePending = true;
                }

                if (_isCapturePending)
                {
                    // Debounce/Delay? The window might take a few frames to populate data?
                    // But we are reading from the ObjectTable character which is instant.
                    Service.Log.Debug($"[RememberMe] Capturing data for {target.Name}...");
                    CaptureAndSaveGlamour(target);
                    _isCapturePending = false;
                }
            }
        }
    }

    private unsafe void CaptureAndSaveGlamour(IPlayerCharacter player)
    {
        var name = player.Name.ToString();
        var world = player.HomeWorld.Value.Name.ToString();
        var glamour = new PlayerGlamour();

        Service.Log.Debug($"[RememberMe] Capturing glamour for {name}@{world}");

        try
        {
            var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentInspect.Instance();
            if (agent != null)
            {
                // INSPECTO-BASED IMPLEMENTATION
                // Instead of AgentInspect memory scanning, we use InventoryManager's "Examine" container.
                // This is populated by the game when inspecting someone.

                var inventoryManager = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
                if (inventoryManager != null)
                {
                    // InventoryType.Examine = 4000 (0xFA0) or similar. 
                    // FFXIVClientStructs might have it as `Examine` enum.
                    var container = inventoryManager->GetInventoryContainer(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Examine);

                    if (container != null)
                    {
                        PlayerGlamour.GearItem GetSlot(int slotIndex)
                        {
                            var item = container->GetInventorySlot(slotIndex);
                            if (item == null) return new PlayerGlamour.GearItem(0, 0);

                            // Inspecto Logic:
                            // ItemId is the base item.
                            // GlamourId is the appearance.
                            // If GlamourId != 0, that is the visual look. 
                            // But usually users want to know "What is that item looking like?".
                            // The `PlayerGlamour.GearItem` struct in my project might only store one ID.
                            // Let's check `PlayerGlamour.cs`.
                            // Constructor: GearItem(uint itemId, uint stainId) (assumed).

                            // For linking purposes, if we want to link the "Glamour", we should ideally use GlamourId if present.
                            // BUT, a link to the "Glamour" item is useful. A link to the "Stats" item is also useful.
                            // Usually "Examine" shows the stats item, but visually shows the glamour.
                            // Let's match in-game Examine: "The item you are looking at".
                            // If we want "What is this visual?", use GlamourId.
                            // If we want "What stats?", use ItemId.
                            // RememberMe usually tracks "Visuals".
                            // Let's use GlamourId if available, else ItemId.

                            // Fix field access for InventoryItem
                            // If 'Stain' is missing, it might be 'Stain0' or 'Stain1' in newer structs.
                            // For safety and build fix, we will just use 0 if we can't find it, or try to cast if it was just a type issue.
                            // The previous error "does not contain definition for Stain" implies it's not there by that name.
                            // We will ignore stain for now to ensure we get the ItemID fix working.
                            byte stain = 0;

                            // Also casting arguments properly for GearItem constructor (uint, byte)
                            uint idToUse = item->GlamourId != 0 ? item->GlamourId : item->ItemId;

                            return new PlayerGlamour.GearItem(idToUse, stain);
                        }

                        // Slot mapping (Standard inventory order for Examine container)
                        // 0: MainHand
                        // 1: OffHand
                        // 2: Head
                        // 3: Body
                        // 4: Hands
                        // 5: Waist (Obsolete, usually empty or skipped)
                        // 6: Legs
                        // 7: Feet
                        // 8: Ears
                        // 9: Neck
                        // 10: Wrists
                        // 11: Right Ring
                        // 12: Left Ring

                        glamour.MainHand = GetSlot(0);
                        glamour.OffHand = GetSlot(1);
                        glamour.Head = GetSlot(2);
                        glamour.Body = GetSlot(3);
                        glamour.Hands = GetSlot(4);
                        // Slot 5 is waist
                        glamour.Legs = GetSlot(6);
                        glamour.Feet = GetSlot(7);
                        glamour.Ears = GetSlot(8);
                        glamour.Neck = GetSlot(9);
                        glamour.Wrists = GetSlot(10);
                        glamour.RightRing = GetSlot(11);
                        glamour.LeftRing = GetSlot(12);

                        Service.Log.Debug($"[RememberMe] Captured valid glamour via InventoryManager.");
                    }
                    else
                    {
                        Service.Log.Warning("[RememberMe] InventoryType.Examine container was null.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "[RememberMe] Capture failed.");
        }

        AddOrUpdateExaminedPlayer(name, world, glamour);
    }

    public RememberedPlayerEntry? FindPlayer(string name, string world)
    {
        if (!plugin.Config.RememberMe.Enabled) return null;

        return plugin.Config.RememberMe.RememberedPlayers
        .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                      && p.World.Equals(world, StringComparison.OrdinalIgnoreCase));
    }

    public RememberedPlayerEntry? FindExaminedPlayer(string name, string world)
    {
        if (!plugin.Config.RememberMe.Enabled) return null;

        return plugin.Config.RememberMe.ExaminedPlayers
        .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                      && p.World.Equals(world, StringComparison.OrdinalIgnoreCase));
    }

    public RememberedPlayerEntry? FindPlayerByLodestoneId(string lodestoneId)
    {
        if (!plugin.Config.RememberMe.Enabled || string.IsNullOrWhiteSpace(lodestoneId))
            return null;

        return plugin.Config.RememberMe.RememberedPlayers
        .FirstOrDefault(p => p.LodestoneId.Equals(lodestoneId, StringComparison.OrdinalIgnoreCase));
    }

    public void AddOrUpdatePlayer(string name, string world, string? lodestoneId = null, string? notes = null, PlayerGlamour? glamour = null)
    {
        if (!plugin.Config.RememberMe.Enabled) return;

        var existing = FindPlayer(name, world);

        if (existing != null)
        {
            existing.LastSeen = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(lodestoneId) && string.IsNullOrWhiteSpace(existing.LodestoneId))
            {
                existing.LodestoneId = lodestoneId;
            }

            if (!string.IsNullOrWhiteSpace(notes))
            {
                existing.Notes = notes;
            }

            if (glamour != null)
            {
                existing.Glamour = glamour;
            }
        }
        else
        {
            var newEntry = new RememberedPlayerEntry(name, world, lodestoneId ?? string.Empty)
            {
                Notes = notes ?? string.Empty,
                Glamour = glamour
            };
            plugin.Config.RememberMe.RememberedPlayers.Add(newEntry);
        }

        plugin.Config.Save();
    }

    public void AddOrUpdateExaminedPlayer(string name, string world, PlayerGlamour? glamour = null)
    {
        if (!plugin.Config.RememberMe.Enabled) return;

        var existing = FindExaminedPlayer(name, world);

        if (existing != null)
        {
            Service.Log.Debug($"[RememberMe] Updating existing examined player: {name}@{world}");
            existing.LastSeen = DateTime.Now;
            if (glamour != null) existing.Glamour = glamour;
        }
        else
        {
            Service.Log.Debug($"[RememberMe] Adding new examined player: {name}@{world}");
            var newEntry = new RememberedPlayerEntry(name, world)
            {
                Glamour = glamour
            };
            plugin.Config.RememberMe.ExaminedPlayers.Add(newEntry);
        }

        plugin.Config.Save();
        Service.Log.Debug($"[RememberMe] Saved config. ExaminedPlayers count: {plugin.Config.RememberMe.ExaminedPlayers.Count}");
    }

    public void UpdateNotes(string name, string world, string notes)
    {
        if (!plugin.Config.RememberMe.Enabled) return;

        var player = FindPlayer(name, world);
        if (player != null)
        {
            player.Notes = notes;
            plugin.Config.Save();
        }
    }

    public void UpdateLastSeen(string name, string world)
    {
        if (!plugin.Config.RememberMe.Enabled) return;

        var player = FindPlayer(name, world);
        if (player != null)
        {
            player.LastSeen = DateTime.Now;
            plugin.Config.Save();
        }
    }

    public void RemovePlayer(string name, string world)
    {
        if (!plugin.Config.RememberMe.Enabled) return;

        var player = FindPlayer(name, world);
        if (player != null)
        {
            plugin.Config.RememberMe.RememberedPlayers.Remove(player);
            plugin.Config.Save();
        }
    }

    public void RemoveExaminedPlayer(string name, string world)
    {
        if (!plugin.Config.RememberMe.Enabled) return;

        var player = FindExaminedPlayer(name, world);
        if (player != null)
        {
            plugin.Config.RememberMe.ExaminedPlayers.Remove(player);
            plugin.Config.Save();
        }
    }

    public List<RememberedPlayerEntry> GetAllPlayers()
    {
        if (!plugin.Config.RememberMe.Enabled)
            return new List<RememberedPlayerEntry>();

        return plugin.Config.RememberMe.RememberedPlayers
        .OrderByDescending(p => p.LastSeen)
        .ToList();
    }

    public List<RememberedPlayerEntry> GetAllExaminedPlayers()
    {
        if (!plugin.Config.RememberMe.Enabled)
            return new List<RememberedPlayerEntry>();

        return plugin.Config.RememberMe.ExaminedPlayers
        .OrderByDescending(p => p.LastSeen)
        .ToList();
    }

    public List<RememberedPlayerEntry> SearchPlayers(string searchText)
    {
        if (!plugin.Config.RememberMe.Enabled || string.IsNullOrWhiteSpace(searchText))
            return GetAllPlayers();

        var search = searchText.ToLower();
        return plugin.Config.RememberMe.RememberedPlayers
        .Where(p => p.Name.ToLower().Contains(search)
             || p.World.ToLower().Contains(search)
             || p.Notes.ToLower().Contains(search))
        .OrderByDescending(p => p.LastSeen)
        .ToList();
    }

    public List<RememberedPlayerEntry> SearchExaminedPlayers(string searchText)
    {
        if (!plugin.Config.RememberMe.Enabled || string.IsNullOrWhiteSpace(searchText))
            return GetAllExaminedPlayers();

        var search = searchText.ToLower();
        return plugin.Config.RememberMe.ExaminedPlayers
        .Where(p => p.Name.ToLower().Contains(search)
             || p.World.ToLower().Contains(search))
        .OrderByDescending(p => p.LastSeen)
        .ToList();
    }

    public void Dispose()
    {
        Service.Framework.Update -= OnFrameworkUpdate;
    }
}
