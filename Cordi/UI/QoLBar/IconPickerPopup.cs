using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Cordi.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;

namespace Cordi.UI.QoLBar;

public class IconPickerPopup
{
    private const int IconSize = 40;

    public bool IsOpen = false;

    private int _searchId = 0;
    private string _searchText = string.Empty;
    private int _selectedTab = 0; // 0 = Game Icons, 1 = Custom Icons
    private List<string>? _cachedCustomIcons;
    private bool _refreshCustom = true;

    private Action<int, string>? _onSelect;

    private record IconRange(int Start, int End, string Name);
    private class IconCategory
    {
        public string Name { get; }
        public List<IconRange> Ranges { get; } = new();
        public List<(int Id, string Sub)>? ValidIcons { get; private set; }
        private bool _isLoading = false;

        public IconCategory(string name) => Name = name;
        public void Add(int start, int end, string name = "") => Ranges.Add(new IconRange(start, end, name));

        public void EnsureLoaded()
        {
            if (ValidIcons != null || _isLoading) return;
            _isLoading = true;

            // Run on thread pool to avoid freezing UI
            Task.Run(() =>
            {
                var list = new List<(int, string)>();
                foreach (var r in Ranges)
                {
                    for (int id = r.Start; id < r.End; id++)
                    {
                        if (IconExists(id))
                            list.Add((id, r.Name));
                    }
                }
                ValidIcons = list;
                _isLoading = false;
            });
        }

        private bool IconExists(int id)
        {
            // Check formatted path for standard icon location
            // "ui/icon/000000/000123.tex"
            var path = $"ui/icon/{id / 1000:000}000/{id:000000}.tex";
            return Service.DataManager.FileExists(path) || Service.DataManager.FileExists(path + "_hr1");
        }
    }

    private readonly List<IconCategory> _categories = new();
    private int _selectedCategoryIndex = 0;

    public IconPickerPopup()
    {
        InitCategories();
    }

    private void InitCategories()
    {
        var fav = new IconCategory("★ Category");
        fav.Add(62_000, 62_600, "Classes/Jobs");
        fav.Add(62_800, 62_900, "Gearsets");
        fav.Add(66_000, 66_400, "Macros");
        fav.Add(90_000, 100_000, "FC Crests/Symbols");
        fav.Add(114_000, 114_100, "New Game+");
        fav.Add(230_850, 231_000, "Classes/Jobs (GPose)");
        _categories.Add(fav);

        var misc = new IconCategory("Misc");
        misc.Add(60_000, 61_000, "UI");
        misc.Add(61_200, 61_250, "Markers");
        misc.Add(61_290, 61_390, "Markers 2");
        misc.Add(61_390, 62_000, "UI 2");
        misc.Add(62_600, 62_620, "HQ FC Banners");
        misc.Add(63_900, 64_000, "Map Markers");
        misc.Add(64_500, 64_550, "Stamps");
        misc.Add(65_000, 65_900, "Currencies");
        misc.Add(180_000, 180_060, "Chocobo Racing");
        misc.Add(230_000, 230_850, "GPose");
        misc.Add(231_000, 240_000, "GPose 2");
        _categories.Add(misc);

        var misc2 = new IconCategory("Misc 2");
        misc2.Add(62_900, 63_200, "Achievements/Hunting Log");
        misc2.Add(63_875, 63_900, "Cosmic Exploration");
        misc2.Add(65_900, 66_000, "Fishing");
        misc2.Add(66_400, 66_500, "Tags");
        misc2.Add(67_000, 68_000, "Fashion Log");
        misc2.Add(70_120, 70_200, "Animals");
        misc2.Add(70_500, 70_960, "Cosmic Exploration 2");
        misc2.Add(70_960, 71_450, "Quests");
        misc2.Add(72_000, 72_500, "BLU UI");
        misc2.Add(72_500, 72_620, "Bozja UI");
        misc2.Add(76_000, 76_200, "Mahjong");
        misc2.Add(80_000, 80_200, "Quest Log");
        misc2.Add(80_730, 81_000, "Relic Log");
        misc2.Add(82_000, 82_100, "Misc UI");
        misc2.Add(82_270, 82_325, "Occult Crescent UI");
        misc2.Add(83_000, 84_000, "FC Ranks");
        misc2.Add(180_060, 180_100, "UI Text");
        misc2.Add(240_000, 241_000, "Strategy Board");
        _categories.Add(misc2);

        var actions = new IconCategory("Actions");
        actions.Add(100, 4_000, "Classes/Jobs");
        actions.Add(5_100, 8_000, "Traits");
        actions.Add(8_000, 9_000, "Fashion");
        actions.Add(9_000, 10_000, "PvP");
        actions.Add(19_600, 19_800, "Event");
        actions.Add(19_800, 20_000, "Mount");
        actions.Add(61_250, 61_290, "Duties/Trials");
        actions.Add(64_200, 64_325, "FC");
        actions.Add(64_550, 64_600, "Occult Crescent");
        actions.Add(64_600, 64_800, "Eureka");
        actions.Add(64_800, 65_000, "NPC");
        actions.Add(70_000, 70_120, "Chocobo Racing");
        actions.Add(82_200, 82_270, "Occult Crescent 2");
        actions.Add(246_000, 250_000, "Emotes");
        _categories.Add(actions);

        var mounts = new IconCategory("Mounts & Minions");
        mounts.Add(4_000, 4_400, "Mounts");
        mounts.Add(4_400, 5_100, "Minions");
        mounts.Add(59_000, 59_400, "Mounts... again?");
        mounts.Add(59_400, 60_000, "Minion Items");
        mounts.Add(68_000, 68_400, "Mounts Log");
        mounts.Add(68_400, 69_000, "Minions Log");
        _categories.Add(mounts);

        var items = new IconCategory("Items");
        items.Add(20_000, 30_000, "General");
        items.Add(50_000, 54_000, "Housing");
        items.Add(58_000, 59_000, "Fashion");
        _categories.Add(items);

        var equip = new IconCategory("Equipment");
        equip.Add(30_000, 50_000, "Equipment");
        equip.Add(54_000, 54_225, "Belts");
        equip.Add(54_225, 54_400, "Flowers");
        equip.Add(54_400, 58_000, "Special Equipment");
        equip.Add(200_000, 210_000, "Glasses");
        _categories.Add(equip);

        var status = new IconCategory("Statuses");
        status.Add(210_000, 230_000, "Statuses");
        _categories.Add(status);
    }

    public void Open(Action<int, string> onSelect)
    {
        _onSelect = onSelect;
        _searchId = 0;
        _searchText = string.Empty;
        _refreshCustom = true;
        _selectedCategoryIndex = 0;
        IsOpen = true;
    }

    public void Draw()
    {
        if (!IsOpen) return;

        ImGui.SetNextWindowSize(new Vector2(700, 500), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Icon Picker", ref IsOpen, ImGuiWindowFlags.None))
        {
            ImGui.End();
            return;
        }

        var tabFlags = ImGuiTabBarFlags.None;
        if (ImGui.BeginTabBar("##iconTabs", tabFlags))
        {
            if (ImGui.BeginTabItem("Game Icons"))
            {
                _selectedTab = 0;
                DrawGameIcons();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Custom Icons"))
            {
                _selectedTab = 1;
                DrawCustomIcons();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void DrawGameIcons()
    {
        float scale = ImGuiHelpers.GlobalScale;
        float iconPx = IconSize * scale;

        float sidebarWidth = 150 * scale;
        ImGui.BeginChild("##sidebar", new Vector2(sidebarWidth, 0), true);
        for (int i = 0; i < _categories.Count; i++)
        {
            if (ImGui.Selectable(_categories[i].Name, _selectedCategoryIndex == i))
            {
                _selectedCategoryIndex = i;
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##content", Vector2.Zero, true);

        var category = _categories[_selectedCategoryIndex];
        category.EnsureLoaded();

        if (category.ValidIcons == null)
        {
            ImGui.Text("Loading icons...");
            ImGui.SameLine();
            // Simple spinner
            float time = (float)ImGui.GetTime();
            int frame = (int)(time * 10) % 4;
            string dots = new string('.', frame + 1);
            ImGui.Text(dots);
            ImGui.EndChild();
            return;
        }

        // Calculate columns
        float availW = ImGui.GetContentRegionAvail().X;
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        int cols = Math.Max(1, (int)((availW) / (iconPx + spacing)));

        int totalLimit = category.ValidIcons.Count;
        int totalRows = (totalLimit + cols - 1) / cols;

        ImGuiListClipper clipper = new();
        clipper.Begin(totalRows);

        while (clipper.Step())
        {
            for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
            {
                if (row >= totalRows) break;

                for (int col = 0; col < cols; col++)
                {
                    int index = row * cols + col;
                    if (index >= totalLimit) break;

                    var (id, subCat) = category.ValidIcons[index];

                    // Draw icon 
                    ISharedImmediateTexture? tex = null;
                    try { tex = Service.TextureProvider.GetFromGameIcon(new GameIconLookup((uint)id)); }
                    catch { }

                    var wrap = tex?.GetWrapOrEmpty();

                    if (col > 0) ImGui.SameLine();

                    ImGui.PushID(index);

                    if (wrap != null && wrap.Handle != IntPtr.Zero)
                    {
                        if (ImGui.ImageButton(wrap.Handle, new Vector2(iconPx, iconPx)))
                        {
                            _onSelect?.Invoke(id, string.Empty);
                            IsOpen = false;
                        }
                    }
                    else
                    {
                        // Fallback button
                        if (ImGui.Button($"#{id}", new Vector2(iconPx, iconPx)))
                        {
                            _onSelect?.Invoke(id, string.Empty);
                            IsOpen = false;
                        }
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"ID: {id}\n{subCat}");

                    ImGui.PopID();
                }
            }
        }
        clipper.End();

        ImGui.EndChild();
    }

    private void DrawCustomIcons()
    {
        float scale = ImGuiHelpers.GlobalScale;
        float iconPx = IconSize * scale;

        var iconsDir = GetCustomIconsDirectory();

        if (ImGui.Button("Open Icons Folder"))
        {
            Directory.CreateDirectory(iconsDir);
            try { System.Diagnostics.Process.Start("explorer.exe", iconsDir); }
            catch { }
        }
        ImGui.SameLine();
        if (ImGui.Button("Refresh"))
            _refreshCustom = true;

        ImGui.SameLine();
        if (ImGui.Button("None"))
        {
            _onSelect?.Invoke(0, string.Empty);
            IsOpen = false;
        }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
        ImGui.TextUnformatted("Place 64x64 .png files in the Icons folder");
        ImGui.PopStyleColor();

        ImGui.Separator();

        if (_refreshCustom || _cachedCustomIcons == null)
        {
            _cachedCustomIcons = ScanCustomIcons(iconsDir);
            _refreshCustom = false;
        }

        if (_cachedCustomIcons.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No custom icons found.");
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), $"Add .png files to:\n{iconsDir}");
            return;
        }

        if (ImGui.BeginChild("##customGrid", Vector2.Zero, false))
        {
            float availW = ImGui.GetContentRegionAvail().X;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            int cols = Math.Max(1, (int)((availW + spacing) / (iconPx + spacing)));

            int col = 0;
            for (int i = 0; i < _cachedCustomIcons.Count; i++)
            {
                var path = _cachedCustomIcons[i];
                ISharedImmediateTexture? tex = null;
                try { tex = Service.TextureProvider.GetFromFile(path); }
                catch { continue; }

                var wrap = tex?.GetWrapOrEmpty();
                if (wrap == null || wrap.Handle == IntPtr.Zero) continue;

                ImGui.PushID(i);
                if (ImGui.ImageButton(wrap.Handle, new Vector2(iconPx, iconPx)))
                {
                    _onSelect?.Invoke(0, path);
                    IsOpen = false;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Path.GetFileName(path));
                ImGui.PopID();

                col++;
                if (col < cols)
                    ImGui.SameLine();
                else
                    col = 0;
            }

            ImGui.EndChild();
        }
    }

    private static string GetCustomIconsDirectory()
    {
        return Path.Combine(CordiPlugin.PluginInterface.ConfigDirectory.FullName, "Icons");
    }

    private static List<string> ScanCustomIcons(string dir)
    {
        var result = new List<string>();
        if (!Directory.Exists(dir)) return result;

        foreach (var file in Directory.GetFiles(dir))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".webp")
                result.Add(file);
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }
}
