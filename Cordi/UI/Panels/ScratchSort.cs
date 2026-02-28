using System;
using Dalamud.Bindings.ImGui;

namespace Cordi.UI.Panels
{
    public unsafe class ScratchSort
    {
        public void Test()
        {
            var specs = ImGui.TableGetSortSpecs();
            if (specs.SpecsCount > 0)
            {
                var spec = specs.Specs;
                var colIndex = spec.ColumnIndex;
                var dir = spec.SortDirection;
                if (dir == ImGuiSortDirection.Ascending) { }
            }
        }
    }
}
