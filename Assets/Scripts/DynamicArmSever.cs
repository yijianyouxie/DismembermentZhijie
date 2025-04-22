using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Animation))]
public class DynamicArmSever : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Transform _shoulderBone;  // 肩膀骨骼（分离点）
    [SerializeField] private Material _woundMaterial;  // 截面材质
    [SerializeField] private float _severForce = 5f;   // 分离力度

    private SkinnedMeshRenderer _bodySMR;
    [SerializeField]
    private AnimationClip _idleAnimation;
    private Mesh _originalMesh;
    private Mesh _originalBodyMesh;
    private Transform _originalRootBone;
    private Transform[] _originalBones;
    private Material[] _severedMaterials;
    private bool _isSevered;
    private List<Transform> _armBones = new List<Transform>();

    void Start()
    {
        _bodySMR = GetComponentInChildren<SkinnedMeshRenderer>();
        _originalMesh = Instantiate(_bodySMR.sharedMesh); // 创建网格副本
        _originalBodyMesh = _bodySMR.sharedMesh;
        _originalRootBone = _bodySMR.rootBone;
        _originalBones = _bodySMR.bones;

        // 预先记录手臂骨骼链
        CollectArmBones(_shoulderBone);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S) )
        {
            SeverArm();
        }
    }

    // 收集手臂骨骼链
    void CollectArmBones(Transform startBone)
    {
        _armBones.Clear();
        Transform current = startBone;
        while (current != null)
        {
            _armBones.Add(current);
            if (current.childCount > 0) current = current.GetChild(0);
            else break;
        }
    }

    public void SeverArm()
    {
        if (_isSevered) return;

        // 步骤1：复制手臂骨骼
        Transform severedRoot = DuplicateBoneHierarchy(_shoulderBone);

        // 步骤2：创建手臂网格
        CreateSeveredArmMesh(severedRoot);

        // 步骤3：更新身体网格
        UpdateBodyMesh();

        // 步骤4：添加物理效果
        AddPhysicsToSeveredArm(severedRoot.gameObject);

        // 步骤5：创建伤口
        CreateWoundEffect();

        // 重置身体动画
        Animation anim = GetComponent<Animation>();
        anim.Stop();
        anim.Play();

        _isSevered = true;
    }

    Transform DuplicateBoneHierarchy(Transform original)
    {
        GameObject newRoot = new GameObject(original.name); // 保持相同名称
        newRoot.transform.SetPositionAndRotation(original.position, original.rotation);

        // 使用字典映射原始骨骼和新骨骼
        Dictionary<Transform, Transform> boneMap = new Dictionary<Transform, Transform>();
        boneMap.Add(original, newRoot.transform);

        // 使用栈进行非递归遍历
        Stack<Transform> stack = new Stack<Transform>();
        stack.Push(original);

        while (stack.Count > 0)
        {
            Transform current = stack.Pop();

            foreach (Transform child in current)
            {
                // 创建新骨骼
                GameObject newBone = new GameObject(child.name);
                newBone.transform.SetParent(boneMap[current]);
                //newBone.transform.SetLocalPositionAndRotation(child.localPosition, child.localRotation);
                newBone.transform.localPosition = child.localPosition;
                newBone.transform.localRotation = child.localRotation;

                boneMap.Add(child, newBone.transform);
                stack.Push(child);
            }
        }
        return newRoot.transform;
    }

    void CreateSeveredArmMesh(Transform newRoot)
    {
        // 获取手臂部分的网格数据
        Mesh severedMesh = ExtractSubMesh(_armBones);

        // 创建新渲染器
        SkinnedMeshRenderer newSMR = newRoot.gameObject.AddComponent<SkinnedMeshRenderer>();
        newSMR.sharedMesh = severedMesh;
        newSMR.materials = _bodySMR.materials;

        // 重新绑定骨骼（关键修正）
        List<Transform> newBones = new List<Transform>();
        foreach (Transform bone in _armBones)
        {
            // 注意：从newRoot开始查找（包含自身）
            Transform newBone = newRoot.FindDeepChild(bone.name);

            // 调试日志
            if (newBone == null)
                Debug.LogError("找不到骨骼:bone.name:" + bone.name);
            else
                Debug.Log("成功绑定骨骼:bone.name:" + bone.name);

            newBones.Add(newBone);
        }

        newSMR.bones = newBones.ToArray();
        newSMR.rootBone = newBones[0];
        // 修改材质设置方式
        newSMR.sharedMaterials = _severedMaterials;

        // 添加调试可视化
        newRoot.gameObject.AddComponent<BoneVisualizer>();
    }


    Mesh ExtractSubMesh(List<Transform> targetBones)
    {
        Mesh newMesh = new Mesh();
        BoneWeight[] weights = _originalMesh.boneWeights;

        // 获取目标骨骼的索引
        HashSet<int> targetBoneIndices = new HashSet<int>();
        for (int i = 0; i < _bodySMR.bones.Length; i++)
        {
            if (targetBones.Contains(_bodySMR.bones[i]))
                targetBoneIndices.Add(i);
        }

        // 顶点映射字典（旧索引 -> 新索引）
        Dictionary<int, int> vertexMap = new Dictionary<int, int>();
        List<Vector3> newVertices = new List<Vector3>();
        List<BoneWeight> newBoneWeights = new List<BoneWeight>();
        List<int> newTriangles = new List<int>();

        // 第一步：收集顶点及相关属性
        List<Vector2> newUV = new List<Vector2>();
        List<Vector2> newUV2 = new List<Vector2>();
        List<Vector2> newUV3 = new List<Vector2>();
        List<Vector2> newUV4 = new List<Vector2>();
        List<Color> newColors = new List<Color>();
        // 第一步：收集所有相关顶点
        for (int subMesh = 0; subMesh < _originalMesh.subMeshCount; subMesh++)
        {
            int[] triangles = _originalMesh.GetTriangles(subMesh);
            for (int i = 0; i < triangles.Length; i++)
            {
                int originalIndex = triangles[i];
                BoneWeight weight = weights[originalIndex];

                // 检查顶点是否属于目标骨骼
                bool isTargetVertex = targetBoneIndices.Contains(weight.boneIndex0) ||
                                      targetBoneIndices.Contains(weight.boneIndex1) ||
                                      targetBoneIndices.Contains(weight.boneIndex2) ||
                                      targetBoneIndices.Contains(weight.boneIndex3);

                if (isTargetVertex && !vertexMap.ContainsKey(originalIndex))
                {
                    vertexMap[originalIndex] = newVertices.Count;
                    newVertices.Add(_originalMesh.vertices[originalIndex]);
                    newBoneWeights.Add(weight);

                    // 收集UV和颜色
                    if (_originalMesh.uv.Length > originalIndex)
                        newUV.Add(_originalMesh.uv[originalIndex]);
                    if (_originalMesh.uv2.Length > originalIndex)
                        newUV2.Add(_originalMesh.uv2[originalIndex]);
                    if (_originalMesh.uv3.Length > originalIndex)
                        newUV3.Add(_originalMesh.uv3[originalIndex]);
                    if (_originalMesh.uv4.Length > originalIndex)
                        newUV4.Add(_originalMesh.uv4[originalIndex]);
                    if (_originalMesh.colors.Length > originalIndex)
                        newColors.Add(_originalMesh.colors[originalIndex]);
                }
            }
        }

        // 第二步：重新构建三角形
        for (int subMesh = 0; subMesh < _originalMesh.subMeshCount; subMesh++)
        {
            int[] triangles = _originalMesh.GetTriangles(subMesh);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // 必须三个顶点都有效
                if (i + 2 >= triangles.Length) continue;

                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                if (vertexMap.TryGetValue(i0, out i0) &&
                    vertexMap.TryGetValue(i1, out i1) &&
                    vertexMap.TryGetValue(i2, out i2))
                {
                    newTriangles.Add(i0);
                    newTriangles.Add(i1);
                    newTriangles.Add(i2);
                }
            }
        }

        // 设置网格数据
        newMesh.vertices = newVertices.ToArray();
        newMesh.boneWeights = newBoneWeights.ToArray();
        newMesh.triangles = newTriangles.ToArray();

        // 添加材质和UV处理
        newMesh.uv = newUV.ToArray();
        if (newUV2.Count == newVertices.Count) newMesh.uv2 = newUV2.ToArray();
        if (newUV3.Count == newVertices.Count) newMesh.uv3 = newUV3.ToArray();
        if (newUV4.Count == newVertices.Count) newMesh.uv4 = newUV4.ToArray();
        if (newColors.Count == newVertices.Count) newMesh.colors = newColors.ToArray();

        // 处理子网格材质
        List<Material> materials = new List<Material>();
        newMesh.subMeshCount = _originalMesh.subMeshCount;

        for (int i = 0; i < _originalMesh.subMeshCount; i++)
        {
            List<int> subTriangles = new List<int>();
            int[] triangles = _originalMesh.GetTriangles(i);

            for (int j = 0; j < triangles.Length; j += 3)
            {
                if (j + 2 >= triangles.Length) continue;

                int i0 = triangles[j];
                int i1 = triangles[j + 1];
                int i2 = triangles[j + 2];

                if (vertexMap.TryGetValue(i0, out i0) &&
                   vertexMap.TryGetValue(i1, out i1) &&
                   vertexMap.TryGetValue(i2, out i2))
                {
                    subTriangles.Add(i0);
                    subTriangles.Add(i1);
                    subTriangles.Add(i2);
                }
            }

            newMesh.SetTriangles(subTriangles, i);
            materials.Add(_bodySMR.sharedMaterials[i]);
        }

        // 保存材质信息
        newMesh.name = "SeveredMesh";
        _severedMaterials = materials.ToArray(); // 新增类字段 private Material[] _severedMaterials;


        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        newMesh.RecalculateTangents();

        return newMesh;
    }

    // 新增辅助方法
    bool IsBoneInArm(int boneIndex)
    {
        if (boneIndex < 0 || boneIndex >= _bodySMR.bones.Length) return false;
        return _armBones.Contains(_bodySMR.bones[boneIndex]);
    }
    void UpdateBodyMesh()
    {
        // 创建新的身体网格（保持原始骨骼结构）
        Mesh newBodyMesh = new Mesh();

        // 复制原始网格数据
        newBodyMesh.vertices = _originalMesh.vertices;
        newBodyMesh.boneWeights = _originalMesh.boneWeights;
        newBodyMesh.bindposes = _originalMesh.bindposes;
        newBodyMesh.triangles = _originalMesh.triangles;

        // 仅过滤掉手臂相关三角形
        List<int> validTriangles = new List<int>();
        for (int subMesh = 0; subMesh < _originalMesh.subMeshCount; subMesh++)
        {
            int[] triangles = _originalMesh.GetTriangles(subMesh);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                bool keepTriangle = true;
                for (int j = 0; j < 3; j++)
                {
                    int index = triangles[i + j];
                    BoneWeight w = _originalMesh.boneWeights[index];

                    if (IsBoneInArm(w.boneIndex0) ||
                       IsBoneInArm(w.boneIndex1) ||
                       IsBoneInArm(w.boneIndex2) ||
                       IsBoneInArm(w.boneIndex3))
                    {
                        keepTriangle = false;
                        break;
                    }
                }

                if (keepTriangle)
                {
                    validTriangles.Add(triangles[i]);
                    validTriangles.Add(triangles[i + 1]);
                    validTriangles.Add(triangles[i + 2]);
                }
            }
        }

        newBodyMesh.triangles = validTriangles.ToArray();
        newBodyMesh.RecalculateNormals();

        // 关键：保持原始骨骼配置
        _bodySMR.sharedMesh = newBodyMesh;
        _bodySMR.bones = _originalBones; // 保持原始骨骼数组
        _bodySMR.rootBone = _originalRootBone; // 新增类字段记录原始根骨骼

        // 重置动画
        GetComponent<Animation>().Play();
    }

    void AddPhysicsToSeveredArm(GameObject severedArm)
    {
        Rigidbody rb = severedArm.AddComponent<Rigidbody>();
        rb.mass = 3f;
        rb.AddForce(Random.onUnitSphere * _severForce, ForceMode.Impulse);

        MeshCollider collider = severedArm.AddComponent<MeshCollider>();
        collider.convex = true;
        collider.sharedMesh = severedArm.GetComponent<SkinnedMeshRenderer>().sharedMesh;

        severedArm.AddComponent<BoneFreezer>();
    }

    void CreateWoundEffect()
    {
        GameObject wound = new GameObject("Wound");
        wound.transform.SetParent(_shoulderBone.parent);
        wound.transform.position = _shoulderBone.position;
        wound.transform.rotation = _shoulderBone.rotation;

        MeshFilter mf = wound.AddComponent<MeshFilter>();
        mf.mesh = GenerateQuadMesh();

        MeshRenderer mr = wound.AddComponent<MeshRenderer>();
        mr.material = _woundMaterial;
    }

    Mesh GenerateQuadMesh()
    {
        Mesh mesh = new Mesh();
        Vector3[] verts = new Vector3[4];
        Vector2[] uv = new Vector2[4];
        int[] tris = {0,1,2, 2,3,0};

        verts[0] = new Vector3(-0.1f, 0, -0.1f);
        verts[1] = new Vector3(0.1f, 0, -0.1f);
        verts[2] = new Vector3(0.1f, 0, 0.1f);
        verts[3] = new Vector3(-0.1f, 0, 0.1f);

        uv[0] = Vector2.zero;
        uv[1] = Vector2.right;
        uv[2] = Vector2.one;
        uv[3] = Vector2.up;

        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        return mesh;
    }
}

// 骨骼冻结组件
public class BoneFreezer : MonoBehaviour
{
    private Transform[] _bones;
    private Vector3[] _initialPositions;
    private Quaternion[] _initialRotations;

    void Start()
    {
        SkinnedMeshRenderer smr = GetComponent<SkinnedMeshRenderer>();
        _bones = smr.bones;
        
        _initialPositions = new Vector3[_bones.Length];
        _initialRotations = new Quaternion[_bones.Length];
        
        for(int i=0; i<_bones.Length; i++)
        {
            _initialPositions[i] = _bones[i].localPosition;
            _initialRotations[i] = _bones[i].localRotation;
        }
    }

    void LateUpdate()
    {
        for(int i=0; i<_bones.Length; i++)
        {
            _bones[i].localPosition = _initialPositions[i];
            _bones[i].localRotation = _initialRotations[i];
        }
    }
}
// BoneVisualizer.cs
public class BoneVisualizer : MonoBehaviour
{
    void OnDrawGizmos()
    {
        SkinnedMeshRenderer smr = GetComponent<SkinnedMeshRenderer>();
        if (smr == null) return;

        Gizmos.color = Color.red;
        foreach (var bone in smr.bones)
        {
            if (bone == null) continue;
            Gizmos.DrawSphere(bone.position, 0.01f);
            if (bone.parent != null)
                Gizmos.DrawLine(bone.position, bone.parent.position);
        }
    }
}

// 扩展方法：深度查找
public static class TransformExtensions
{
    // 修正后的深度查找方法
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        // 先检查自己
        if (parent.name == name) return parent;

        // 再递归检查子物体
        foreach (Transform child in parent)
        {
            var result = child.FindDeepChild(name);
            if (result != null) return result;
        }
        return null;
    }


    // 辅助方法：显示骨骼层级路径
    public static string GetHierarchyPath(this Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}