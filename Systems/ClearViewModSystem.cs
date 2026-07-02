using ClearView.Commands;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ClearView.Systems
{
    public sealed class ClearViewModSystem : ModSystem
    {
        private Harmony harmony;
        private ClearViewCommands commands;

        internal static ClearViewState State { get; private set; }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            State = new ClearViewState(api);
            commands = new ClearViewCommands(api, State);
            commands.Register();

            harmony = new Harmony("saltywater.clearview");
            harmony.PatchAll();
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll("saltywater.clearview");
            State?.Dispose();
            State = null;
        }
    }
}
