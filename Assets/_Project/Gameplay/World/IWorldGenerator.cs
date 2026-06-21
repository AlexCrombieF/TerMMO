namespace Doodgy.Gameplay
{
    /// <summary>
    /// Fills a chunk's tiles based purely on the chunk's coordinate and a baked-in
    /// seed. Implementations MUST be deterministic and stateless across chunks so
    /// chunks can be generated independently, in any order, with seamless edges —
    /// the prerequisite for chunk streaming and server-authoritative generation.
    /// </summary>
    public interface IWorldGenerator
    {
        /// <summary>Populates the given (empty) chunk in place.</summary>
        void Generate(Chunk chunk);
    }
}
