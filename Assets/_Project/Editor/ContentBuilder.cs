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
        private const string ItemDbPath = "Assets/_Project/Content/Databases/ItemDatabase.asset";

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
            Sprite rawCoalSpr = EditorSpriteUtil.LoadSprite(Items + "RawCoal.aseprite") ?? coalSpr;
            Sprite rawIronSpr = EditorSpriteUtil.LoadSprite(Items + "RawIron.aseprite") ?? ironSpr;
            Sprite doorSpr = EditorSpriteUtil.LoadSprite(Tiles + "Door.aseprite") ?? planksSpr;
            Sprite doorOpenSpr = EditorSpriteUtil.LoadSprite(Tiles + "DoorOpen.aseprite"); // null = fade fallback
            Sprite chestSpr = EditorSpriteUtil.LoadSprite(Tiles + "Chest.aseprite") ?? benchSpr;
            Sprite furnaceSpr = EditorSpriteUtil.LoadSprite(Tiles + "Furnace.aseprite") ?? stoneSpr;
            Sprite ingotSpr = EditorSpriteUtil.LoadSprite(Items + "IronIngot.aseprite") ?? rawIronSpr;
            Sprite woodSwordSpr = EditorSpriteUtil.LoadSprite(Items + "WoodenSword.aseprite");
            Sprite appleSpr = EditorSpriteUtil.LoadSprite(Items + "Apple.aseprite");

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
            ItemData itDoor = LoadOrCreate<ItemData>(Items + "Item_Door.asset");
            ItemData itChest = LoadOrCreate<ItemData>(Items + "Item_Chest.asset");
            ItemData itIngot = LoadOrCreate<ItemData>(Items + "Item_IronIngot.asset");
            ItemData itFurnace = LoadOrCreate<ItemData>(Items + "Item_Furnace.asset");
            ItemData itWoodSword = LoadOrCreate<ItemData>(Items + "Item_WoodenSword.asset");
            ItemData itApple = LoadOrCreate<ItemData>(Items + "Item_Apple.asset");

            // --- configure items ---
            Item(itDirt,  10, "Dirt",        dirtSpr,   ItemCategory.TileBlock, 100, dirtTile);
            Item(itStone, 11, "Stone",       stoneSpr,  ItemCategory.TileBlock, 100, stoneTile);
            Item(itGrass, 12, "Grass Block", grassSpr,  ItemCategory.TileBlock, 100, grassTile);
            Item(itIron,  13, "Raw Iron",    rawIronSpr, ItemCategory.Material, 100, null);
            Item(itCoal,  17, "Raw Coal",    rawCoalSpr, ItemCategory.Material, 100, null);
            Item(itWood,  14, "Wood",        trunkSpr,  ItemCategory.Material,  100, null);
            Item(itPlanks,15, "Wood Planks", planksSpr, ItemCategory.TileBlock, 100, planksTile);
            Item(itTorch, 16, "Torch",       torchSpr,  ItemCategory.TileBlock, 100, torchTile);
            Item(itBench, 18, "Workbench",   benchSpr,  ItemCategory.TileBlock, 100, null);
            SetObject(itBench, benchSpr, 2, 1, "Workbench"); // placed as a 2x1 object
            Item(itDoor,  23, "Door",        doorSpr,   ItemCategory.TileBlock, 100, null);
            // 1x2 so the door reads right next to the 1.8-tile player. The art is
            // 16x48 for now, so it compresses slightly — redraw at 16x32 for pixel-perfect.
            SetObject(itDoor, doorSpr, 1, 2, "Door", doorOpenSpr);
            Item(itChest, 24, "Chest",       chestSpr,  ItemCategory.TileBlock, 100, null);
            SetObject(itChest, chestSpr, 2, 1, "Chest");     // 2x1 placed object, 30-slot storage
            Item(itIngot, 25, "Iron Ingot",  ingotSpr,  ItemCategory.Material,  100, null);
            Item(itFurnace, 26, "Furnace",   furnaceSpr, ItemCategory.TileBlock, 100, null);
            SetObject(itFurnace, furnaceSpr, 2, 2, "Furnace"); // 2x2 smelting station
            // Weapon archetype: damage stats arrive with the combat system; the
            // item exists now so it's craftable and takes a hotbar slot.
            Item(itWoodSword, 27, "Wooden Sword", woodSwordSpr, ItemCategory.Weapon, 1, null);
            Item(itApple, 28, "Apple", appleSpr, ItemCategory.Consumable, 100, null);
            SetHeal(itApple, 15f); // right-click to eat
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
                itTorch, 5, "", (itWood, 1), (itCoal, 1));
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_Workbench.asset"),
                itBench, 1, "", (itWood, 10));
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_Planks.asset"),
                itPlanks, 4, "Workbench", (itWood, 1));
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_StonePickaxe.asset"),
                itStonePick, 1, "Workbench", (itStone, 10), (itWood, 5));
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_Door.asset"),
                itDoor, 1, "Workbench", (itWood, 6));
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_Furnace.asset"),
                itFurnace, 1, "Workbench", (itStone, 25), (itCoal, 10), (itTorch, 1), (itWood, 10));
            // Smelting moved into the furnace itself (input/fuel/output slots) —
            // remove the old crafting-menu ingot recipe if it exists.
            AssetDatabase.DeleteAsset(Recipes + "Recipe_IronIngot.asset");
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_Chest.asset"),
                itChest, 1, "Workbench", (itIngot, 2), (itWood, 10));
            SetRecipe(LoadOrCreate<Recipe>(Recipes + "Recipe_WoodenSword.asset"),
                itWoodSword, 1, "", (itWood, 8));

            // --- furnace data: what burns and what smelts ---
            SetFuel(itWood, 4f);
            SetFuel(itCoal, 12f);
            SetSmelt(itIron, itIngot, 4f);

            // --- torch animation from its Aseprite frames ---
            SetTileAnimation(torchTile, EditorSpriteUtil.LoadAllSprites(Tiles + "Torch.aseprite"), 8f);

            // --- grass edge variants (left edge + right-edge variety pool) ---
            SetEdges(grassTile,
                new[] { EditorSpriteUtil.LoadSprite(Tiles + "GrassTopLeft.aseprite") },
                new[]
                {
                    EditorSpriteUtil.LoadSprite(Tiles + "GrassTopRight.aseprite"),
                    EditorSpriteUtil.LoadSprite(Tiles + "GrassTopRight2.aseprite"),
                    EditorSpriteUtil.LoadSprite(Tiles + "GrassTopRight3.aseprite"),
                });

            RegisterAllTiles();
            RegisterAllItems();

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

        private static void SetObject(ItemData item, Sprite sprite, int w, int h, string kind,
                                      Sprite altSprite = null)
        {
            var so = new SerializedObject(item);
            so.FindProperty("objectSprite").objectReferenceValue = sprite;
            so.FindProperty("objectSize").vector2IntValue = new Vector2Int(w, h);
            so.FindProperty("objectKind").stringValue = kind;
            so.FindProperty("objectAltSprite").objectReferenceValue = altSprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void SetHeal(ItemData item, float amount)
        {
            var so = new SerializedObject(item);
            so.FindProperty("healAmount").floatValue = amount;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void SetFuel(ItemData item, float seconds)
        {
            var so = new SerializedObject(item);
            so.FindProperty("fuelSeconds").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void SetSmelt(ItemData item, ItemData product, float seconds)
        {
            var so = new SerializedObject(item);
            so.FindProperty("smeltsInto").objectReferenceValue = product;
            so.FindProperty("smeltSeconds").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void SetEdges(TileData tile, Sprite[] left, Sprite[] right)
        {
            var so = new SerializedObject(tile);
            SerializedProperty l = so.FindProperty("edgeLeftSprites");
            l.arraySize = left.Length;
            for (int i = 0; i < left.Length; i++)
                l.GetArrayElementAtIndex(i).objectReferenceValue = left[i];
            SerializedProperty r = so.FindProperty("edgeRightSprites");
            r.arraySize = right.Length;
            for (int i = 0; i < right.Length; i++)
                r.GetArrayElementAtIndex(i).objectReferenceValue = right[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
        }

        private static void SetTileAnimation(TileData tile, Sprite[] frames, float fps)
        {
            var so = new SerializedObject(tile);
            SerializedProperty arr = so.FindProperty("animationSprites");
            arr.arraySize = frames != null ? frames.Length : 0;
            for (int i = 0; i < arr.arraySize; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            so.FindProperty("animationFps").floatValue = fps;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
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

        private static void SetRecipe(Recipe r, ItemData output, int outCount, string station,
                                      params (ItemData item, int count)[] inputs)
        {
            var so = new SerializedObject(r);
            so.FindProperty("output").objectReferenceValue = output;
            so.FindProperty("outputCount").intValue = outCount;
            so.FindProperty("requiredStation").stringValue = station;
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

        private static void RegisterAllItems()
        {
            var db = LoadOrCreate<ItemDatabase>(ItemDbPath);

            var all = new List<ItemData>();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/_Project/Content/Items" }))
                all.Add(AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid)));

            var so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("items");
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
