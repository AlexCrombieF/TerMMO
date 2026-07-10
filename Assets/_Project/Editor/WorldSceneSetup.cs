using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Doodgy.Data;
using Doodgy.Gameplay;

namespace Doodgy.EditorTools
{
    /// <summary>
    /// One-click test-scene builder. Menu: Doodgy > Setup Test Scene.
    /// Creates the World (+ Grid + lighting), a Player (movement, inventory, hotbar
    /// HUD, world editing), and parents the Main Camera to the player. Wires content
    /// assets by path and grants a starting kit. Editor-only convenience.
    ///
    /// Run 'Doodgy > Build Content' first so the item/tile assets exist.
    /// </summary>
    public static class WorldSceneSetup
    {
        private const string DbPath = "Assets/_Project/Content/Databases/TileDatabase.asset";
        private const string GenPath = "Assets/_Project/Content/Databases/WorldGenSettings.asset";
        private const string Items = "Assets/_Project/Content/Items/";
        private const string Tiles = "Assets/_Project/Content/Tiles/";
        private const string UI = "Assets/_Project/Content/UI/";
        private const string FX = "Assets/_Project/Content/FX/";

        [MenuItem("Doodgy/Setup Test Scene")]
        public static void SetupTestScene()
        {
            var db = AssetDatabase.LoadAssetAtPath<TileDatabase>(DbPath);
            var gen = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(GenPath);

            if (db == null || gen == null)
            {
                EditorUtility.DisplayDialog("Doodgy",
                    "Couldn't find TileDatabase / WorldGenSettings in Content/Databases.\n" +
                    "Let Unity finish importing, then try again.", "OK");
                return;
            }

            // Idempotent: remove any previous World/Player so re-running replaces
            // the setup instead of stacking duplicates.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "World" || root.name == "Player")
                    Undo.DestroyObjectImmediate(root);
            }

            // --- World -------------------------------------------------------
            var worldGo = new GameObject("World");
            Undo.RegisterCreatedObjectUndo(worldGo, "Create World");
            var grid = worldGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            var world = worldGo.AddComponent<World>();

            var worldSo = new SerializedObject(world);
            worldSo.FindProperty("tileDatabase").objectReferenceValue = db;
            worldSo.FindProperty("genSettings").objectReferenceValue = gen;
            worldSo.ApplyModifiedPropertiesWithoutUndo();

            worldGo.AddComponent<LightingSystem>(); // tile-based darkness/skylight

            var walls = worldGo.AddComponent<BackgroundWalls>();
            var wallsSo = new SerializedObject(walls);
            wallsSo.FindProperty("world").objectReferenceValue = world;
            wallsSo.FindProperty("wallSprite").objectReferenceValue = EditorSpriteUtil.LoadSprite(Tiles + "CaveWall.aseprite");
            wallsSo.ApplyModifiedPropertiesWithoutUndo();

            var trees = worldGo.AddComponent<TreeSpawner>();
            var treeSo = new SerializedObject(trees);
            treeSo.FindProperty("world").objectReferenceValue = world;
            treeSo.FindProperty("trunkSprite").objectReferenceValue = EditorSpriteUtil.LoadSprite(Tiles + "TreeTrunk.aseprite");
            treeSo.FindProperty("canopySprite").objectReferenceValue = EditorSpriteUtil.LoadSprite(Tiles + "TreeCanopy.aseprite");
            treeSo.FindProperty("stumpSprite").objectReferenceValue = EditorSpriteUtil.LoadSprite(Tiles + "TreeStump.aseprite");
            treeSo.FindProperty("woodItem").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemData>(Items + "Item_Wood.asset");
            treeSo.ApplyModifiedPropertiesWithoutUndo();

            // Surface decorations: uses every sprite dropped into Content/Decor.
            var decor = worldGo.AddComponent<DecorationSpawner>();
            var decorSo = new SerializedObject(decor);
            decorSo.FindProperty("world").objectReferenceValue = world;
            string[] decorGuids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/_Project/Content/Decor" });
            SerializedProperty dsArr = decorSo.FindProperty("sprites");
            dsArr.arraySize = decorGuids.Length;
            for (int i = 0; i < decorGuids.Length; i++)
                dsArr.GetArrayElementAtIndex(i).objectReferenceValue =
                    EditorSpriteUtil.LoadSprite(AssetDatabase.GUIDToAssetPath(decorGuids[i]));
            decorSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Player ------------------------------------------------------
            var playerGo = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(playerGo, "Create Player");
            playerGo.transform.position = new Vector3(128f, 100f, 0f); // drops onto terrain

            playerGo.AddComponent<Rigidbody2D>();
            var col = playerGo.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.8f, 1.8f);
            col.direction = CapsuleDirection2D.Vertical;

            playerGo.AddComponent<PlayerController>();
            var inv = playerGo.AddComponent<PlayerInventory>();
            var edit = playerGo.AddComponent<WorldEditController>();
            var hud = playerGo.AddComponent<HotbarHUD>();
            var craft = playerGo.AddComponent<CraftingPanel>();
            var cracks = playerGo.AddComponent<MiningCrackOverlay>();
            var backpack = playerGo.AddComponent<InventoryUI>();
            var saves = playerGo.AddComponent<SaveSystem>();
            var look = playerGo.AddComponent<PlayerAppearanceRenderer>();
            var creator = playerGo.AddComponent<CharacterCreationUI>();

            // --- Camera (parented to player so it follows) -------------------
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 12f;
            cam.transform.SetParent(playerGo.transform);
            cam.transform.localPosition = new Vector3(0f, 0f, -10f);

            // Pixel Perfect Camera: snaps rendering to the 16px grid, which kills
            // the hairline seams between chunk tilemap meshes (and sharpens art).
            // Added via reflection so this editor assembly needs no URP reference.
            var ppcType = System.Type.GetType(
                "UnityEngine.Rendering.Universal.PixelPerfectCamera, Unity.RenderPipelines.Universal.Runtime");
            if (ppcType != null)
            {
                Component ppc = cam.GetComponent(ppcType);
                if (ppc == null) ppc = cam.gameObject.AddComponent(ppcType);
                var ppcSo = new SerializedObject(ppc);
                ppcSo.FindProperty("m_AssetsPPU").intValue = 16;
                ppcSo.FindProperty("m_RefResolutionX").intValue = 640;
                ppcSo.FindProperty("m_RefResolutionY").intValue = 360;
                ppcSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[Doodgy] URP PixelPerfectCamera type not found — chunk seams may show.");
            }

            // --- Wiring ------------------------------------------------------
            var editSo = new SerializedObject(edit);
            editSo.FindProperty("world").objectReferenceValue = world;
            editSo.FindProperty("worldCamera").objectReferenceValue = cam;
            editSo.FindProperty("inventory").objectReferenceValue = inv;
            editSo.ApplyModifiedPropertiesWithoutUndo();

            GrantStartingKit(inv);

            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("slotFrame").objectReferenceValue = EditorSpriteUtil.LoadSprite(UI + "SlotFrame.aseprite");
            hudSo.FindProperty("slotHighlight").objectReferenceValue = EditorSpriteUtil.LoadSprite(UI + "SlotHighlight.aseprite");
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var craftSo = new SerializedObject(craft);
            craftSo.FindProperty("world").objectReferenceValue = world;
            craftSo.FindProperty("slotFrame").objectReferenceValue = EditorSpriteUtil.LoadSprite(UI + "SlotFrame.aseprite");
            string[] recipeGuids = AssetDatabase.FindAssets("t:Recipe", new[] { "Assets/_Project/Content/Recipes" });
            SerializedProperty rp = craftSo.FindProperty("recipes");
            rp.arraySize = recipeGuids.Length;
            for (int i = 0; i < recipeGuids.Length; i++)
                rp.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(recipeGuids[i]));
            craftSo.ApplyModifiedPropertiesWithoutUndo();

            var crackSo = new SerializedObject(cracks);
            crackSo.FindProperty("editor").objectReferenceValue = edit;
            Sprite[] crackFrames =
            {
                EditorSpriteUtil.LoadSprite(FX + "Crack1.aseprite"),
                EditorSpriteUtil.LoadSprite(FX + "Crack2.aseprite"),
                EditorSpriteUtil.LoadSprite(FX + "Crack3.aseprite"),
                EditorSpriteUtil.LoadSprite(FX + "Crack4.aseprite"),
                EditorSpriteUtil.LoadSprite(FX + "Crack5.aseprite"),
            };
            SerializedProperty cf = crackSo.FindProperty("crackFrames");
            cf.arraySize = crackFrames.Length;
            for (int i = 0; i < crackFrames.Length; i++)
                cf.GetArrayElementAtIndex(i).objectReferenceValue = crackFrames[i];
            crackSo.ApplyModifiedPropertiesWithoutUndo();

            var backpackSo = new SerializedObject(backpack);
            backpackSo.FindProperty("slotFrame").objectReferenceValue = EditorSpriteUtil.LoadSprite(UI + "SlotFrame.aseprite");
            backpackSo.ApplyModifiedPropertiesWithoutUndo();

            var savesSo = new SerializedObject(saves);
            savesSo.FindProperty("world").objectReferenceValue = world;
            savesSo.FindProperty("itemDatabase").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/_Project/Content/Databases/ItemDatabase.asset");
            savesSo.ApplyModifiedPropertiesWithoutUndo();

            // Character appearance layers (empty until the art is drawn — the
            // creator falls back to a tinted placeholder).
            const string PlayerArt = "Assets/_Project/Content/Player/";
            var lookSo = new SerializedObject(look);
            lookSo.FindProperty("bodySprite").objectReferenceValue = EditorSpriteUtil.LoadSprite(PlayerArt + "PlayerBody.aseprite");
            lookSo.FindProperty("clothesSprite").objectReferenceValue = EditorSpriteUtil.LoadSprite(PlayerArt + "PlayerClothes.aseprite");
            lookSo.FindProperty("eyesBaseSprite").objectReferenceValue = EditorSpriteUtil.LoadSprite(PlayerArt + "PlayerEyesBase.aseprite");
            lookSo.FindProperty("eyesSprite").objectReferenceValue = EditorSpriteUtil.LoadSprite(PlayerArt + "PlayerEyes.aseprite");
            Sprite[] walkFrames = EditorSpriteUtil.LoadAllSprites(PlayerArt + "PlayerWalk.aseprite");
            SerializedProperty walkArr = lookSo.FindProperty("walkFrames");
            walkArr.arraySize = walkFrames.Length;
            for (int i = 0; i < walkFrames.Length; i++)
                walkArr.GetArrayElementAtIndex(i).objectReferenceValue = walkFrames[i];

            string[] hairGuids = AssetDatabase.FindAssets("Hair t:Sprite", new[] { "Assets/_Project/Content/Player" });
            SerializedProperty hairArr = lookSo.FindProperty("hairStyles");
            hairArr.arraySize = hairGuids.Length;
            for (int i = 0; i < hairGuids.Length; i++)
                hairArr.GetArrayElementAtIndex(i).objectReferenceValue =
                    EditorSpriteUtil.LoadSprite(AssetDatabase.GUIDToAssetPath(hairGuids[i]));
            lookSo.ApplyModifiedPropertiesWithoutUndo();

            // Gameplay is locked until the character creator's Start is pressed.
            var creatorSo = new SerializedObject(creator);
            SerializedProperty dis = creatorSo.FindProperty("disableWhileOpen");
            var toDisable = new Behaviour[] { playerGo.GetComponent<PlayerController>(), edit, inv, backpack, craft, saves };
            dis.arraySize = toDisable.Length;
            for (int i = 0; i < toDisable.Length; i++)
                dis.GetArrayElementAtIndex(i).objectReferenceValue = toDisable[i];
            creatorSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = worldGo;

            EditorUtility.DisplayDialog("Doodgy",
                "Test scene created.\n\n" +
                "Press Play.\n" +
                "  Move: A/D  -  Jump: Space\n" +
                "  Mine: hold Left-click  -  Place: Right-click\n" +
                "  Chop trees: select Axe, hold Left-click\n" +
                "  Hotbar: number keys / scroll wheel  -  Craft: C\n" +
                "  Backpack: E  -  Save: F5  -  Load: F9\n\n" +
                "Save the scene (Ctrl+S) to keep it.", "OK");
        }

        private static void GrantStartingKit(PlayerInventory inv)
        {
            var kit = new (string path, int count)[]
            {
                (Items + "Item_WoodenPickaxe.asset", 1),
                (Items + "Item_WoodenAxe.asset", 1),
                (Items + "Item_Torch.asset", 5),
            };

            var found = new List<(ItemData item, int count)>();
            foreach ((string path, int count) in kit)
            {
                var it = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (it != null) found.Add((it, count));
            }

            var so = new SerializedObject(inv);
            SerializedProperty arr = so.FindProperty("startingItems");
            arr.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("item").objectReferenceValue = found[i].item;
                e.FindPropertyRelative("count").intValue = found[i].count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (found.Count == 0)
                Debug.LogWarning("[Doodgy] No item assets found — run 'Doodgy > Build Content' first " +
                                 "so the starting kit can be granted.");
        }
    }
}
