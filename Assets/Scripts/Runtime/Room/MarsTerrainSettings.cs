using UnityEngine;

namespace Outpost.Runtime.Room
{
    /// <summary>
    /// Holds exposed procedural Mars play-terrain and backdrop-terrain generation parameters
    /// so designers can inspect and regenerate terrain cleanly from the Outpost menu.
    /// </summary>
    public class MarsTerrainSettings : MonoBehaviour
    {
        [Header("Play Terrain (300m x 300m, res 513)")]
        public int seed = 202604;
        public float sizeMeters = 300f;
        public int heightmapResolution = 513;
        public float flatPlayRadius = 30f;
        public float flatPlayElevation = 2.0f;
        public float duneAmplitude = 1.65f;
        public float ridgeAmplitude = 6.2f;
        public float outerRimMinRadius = 118f;
        public float outerRimMaxRadius = 142f;
        public float outerRimHeight = 26.0f;

        [Header("Backdrop Terrain (1200m x 1200m)")]
        public float backdropSizeMeters = 1200f;
        public float verticalExaggeration = 2.0f;
        public string lastRawPath = "";
        public int lastRawResolution = 0;
    }
}
