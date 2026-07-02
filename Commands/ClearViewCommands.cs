using System;
using ClearView.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ClearView.Commands
{
    internal sealed class ClearViewCommands
    {
        private readonly ICoreClientAPI capi;
        private readonly ClearViewState state;

        public ClearViewCommands(ICoreClientAPI capi, ClearViewState state)
        {
            this.capi = capi;
            this.state = state;
        }

        public void Register()
        {
            // save right away so command changes survive reloads
            CommandArgumentParsers parsers = capi.ChatCommands.Parsers;

            capi.ChatCommands.Create("clearview")
                .WithRootAlias("cv")
                .WithDescription("Clear View: fade blocks that hide the player in third person")
                .HandleWith(OnStatus)
                .BeginSubCommand("opacity")
                    .WithDescription("Set how visible faded blocks remain. Lower values are more transparent")
                    .WithArgs(parsers.OptionalFloat("opacity"))
                    .HandleWith(OnOpacity)
                .EndSubCommand()
                .BeginSubCommand("horizontal")
                    .WithDescription("Set how wide the fade area is around the player")
                    .WithArgs(parsers.OptionalFloat("multiplier"))
                    .HandleWith(OnHorizontal)
                .EndSubCommand()
                .BeginSubCommand("vertical")
                    .WithDescription("Set how tall the fade area is above the player's feet")
                    .WithArgs(parsers.OptionalFloat("multiplier"))
                    .HandleWith(OnVertical)
                .EndSubCommand()
                .BeginSubCommand("camera")
                    .WithDescription("Let the camera stay inside fadeable blocks above the player. Ground and ignored materials still block it")
                    .WithArgs(parsers.OptionalBool("enabled"))
                    .HandleWith(OnCamera)
                .EndSubCommand()
                .BeginSubCommand("ignore")
                    .WithDescription("Choose block materials Clear View should not fade")
                    .WithArgs(parsers.OptionalWord("material"), parsers.OptionalBool("ignored"))
                    .HandleWith(OnIgnore);
        }

        private TextCommandResult OnStatus(TextCommandCallingArgs args)
        {
            return TextCommandResult.Success($"Clear View: opacity {state.TargetOpacity:0.##}, horizontal {state.HorizontalAreaMultiplier:0.##}x, vertical {state.VerticalAreaMultiplier:0.##}x, camera inside blocks {OnOff(state.AllowCameraInsideBlocks)}, ignored materials: {state.IgnoredMaterialsText()}");
        }

        private TextCommandResult OnOpacity(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Fade opacity is {state.TargetOpacity:0.##}. Lower values are more transparent.");
            }

            float value = (float)args.Parsers[0].GetValue();
            if (value < 0.01f || value > 1f)
            {
                return TextCommandResult.Error("Opacity must be between 0.01 and 1. Use a lower value for more transparency.");
            }

            state.TargetOpacity = value;
            state.SaveSettings();
            return TextCommandResult.Success($"Fade opacity set to {state.TargetOpacity:0.##}.");
        }

        private TextCommandResult OnHorizontal(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Horizontal detection area is {state.HorizontalAreaMultiplier:0.##}x.");
            }

            float value = (float)args.Parsers[0].GetValue();
            if (value <= 0f)
            {
                return TextCommandResult.Error("Horizontal detection area must be greater than 0.");
            }

            state.HorizontalAreaMultiplier = value;
            state.SaveSettings();
            return TextCommandResult.Success($"Horizontal detection area set to {state.HorizontalAreaMultiplier:0.##}x.");
        }

        private TextCommandResult OnVertical(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Vertical detection area is {state.VerticalAreaMultiplier:0.##}x.");
            }

            float value = (float)args.Parsers[0].GetValue();
            if (value <= 0f)
            {
                return TextCommandResult.Error("Vertical detection area must be greater than 0.");
            }

            state.VerticalAreaMultiplier = value;
            state.SaveSettings();
            return TextCommandResult.Success($"Vertical detection area set to {state.VerticalAreaMultiplier:0.##}x.");
        }

        private TextCommandResult OnCamera(TextCommandCallingArgs args)
        {
            // no value means toggle, useful when testing a material in game
            if (args.Parsers[0].IsMissing)
            {
                state.AllowCameraInsideBlocks = !state.AllowCameraInsideBlocks;
                state.SaveSettings();
            }
            else
            {
                state.AllowCameraInsideBlocks = (bool)args.Parsers[0].GetValue();
                state.SaveSettings();
            }

            return TextCommandResult.Success($"Camera can rest inside fadeable upper blocks: {OnOff(state.AllowCameraInsideBlocks)}. Ground and ignored materials still block it.");
        }

        private TextCommandResult OnIgnore(TextCommandCallingArgs args)
        {
            // use the enum material name so the config stays readable
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Ignored materials: {state.IgnoredMaterialsText()}. Example: .cv ignore Leaves true. Materials: {MaterialNames()}.");
            }

            string materialName = (string)args.Parsers[0].GetValue();
            if (!TryParseMaterial(materialName, out EnumBlockMaterial material))
            {
                return TextCommandResult.Error($"Unknown block material '{materialName}'. Use one of: {MaterialNames()}.");
            }

            bool ignored;
            if (args.Parsers[1].IsMissing)
            {
                ignored = state.ToggleMaterialIgnored(material);
            }
            else
            {
                ignored = (bool)args.Parsers[1].GetValue();
                state.SetMaterialIgnored(material, ignored);
            }

            return TextCommandResult.Success($"{material} blocks will {IgnoredText(ignored)}.");
        }

        private bool TryParseMaterial(string value, out EnumBlockMaterial material)
        {
            return Enum.TryParse(value, true, out material) && Enum.IsDefined(typeof(EnumBlockMaterial), material);
        }

        private string MaterialNames()
        {
            return string.Join(", ", Enum.GetNames(typeof(EnumBlockMaterial)));
        }

        private string IgnoredText(bool ignored)
        {
            return ignored ? "not fade" : "fade again";
        }

        private string OnOff(bool enabled)
        {
            return enabled ? "on" : "off";
        }
    }
}
