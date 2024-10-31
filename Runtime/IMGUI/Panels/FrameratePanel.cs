using UnityEngine;
using PitGL.IMGUI;

namespace PitGL.IMGUI
{
    public class FrameratePanel : MonoBehaviour
    {
        public static FrameratePanel instance;

        public bool gui = false;


        const float deltaTimeLerp = 0.1f;
        private float avgDeltaTime = 0f;

        private void Awake()
        {
            instance = this;
        }

        private void Update()
        {
            avgDeltaTime = Mathf.Lerp(avgDeltaTime, Time.deltaTime, deltaTimeLerp);
        }

        private void OnGUI()
        {
            if (!gui) return;

            IMGUI_Layout.InsideBox(0.2f, 0.1f, IMGUI_Layout.Anchor.BottomLeft, (rect) =>
            {
                float avgFramerate = 1f / avgDeltaTime;
                float framerate = 1f / Time.deltaTime;
                IMGUI_Layout.Label(ref rect, $"Fps (avg): {avgFramerate}");
                IMGUI_Layout.Label(ref rect, $"Fps: {framerate}");
            });
        }
    }
}
