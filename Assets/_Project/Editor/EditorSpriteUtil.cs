using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Doodgy.EditorTools
{
    /// <summary>Shared helper for loading the Sprite from an imported asset (e.g. an
    /// Aseprite file, whose sprite is a sub-asset / representation).</summary>
    public static class EditorSpriteUtil
    {
        public static Sprite LoadSprite(string path)
        {
            var main = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (main != null) return main;

            foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (o is Sprite s) return s;

            return null;
        }

        /// <summary>All sprites in an asset (e.g. multi-frame Aseprite), ordered by name.</summary>
        public static Sprite[] LoadAllSprites(string path)
        {
            var list = new List<Sprite>();
            var main = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (main != null) list.Add(main);
            foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (o is Sprite s && !list.Contains(s)) list.Add(s);

            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }
    }
}
