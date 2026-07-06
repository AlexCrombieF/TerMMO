using UnityEngine;

namespace Doodgy.Data
{
    /// <summary>
    /// A crafting recipe: a set of input items -> an output item. Optionally
    /// requires the player to be near a workbench. Data-driven so new recipes are
    /// added as assets, not code.
    /// </summary>
    [CreateAssetMenu(menuName = "Doodgy/Items/Recipe", fileName = "Recipe_")]
    public sealed class Recipe : ScriptableObject
    {
        [System.Serializable]
        public struct Ingredient
        {
            public ItemData item;
            public int count;
        }

        public Ingredient[] inputs;
        public ItemData output;
        [Min(1)] public int outputCount = 1;

        [Tooltip("Placed-object kind the player must be near to craft this " +
                 "(\"Workbench\", \"Furnace\", ...). Empty = craftable anywhere.")]
        public string requiredStation = "";
    }
}
