using UnityEngine;
using UnityEngine.Tilemaps;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Runtime TileBase that plays a sprite loop via the Tilemap's built-in
    /// animation path (no 2D-extras dependency). Created by RuntimeTileResolver
    /// for tiles whose TileData has animation frames (e.g. the torch flame).
    /// </summary>
    public sealed class AnimatedRuntimeTile : TileBase
    {
        public Sprite[] Frames;
        public float Fps = 8f;
        public Color Color = Color.white;
        public Tile.ColliderType ColliderType = Tile.ColliderType.None;

        public override void GetTileData(Vector3Int position, ITilemap tilemap,
                                         ref UnityEngine.Tilemaps.TileData tileData)
        {
            tileData.sprite = Frames != null && Frames.Length > 0 ? Frames[0] : null;
            tileData.color = Color;
            tileData.colliderType = ColliderType;
        }

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap,
                                                  ref TileAnimationData animationData)
        {
            if (Frames == null || Frames.Length < 2) return false;
            animationData.animatedSprites = Frames;
            animationData.animationSpeed = Fps;
            animationData.animationStartTime = 0f;
            return true;
        }
    }
}
