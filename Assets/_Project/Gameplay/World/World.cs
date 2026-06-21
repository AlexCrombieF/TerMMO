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

        [Header("Flat test world")]
        [Tooltip("World width/height in chunks. 4x2 == 128x64 tiles.")]
        [SerializeField] private int worldChunksX = 4;
        [SerializeField] private int worldChunksY = 2;

        [Tooltip("Tile id used for the top soil layer.")]
        [SerializeField] private ushort surfaceTileId = 1;   // e.g. Dirt
        [Tooltip("Tile id used below the soil layer.")]
        [SerializeField] private ushort deepTileId = 2;      // e.g. Stone

        [Tooltip("Height (in tiles, from y=0) of the solid ground surface.")]
        [SerializeField] private int surfaceHeight = 40;
        [Tooltip("How many tiles of soil sit on top of the deep layer.")]
        [SerializeField] private int soilDepth = 6;

        private readonly Dictionary<Vector2Int, Chunk> _chunks = new Dictionary<Vector2Int, Chunk>();
        private readonly Dictionary<Vector2Int, ChunkRenderer> _renderers = new Dictionary<Vector2Int, ChunkRenderer>();
        private readonly RuntimeTileResolver _resolver = new RuntimeTileResolver();

        /// <summary>Fired after any authoritative tile change. See <see cref="TileChangedEvent"/>.</summary>
        public event Action<TileChangedEvent> OnTileChanged;

        public TileDatabase Tiles => tileDatabase;

        private void Start()
        {
            if (tileDatabase == null)
            {
                Debug.LogError("[World] No TileDatabase assigned.", this);
                return;
            }
            GenerateFlatTestWorld();
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

        // ----------------------------------------------------- test world (step 2)

        private void GenerateFlatTestWorld()
        {
            int size = WorldConstants.ChunkSize;

            for (int cy = 0; cy < worldChunksY; cy++)
            {
                for (int cx = 0; cx < worldChunksX; cx++)
                {
                    var cc = new Vector2Int(cx, cy);
                    Chunk chunk = CreateChunk(cc);

                    // Fill: stone below, a band of soil on top, air above surface.
                    int baseX = cx * size;
                    int baseY = cy * size;
                    for (int ly = 0; ly < size; ly++)
                    {
                        int worldY = baseY + ly;
                        ushort id;
                        if (worldY >= surfaceHeight) id = WorldConstants.AirTileId;
                        else if (worldY >= surfaceHeight - soilDepth) id = surfaceTileId;
                        else id = deepTileId;

                        if (id == WorldConstants.AirTileId) continue;
                        for (int lx = 0; lx < size; lx++)
                            chunk.SetLocal(lx, ly, id);
                    }
                    chunk.ClearDirty(); // generated state is the baseline, not a player edit

                    _renderers[cc].RenderAll(chunk, tileDatabase, _resolver);
                }
            }

            Debug.Log($"[World] Generated flat test world: {worldChunksX}x{worldChunksY} chunks " +
                      $"({worldChunksX * size}x{worldChunksY * size} tiles).");
        }
    }
}
