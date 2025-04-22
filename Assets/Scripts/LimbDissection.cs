using UnityEngine;
using System.Collections.Generic;

// 自定义结构体用于存储关节的位置和旋转信息
public struct JointData
{
    public Vector3 position;
    public Quaternion rotation;

    public JointData(Vector3 pos, Quaternion rot)
    {
        position = pos;
        rotation = rot;
    }
}

public class LimbDissection : MonoBehaviour
{
    // 损伤区域关节
    public Transform damageZoneJoint;
    private Dictionary<Transform, JointData> originalJointData = new Dictionary<Transform, JointData>();

    private void Start()
    {
        // 记录所有关节的原始位置和旋转
        RecordOriginalJointData();
    }

    // 记录所有关节的原始位置和旋转
    private void RecordOriginalJointData()
    {
        Transform[] allJoints = GetComponentsInChildren<Transform>();
        foreach (Transform joint in allJoints)
        {
            originalJointData[joint] = new JointData(joint.position, joint.rotation);
        }
    }

    // 创建残肢
    public void CreateStump()
    {
        // 获取损伤区域关节下方的所有子关节
        Transform[] childJoints = damageZoneJoint.GetComponentsInChildren<Transform>();

        // 获取Animation组件
        Animation animation = GetComponent<Animation>();
        if (animation != null)
        {
            // 遍历Animation组件中的所有动画状态
            foreach (AnimationState state in animation)
            {
                // 遍历所有子关节
                foreach (Transform joint in childJoints)
                {
                    if (joint != damageZoneJoint)
                    {
                        // 禁用该关节在动画状态中的权重
                        string relativePath = GetRelativePath(joint, transform);
                        // 创建一个临时的动画名称，用于区分不同关节的动画状态
                        string tempAnimationName = state.name + "_" + relativePath;

                        // 检查动画状态是否存在
                        if (animation.GetClip(tempAnimationName) == null)
                        {
                            // 如果不存在，将原始动画剪辑添加到Animation组件中
                            animation.AddClip(state.clip, tempAnimationName);
                        }

                        // 为该关节播放动画
                        animation.Play(tempAnimationName, PlayMode.StopSameLayer);

                        // 获取动画状态并设置权重
                        AnimationState tempState = animation[tempAnimationName];
                        if (tempState != null)
                        {
                            tempState.weight = 0f;
                            tempState.layer = state.layer;
                            tempState.wrapMode = state.wrapMode;
                            tempState.speed = state.speed;
                            tempState.time = state.time;
                            tempState.normalizedTime = state.normalizedTime;
                        }
                    }
                }
            }
        }

        foreach (Transform joint in childJoints)
        {
            if (joint != damageZoneJoint)
            {
                // 将子关节位置移动到损伤区域关节位置
                joint.position = damageZoneJoint.position;
                joint.rotation = damageZoneJoint.rotation;

                // 这里可以进一步处理骨骼权重等，简化示例暂不处理
            }
        }
    }

    // 创建被肢解部分
    public void CreateDismemberedPart()
    {
        // 创建一个新的游戏对象来表示被肢解部分
        GameObject dismemberedPart = new GameObject("DismemberedPart");
        dismemberedPart.transform.position = damageZoneJoint.position;
        dismemberedPart.transform.rotation = damageZoneJoint.rotation;

        Transform currentJoint = damageZoneJoint;
        List<Transform> clonedJoints = new List<Transform>();

        while (currentJoint != null)
        {
            // 从原始数据中获取关节的位置和旋转
            JointData data = originalJointData[currentJoint];

            // 克隆关节及其子物体到被肢解部分
            GameObject clonedJoint = Instantiate(currentJoint.gameObject, dismemberedPart.transform);
            clonedJoint.transform.position = data.position;
            clonedJoint.transform.rotation = data.rotation;
            clonedJoints.Add(clonedJoint.transform);

            // 手动移除当前关节从其父节点中
            if (currentJoint.parent != null)
            {
                currentJoint.SetParent(null, true);
            }

            currentJoint = currentJoint.parent;
        }

        // 处理SkinnedMeshRenderer组件
        SkinnedMeshRenderer originalRenderer = damageZoneJoint.GetComponentInChildren<SkinnedMeshRenderer>();
        if (originalRenderer != null)
        {
            SkinnedMeshRenderer clonedRenderer = dismemberedPart.AddComponent<SkinnedMeshRenderer>();
            clonedRenderer.sharedMesh = originalRenderer.sharedMesh;
            clonedRenderer.sharedMaterials = originalRenderer.sharedMaterials;

            // 重新设置骨骼
            Transform[] originalBones = originalRenderer.bones;
            Transform[] newBones = new Transform[originalBones.Length];
            for (int i = 0; i < originalBones.Length; i++)
            {
                foreach (Transform cloned in clonedJoints)
                {
                    if (cloned.name == originalBones[i].name)
                    {
                        newBones[i] = cloned;
                        break;
                    }
                }
            }
            clonedRenderer.bones = newBones;
        }

        // 添加刚体组件以实现物理模拟
        Rigidbody rb = dismemberedPart.AddComponent<Rigidbody>();
        rb.useGravity = true;
    }

    // 获取相对路径
    private string GetRelativePath(Transform child, Transform parent)
    {
        string path = child.name;
        Transform current = child.parent;
        while (current != null && current != parent)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }
}
