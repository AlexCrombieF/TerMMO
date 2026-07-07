using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Versioned binary save/load for the whole session. F5 saves, F9 loads.
    ///
    /// The world saves as seed + ONLY the chunks modified since generation
    /// (Chunk.Dirty), so files stay tiny: loading regenerates from the seed and
    /// overwrites just the edited chunks. Items and tiles serialize by their
    /// stable numeric ids via ItemDatabase / chunk arrays.
    ///
    /// Authority note: in multiplayer this runs server-side only — the server
    /// owns the world file and player state. Format:
    ///   magic "DGY1", version, seed,
    ///   dirty chunks (count, then coord + tile array each),
    ///   player (position, selected slot, inventory slots as id+count),
    ///   placed objects (count, then item id + world position each).
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class SaveSystem : MonoBehaviour
    {
        private const string Magic = "DGY1";
        private const int Version = 3; // v2: storage contents; v3: player appearance

        [SerializeField] private World world;
        [SerializeField] private ItemDatabase itemDatabase;

        private PlayerInventory _player;

        private string SavePath => Path.Combine(Application.persistentDataPath, "doodgy_save0.bin");

        private void Awake() => _player = GetComponent<PlayerInventory>();

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || world == null) return;
            if (kb.f5Key.wasPressedThisFrame) Save();
            if (kb.f9Key.wasPressedThisFrame) Load();
        }

        // ------------------------------------------------------------------ save

        public void Save()
        {
            using (var w = new BinaryWriter(File.Create(SavePath)))
            {
                w.Write(Magic);
                w.Write(Version);
                w.Write(world.Seed);

                // Only chunks modified since generation.
                var dirty = new List<Chunk>();
                foreach (Chunk c in world.Chunks)
                    if (c.Dirty) dirty.Add(c);

                w.Write(dirty.Count);
                foreach (Chunk c in dirty)
                {
                    w.Write(c.Coord.x);
                    w.Write(c.Coord.y);
                    ushort[] tiles = c.Raw;
                    for (int i = 0; i < tiles.Length; i++) w.Write(tiles[i]);
                }

                // Player: position + hotbar selection + inventory by item id.
                Vector3 pos = _player.transform.position;
                w.Write(pos.x);
                w.Write(pos.y);
                w.Write(_player.Selected);
                w.Write(_player.Inventory.Size);
                for (int i = 0; i < _player.Inventory.Size; i++)
                {
                    ItemStack s = _player.Inventory.Get(i);
                    w.Write(s.IsEmpty ? 0 : s.Item.Id);
                    w.Write(s.IsEmpty ? 0 : s.Count);
                }

                // Placed objects (workbench etc.) by source item id + position.
                IReadOnlyList<PlacedObject> objs = PlacedObject.Registry;
                int placeable = 0;
                for (int i = 0; i < objs.Count; i++)
                    if (objs[i] != null && objs[i].Source != null) placeable++;
                w.Write(placeable);
                for (int i = 0; i < objs.Count; i++)
                {
                    PlacedObject o = objs[i];
                    if (o == null || o.Source == null) continue;
                    w.Write(o.Source.Id);
                    w.Write(o.transform.position.x);
                    w.Write(o.transform.position.y);

                    // v2: storage objects (chest, furnace) carry their contents.
                    var holder = o.GetComponent<IHasInventory>();
                    w.Write(holder != null);
                    if (holder != null)
                    {
                        w.Write(holder.Inventory.Size);
                        for (int s = 0; s < holder.Inventory.Size; s++)
                        {
                            ItemStack stack = holder.Inventory.Get(s);
                            w.Write(stack.IsEmpty ? 0 : stack.Item.Id);
                            w.Write(stack.IsEmpty ? 0 : stack.Count);
                        }
                    }
                }

                // v3: player appearance.
                var look = GetComponent<PlayerAppearanceRenderer>();
                PlayerAppearance a = look != null ? look.Current : PlayerAppearance.Default;
                w.Write(a.hairStyle);
                WriteColor(w, a.skinColor);
                WriteColor(w, a.hairColor);
                WriteColor(w, a.eyeColor);
            }

            Debug.Log($"[Save] Saved to {SavePath}");
        }

        // ------------------------------------------------------------------ load

        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning("[Save] No save file yet — press F5 to save first.");
                return;
            }

            using (var r = new BinaryReader(File.OpenRead(SavePath)))
            {
                if (r.ReadString() != Magic)
                {
                    Debug.LogError("[Save] Not a Doodgy save file.");
                    return;
                }
                int version = r.ReadInt32();
                if (version > Version)
                {
                    Debug.LogError($"[Save] Save version {version} is newer than this build supports ({Version}).");
                    return;
                }

                // World: regenerate from the saved seed, then overwrite edited chunks.
                int seed = r.ReadInt32();
                world.GenerateWorld(seed);

                int chunkCount = r.ReadInt32();
                var tiles = new ushort[WorldConstants.TilesPerChunk];
                for (int c = 0; c < chunkCount; c++)
                {
                    var cc = new Vector2Int(r.ReadInt32(), r.ReadInt32());
                    for (int i = 0; i < tiles.Length; i++) tiles[i] = r.ReadUInt16();
                    world.ApplyChunkData(cc, tiles);
                }

                // Player.
                var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), 0f);
                _player.transform.position = pos;
                var rb = _player.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;

                int selected = r.ReadInt32();
                int slotCount = r.ReadInt32();
                for (int i = 0; i < _player.Inventory.Size; i++)
                {
                    ItemStack stack = ItemStack.Empty;
                    if (i < slotCount)
                    {
                        int id = r.ReadInt32();
                        int count = r.ReadInt32();
                        ItemData item = itemDatabase != null ? itemDatabase.Get(id) : null;
                        if (item != null && count > 0) stack = new ItemStack(item, count);
                    }
                    _player.Inventory.SetSlot(i, stack);
                }
                _player.Select(selected);

                // Placed objects: clear live ones, respawn saved ones.
                var existing = new List<PlacedObject>(PlacedObject.Registry);
                foreach (PlacedObject o in existing)
                    if (o != null) Destroy(o.gameObject);

                int objCount = r.ReadInt32();
                for (int i = 0; i < objCount; i++)
                {
                    int id = r.ReadInt32();
                    float x = r.ReadSingle();
                    float y = r.ReadSingle();

                    ItemData item = itemDatabase != null ? itemDatabase.Get(id) : null;
                    GameObject spawned = null;
                    if (item != null && item.IsPlaceableObject)
                    {
                        // Saved position is the footprint centre; recover the bottom-left tile.
                        var bl = new Vector2Int(
                            Mathf.RoundToInt(x - item.ObjectSize.x * 0.5f),
                            Mathf.RoundToInt(y - item.ObjectSize.y * 0.5f));
                        spawned = PlacedObject.Spawn(item, bl);
                    }

                    // v2: storage contents follow the object entry.
                    if (version >= 2 && r.ReadBoolean())
                    {
                        int storageSlots = r.ReadInt32();
                        var holder = spawned != null ? spawned.GetComponent<IHasInventory>() : null;
                        for (int s = 0; s < storageSlots; s++)
                        {
                            int itemId = r.ReadInt32();
                            int count = r.ReadInt32();
                            if (holder == null || s >= holder.Inventory.Size) continue;
                            ItemData stackItem = itemDatabase != null ? itemDatabase.Get(itemId) : null;
                            holder.Inventory.SetSlot(s, stackItem != null && count > 0
                                ? new ItemStack(stackItem, count) : ItemStack.Empty);
                        }
                    }
                }

                // v3: player appearance.
                if (version >= 3)
                {
                    var a = new PlayerAppearance
                    {
                        hairStyle = r.ReadInt32(),
                        skinColor = ReadColor(r),
                        hairColor = ReadColor(r),
                        eyeColor = ReadColor(r),
                    };
                    var look = GetComponent<PlayerAppearanceRenderer>();
                    if (look != null) look.Apply(a);
                }
            }

            // Loose drops from the old session make no sense after a load.
            foreach (ItemPickup p in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
                Destroy(p.gameObject);

            // Lighting + trees rebuild over the final loaded tiles.
            world.NotifyWorldRefreshed();

            Debug.Log("[Save] Loaded.");
        }

        private static void WriteColor(BinaryWriter w, Color c)
        {
            w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a);
        }

        private static Color ReadColor(BinaryReader r)
            => new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
