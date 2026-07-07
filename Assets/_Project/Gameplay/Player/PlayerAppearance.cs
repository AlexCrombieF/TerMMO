using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// A player's chosen look: hairstyle index plus tint colours applied to the
    /// white/grayscale character layers. Serializable so it round-trips through
    /// JsonUtility (PlayerPrefs) and the binary save file.
    /// </summary>
    [System.Serializable]
    public struct PlayerAppearance
    {
        public int hairStyle;
        public Color skinColor;
        public Color hairColor;
        public Color eyeColor;

        public static PlayerAppearance Default => new PlayerAppearance
        {
            hairStyle = 0,
            skinColor = new Color(0.93f, 0.76f, 0.60f),
            hairColor = new Color(0.30f, 0.20f, 0.12f),
            eyeColor = new Color(0.25f, 0.45f, 0.85f),
        };
    }
}
