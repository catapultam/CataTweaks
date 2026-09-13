using System.Collections.Generic;

namespace CataTweaks;

// Multi-select TMP dropdowns pack their selection into the bits of an int. Kept free of Unity
// and game types (and of the game's own GetBitIndices extension) so test\ can compile it.
internal static class BitFilter
{
    // Bits past the end of the option list are the overflow entry or stale selections left over
    // from a longer list, and select nothing.
    internal static List<int> SelectedIndices(int value, int count)
    {
        var selected = new List<int>();
        for (int i = 0; i < 31 && i < count; i++)
        {
            if ((value & (1 << i)) != 0)
            {
                selected.Add(i);
            }
        }
        return selected;
    }
}
