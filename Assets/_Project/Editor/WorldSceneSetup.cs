using UnityEditor;
using UnityEngine;
using Doodgy.Data;
using Doodgy.Gameplay;

namespace Doodgy.EditorTools
{
    /// <summary>
    /// One-click test-scene builder. Menu: Doodgy > Setup Test Scene.
    /// Creates the World (+ Grid), a Player (Rigidbody/Collider/controllers), and
    /// parents the Main Camera to the player, wiring up the content assets by path.
    /// Editor-only convenience — not shipped game code.
    /// </summary>
    public static class WorldSceneSetup
    {
        private const string DbPath = "Assets/_Project/Content/Databases/TileDatabase.asset";
        private const string GenPath = "Assets/_Project/Content/Databases/WorldGenSettings.asset";
        private const string StonePath = "Assets/_Project/Content/Tiles/Tile_Stone.asset";

        [MenuItem("Doodgy/Setup Test Scene")]
        public static void SetupTestScene()
        {
            var db = AssetDatabase.LoadAssetAtPath<TileDatabase>(DbPath);
            var gen = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(GenPath);
            var stone = AssetDatabase.LoadAssetAtPath<TileData>(StonePath);

            if (db == null || gen == null)
            {
                EditorUtility.DisplayDialog("Doodgy",
                    "Couldn't find TileDatabase / WorldGenSettings in Content/Databases.\n" +
                    "Let Unity finish importing, then try again.", "OK");
                return;
            }

            // Idempotent: remove any previous World/Player so re-running this
            // command replaces the setup instead of stacking duplicates.
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

            // --- Player ------------------------------------------------------
            var playerGo = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(playerGo, "Create Player");
            // Above the surface (base 64 + amplitude 14) so it drops onto terrain.
            playerGo.transform.position = new Vector3(128f, 100f, 0f);

            playerGo.AddComponent<Rigidbody2D>();
            var col = playerGo.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.8f, 1.8f);
            col.direction = CapsuleDirection2D.Vertical;

            playerGo.AddComponent<PlayerController>();
            var edit = playerGo.AddComponent<WorldEditController>();

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

            var editSo = new SerializedObject(edit);
            editSo.FindProperty("world").objectReferenceValue = world;
            editSo.FindProperty("worldCamera").objectReferenceValue = cam;
            if (stone != null) editSo.FindProperty("placeTile").objectReferenceValue = stone;
            editSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = worldGo;

            EditorUtility.DisplayDialog("Doodgy",
                "Test scene created.\n\n" +
                "Press Play.\n" +
                "  Move: A/D  -  Jump: Space\n" +
                "  Mine: hold Left-click  -  Place: Right-click\n\n" +
                "Remember to save the scene (Ctrl+S) if you want to keep it.", "OK");
        }
    }
}
