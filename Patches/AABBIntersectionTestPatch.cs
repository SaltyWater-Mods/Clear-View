using System.Reflection;
using ClearView.Compatibility;
using ClearView.Systems;
using HarmonyLib;
using Vintagestory.API.MathTools;

namespace ClearView.Patches
{
    // ttp calls the normal block raytrace, so this is where we swap the filter while ttp is active
    [HarmonyPatch]
    internal static class AABBIntersectionTestPatch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(AABBIntersectionTest),
                nameof(AABBIntersectionTest.GetSelectedBlock),
                new[] { typeof(float), typeof(BlockFilter), typeof(bool) }
            );
        }

        public static void Prefix(ref BlockFilter filter)
        {
            if (!TrueThirdPersonCompat.Active)
            {
                return;
            }

            ClearViewState state = ClearViewModSystem.State;
            if (state == null)
            {
                return;
            }

            BlockFilter original = filter;
            // keep ttp filter first, then let clear view decide if the block still needs real camera collision
            filter = (pos, block) =>
            {
                if (original != null && !original(pos, block))
                {
                    return false;
                }

                return CameraPatch.BlockMustUseVanillaCameraCollision(pos, block, TrueThirdPersonCompat.PlayerBottomY, state);
            };
        }
    }
}
