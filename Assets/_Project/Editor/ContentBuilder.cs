using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.EditorTools
{
    /// <summary>
    /// Generates / updates the ItemData, TileData and Recipe assets and wires their
    /// sprites, drops, place-tile links and ingredients — then registers tiles in
    /// the TileDatabase. Idempotent: re-run any time. Menu: Doodgy.
    /// </summary>
    public static class ContentBuilder
    {
        private const string Tiles = "Assets/_Project/Content/Tiles/";
        private const string Items = "Assets/_Project/Content/Items/";
        private const string Recipes = "Assets/_Project/Content/Recipes/";
        private const string DbPath = "Assets/_Project/Content/Databases/TileDatabase.asset";

        [MenuItem("Doodgy/Build Content")]
        public static void Build()
        {
            EnsureFolder("Recipes");

            // --- sprites (some may be null until art is added; placeholders cover it) ---
            Sprite dirtSpr = EditorSpriteUtil.LoadSprite(Tiles + "Dirt.aseprite");
            Sprite stoneSpr = EditorSpriteUtil.LoadSprite(Tiles + "Stone.aseprite");
            Sprite grassSpr = EditorSpriteUtil.LoadSprite(Tiles + "Grass.aseprite");
            Sprite ironSpr = EditorSpriteUtil.LoadSprite(Tiles + "IronOre.aseprite");
            Sprite coalSpr = EditorSpriteUtil.LoadSprite(Tiles + "CoalOre.aseprite");
            Sprite trunkSpr = EditorSpriteUtil.LoadSprite(Tiles + "TreeTrunk.aseprite");
            Sprite planksSpr = EditorSpriteUtil.LoadSprite(Tiles + "WoodPlanks.aseprite");
            Sprite torchSpr = EditorSpriteUtil.LoadSprite(Tiles + "Torch.aseprite");
            Sprite benchSpr = EditorSpriteUtil.LoadSprite(Tiles + "Workbench.aseprite");
            Sprite pickSpr = EditorSpriteUtil.LoadSprite(Items + "WoodenPickaxe.aseprite");
            Sprite axeSpr = EditorSpriteUtil.LoadSprite(Items + "WoodenAxe.aseprite");
            Sprite stonePickSpr = EditorSpriteUtil.LoadSprite(Items + "StonePickaxe.aseprite") ?? pickSpr;

            // --- existing tiles (only refresh drops) ---
            TileData dirtTile = Load<TileData>(Tiles + "Tile_Dirt.asset");
            TileData stoneTile = Load<TileData>(Tiles + "Tile_Stone.asset");
            TileData grassTile = Load<TileData>(Tiles + "Tile_Grass.asset");
            TileData ironTile = Load<TileData>(Tiles + "Tile_Ore.asset");

            // --- new tiles ---
            TileData torchTile = LoadOrCreate<TileData>(Tiles + "Tile_Torch.asset");
            TileData planksTile = LoadOrCreate<TileData>(Tiles + "Tile_WoodPlanks.asset");
            TileData coalTile = LoadOrCreate<TileData>(Tiles + "Tile_Coal.asset");
            TileData benchTile = LoadOrCreate<TileData>(Tiles + "Tile_Workbench.asset");

            // --- items ---
            ItemData itDirt = LoadOrCreate<ItemData>(Items + "Item_Dirt.asset");
            ItemData itStone = LoadOrCreate<ItemData>(Items + "Item_Stone.asset");
            ItemData itGrass = LoadOrCreate<ItemData>(Items + "Item_Grass.asset");
            ItemData itIron = LoadOrCreate<ItemData>(Items + "Item_IronOre.asset");
            ItemData itCoal = LoadOrCreate<ItemData>(Items + "Item_Coal.asset");
            ItemData itWood = LoadOrCreate<ItemData>(Items + "Item_Wood.asset");
            ItemData itPlanks = LoadOrCreate<ItemData>(Items + "Item_WoodPlanks.asset");
            ItemData itTorch = LoadOrCreate<ItemData>(Items + "Item_Torch.asset");
            ItemData itBench = LoadOrCreate<ItemData>(Items + "Item_Workbench.asset");
            ItemData itPick = LoadOrCreate<ItemData>(Items + "Item_WoodenPickaxe.asset");
            ItemData itAxe = LoadOrCreate<ItemData>(Items + "Item_WoodenAxe.asset");
            ItemData itStonePick = LoadOrCreate<ItemData>(Items + "Item_StonePickaxe.asset");

            // --- configure items ---
            Item(itDirt,  10, "Dirt",        dirtSpr,   ItemCategory.TileBlock, 999, dirtTile);
            Item(itStone, 11, "Stone",       stoneSpr,  ItemCategory.TileBlock, 999, stoneTile);
            Item(itGrass, 12, "Grass Block", grassSpr,  ItemCategory.TileBlock, 999, grassTile);
            Item(itIron,  13, "Iron Ore",    ironSpr,   ItemCategory.Material,  999, null);
            Item(itCoal,  17, "Coal",        coalSpr,   ItemCategory.Material,  999, null);
            Item(itWood,  14, "Wood",        trunkSpr,  ItemCategory.Material,  999, null);
            Item(itPlanks,15, "Wood Planks", planksSpr, ItemCategory.TileBlock, 999, planksTile);
            Item(itTorch, 16, "Torch",       torchSpr,  ItemCategory.TileBlock, 999, torchTile);
            Item(itBench, 18, "Workbench",   benchSpr,  ItemCategory.TileBlock, 99,  null);
            SetObject(itBench, benchSpr, 2, 1, "Workbench"); // placed as a 2x1 object
            Tool(itPick,      20, "Wooden Pickaxe", pickSpr,      ToolType.Pickaxe, 1, 2.2f, 5f);
            Tool(itAxe,       21, "Wooden Axe",     axeSpr,       ToolType.Axe,     1, 3f,   5f);
            Tool(itStonePick, 22, "Stone Pickaxe",  stonePickSpr, ToolType.Pickaxe, 2, 5f,   5f);

            // --- configure new tiles ---
            Tile(torchTile, 5, "Torch", torchSpr,
                solid: false, hardness: 0.2f, required: ToolType.None,
                drop: itTorch, blocksLight: false, emits: true, lightIntensity: 0.9f);
            Tile(planksTile, 6, "Wood Planks", planksSpr,
                solid: true, hardness: 2f, required: ToolType.None,
                drop: itPlanks, blocksLight: true, emits: false, lightIntensity: 0f);
            Tile(coalTile, 7, "Coal Ore", coalSpr,
                solid: true, hardness: 3f, required: ToolType.Pickaxe,
                drop: itCoal, blocksLight: true, emits: false, lightIntensity: 0f);
            if (coalSpr == null) SetTint(coalTile, new Color(0.13f, 0.13f, 0.15f)); // dark placeholder
            Tile(benchTile, 8, "Workbench", benchSpr,
                solid: true, hardness: 1.5f, required: ToolType.None,
                drop: itBench, blocksLight: false, emits: false, lightIntensity: 0f);

            // --- drops on existing tiles ---
            SetDrop(dirtTile, itDirt);
            SetDrop(stoneTile, itStone);
            SetDrop(grassTile, itDirt);   // grass yields dirt (Terraria-style)
            SetDrop(ironTile, itIron);
            // Iron requires a tier-2 pickaxe (Stone Pickaxe); wooden (tier 1) can't mine it.
            SetMiningGate(ironTile, ToolType.Pickaxe, 2);

            // --- recipes ---
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_Torch.asset"),
                itTorch, 5, false, (itWood, 1), (itCoal, 1));
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_Workbench.asset"),
                itBench, 1, false, (itWood, 10));
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_Planks.asset"),
                itPlanks, 4, true, (itWood, 1));
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_StonePickaxe.asset"),
                itStonePick, 1, true, (itStone, 10), (itWood, 5));

            RegisterAllTiles();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Doodgy",
                "Content built: items, tiles (incl. coal + workbench), and recipes.\n\n" +
                "Run 'Doodgy > Setup Test Scene' and press Play. Press C to craft.",
                "OK");
        }

        // ---------- configuration helpers ----------

        private static void Item(ItemData item, int id, string name, Sprite icon,
                                 ItemCategory cat, int maxStack, TileData places)
        {
            var so = new SerializedObject(item);
            so.FindProperty("id").intValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("category").enumValueIndex = (int)cat;
            so.FindProperty("maxStackSize").intValue = maxStack;
            so.FindProperty("placesTile").objectReferenceValue = places;
            so.FindProperty("toolType").enumValueIndex = (int)ToolType.None;
            so.FindProperty("toolTier").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void Tool(ItemData item, int id, string name, Sprite icon,
                                 ToolType tool, int tier, float power, float reach)
        {
            var so = new SerializedObject(item);
            so.FindProperty("id").intValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("category").enumValueIndex = (int)ItemCategory.Tool;
            so.FindProperty("maxStackSize").intValue = 1;
            so.FindProperty("placesTile").objectReferenceValue = null;
            so.FindProperty("toolType").enumValueIndex = (int)tool;
            so.FindProperty("toolTier").intValue = tier;
            so.FindProperty("miningPower").floatValue = power;
            so.FindProperty("reach").floatValue = reach;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void Tile(TileData tile, int id, string name, Sprite sprite,
                                 bool solid, float hardness, ToolType required, ItemData drop,
                                 bool blocksLight, bool emits, float lightIntensity)
        {
            var so = new SerializedObject(tile);
            so.FindProperty("id").intValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("sprite").objectReferenceValue = sprite;
            so.FindProperty("isSolid").boolValue = solid;
            so.FindProperty("hardness").floatValue = hardness;
            so.FindProperty("requiredTool").enumValueIndex = (int)required;
            so.FindProperty("minToolTier").intValue = 0;
            so.FindProperty("dropItem").objectReferenceValue = drop;
            so.FindProperty("dropMin").intValue = 1;
            so.FindProperty("dropMax").intValue = 1;
            so.FindProperty("blocksLight").boolValue = blocksLight;
            so.FindProperty("emitsLight").boolValue = emits;
            so.FindProperty("lightIntensity").floatValue = lightIntensity;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
        }

        private static void SetObject(ItemData item, Sprite sprite, int w, int h, string kind)
        {
            var so = new SerializedObject(item);
            so.FindProperty("objectSprite").objectReferenceValue = sprite;
            so.FindProperty("objectSize").vector2IntValue = new Vector2Int(w, h);
            so.FindProperty("objectKind").stringValue = kind;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void SetTint(TileData tile, Color c)
        {
            var so = new SerializedObject(tile);
            so.FindProperty("tint").colorValue = c;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
        }

        private static void SetDrop(TileData tile, ItemData drop)
        {
            if (tile == null) return;
            var so = new SerializedObject(tile);
            so.FindProperty("dropItem").objectReferenceValue = drop;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
        }

        private static void SetMiningGate(TileData tile, ToolType required, int minTier)
        {
            if (tile == null) return;
            var so = new SerializedObject(tile);
            so.FindProperty("requiredTool").enumValueIndex = (int)required;
            so.FindProperty("minToolTier").intValue = minTier;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
        }

        private static void SetRecipe(Recipe r, ItemData output, int outCount, bool bench,
                                      params (ItemData item, int count)[] inputs)
        {
            var so = new SerializedObject(r);
            so.FindProperty("output").objectReferenceValue = output;
            so.FindProperty("outputCount").intValue = outCount;
            so.FindProperty("requiresWorkbench").boolValue = bench;
            SerializedProperty arr = so.FindProperty("inputs");
            arr.arraySize = inputs.Length;
            for (int i = 0; i < inputs.Length; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("item").objectReferenceValue = inputs[i].item;
                e.FindPropertyRelative("count").intValue = inputs[i].count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(r);
        }

        private static void RegisterAllTiles()
        {
            var db = Load<TileDatabase>(DbPath);
            if (db == null) return;

            var all = new List<TileData>();
            foreach (string guid in AssetDatabase.FindAssets("t:TileData", new[] { "Assets/_Project/Content/Tiles" }))
                all.Add(AssetDatabase.LoadAssetAtPath<TileData>(AssetDatabase.GUIDToAssetPath(guid)));

            var so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("tiles");
            list.arraySize = all.Count;
            for (int i = 0; i < all.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
        }

        // ---------- asset helpers ----------

        private static void EnsureFolder(string name)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Content/" + name))
                AssetDatabase.CreateFolder("Assets/_Project/Content", name);
        }

        private static T Load<T>(string path) where T : Object
            => AssetDatabase.LoadAssetAtPath<T>(path);

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            return a;
        }
    }
}
