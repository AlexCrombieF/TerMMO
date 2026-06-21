namespace Doodgy.Core
{
    /// <summary>
    /// Tool categories that gate which tiles can be mined. A tile declares the
    /// <see cref="ToolType"/> (and minimum tier) it requires; a tool item
    /// declares the type/tier it provides. The mining system compares the two.
    /// </summary>
    public enum ToolType
    {
        /// <summary>No tool / bare hands. Can only affect tiles that require None.</summary>
        None = 0,

        /// <summary>Stone, ore, and most "hard" foreground tiles.</summary>
        Pickaxe = 1,

        /// <summary>Wood and other choppable tiles.</summary>
        Axe = 2,

        /// <summary>Background walls / placed furniture removal.</summary>
        Hammer = 3,

        /// <summary>Fast pickaxe variant (higher-tier mining).</summary>
        Drill = 4,
    }
}
