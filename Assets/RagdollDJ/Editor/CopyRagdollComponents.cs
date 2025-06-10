using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class CopyRagdollComponents : EditorWindow
{
    [MenuItem("Tools/RagdollTools")]
    static void AddWindow()
    {
        Rect wr = new Rect(0, 0, 300, 200);
        CopyRagdollComponents window = (CopyRagdollComponents)EditorWindow.GetWindowWithRect(typeof(CopyRagdollComponents), wr, true, "CopyRagdollComponents");
        window.Show();
    }

    private Transform sourceModel;
    private Transform targetModel;
    private float totalMass = -1;

    private float massCoefficient = -1;
    private Object foldPath = null;

    //绘制窗口时调用
    void OnGUI()
    {
        sourceModel = EditorGUILayout.ObjectField("Source Model", sourceModel, typeof(Transform), true) as Transform;
        targetModel = EditorGUILayout.ObjectField("Target Model", targetModel, typeof(Transform), true) as Transform;
        if (GUILayout.Button("Copy Ragdoll Components", GUILayout.Width(200)))
        {
            this.ProcessCopy();
        }

        EditorGUILayout.Separator();

        totalMass = EditorGUILayout.FloatField("Total Mass", totalMass);
        if (GUILayout.Button("Adjust Total Mass", GUILayout.Width(200)))
        {
            this.ProcessMass();
        }

        massCoefficient = EditorGUILayout.FloatField("Mass Coefficient", massCoefficient);
        foldPath = EditorGUILayout.ObjectField("导出检查结果Excel路径", foldPath, typeof(Object), false);
        if (GUILayout.Button("Batch Adjust Total Mass", GUILayout.Width(200)))
        {
            this.BatchProcessMass();
        }
    }

    private void ProcessMass()
    {
        if (totalMass <= 0)
        {
            Debug.Log("Total Mass invalid!");
            return;
        }
        if (targetModel == null)
        {
            Debug.Log("Target model empty!");
            return;
        }

        Rigidbody[] rigidBodys = targetModel.GetComponentsInChildren<Rigidbody>();
        float currentTotalMass = 0;
        for (int i = 0; i < rigidBodys.Length; i++)
            currentTotalMass += rigidBodys[i].mass;
        for (int i = 0; i < rigidBodys.Length; i++)
            rigidBodys[i].mass *= totalMass / currentTotalMass;
    }

    private void BatchProcessMass()
    {
        if (massCoefficient <= 0)
        {
            Debug.Log("Mass Coefficient invalid!");
            return;
        }
        if (foldPath == null)
        {
            Debug.Log("Empty path!");
            return;
        }

        string path = AssetDatabase.GetAssetPath(foldPath);

        string head = Application.dataPath.TrimEnd("Asset".ToCharArray());
        if (!Directory.Exists(head + path))
        {
            Debug.Log("Path not exist!");
            return;
        }

        DirectoryInfo direction = new DirectoryInfo(path);
        FileInfo[] files = direction.GetFiles("*", SearchOption.TopDirectoryOnly);

        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].Name.EndsWith(".meta"))
            {
                continue;
            }

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path + "/" + files[i].Name);
            if (go != null)
            {
                Rigidbody[] rigidBodys = go.GetComponentsInChildren<Rigidbody>();
                if (rigidBodys.Length > 0)
                    Debug.Log(files[i].Name);

                for (int j = 0; j < rigidBodys.Length; j++)
                    rigidBodys[j].mass *= massCoefficient;
            }
        }
    }

    private void ProcessCopy()
    {
        if (sourceModel == null)
        {
            Debug.Log("Source model empty!");
            return;
        }
        if (targetModel == null)
        {
            Debug.Log("Target model empty!");
            return;
        }

        bool boneCheck = true;
        Rigidbody[] rigidBodys = sourceModel.GetComponentsInChildren<Rigidbody>();

        // 检查骨骼是否匹配
        for (int i = 0; i < rigidBodys.Length; ++i)
        {
            if (GetBone(targetModel, rigidBodys[i].name) == null)
            {
                Debug.Log("Target Model doesn't has bone of name " + rigidBodys[i].name);
                boneCheck = false;
            }

            if (rigidBodys[i].GetComponent<Collider>() != null)
            {
                if (rigidBodys[i].GetComponent<Collider>() is CapsuleCollider || rigidBodys[i].GetComponent<Collider>() is BoxCollider || rigidBodys[i].GetComponent<Collider>() is SphereCollider)
                    continue;
                    
                Debug.Log(rigidBodys[i].name + " has neither CapsuleCollider, BoxCollider or SphereCollider");
                boneCheck = false;
            }
        }
        if (!boneCheck)
        {
            Debug.Log("Target Model bones doesn't match the Source Model, copy failed!");
            return;
        }

        // 开始拷贝
        for (int i = 0; i < rigidBodys.Length; ++i)
        {
            Transform targetBone = GetBone(targetModel, rigidBodys[i].name);
            Rigidbody rigidBody = targetBone.gameObject.AddComponent<Rigidbody>();
            CopyRigidBody(rigidBodys[i], rigidBody);

            if (rigidBodys[i].GetComponent<Collider>() != null)
            {
                if (rigidBodys[i].GetComponent<Collider>() is CapsuleCollider)
                {
                    CapsuleCollider capsule = targetBone.gameObject.AddComponent<CapsuleCollider>();
                    CopyCapsuleCollider(rigidBodys[i].GetComponent<Collider>() as CapsuleCollider, capsule);
                }
                else if (rigidBodys[i].GetComponent<Collider>() is BoxCollider)
                {
                    BoxCollider box = targetBone.gameObject.AddComponent<BoxCollider>();
                    CopyBoxCollider(rigidBodys[i].GetComponent<Collider>() as BoxCollider, box);
                }
                else if (rigidBodys[i].GetComponent<Collider>() is SphereCollider)
                {
                    SphereCollider sphere = targetBone.gameObject.AddComponent<SphereCollider>();
                    CopySphereCollider(rigidBodys[i].GetComponent<Collider>() as SphereCollider, sphere);
                }
            }

            CharacterJoint characterJoint = rigidBodys[i].GetComponent<CharacterJoint>();
            if (characterJoint != null)
            {
                CharacterJoint targetJoint = targetBone.gameObject.AddComponent<CharacterJoint>();
                CopyCharacterJoint(characterJoint, targetJoint);
            }
        }
    }

    private Transform GetBone(Transform trans, string bone)
    {
        if (trans.name == bone)
            return trans;

        for (int i = 0; i < trans.childCount; i++)
        {
            Transform child = GetBone(trans.GetChild(i), bone);
            if (child != null)
                return child;
        }

        return null;
    }

    private void CopyRigidBody(Rigidbody source, Rigidbody target)
    {
        target.mass = source.mass;
        target.drag = source.drag;
        target.angularDrag = source.angularDrag;
        target.useGravity = source.useGravity;
        target.isKinematic = source.isKinematic;
        target.interpolation = source.interpolation;
        target.collisionDetectionMode = source.collisionDetectionMode;
        target.constraints = source.constraints;
    }
    private void CopyCharacterJoint(CharacterJoint source, CharacterJoint target)
    {
        target.connectedBody = GetBone(targetModel, source.connectedBody.name).GetComponent<Rigidbody>();
        target.anchor = source.anchor;
        target.axis = source.axis;
        target.autoConfigureConnectedAnchor = source.autoConfigureConnectedAnchor;
        target.connectedAnchor = source.connectedAnchor;
        target.swingAxis = source.swingAxis;
        target.lowTwistLimit = source.lowTwistLimit;
        target.highTwistLimit = source.highTwistLimit;
        target.swing1Limit = source.swing1Limit;
        target.swing2Limit = source.swing2Limit;
        target.breakForce = source.breakForce;
        target.breakTorque = source.breakTorque;
        target.enableCollision = source.enableCollision;
    }

    private void CopyCapsuleCollider(CapsuleCollider source, CapsuleCollider target)
    {
        target.direction = source.direction;
        target.isTrigger = source.isTrigger;
        target.sharedMaterial = source.sharedMaterial;
        target.center = source.center;
        target.radius = source.radius;
        target.height = source.height;
    }
    private void CopyBoxCollider(BoxCollider source, BoxCollider target)
    {
        target.isTrigger = source.isTrigger;
        target.sharedMaterial = source.sharedMaterial;
        target.center = source.center;
        target.size = source.size;
    }
    private void CopySphereCollider(SphereCollider source, SphereCollider target)
    {
        target.isTrigger = source.isTrigger;
        target.sharedMaterial = source.sharedMaterial;
        target.center = source.center;
        target.radius = source.radius;
    }
}
