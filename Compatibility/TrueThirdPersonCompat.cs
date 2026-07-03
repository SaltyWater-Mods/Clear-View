using System;
using System.Reflection;
using ClearView.Systems;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ClearView.Compatibility
{
    // true third person has its own wall check, this only wraps that bit when the mod exists
    internal static class TrueThirdPersonCompat
    {
        [ThreadStatic]
        private static bool active;

        [ThreadStatic]
        private static double playerBottomY;

        public static bool Active
        {
            get { return active; }
        }

        public static double PlayerBottomY
        {
            get { return playerBottomY; }
        }

        public static void Patch(Harmony harmony)
        {
            Type patchType = AccessTools.TypeByName("TrueThirdPerson.CameraOverwritePatch");
            if (patchType == null)
            {
                return;
            }

            MethodInfo method = AccessTools.Method(patchType, "GetCameraMatrixFinish");
            if (method == null)
            {
                return;
            }

            harmony.Patch(
                method,
                prefix: new HarmonyMethod(typeof(TrueThirdPersonCompat), nameof(Prefix)),
                finalizer: new HarmonyMethod(typeof(TrueThirdPersonCompat), nameof(Finalizer))
            );
        }

        private static void Prefix(AABBIntersectionTest intersectionTester, Vec3d worldPos)
        {
            // this flag is only for the camera raytrace happening inside ttp right now
            active = false;
            playerBottomY = 0;

            if (ClearViewModSystem.State == null)
            {
                return;
            }

            IClientWorldAccessor world = intersectionTester.bsTester as IClientWorldAccessor;
            EntityPlayer player = world?.Player.Entity;
            if (player == null)
            {
                return;
            }

            active = true;
            // same floor line as clear view camera collision so low blocks still block
            playerBottomY = worldPos.Y + player.CollisionBox.Y1 + 0.05;
        }

        private static void Finalizer()
        {
            // clear even if ttp camera code throws before reaching the end
            active = false;
            playerBottomY = 0;
        }
    }
}
