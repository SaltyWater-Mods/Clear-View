namespace ClearView.Systems
{
    internal sealed class ClearViewSettings
    {
        // loaded from ModConfig\clearview.json
        public float TargetOpacity = 0.10f;
        public float HorizontalAreaMultiplier = 1f;
        public float VerticalAreaMultiplier = 1f;
        public bool AllowCameraInsideBlocks = true;
        public string[] IgnoredMaterials = new string[0];
    }
}
