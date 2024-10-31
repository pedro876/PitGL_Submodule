using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace PitGL
{
    public class ShaderFinderObject : ScriptableObject
    {
        [SerializeField] string filePath;
        [SerializeField] string searchName;
        [SerializeField] bool collapseResults;
    }

    [CustomEditor(typeof(ShaderFinderObject))]
    public class ShaderFinderObjectEditor : Editor
    {
        SerializedProperty filePath;
        SerializedProperty searchName;
        SerializedProperty collapseResults;
        TreeNode rootNode;

        private void OnEnable()
        {
            filePath = serializedObject.FindProperty(nameof(filePath));
            searchName = serializedObject.FindProperty(nameof(searchName));
            collapseResults = serializedObject.FindProperty(nameof(collapseResults));
            rootNode = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(filePath);
            EditorGUILayout.PropertyField(searchName);
            EditorGUILayout.PropertyField(collapseResults);

            if (GUILayout.Button("FIND REFERENECS"))
            {
                rootNode = new TreeNode(filePath.stringValue, !collapseResults.boolValue);
                SearchRecursivelyForMatches(rootNode, filePath.stringValue, searchName.stringValue);
            }

            if(rootNode != null)
            {
                DisplayTreeNode(rootNode);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DisplayTreeNode(TreeNode node)
        {
            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                node.foldout = EditorGUILayout.Foldout(node.foldout, $"{node.filePath} ({node.matchCount})");
                if (GUILayout.Button("COPY", GUILayout.Width(60)))
                {
                    EditorGUIUtility.systemCopyBuffer = node.filePath;
                }
            }

            if (!node.foldout) return;

            EditorGUI.indentLevel++;

            if(node.entries.Count > 0)
            {
                //EditorGUILayout.LabelField("Matches:");

                for (int i = 0; i < node.entries.Count; i++)
                {
                    Entry entry = node.entries[i];

                    using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                    {
                        EditorGUILayout.LabelField($"Line {entry.lineIndex} \"{entry.line}\"");
                        if(GUILayout.Button("GO", GUILayout.Width(60)))
                        {
                            OpenScriptAtLine(node.filePath, entry.lineIndex);
                        }
                    }
                }

                EditorGUILayout.Space();
            }
            
            if(node.children.Count > 0)
            {
                //EditorGUILayout.LabelField("Dependencies");
                EditorGUI.indentLevel++;

                for (int i = 0; i < node.children.Count; i++)
                {
                    TreeNode child = node.children[i];
                    if (child.matchCount > 0) DisplayTreeNode(child);
                }

                EditorGUI.indentLevel--;
            }
            
            EditorGUI.indentLevel--;
        }

        private void OpenScriptAtLine(string filePath, int line)
        {
            Object script = AssetDatabase.LoadAssetAtPath<Object>(filePath);
            if (script != null)
            {
                AssetDatabase.OpenAsset(script, line);
                Debug.Log($"Opened {filePath} at line {line}");
            }
            else
            {
                Debug.LogError($"Script not found at path: {filePath}");
            }
        }

        private int SearchRecursivelyForMatches(TreeNode node, string filePath, string searchName)
        {
            string allText = null;
            try
            {
                allText = File.ReadAllText(filePath);
            }
            catch(System.Exception e)
            {
                Debug.LogException(e);
            }

            if (allText == null) return 0;
            string[] allLines = allText.Split("\n");
            

            string searchPattern = $@"\b{Regex.Escape(searchName)}\b";
            foreach (Match match in Regex.Matches(allText, searchPattern))
            {
                string[] lines = allText.Substring(0, match.Index).Split("\n");
                Entry entry = new Entry()
                {
                    line = allLines[lines.Length-1].Replace("\t", "").Trim(),
                    lineIndex = lines.Length,
                };
                node.entries.Add(entry);
                node.matchCount++;
                Debug.Log($"Matched path: {entry.line} at line {entry.lineIndex}");
            }

            string includePattern = @"#(?:include|include_with_pragmas)\s+""([^""]+)""";
            foreach (Match match in Regex.Matches(allText, includePattern))
            {
                string childPath = match.Groups[1].Value;

                TreeNode childNode = new TreeNode(childPath.Replace("\t", "").Trim(), node.foldout);
                node.children.Add(childNode);
                int childrenMatchCount = SearchRecursivelyForMatches(childNode, childPath, searchName);
                Debug.Log($"Dependency: {childPath} at line with match count {childrenMatchCount}");

                node.matchCount += childrenMatchCount;
            }

            return node.matchCount;
        }

        private class TreeNode
        {
            public string filePath;
            public List<Entry> entries;
            public List<TreeNode> children;
            public bool foldout = true;
            public int matchCount;

            public TreeNode(string filePath, bool collapse)
            {
                this.filePath = filePath;
                this.foldout = collapse;
                this.entries = new List<Entry>();
                this.children = new List<TreeNode>();
                this.matchCount = 0;
            }
        }

        private class Entry
        {
            public string line;
            public int lineIndex;
        }
    }
}


