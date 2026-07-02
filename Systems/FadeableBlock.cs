using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ClearView.Systems
{
    internal static class FadeableBlock
    {
        public static bool CanBeSelectedByCamera(Block block, ClearViewState state)
        {
            return block.Id != 0 && CanBeCaptured(block, state);
        }

        public static bool CanBeCaptured(Block block, ClearViewState state)
        {
            return block.DrawType != EnumDrawType.Empty
                && block.DrawType != EnumDrawType.Liquid
                && block.RenderPass != EnumChunkRenderPass.Meta
                && block.RenderPass != EnumChunkRenderPass.Liquid
                && block.RenderPass != EnumChunkRenderPass.TopSoil
                && !state.IsMaterialIgnored(block);
        }

        public static bool BlocksCamera(Block block)
        {
            return block.CollisionBoxes != null
                && block.CollisionBoxes.Length > 0
                && block.RenderPass != EnumChunkRenderPass.Transparent
                && block.RenderPass != EnumChunkRenderPass.Meta;
        }

        public static bool CanRenderCapturedPass(EnumChunkRenderPass pass)
        {
            return pass != EnumChunkRenderPass.Liquid
                && pass != EnumChunkRenderPass.Meta
                && pass != EnumChunkRenderPass.TopSoil;
        }
    }
}
