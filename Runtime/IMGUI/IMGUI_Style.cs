using System.Collections.Generic;
using UnityEngine;

namespace PitGL.IMGUI
{
    public static class IMGUI_Style
    {
        public const float STANDARD_ELEMENT_PADDING = 8f;
        public const float STANDARD_ELEMENT_INDENT = 16f;

        #region BOXES

        private static Dictionary<Color, Texture2D> pixel_textures = new Dictionary<Color, Texture2D>();
        private static Dictionary<Color, GUIStyle> pixel_boxes = new Dictionary<Color, GUIStyle>();
        private static Dictionary<Texture2D, GUIStyle> texture_boxes = new Dictionary<Texture2D, GUIStyle>();

        public static Texture2D GetTexture(Color color)
        {
            if(!pixel_textures.TryGetValue(color, out Texture2D tex))
            {
                tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, color);
                tex.Apply();
                pixel_textures.Add(color, tex);
            }
            return tex;
        }

        public static GUIStyle GetBoxStyle(Color color)
        {
            if (!pixel_boxes.TryGetValue(color, out GUIStyle style))
            {
                style = new GUIStyle();
                style.normal.background = GetTexture(color);
                style.hover = style.normal;
                style.active = style.normal;
                style.focused = style.normal;
                pixel_boxes.Add(color, style);
            }
            return style;
        }

        public static GUIStyle GetBoxStyle(Texture2D texture)
        {
            if (!texture_boxes.TryGetValue(texture, out GUIStyle style))
            {
                style = new GUIStyle();
                style.normal.background = texture;
                style.hover = style.normal;
                style.active = style.normal;
                style.focused = style.normal;
                texture_boxes.Add(texture, style);
            }
            return style;
        }

        #endregion

        #region TEXT

        private static Dictionary<TextDescription, GUIStyle> text_styles = new Dictionary<TextDescription, GUIStyle>();

        public struct TextDescription
        {
            public TextAnchor alignment;
            public FontStyle fontStyle;
            public Color color;
            public int fontSize;

            public TextDescription(
                TextAnchor alignment = TextAnchor.MiddleLeft,
                FontStyle fontStyle = FontStyle.Normal,
                Color color = default(Color),
                int fontSize = 12
            )
            {
                this.alignment = alignment;
                this.fontStyle = fontStyle;
                this.color = color;
                this.fontSize = fontSize;
            }
        }

        public static GUIStyle GetTextStyle(TextDescription desc)
        {
            if (!text_styles.TryGetValue(desc, out GUIStyle style))
            {
                style = new GUIStyle(GUI.skin.label);
                style.alignment = desc.alignment;
                style.fontStyle = desc.fontStyle;
                style.fontSize = desc.fontSize;
                style.richText = true;
                style.normal.textColor = desc.color;
                style.hover = style.normal;
                style.active = style.normal;
                style.focused = style.normal;
                text_styles.Add(desc, style);
            }
            return style;
        }

        #endregion

        #region UTILITIES

        public static Color ColorFromHex(string hex, float opacity = 1f)
        {
            int offset = hex[0].Equals('#') ? 1 : 0;
            float r = byte.Parse(hex.Substring(0 + offset, 2), System.Globalization.NumberStyles.HexNumber);
            float g = byte.Parse(hex.Substring(2 + offset, 2), System.Globalization.NumberStyles.HexNumber);
            float b = byte.Parse(hex.Substring(4 + offset, 2), System.Globalization.NumberStyles.HexNumber);
            float a = opacity;
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        #endregion
    }
}
