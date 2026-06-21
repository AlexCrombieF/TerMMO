namespace Doodgy.Core
{
    /// <summary>
    /// Broad classification of an item, used for UI filtering, equip-slot
    /// validation, and behaviour dispatch. Kept intentionally coarse — fine
    /// detail (which armour slot, which weapon archetype) lives on the
    /// specialised ItemData subclasses added in later build steps.
    /// </summary>
    public enum ItemCategory
    {
        /// <summary>Raw/crafting material (ore, wood, gems).</summary>
        Material = 0,

        /// <summary>A placeable tile/block (links to a TileData via PlacesTile).</summary>
        TileBlock = 1,

        /// <summary>Mining/building tool (pickaxe, axe, hammer).</summary>
        Tool = 2,

        /// <summary>Melee / ranged / magic weapon.</summary>
        Weapon = 3,

        /// <summary>Wearable armour piece.</summary>
        Armor = 4,

        /// <summary>Equippable accessory granting stat modifiers.</summary>
        Accessory = 5,

        /// <summary>Potions, food, single-use items.</summary>
        Consumable = 6,

        /// <summary>Quest / key item, usually non-droppable.</summary>
        Quest = 7,
    }
}
