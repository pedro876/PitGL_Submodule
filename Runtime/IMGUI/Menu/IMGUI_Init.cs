#define INCLUDE_IN_RELEASE
#if UNITY_EDITOR || DEVELOPMENT_BUILD || INCLUDE_IN_RELEASE

using UnityEngine;

namespace PitGL.IMGUI
{
    public static class IMGUI_Init
    {
        [RuntimeInitializeOnLoadMethod]
        public static void Init()
        {
            new GameObject("IMGUI", typeof(IMGUI_Menu));
        }
    }
}

#endif