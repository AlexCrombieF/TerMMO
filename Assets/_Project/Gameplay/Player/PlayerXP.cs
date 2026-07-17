using System;
using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// XP and levels. Curve per the design spec: XP_to_next = base * level^exponent,
    /// both exposed for balancing. Kills grant XP now; mining/crafting XP and
    /// stat/skill points on level-up arrive with the full stat system (step 6) —
    /// this is the foundation they hook into.
    /// </summary>
    public sealed class PlayerXP : MonoBehaviour
    {
        [SerializeField] private float xpBase = 50f;
        [SerializeField] private float xpExponent = 1.5f;

        public int Level { get; private set; } = 1;
        public float Xp { get; private set; }
        public float XpToNext => xpBase * Mathf.Pow(Level, xpExponent);

        /// <summary>Raised on any XP gain or level-up (UI listens).</summary>
        public event Action Changed;

        public void AddXP(float amount)
        {
            if (amount <= 0f) return;
            Xp += amount;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                Debug.Log($"[XP] Level up! Now level {Level}.");
            }
            Changed?.Invoke();
        }
    }
}
