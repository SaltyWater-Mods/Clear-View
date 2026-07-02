using System;
using System.Collections.Generic;
using ClearView.Systems;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace ClearView.Patches
{
    // vanilla solves blocked view by moving the camera closer
    [HarmonyPatch(typeof(Camera), nameof(Camera.LimitThirdPersonCameraToWalls))]
    internal static class CameraPatch
    {
        private const double RayStep = 0.22;
        private const double SilhouetteStep = 0.33;
        private const double StartOffset = 0.25;

        private static readonly Cuboidf CameraBox = new Cuboidf(0.08f);
        private static readonly AccessTools.FieldRef<FloatRef, float> CameraDistanceValue = AccessTools.FieldRefAccess<FloatRef, float>("value");
        private static readonly HashSet<BlockKey> Hits = new HashSet<BlockKey>();
        private static readonly BlockPos SamplePos = new BlockPos(0);

        public static bool Prefix(Camera __instance, AABBIntersectionTest intersectionTester, double yaw, Vec3d eye, Vec3d target, FloatRef curtppcameradistance, ref bool __result)
        {
            ClearViewState state = ClearViewModSystem.State;
            if (state == null)
            {
                return true;
            }

            Hits.Clear();

            // use the player collision box so the fade area matches the body
            IClientWorldAccessor world = (IClientWorldAccessor)intersectionTester.bsTester;
            EntityPlayer player = world.Player.Entity;
            Cuboidf box = player.CollisionBox;
            double baseX = target.X - player.LocalEyePos.X;
            double baseY = target.Y - player.LocalEyePos.Y;
            double baseZ = target.Z - player.LocalEyePos.Z;
            double playerBottomY = baseY + box.Y1 + 0.05;

            // sample the camera side of the player instead of a full area around it
            CollectSilhouette(intersectionTester, eye, baseX, baseY, baseZ, box, state.HorizontalAreaMultiplier, state.VerticalAreaMultiplier, state, Hits);

            if (state.AllowCameraInsideBlocks)
            {
                if (PathMustUseVanillaCameraCollision(intersectionTester, __instance, eye, target, playerBottomY, state))
                {
                    state.SetWanted(Hits);
                    return true;
                }

                CollectCameraBlock(intersectionTester, eye, playerBottomY, state, Hits);
            }

            state.SetWanted(Hits);
            if (!state.AllowCameraInsideBlocks && world.CollisionTester.IsColliding(world.BlockAccessor, CameraBox, eye, false))
            {
                return true;
            }

            CameraDistanceValue(curtppcameradistance) = __instance.Tppcameradistance;
            __result = true;
            return false;
        }

        private static bool PathMustUseVanillaCameraCollision(AABBIntersectionTest intersectionTester, Camera camera, Vec3d eye, Vec3d target, double playerBottomY, ClearViewState state)
        {
            // check if something on the path still needs vanilla camera collision
            Line3D pick = new Line3D();
            pick.Start = target.ToDoubleArray();
            pick.End = eye.ToDoubleArray();
            intersectionTester.LoadRayAndPos(pick);

            BlockSelection selection = intersectionTester.GetSelectedBlock(camera.TppCameraDistanceMax, (pos, block) =>
            {
                return BlockMustUseVanillaCameraCollision(pos, block, playerBottomY, state);
            });

            return selection != null;
        }

        private static bool BlockMustUseVanillaCameraCollision(BlockPos pos, Block block, double playerBottomY, ClearViewState state)
        {
            // low blocks still use vanilla collision so the camera does not sit under terrain
            if (!FadeableBlock.BlocksCamera(block))
            {
                return false;
            }

            if (pos.Y <= (int)Math.Floor(playerBottomY))
            {
                return true;
            }

            return !FadeableBlock.CanBeSelectedByCamera(block, state);
        }

        private static void CollectCameraBlock(AABBIntersectionTest intersectionTester, Vec3d eye, double playerBottomY, ClearViewState state, HashSet<BlockKey> hits)
        {
            SamplePos.SetAndCorrectDimension((int)Math.Floor(eye.X), (int)Math.Floor(eye.Y), (int)Math.Floor(eye.Z));
            if (SamplePos.Y <= (int)Math.Floor(playerBottomY))
            {
                return;
            }

            Block block = intersectionTester.bsTester.GetBlock(SamplePos);
            if (FadeableBlock.CanBeSelectedByCamera(block, state))
            {
                hits.Add(new BlockKey(SamplePos));
            }
        }

        private static void CollectSilhouette(AABBIntersectionTest intersectionTester, Vec3d eye, double baseX, double baseY, double baseZ, Cuboidf box, float horizontalMultiplier, float verticalMultiplier, ClearViewState state, HashSet<BlockKey> hits)
        {
            // horizontal grows from the camera view, vertical grows from the player feet
            double centerX = baseX + (box.X1 + box.X2) * 0.5;
            double centerZ = baseZ + (box.Z1 + box.Z2) * 0.5;
            double bottomY = baseY + box.Y1 + 0.04;
            double topY = bottomY + (box.Y2 - box.Y1) * verticalMultiplier - 0.02;

            double viewX = eye.X - centerX;
            double viewZ = eye.Z - centerZ;
            double viewLength = Math.Sqrt(viewX * viewX + viewZ * viewZ);
            if (viewLength <= 0.001)
            {
                viewX = 0;
                viewZ = 1;
                viewLength = 1;
            }

            double rightX = -viewZ / viewLength;
            double rightZ = viewX / viewLength;
            double halfWidth = Math.Max(box.X2 - box.X1, box.Z2 - box.Z1) * 0.5 * horizontalMultiplier;
            double height = Math.Max(0.01, topY - bottomY);
            // bigger fade area needs more rays or small blockers get missed
            int horizontalSamples = Math.Max(2, (int)Math.Ceiling((halfWidth * 2) / SilhouetteStep) + 1);
            int verticalSamples = Math.Max(2, (int)Math.Ceiling(height / SilhouetteStep) + 1);

            for (int yIndex = 0; yIndex < verticalSamples; yIndex++)
            {
                double y = bottomY + height * yIndex / (verticalSamples - 1);

                for (int xIndex = 0; xIndex < horizontalSamples; xIndex++)
                {
                    double offset = -halfWidth + halfWidth * 2 * xIndex / (horizontalSamples - 1);
                    Collect(intersectionTester, new Vec3d(centerX + rightX * offset, y, centerZ + rightZ * offset), eye, bottomY, state, hits);
                }
            }
        }

        private static void Collect(AABBIntersectionTest intersectionTester, Vec3d from, Vec3d to, double minY, ClearViewState state, HashSet<BlockKey> hits)
        {
            double dirX = to.X - from.X;
            double dirY = to.Y - from.Y;
            double dirZ = to.Z - from.Z;
            double length = Math.Sqrt(dirX * dirX + dirY * dirY + dirZ * dirZ);
            if (length <= 0.001)
            {
                return;
            }

            dirX /= length;
            dirY /= length;
            dirZ /= length;

            int samples = (int)(length / RayStep) + 1;
            for (int i = 0; i < samples; i++)
            {
                double distance = StartOffset + i * RayStep;
                if (distance >= length)
                {
                    break;
                }

                double sampleY = from.Y + dirY * distance;
                if (sampleY < minY)
                {
                    continue;
                }

                SamplePos.SetAndCorrectDimension(
                    (int)Math.Floor(from.X + dirX * distance),
                    (int)Math.Floor(sampleY),
                    (int)Math.Floor(from.Z + dirZ * distance)
                );

                Block block = intersectionTester.bsTester.GetBlock(SamplePos);
                if (FadeableBlock.CanBeSelectedByCamera(block, state))
                {
                    hits.Add(new BlockKey(SamplePos));
                }
            }
        }
    }
}
