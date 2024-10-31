using PitGL.IMGUI;
using System;
using UnityEngine;

namespace PitGL.IMGUI
{
    public static class IMGUI_Layout
    {
        private const float SCREEN_SCALE = 0.8f;
        private const float SCREEN_WIDTH = (int)(1280f * SCREEN_SCALE);
        private const float SCREEN_HEIGHT = (int)(720f * SCREEN_SCALE);
        private const float OPACITY = 0.9f;
        private const float BOX_PADDING = 4f;
        private const float LABEL_HEIGHT = 14f;

        private static float ScaleFactor => Screen.height / SCREEN_HEIGHT;

        public static void Label(ref Rect rect, string text, GUIStyle style = null, float height = LABEL_HEIGHT)
        {
            if (style == null)
            {
                style = IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleLeft, FontStyle.Normal, Color.white));
            }
            
            height *= ScaleFactor;
            Rect labelRect = rect;
            labelRect.height = height;
            GUI.Label(labelRect, text, style);

            rect.y += height;
            rect.height -= height;
        }

        public static void InsideBox(float widthPct, float heightPct, Anchor anchor, Action<Rect> action, float posXPct = 0f, float posYPct = 0f, float opacity = OPACITY, float padding = BOX_PADDING)
        {
            InsideRect(widthPct, heightPct, anchor, (rect) =>
            {
                GUI.Box(rect, "", IMGUI_Style.GetBoxStyle(new Color(0, 0, 0, opacity)));
                padding *= ScaleFactor;
                rect.x += padding;
                rect.width -= padding;
                rect.y += padding;
                rect.height -= padding;
                action(rect);
            }, posXPct, posYPct);
        }

        public static void InsideRect(float widthPct, float heightPct, Anchor anchor, Action<Rect> action, float posXPct = 0f, float posYPct = 0f)
        {
            float width = widthPct * SCREEN_WIDTH;
            float height = heightPct * SCREEN_HEIGHT;

            Matrix4x4 ogMatrix = GUI.matrix;
            float scaleFactor = ScaleFactor;

            bool isAnchorRight = anchor == Anchor.Right || anchor == Anchor.TopRight || anchor == Anchor.BottomRight;
            bool isAnchorLeft = anchor == Anchor.Left || anchor == Anchor.TopLeft || anchor == Anchor.BottomLeft;
            float matrixXDisplacement;
            if (isAnchorRight) matrixXDisplacement = Screen.width - SCREEN_WIDTH * scaleFactor;
            else if (isAnchorLeft) matrixXDisplacement = 0f;
            else matrixXDisplacement = 0f;

            GUI.matrix = Matrix4x4.TRS(new Vector3(matrixXDisplacement, 0f, 0f), Quaternion.identity, new Vector3(scaleFactor, scaleFactor, 1f));

            Rect rect;
            switch (anchor)
            {
                default:
                case Anchor.Center:
                    rect = new Rect(CenterX(), CenterY(), width, height);
                    break;
                case Anchor.Left:
                    rect = new Rect(Left(), CenterY(), width, height);
                    break;
                case Anchor.Right:
                    rect = new Rect(Right(), CenterY(), width, height);
                    break;
                case Anchor.Top:
                    rect = new Rect(CenterX(), Top(), width, height);
                    break;
                case Anchor.Bottom:
                    rect = new Rect(CenterX(), Bottom(), width, height);
                    break;
                case Anchor.TopLeft:
                    rect = new Rect(Left(), Top(), width, height);
                    break;
                case Anchor.TopRight:
                    rect = new Rect(Right(), Top(), width, height);
                    break;
                case Anchor.BottomLeft:
                    rect = new Rect(Left(), Bottom(), width, height);
                    break;
                case Anchor.BottomRight:
                    rect = new Rect(Right(), Bottom(), width, height);
                    break;
            }

            rect.x += posXPct * SCREEN_WIDTH;
            rect.y += posYPct * SCREEN_HEIGHT;

            action(rect);

            GUI.matrix = ogMatrix;

            float Left() => 0f;
            float Right() => SCREEN_WIDTH - width;
            float Top() => 0f;
            float Bottom() => SCREEN_HEIGHT - height;
            float CenterX() => SCREEN_WIDTH / 2f - width / 2f;
            float CenterY() => SCREEN_HEIGHT / 2f - height / 2f;
        }

        public enum Anchor
        {
            Center,
            Left, Top, Right, Bottom,
            TopLeft, TopRight, BottomLeft, BottomRight,
        }
    }
}
