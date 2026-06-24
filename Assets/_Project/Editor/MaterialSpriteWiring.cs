using UnityEditor;
using UnityEngine;
using Doodgy.Data;

namespace Doodgy.EditorTools
{
    /// <summary>
    /// Assigns the imported Dirt/Grass Aseprite sprites onto their TileData assets.
    /// Done in code because Aseprite-imported sprite sub-asset ids aren't known
    /// until Unity imports the files, so they can't be referenced by hand-edited
    /// YAML. Re-run any time after changing the source art. Menu: Doodgy.
    /// </summary>
    public static class MaterialSpriteWiring
    {
        private const string Dir = "Assets/_Project/Content/Tiles/";

        // (source .aseprite, target Tile_*.asset) pairs to wire.
        private static readonly (string sprite, string tile)[] Mappings =
        {
            (Dir + "Dirt.aseprite",    Dir + "Tile_Dirt.asset"),
            (Dir + "Grass.aseprite",   Dir + "Tile_Grass.asset"),
            (Dir + "Stone.aseprite",   Dir + "Tile_Stone.asset"),
            (Dir + "IronOre.aseprite", Dir + "Tile_Ore.asset"),
        };

        [MenuItem("Doodgy/Wire Material Sprites")]
        public static void Wire()
        {
            int wired = 0;
            foreach (var (sprite, tile) in Mappings)
                wired += Assign(sprite, tile) ? 1 : 0;

            if (wired > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("Doodgy",
                $"Wired {wired} of {Mappings.Length} material sprites.\n\n" +
                (wired < Mappings.Length
                    ? "If a sprite is missing, let Unity finish importing the .aseprite files and run this again."
                    : "Tiles now use your sprites. Regenerate the world to see them."),
                "OK");
        }

        private static bool Assign(string spritePath, string tilePath)
        {
            Sprite sprite = LoadSprite(spritePath);
            var tile = AssetDatabase.LoadAssetAtPath<TileData>(tilePath);
            if (sprite == null || tile == null)
            {
                Debug.LogWarning($"[Doodgy] Could not wire '{spritePath}' -> '{tilePath}' " +
                                 $"(sprite={(sprite != null)}, tile={(tile != null)}).");
                return false;
            }

            var so = new SerializedObject(tile);
            so.FindProperty("sprite").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
            return true;
        }

        // The Aseprite importer exposes the frame as a Sprite sub-asset, so it may
        // not be the main asset — search both the main object and representations.
        private static Sprite LoadSprite(string path)
        {
            var main = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (main != null) return main;

            foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (o is Sprite s) return s;

            return null;
        }
    }
}
