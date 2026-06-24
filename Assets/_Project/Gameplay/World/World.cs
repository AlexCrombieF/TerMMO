using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Owns the world's chunk data and is the single AUTHORITATIVE entry point
    /// for tile reads/writes. Lives on a GameObject that also has a Grid (the
    /// parent of every chunk Tilemap).
    ///
    /// SERVER-AUTHORITY MODEL: <see cref="SetTile"/> is the "apply result" path.
    /// In multiplayer the server validates an intent, calls SetTile, then
    /// broadcasts. Single-player calls it directly. Clients must never call this
    /// speculatively for anything they don't own — keep edits flowing through
    /// validated intents (see WorldEditController).
    ///
    /// Step 2 scope: generates a fixed FLAT test world. Procedural generation,
    /// streaming (load/unload around players), and the TileDatabase id registry
    /// usage all build on this same API in later steps.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public sealed class World : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private TileDatabase tileDatabase;

        [Header("Procedural generation")]
        [Tooltip("World size in chunks. Streaming comes later; for now the world " +
                 "is a fixed rectangle generated up-front. 8x4 == 256x128 tiles.")]
        [SerializeField] private int worldChunksX = 8;
        [SerializeField] private int worldChunksY = 4;

        [SerializeField] private WorldGenSettings genSettings;

        [Tooltip("World seed. Same seed + same settings == identical world.")]
        [SerializeField] private int seed = 12345;
        [Tooltip("If true, a random seed is chosen at startup (logged to Console).")]
        [SerializeField] private bool randomizeSeed = false;

        private IWorldGenerator _generator;

        private readonly Dictionary<Vector2Int, Chunk> _chunks = new Dictionary<Vector2Int, Chunk>();
        private readonly Dictionary<Vector2Int, ChunkRenderer> _renderers = new Dictionary<Vector2Int, ChunkRenderer>();
        private readonly RuntimeTileResolver _resolver = new RuntimeTileResolver();

        /// <summary>Fired after any authoritative tile change. See <see cref="TileChangedEvent"/>.</summary>
        public event Action<TileChangedEvent> OnTileChanged;

        public TileDatabase Tiles => tileDatabase;

        public int Seed => seed;

        private void Start()
        {
            if (tileDatabase == null)
            {
                Debug.LogError("[World] No TileDatabase assigned.", this);
                return;
            }
            if (genSettings == null)
            {
                Debug.LogError("[World] No WorldGenSettings assigned.", this);
                return;
            }
            GenerateWorld();
        }

        // ---------------------------------------------------------------- reads

        /// <summary>Reads a tile id at an absolute tile coordinate. Air for unloaded areas.</summary>
        public ushort GetTile(Vector2Int tile)
        {
            Vector2Int cc = WorldCoords.TileToChunk(tile);
            if (!_chunks.TryGetValue(cc, out Chunk chunk)) return WorldConstants.AirTileId;
            Vector2Int local = WorldCoords.TileToLocal(tile);
            return chunk.GetLocal(local.x, local.y);
        }

        public bool IsLoaded(Vector2Int tile)
            => _chunks.ContainsKey(WorldCoords.TileToChunk(tile));

        // ---------------------------------------------------- authoritative write

        /// <summary>
        /// Authoritative tile write: updates data, redraws the one cell, raises
        /// <see cref="OnTileChanged"/>. Returns false if nothing changed (out of
        /// loaded world, or same id). Validation (reach/tool/permission) happens
        /// in the caller BEFORE this is invoked.
        /// </summary>
        public bool SetTile(Vector2Int tile, ushort id)
        {
            Vector2Int cc = WorldCoords.TileToChunk(tile);
            if (!_chunks.TryGetValue(cc, out Chunk chunk)) return false;

            Vector2Int local = WorldCoords.TileToLocal(tile);
            ushort prev = chunk.GetLocal(local.x, local.y);
            if (!chunk.SetLocal(local.x, local.y, id)) return false; // unchanged

            _renderers[cc].SetTile(tile, id, tileDatabase, _resolver);
            OnTileChanged?.Invoke(new TileChangedEvent(tile, prev, id));
            return true;
        }

        // --------------------------------------------------------------- chunks

        private Chunk CreateChunk(Vector2Int cc)
        {
            var chunk = new Chunk(cc);

            var go = new GameObject($"Chunk_{cc.x}_{cc.y}");
            go.transform.SetParent(transform, worldPositionStays: false);

            // Tilemap + renderer for visuals.
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();

            // Static collision so the world is solid-ready for the player (step 4).
            // CompositeCollider2D is a later optimization; per-tile colliders are
            // fine at this stage.
            go.AddComponent<TilemapCollider2D>();

            // ChunkRenderer caches the Tilemap in its Awake, which runs on AddComponent.
            var renderer = go.AddComponent<ChunkRenderer>();

            _chunks[cc] = chunk;
            _renderers[cc] = renderer;
            return chunk;
        }

        // ----------------------------------------------------- generation (step 3)

        /// <summary>
        /// (Re)builds the whole fixed-rectangle world from the procedural
        /// generator. Exposed as a context-menu action so you can iterate on
        /// WorldGenSettings while in Play mode. (Editor 'Regenerate' is intended
        /// for Play mode — chunk GameObjects are runtime-only.)
        /// </summary>
        [ContextMenu("Regenerate World")]
        public void GenerateWorld()
        {
            ClearWorld();

            if (randomizeSeed) seed = new System.Random().Next(int.MinValue, int.MaxValue);
            _generator = new ProceduralWorldGenerator(genSettings, seed);

            int size = WorldConstants.ChunkSize;
            for (int cy = 0; cy < worldChunksY; cy++)
            {
                for (int cx = 0; cx < worldChunksX; cx++)
                {
                    var cc = new Vector2Int(cx, cy);
                    Chunk chunk = CreateChunk(cc);
                    _generator.Generate(chunk);
                    _renderers[cc].RenderAll(chunk, tileDatabase, _resolver);
                }
            }

            Debug.Log($"[World] Generated {worldChunksX}x{worldChunksY} chunks " +
                      $"({worldChunksX * size}x{worldChunksY * size} tiles), seed {seed}.");
        }

        private void ClearWorld()
        {
            foreach (ChunkRenderer rend in _renderers.Values)
                if (rend != null) Destroy(rend.gameObject);
            _chunks.Clear();
            _renderers.Clear();
            _resolver.Clear(); // pick up any reassigned tile sprites
        }
    }
}
