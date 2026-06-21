namespace Doodgy.Core
{
    /// <summary>
    /// Item rarity tier. Drives name colour in UI and, for gear, the number/size
    /// of random stat rolls (wired up in the gear step). Ordered low → high so it
    /// can be compared numerically.
    /// </summary>
    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
    }
}
