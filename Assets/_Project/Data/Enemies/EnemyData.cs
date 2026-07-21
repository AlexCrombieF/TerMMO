using UnityEngine;

namespace Doodgy.Data
{
    /// <summary>
    /// Designer-authored definition of an enemy type (cave rat, later zombies,
    /// bats...). Stats and animation frames live here; behaviour lives in an AI
    /// component chosen by <see cref="aiKind"/> — new enemies are added as
    /// assets, not code, unless they need a brand-new brain.
    /// </summary>
    [CreateAssetMenu(menuName = "Doodgy/Enemies/Enemy Data", fileName = "Enemy_New")]
    public sealed class EnemyData : ScriptableObject
    {
        public string displayName = "Enemy";

        [Header("Stats")]
        [Min(1f)] public float maxHealth = 20f;
        [Min(0f)] public float contactDamage = 8f;
        [Min(0f)] public float moveSpeed = 4f;
        [Min(0f)] public float aggroRange = 9f;
        [Min(0f)] public float xpReward = 10f;

        [Header("Presentation (frame 1 doubles as idle)")]
        public Sprite[] frames;
        [Min(0.1f)] public float animFps = 10f;

        [Header("Loot")]
        public ItemData dropItem;
        [Min(0)] public int dropMin = 1;
        [Min(0)] public int dropMax = 1;
        [Range(0f, 1f)] public float dropChance = 1f;

        [Header("Behaviour")]
        [Tooltip("Which AI component drives it. 'Scurry' = ground chaser (rat).")]
        public string aiKind = "Scurry";
    }
}
