using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PitGL
{
    [Serializable]
    public class CustomVolumeComponent : VolumeComponent
    {
        public BoolParameter isActive = new BoolParameter(true);
        public ClampedFloatParameter horizontalBlur = new ClampedFloatParameter(0.05f, 0, 0.5f);
        public ClampedFloatParameter verticalBlur = new ClampedFloatParameter(0.05f, 0, 0.5f);
    }
}
