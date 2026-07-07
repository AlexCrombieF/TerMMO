namespace Doodgy.Gameplay
{
    /// <summary>
    /// A placed object that carries an item inventory (chest, furnace...). The
    /// save system persists any placed object exposing this, and pickup rules
    /// require it to be empty first.
    /// </summary>
    public interface IHasInventory
    {
        Inventory Inventory { get; }
    }
}
