namespace ClearView.Systems
{
    internal sealed class FadeEntry
    {
        // state for one block currently faded by the camera
        public float Opacity = 1f;
        public bool Wanted; // seen by camera this frame
        public bool HiddenInTerrain = true;
        public bool MeshReady;
        public long LastWantedMs;
    }
}
