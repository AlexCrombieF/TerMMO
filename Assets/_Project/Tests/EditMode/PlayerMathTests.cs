using NUnit.Framework;
using UnityEngine;
using Doodgy.Gameplay;

namespace Doodgy.Tests
{
    /// <summary>
    /// EditMode tests for the pure movement math. The physics-driven parts of the
    /// player are validated by play-testing (see step 4 instructions); this locks
    /// the one closed-form equation we rely on.
    /// </summary>
    public class PlayerMathTests
    {
        [Test]
        public void JumpVelocity_ReachesRequestedHeight()
        {
            // v = sqrt(2 g h)  =>  apex height h = v^2 / (2 g)
            float g = 9.81f * 3.5f;     // gravity * gravityScale
            float h = 3.2f;
            float v = PlayerController.CalculateJumpVelocity(h, g);

            float apex = (v * v) / (2f * g);
            Assert.AreEqual(h, apex, 0.001f, "Computed jump velocity must reach the requested height.");
        }

        [Test]
        public void JumpVelocity_NonNegative_ForBadInput()
        {
            Assert.AreEqual(0f, PlayerController.CalculateJumpVelocity(-5f, 30f), 0.0001f);
            Assert.AreEqual(0f, PlayerController.CalculateJumpVelocity(3f, -30f), 0.0001f);
        }
    }
}
