using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor;

namespace PitGL
{
    public class SkeletonAO : MonoBehaviour
    {
        [System.Serializable]
        class Bone
        {
            public Transform joint0;
            public Transform joint1;
            public int depth;
        }

        [System.Serializable]
        class Joint
        {
            public Transform transform;
            public Vector3 displacement;
            public float radius = 0.2f;
        }

        [SerializeField, ForceAsset("Runtime/Rendering/SkeletonAO/CapsuleBoneBase.fbx")] MeshFilter boneMeshFilter;

        [SerializeField] float minBoneLength = 0.1f;
        [SerializeField] float defaultJointRadius = 0.1f;
        [SerializeField] int maxSkeletonDepth = 10;
        [SerializeField] Transform rigRoot;
        [SerializeField, HideInInspector] Bone[] bones;
        [SerializeField, HideInInspector] Joint[] joints;

        private Mesh skeletonMesh;

        private void Start()
        {
            
        }

        private void OnDrawGizmosSelected()
        {
            if (bones == null) return;
            if (bones.Length == 0 ) return;

            Gizmos.color = Color.red;
            Gizmos.matrix = transform.localToWorldMatrix;
            if (skeletonMesh != null)
            {
                Gizmos.DrawMesh(skeletonMesh);
            }

           // Gizmos.DrawMesh(boneMeshFilter.sharedMesh);

            Dictionary<Transform, Joint> dict = new Dictionary<Transform, Joint>();
            if(joints != null)
            {
                for (int i = 0; i < joints.Length; i++)
                {
                    if (joints[i] == null) continue;
                    if (joints[i].transform == null) continue;
                    dict[joints[i].transform] = joints[i];
                }
            }
            

            for (int i = 0; i < bones.Length; i++)
            {
                Bone bone = bones[i];
                if (bone == null) continue;
                if(bone.joint0 == null) continue;
                if(bone.joint1 == null) continue;

                Vector3 displacement0 = Vector3.zero;
                Vector3 displacement1 = Vector3.zero;
                float radius0 = defaultJointRadius;
                float radius1 = defaultJointRadius;

                if(dict.TryGetValue(bone.joint0, out Joint joint0))
                {
                    displacement0 = joint0.displacement;
                    radius0 = joint0.radius;
                }

                if (dict.TryGetValue(bone.joint1, out Joint joint1))
                {
                    displacement1 = joint1.displacement;
                    radius1 = joint1.radius;
                }

                Vector3 posJoint0 = displacement0;
                Vector3 posJoint1 = bone.joint0.InverseTransformPoint(bone.joint1.TransformPoint(displacement1));

                Gizmos.matrix = bone.joint0.localToWorldMatrix;
                Gizmos.DrawWireSphere(posJoint0, radius0);
                Gizmos.DrawWireSphere(posJoint1, radius1);


                //Gizmos.DrawLine(Vector3.right * bone.radius0, bone.joint0.InverseTransformPoint(bone.joint1.TransformPoint(Vector3.right * bone.radius1)));
                //Gizmos.DrawLine(Vector3.left * bone.radius0, bone.joint0.InverseTransformPoint(bone.joint1.TransformPoint(Vector3.left * bone.radius1)));
                //Gizmos.DrawLine(Vector3.forward * bone.radius0, bone.joint0.InverseTransformPoint(bone.joint1.TransformPoint(Vector3.forward * bone.radius1)));
                //Gizmos.DrawLine(Vector3.back * bone.radius0, bone.joint0.InverseTransformPoint(bone.joint1.TransformPoint(Vector3.back * bone.radius1)));
                //Gizmos.DrawLine(posJoint0, posJoint1);
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawLine(bone.joint0.TransformPoint(displacement0 + Vector3.right * radius0), bone.joint1.TransformPoint(displacement1 + Vector3.right * radius1));
                Gizmos.DrawLine(bone.joint0.TransformPoint(displacement0 + Vector3.left * radius0), bone.joint1.TransformPoint(displacement1 + Vector3.left * radius1));
                Gizmos.DrawLine(bone.joint0.TransformPoint(displacement0 + Vector3.forward * radius0), bone.joint1.TransformPoint(displacement1 + Vector3.forward * radius1));
                Gizmos.DrawLine(bone.joint0.TransformPoint(displacement0 + Vector3.back * radius0), bone.joint1.TransformPoint(displacement1 + Vector3.back * radius1));
                
            }
        }

        [ContextMenu("Deduct skeleton from hierarchy")]
        private void DeductSekeletonFromHierarchy()
        {
            List<Bone> boneList = new List<Bone>();
            Dictionary<Transform, Joint> jointsDict = new Dictionary<Transform, Joint>();

            if (joints != null)
            {
                for (int i = 0; i < joints.Length; i++)
                {
                    if (joints[i] == null) continue;
                    if (joints[i].transform == null) continue;
                    jointsDict[joints[i].transform] = joints[i];
                }
            }

            Transform t = rigRoot;

            var candidates = new Stack<(Transform, Transform, int)>();

            int childCount = t.childCount;
            for (int i = 0; i < childCount; i++)
            {
                candidates.Push((t.GetChild(i), null, 0));
            }

            while (candidates.Count > 0)
            {
                (Transform joint0, Transform joint1, int depth) = candidates.Pop();
                if (depth > maxSkeletonDepth) continue;
                if(joint1 == null)
                {
                    for (int i = 0; i < joint0.childCount; i++)
                    {
                        candidates.Push((joint0, joint0.GetChild(i), depth));
                    }
                }
                else
                {
                    float boneLength = Vector3.Distance(joint0.position, joint1.position);
                    bool isFeashible = boneLength >= minBoneLength;
                    if(isFeashible)
                    {
                        Bone bone = new Bone()
                        {
                            joint0 = joint0,
                            joint1 = joint1,
                            depth = depth,
                        };
                        boneList.Add(bone);
                        if (!jointsDict.ContainsKey(joint0)) jointsDict.Add(joint0, new Joint() { transform = joint0, displacement = Vector3.zero, radius = defaultJointRadius});
                        if (!jointsDict.ContainsKey(joint1)) jointsDict.Add(joint1, new Joint() { transform = joint1, displacement = Vector3.zero, radius = defaultJointRadius});
                        candidates.Push((joint1, null, depth + 1));
                    }
                    else
                    {
                        //Skip joint1 and select its children as next candidates for joint0
                        //for (int i = 0; i < joint1.childCount; i++)
                        //{
                        //    candidates.Push((joint0, joint1.GetChild(i)));
                        //}
                        candidates.Push((joint1, null, depth + 1));
                    }
                }
            }

            bones = boneList.ToArray();

            joints = new Joint[jointsDict.Count];
            int jointIndex = 0;
            foreach (var joint in jointsDict.Values)
            {
                joints[jointIndex++] = joint;
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }


        [ContextMenu("Generate mesh from skeleton")]
        private void GenerateMeshFromSkeleton()
        {
            CombineInstance[] instances = new CombineInstance[bones.Length];

            Mesh baseMesh = boneMeshFilter.sharedMesh;

            // Initialize bone to joint dictionary
            Dictionary<Transform, Joint> jointsDict = new Dictionary<Transform, Joint>();
            if (joints != null)
            {
                for (int i = 0; i < joints.Length; i++)
                {
                    jointsDict.Add(joints[i].transform, joints[i]);
                }
            }

            for(int i = 0; i < bones.Length; i++)
            {
                if (!jointsDict.ContainsKey(bones[i].joint0)) jointsDict.Add(bones[i].joint0, new Joint() { transform = bones[i].joint0, displacement = Vector3.zero, radius = defaultJointRadius });
                if (!jointsDict.ContainsKey(bones[i].joint1)) jointsDict.Add(bones[i].joint1, new Joint() { transform = bones[i].joint1, displacement = Vector3.zero, radius = defaultJointRadius });
            }

            // Create bone meshes
            for (int i = 0; i < bones.Length; i++)
            {

                Bone bone = bones[i];
                Joint joint0 = jointsDict[bones[i].joint0];
                Joint joint1 = jointsDict[bones[i].joint1];

                Vector3[] vertices = new Vector3[baseMesh.vertices.Length];
                Vector3[] normals = new Vector3[baseMesh.normals.Length];
                int[] triangles = new int[baseMesh.triangles.Length];
                baseMesh.vertices.CopyTo(vertices, 0);
                baseMesh.normals.CopyTo(normals, 0);
                baseMesh.triangles.CopyTo(triangles, 0);

                Vector3 joint0Pos = joint0.transform.TransformPoint(joint0.displacement);
                Vector3 joint1Pos = joint1.transform.TransformPoint(joint1.displacement);

                Quaternion rot = Quaternion.LookRotation(joint1Pos - joint0Pos) * Quaternion.Euler(90f, 0f, 0f);

                Matrix4x4 joint0matrix = Matrix4x4.TRS(joint0Pos, rot, Vector3.one * joint0.radius);
                Matrix4x4 joint1matrix = Matrix4x4.TRS(joint1Pos, rot, Vector3.one * joint1.radius);

                //Scale vertices to joint radiuses
                for(int v = 0; v < vertices.Length; v++)
                {
                    bool isJoint0 = vertices[v].y < 0.1f;

                    if (isJoint0)
                    {
                        vertices[v] = joint0matrix.MultiplyPoint(vertices[v]);
                    }
                    else
                    {
                        vertices[v].y -= 1f;
                        vertices[v] = joint1matrix.MultiplyPoint(vertices[v]);
                    }
                    
                    vertices[v] = transform.InverseTransformPoint(vertices[v]);
                }

                for (int n = 0; n < normals.Length; n++)
                {
                    normals[n] = bone.joint0.TransformDirection(normals[n]);
                    normals[n] = transform.InverseTransformDirection(normals[n]);
                }
                
                Mesh mesh = new Mesh();
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.triangles = triangles;
                instances[i].mesh = mesh;
            }

            // Combine meshes
            skeletonMesh = new Mesh();
            skeletonMesh.CombineMeshes(instances, true, false, false);
            skeletonMesh.RecalculateBounds();
        }

        [ContextMenu("Clear skeleton mesh")]
        private void ClearSkeletonMesh()
        {
            if(skeletonMesh != null)
            {
                DestroyImmediate(skeletonMesh);
                skeletonMesh = null;
            }
            
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SkeletonAO))]
    public class SkeletonAOEditor : Editor
    {
        SerializedProperty bones;
        SerializedProperty joints;
        SerializedProperty defaultJointRadius;

        private void OnEnable()
        {
            bones = serializedObject.FindProperty(nameof(bones));
            joints = serializedObject.FindProperty(nameof(joints));
            defaultJointRadius = serializedObject.FindProperty(nameof(defaultJointRadius));
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();
            GUILayout.Space(14);
            GUILayout.Label("BONES:", EditorStyles.boldLabel);
            for(int i = 0; i < bones.arraySize; i++)
            {
                using (new GUILayout.HorizontalScope())
                {
                    var elem = bones.GetArrayElementAtIndex(i);
                    var joint0 = elem.FindPropertyRelative("joint0");
                    var joint1 = elem.FindPropertyRelative("joint1");
                    var depth = elem.FindPropertyRelative("depth");
                    int indent = depth.intValue * 12;
                    GUILayout.Space(indent);

                    joint0.objectReferenceValue = EditorGUILayout.ObjectField(joint0.objectReferenceValue, typeof(Transform), true, GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.5f - indent));
                    GUILayout.Label("->", GUILayout.Width(20));
                    joint1.objectReferenceValue = EditorGUILayout.ObjectField(joint1.objectReferenceValue, typeof(Transform), true);
                    //EditorGUILayout.ObjectField(joint1);
                    if(GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        bones.DeleteArrayElementAtIndex(i);
                    }
                }
            }
            if (GUILayout.Button("ADD"))
            {
                bones.InsertArrayElementAtIndex(bones.arraySize);
                var elem = bones.GetArrayElementAtIndex(bones.arraySize - 1);
                var joint0 = elem.FindPropertyRelative("joint0");
                var joint1 = elem.FindPropertyRelative("joint1");
                var depth = elem.FindPropertyRelative("depth");
                joint0.objectReferenceValue = null;
                joint1.objectReferenceValue = null;
                depth.intValue = 0;
            }

            GUILayout.Space(14);
            GUILayout.Label("JOINTS:", EditorStyles.boldLabel);
            float labelWidth = EditorGUIUtility.labelWidth;
            for (int i = 0; i < joints.arraySize; i++)
            {
                using (new GUILayout.HorizontalScope())
                {
                    var elem = joints.GetArrayElementAtIndex(i);
                    var transform = elem.FindPropertyRelative("transform");
                    var radius = elem.FindPropertyRelative("radius");
                    var displacement = elem.FindPropertyRelative("displacement");

                    transform.objectReferenceValue = EditorGUILayout.ObjectField(transform.objectReferenceValue, typeof(Transform), true, GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth * 0.3f));

                    GUILayout.Space(10);
                    EditorGUIUtility.labelWidth = 50f;
                    radius.floatValue = EditorGUILayout.FloatField("Radius:", radius.floatValue, GUILayout.MaxWidth(80));

                    GUILayout.Space(10);
                    EditorGUIUtility.labelWidth = 90f;
                    displacement.vector3Value = EditorGUILayout.Vector3Field("Displacement:", displacement.vector3Value);

                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        joints.DeleteArrayElementAtIndex(i);
                    }
                }
            }
            if (GUILayout.Button("ADD"))
            {
                joints.InsertArrayElementAtIndex(joints.arraySize);
                var elem = joints.GetArrayElementAtIndex(joints.arraySize - 1);
                var transform = elem.FindPropertyRelative("transform");
                var radius = elem.FindPropertyRelative("radius");
                var displacement = elem.FindPropertyRelative("displacement");
                transform.objectReferenceValue = null;
                radius.floatValue = defaultJointRadius.floatValue;
                displacement.vector3Value = Vector3.zero;
            }


            EditorGUIUtility.labelWidth = labelWidth;

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
