using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PitGL
{
    public class SkeletonAO : MonoBehaviour
    {
        [System.Serializable]
        class Bone
        {
            public Transform joint0;
            public Transform joint1;
        }

        [System.Serializable]
        class Joint
        {
            public Transform transform;
            public Vector3 displacement;
            public float radius = 0.2f;
        }

        [SerializeField] float minBoneLength = 0.1f;
        [SerializeField] float defaultJointRadius = 0.1f;
        [SerializeField] Bone[] bones;
        [SerializeField] Joint[] joints;

        private void Start()
        {
            
        }

        private void OnDrawGizmosSelected()
        {
            if (bones == null) return;
            if (bones.Length == 0 ) return;

            Gizmos.color = Color.red;

            Dictionary<Transform, Joint> dict = new Dictionary<Transform, Joint>();
            if(joints != null)
            {
                for (int i = 0; i < joints.Length; i++)
                {
                    dict.Add(joints[i].transform, joints[i]);
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

        [ContextMenu("Build from hierarchy")]
        private void BuildFromHierarchy()
        {
            List<Bone> boneList = new List<Bone>();
            Dictionary<Transform, Joint> jointsDict = new Dictionary<Transform, Joint>();

            if (joints != null)
            {
                for (int i = 0; i < joints.Length; i++)
                {
                    jointsDict.Add(joints[i].transform, joints[i]);
                }
            }

            Transform t = transform;

            var candidates = new Stack<(Transform, Transform)>();

            int childCount = t.childCount;
            for (int i = 0; i < childCount; i++)
            {
                candidates.Push((t.GetChild(i), null));
            }

            while (candidates.Count > 0)
            {
                (Transform joint0, Transform joint1) = candidates.Pop();
                if(joint1 == null)
                {
                    for (int i = 0; i < joint0.childCount; i++)
                    {
                        candidates.Push((joint0, joint0.GetChild(i)));
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
                        };
                        boneList.Add(bone);
                        if (!jointsDict.ContainsKey(joint0)) jointsDict.Add(joint0, new Joint() { transform = joint0, displacement = Vector3.zero, radius = defaultJointRadius});
                        if (!jointsDict.ContainsKey(joint1)) jointsDict.Add(joint1, new Joint() { transform = joint1, displacement = Vector3.zero, radius = defaultJointRadius});
                        candidates.Push((joint1, null));
                    }
                    else
                    {
                        //Skip joint1 and select its children as next candidates for joint0
                        for (int i = 0; i < joint1.childCount; i++)
                        {
                            candidates.Push((joint0, joint1.GetChild(i)));
                        }
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

            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
}
