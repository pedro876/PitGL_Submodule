#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PitGL.TextureCode
{
    /// <summary>
    /// The aim of this utility is to convert small textures into scripts with hardcoded hex colors. This is simply a way to have access
    /// to a texture for GUI purposes without having to load it from an asset bundle, resources or elsewhere. This way, it does not affect
    /// how the game handles loading resources, because the texture lives inside the code from the beginning.
    /// </summary>
    public abstract class TextureCodeBase
    {
        protected static Color[] HexToColors(string[] hex)
        {
            int h = hex.Length;
            int w = hex[0].Length / 8;

            Color[] colors = new Color[w * h];
            for(int y = 0; y < h; y++)
            {
                string row = hex[y];
                int i = 0;
                for(int x = 0; x < w; x++)
                {
                    byte r = byte.Parse($"{row[i + 0]}{row[i + 1]}", System.Globalization.NumberStyles.HexNumber);
                    byte g = byte.Parse($"{row[i + 2]}{row[i + 3]}", System.Globalization.NumberStyles.HexNumber);
                    byte b = byte.Parse($"{row[i + 4]}{row[i + 5]}", System.Globalization.NumberStyles.HexNumber);
                    byte a = byte.Parse($"{row[i + 6]}{row[i + 7]}", System.Globalization.NumberStyles.HexNumber);
                    i += 8;
                    colors[y * w + x] = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
                }
            }

            return colors;
        }

        #region EDITOR
#if UNITY_EDITOR
        private const int MAX_WIDTH = 64;
        private const int MAX_HEIGHT = 64;
        private const string MENU_PATH = "Assets/PitGL/Texture2D to TextureCode";

        [MenuItem(MENU_PATH, validate = true)] // Validate function
        private static bool ValidateProcessTexture()
        {
            return Selection.activeObject is Texture2D;
        }
        [MenuItem(MENU_PATH, validate = false, priority = 0)] // Actual menu item function
        private static void ProcessTexture()
        {
            Texture2D texture = Selection.activeObject as Texture2D;
            if (texture != null)
            {
                Debug.Log($"Processing texture {texture.name} with size {texture.width}x{texture.height}");
                if (!texture.isReadable)
                {
                    Debug.LogError("Texture is not marked as readable. Toggle on the Read/Write option in the texture");
                    return;
                }
                if (texture.width * texture.height > MAX_WIDTH * MAX_HEIGHT)
                {
                    Debug.LogError($"Texture is too big, maximum resolution allowed is {MAX_WIDTH}x{MAX_HEIGHT} or pixel count < {MAX_WIDTH * MAX_HEIGHT}");
                    return;
                }
                CreateTextureScript(texture);
            }
        }

        private static void CreateTextureScript(Texture2D texture)
        {
            int depth = 0;
            StringBuilder sb = new StringBuilder();
            string path = AssetDatabase.GetAssetPath(texture);
            path = path.Substring(0, path.LastIndexOf("/") + 1);
            path += $"TextureCode_{texture.name.Replace(' ', '_')}.cs";
            Line("using UnityEngine;");
            Line();
            Line($"namespace {nameof(PitGL)}.{nameof(TextureCode)}");
            BracketIn();
            {
                Line($"public class TextureCode_{texture.name} : {nameof(TextureCodeBase)}");
                BracketIn();
                {
                    Line("private static Texture2D tex;");
                    Line("public static Texture2D Tex => tex == null ? Generate() : tex;");
                    Line("private static Texture2D Generate()");
                    BracketIn();
                    {
                        Line($"tex = new Texture2D({texture.width}, {texture.height}, TextureFormat.ARGB32, {Str(texture.mipmapCount > 1)});");
                        Line($"tex.name = \"{texture.name}\";");
                        Line($"tex.filterMode = FilterMode.{texture.filterMode};");
                        Line($"tex.wrapMode = TextureWrapMode.{texture.wrapMode};");
#if USE_HEX_FORMAT
                        Line("tex.SetPixels(HexToColors(GetHex()));");
#else
                        Line("tex.SetPixels(new Color[]");
                        BracketIn();
                        {
                            Color[] colors = texture.GetPixels();
                            for (int i = 0; i < colors.Length; i++)
                            {
                                Color c = colors[i];
                                Line($"new Color({Str(c.r)}f,{Str(c.g)}f,{Str(c.b)}f,{Str(c.a)}f),");
                            }
                        }
                        BracketOut();
                        Line(");");
#endif
                        Line("tex.Apply();");
                        Line("return tex;");
                    }
                    BracketOut();
#if USE_HEX_FORMAT
                    Line();
                    Line("private static string[] GetHex()");
                    BracketIn();
                    {
                        Line($"string[] hex = new string[{texture.height}];");

                        Color[] colors = texture.GetPixels();
                        int w = texture.width;
                        int h = texture.height;
                        int i = 0;
                        for (int y = 0; y < h; y++)
                        {
                            Indent();
                            sb.Append($"hex[{y}] = \"");
                            for (int x = 0; x < w; x++)
                            {
                                Color c = colors[i++];
                                byte r = (byte)Mathf.RoundToInt(c.r * 255f);
                                byte g = (byte)Mathf.RoundToInt(c.g * 255f);
                                byte b = (byte)Mathf.RoundToInt(c.b * 255f);
                                byte a = (byte)Mathf.RoundToInt(c.a * 255f);
                                sb.Append(r.ToString("X2"));
                                sb.Append(g.ToString("X2"));
                                sb.Append(b.ToString("X2"));
                                sb.Append(a.ToString("X2"));
                            }
                            sb.AppendLine("\";");
                        }

                        Line("return hex;");
                    }
                    BracketOut();
#endif

                }
                BracketOut();
            }
            BracketOut();

            Debug.Log($"Generated script at {path}");
            System.IO.File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();

            void BracketIn()
            {
                Line("{");
                depth++;
            }

            void BracketOut()
            {
                depth--;
                Line("}");
            }

            void Indent()
            {
                for (int i = 0; i < depth; i++)
                {
                    sb.Append("\t");
                }
            }

            void Line(string line = null)
            {
                if (line == null)
                {
                    sb.AppendLine();
                }
                else
                {
                    Indent();
                    sb.AppendLine(line);
                }

            }


        }

        private static string Str(float x) => x.ToString(CultureInfo.InvariantCulture);
        private static string Str(bool x) => x.ToString().ToLower();
#endif
        #endregion
    }
}
