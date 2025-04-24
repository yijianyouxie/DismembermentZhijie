using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Animation))]
public class DynamicArmSever : MonoBehaviour
{
    [Header("Settings")]
    //[SerializeField] private Transform _shoulderBone;  // 肩膀骨骼（分离点）
    //[SerializeField] private Transform _headBone;  // 肩膀骨骼（分离点）
    [SerializeField] private List<Transform> dismemberBoneList;  // 肩膀骨骼（分离点）
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
    //private bool _isSevered;
    private Dictionary<Transform, List<Transform>> _partBonesDic = new Dictionary<Transform, List<Transform>>(4);
    private HashSet<int> partSeverdSet = new HashSet<int>();
    //private List<Transform> _partBonesList = new List<Transform>();

    void Start()
    {
        _bodySMR = GetComponentInChildren<SkinnedMeshRenderer>();
        _originalMesh = Instantiate(_bodySMR.sharedMesh); // 创建网格副本
        _originalBodyMesh = _bodySMR.sharedMesh;
        _originalRootBone = _bodySMR.rootBone;
        _originalBones = _bodySMR.bones;

        //// 预先记录手臂骨骼链
        //CollectArmBones(_shoulderBone);
        //CollectArmBones(_headBone);
        Transform tr;
        for(int i = 0; i < dismemberBoneList.Count; i++)
        {
            tr = dismemberBoneList[i];
            if(null != tr)
            {
                CollectArmBones(tr);
            }
        }
    }

    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.S) )
        {
            if(partSeverdSet.Contains(0))
            {
                return;
            }
            SeverArm(dismemberBoneList[0]);
            partSeverdSet.Add(0);
        }

        if(Input.GetKeyDown(KeyCode.A))
        {
            if (partSeverdSet.Contains(1))
            {
                return;
            }
            SeverArm(dismemberBoneList[1]);
            partSeverdSet.Add(1);
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            if (partSeverdSet.Contains(2))
            {
                return;
            }
            SeverArm(dismemberBoneList[2]);
            partSeverdSet.Add(2);
        }
    }

    // 收集手臂骨骼链
    void CollectArmBones(Transform startBone)
    {
        var bones = new List<Transform>(4);
        _partBonesDic[startBone] = bones;
        bones.Clear();
        Transform current = startBone;
        while (current != null)
        {
            bones.Add(current);
            if (current.childCount > 0) current = current.GetChild(0);
            else break;
        }
    }

    public void SeverArm(Transform tr)
    {
        //if (_isSevered) return;

        Animation ani = GetComponent<Animation>();
        ani.Sample();

        // 步骤1：复制手臂骨骼
        Transform severedRoot = DuplicateBoneHierarchy(tr);

        // 步骤2：创建手臂网格
        CreateSeveredArmMesh(tr, severedRoot);

        // 步骤3：更新身体网格
        UpdateBodyMesh(tr);

        // 步骤4：添加物理效果
        AddPhysicsToSeveredArm(severedRoot.gameObject);

        // 步骤5：创建伤口
        CreateWoundEffect(tr);

        // 重置身体动画
        Animation anim = GetComponent<Animation>();
        anim.Stop();
        anim.Play();

        //_isSevered = true;
    }
    public static Dictionary<Transform, int> originalBoneIndexMap = new Dictionary<Transform, int>();
    Transform DuplicateBoneHierarchy(Transform original)
    {
        originalBoneIndexMap.Clear(); // 清空旧的映射

        GameObject newRoot = new GameObject(original.name); // 保持相同名称
        //newRoot.transform.SetPositionAndRotation(original.position, original.rotation);
        //Debug.LogError("====original.position:"+ original.position + " newRoot.transform:"+ newRoot.transform.position);
        // 使用当前动画姿势的位置和旋转
        newRoot.transform.position = original.position;
        newRoot.transform.rotation = original.rotation;
        newRoot.transform.localScale = original.localScale;

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
                //newBone.transform.SetParent(boneMap[current]);
                ////newBone.transform.SetLocalPositionAndRotation(child.localPosition, child.localRotation);
                //newBone.transform.localPosition = child.localPosition;
                //newBone.transform.localRotation = child.localRotation;
                //// 使用世界坐标对齐
                //newBone.transform.SetParent(boneMap[current], false);
                //newBone.transform.position = child.position;
                //newBone.transform.rotation = child.rotation;
                //Debug.LogError("====child.position:" + child.position + " newBone.transform.position:" + newBone.transform.position);
                // 使用当前动画姿势的局部变换
                newBone.transform.SetParent(boneMap[current], false);
                newBone.transform.localPosition = child.localPosition;
                newBone.transform.localRotation = child.localRotation;
                newBone.transform.localScale = child.localScale;

                boneMap.Add(child, newBone.transform);
                stack.Push(child);
            }
        }

        // 在遍历骨骼时记录索引
        List<Transform> newBoneList = new List<Transform>();
        var partBonesList = _partBonesDic[original];
        foreach (var bone in partBonesList)
        {
            Transform newBone;
            if (boneMap.TryGetValue(bone, out newBone))
            {
                newBoneList.Add(newBone);
            }
        }

        for (int i = 0; i < newBoneList.Count; i++)
        {
            originalBoneIndexMap[partBonesList[i]] = i;
        }

        return newRoot.transform;
    }

    void CreateSeveredArmMesh(Transform original, Transform newRoot)
    {
        var partBonesList = _partBonesDic[original];
        // 获取手臂部分的网格数据
        Mesh severedMesh = ExtractSubMesh(partBonesList, newRoot);

        // 创建新渲染器
        SkinnedMeshRenderer newSMR = newRoot.gameObject.AddComponent<SkinnedMeshRenderer>();
        newSMR.sharedMesh = severedMesh;
        newSMR.materials = _bodySMR.materials;

        // 重新绑定骨骼（关键修正）
        List<Transform> newBones = new List<Transform>();
        foreach (Transform bone in partBonesList)
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

        // 重新计算绑定姿势
        Matrix4x4[] bindPoses = new Matrix4x4[newBones.Count];
        for (int i = 0; i < newBones.Count; i++)
        {
            // 计算相对根骨骼的变换矩阵
            bindPoses[i] = newBones[i].worldToLocalMatrix * newRoot.localToWorldMatrix;
        }
        severedMesh.bindposes = bindPoses;

        newSMR.bones = newBones.ToArray();
        newSMR.rootBone = newBones[0];
        // 修改材质设置方式
        newSMR.sharedMaterials = _severedMaterials;

        // 添加调试可视化
        newRoot.gameObject.AddComponent<BoneVisualizer>();
    }


    Mesh ExtractSubMesh(List<Transform> targetBones, Transform newRoot)
    {
        Mesh currentMesh = new Mesh();
        _bodySMR.BakeMesh(currentMesh);
        var currVertices = currentMesh.vertices;

        Mesh newMesh = new Mesh();
        BoneWeight[] weights = _originalMesh.boneWeights;

        // 获取目标骨骼的索引
        HashSet<int> targetBoneIndices = new HashSet<int>();
        for (int i = 0; i < _originalBones.Length; i++)
        {
            if (targetBones.Contains(_originalBones[i]))
                targetBoneIndices.Add(i);
        }

        // 顶点映射字典（旧索引 -> 新索引）
        Dictionary<int, int> vertexMap = new Dictionary<int, int>(2048);
        List<Vector3> newVertices = new List<Vector3>(512);
        List<BoneWeight> newBoneWeights = new List<BoneWeight>(512);
        List<int> newTriangles = new List<int>(2048);

        // 第一步：收集顶点及相关属性
        List<Vector2> newUV = new List<Vector2>(512);
        List<Vector2> newUV2 = new List<Vector2>(512);
        List<Vector2> newUV3 = new List<Vector2>(512);
        List<Vector2> newUV4 = new List<Vector2>(512);
        List<Color> newColors = new List<Color>(512);
        var oriUV = _originalMesh.uv;
        var oriUV2 = _originalMesh.uv2;
        var oriUV3 = _originalMesh.uv3;
        var oriUV4 = _originalMesh.uv4;
        var oriColors = _originalMesh.colors;
        Dictionary<int, int[]> oriTriangles = new Dictionary<int, int[]>(4);

        //// 获取坐标转换矩阵
        //Matrix4x4 originalRootWorldMatrix = _bodySMR.rootBone.localToWorldMatrix;
        //Matrix4x4 newRootWorldMatrix = newRoot.localToWorldMatrix;

        // 第一步：收集所有相关顶点
        for (int subMesh = 0; subMesh < _originalMesh.subMeshCount; subMesh++)
        {
            int[] triangles = _originalMesh.GetTriangles(subMesh);
            oriTriangles[subMesh] = triangles;
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
                    //// 坐标转换
                    //Vector3 worldPos = originalRootWorldMatrix.MultiplyPoint3x4(_originalMesh.vertices[originalIndex]);
                    //Vector3 localPos = newRootWorldMatrix.inverse.MultiplyPoint3x4(worldPos);
                    // 获取顶点在世界空间的位置
                    Vector3 worldPos = _bodySMR.transform.TransformPoint(currVertices[originalIndex]);
                    //Vector3 vertexPos = _originalMesh.vertices[originalIndex];

                    //// 计算加权后的世界位置
                    //Vector3 worldPos = Vector3.zero;
                    //float totalWeight = 0f;

                    //if (weight.weight0 > 0)
                    //    worldPos += _bodySMR.bones[weight.boneIndex0].TransformPoint(vertexPos) * weight.weight0;
                    //if (weight.weight1 > 0)
                    //    worldPos += _bodySMR.bones[weight.boneIndex1].TransformPoint(vertexPos) * weight.weight1;
                    //if (weight.weight2 > 0)
                    //    worldPos += _bodySMR.bones[weight.boneIndex2].TransformPoint(vertexPos) * weight.weight2;
                    //if (weight.weight3 > 0)
                    //    worldPos += _bodySMR.bones[weight.boneIndex3].TransformPoint(vertexPos) * weight.weight3;
                    // 转换到新骨骼的局部空间
                    Vector3 localPos = newRoot.InverseTransformPoint(worldPos);

                    vertexMap[originalIndex] = newVertices.Count;
                    newVertices.Add(localPos);
                    //Debug.LogError("========_orivertices:"+ _originalMesh.vertices[originalIndex] + " worldPos:"+ worldPos + " localPos:" + localPos);

                    BoneWeight newWeight = new BoneWeight();

                    // 转换每个骨骼索引
                    newWeight.boneIndex0 = GetMappedBoneIndex(weight.boneIndex0);
                    newWeight.boneIndex1 = GetMappedBoneIndex(weight.boneIndex1);
                    newWeight.boneIndex2 = GetMappedBoneIndex(weight.boneIndex2);
                    newWeight.boneIndex3 = GetMappedBoneIndex(weight.boneIndex3);

                    newWeight.weight0 = weight.weight0;
                    newWeight.weight1 = weight.weight1;
                    newWeight.weight2 = weight.weight2;
                    newWeight.weight3 = weight.weight3;
                    newBoneWeights.Add(newWeight);

                    // 收集UV和颜色
                    if (oriUV.Length > originalIndex)
                        newUV.Add(oriUV[originalIndex]);
                    if (oriUV2.Length > originalIndex)
                        newUV2.Add(oriUV2[originalIndex]);
                    if (oriUV3.Length > originalIndex)
                        newUV3.Add(oriUV2[originalIndex]);
                    if (oriUV4.Length > originalIndex)
                        newUV4.Add(oriUV4[originalIndex]);
                    if (oriColors.Length > originalIndex)
                        newColors.Add(oriColors[originalIndex]);
                }
            }
        }

        // 第二步：重新构建三角形
        for (int subMesh = 0; subMesh < _originalMesh.subMeshCount; subMesh++)
        {
            int[] triangles = oriTriangles[subMesh];
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
        List<Material> materials = new List<Material>(4);
        newMesh.subMeshCount = _originalMesh.subMeshCount;

        for (int i = 0; i < _originalMesh.subMeshCount; i++)
        {
            List<int> subTriangles = new List<int>(1024);
            int[] triangles = oriTriangles[i];

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

    // 新增辅助方法：将原始骨骼索引转换为新骨骼索引
    int GetMappedBoneIndex(int originalBoneIndex)
    {
        if (originalBoneIndex < 0 || originalBoneIndex >= _originalBones.Length)
            return 0;

        Transform originalBone = _originalBones[originalBoneIndex];
        if (originalBoneIndexMap.ContainsKey(originalBone))
            return originalBoneIndexMap[originalBone];

        return 0; // 默认指向根骨骼
    }

    // 新增辅助方法
    bool IsBoneInArm(List<Transform> partBonesList, int boneIndex)
    {
        if (boneIndex < 0 || boneIndex >= _originalBones.Length) return false;
        return partBonesList.Contains(_originalBones[boneIndex]);
    }
    void UpdateBodyMesh(Transform original)
    {
        var partBonesList = _partBonesDic[original];
        // 创建新的身体网格（保持原始骨骼结构）
        Mesh newBodyMesh = new Mesh();

        var boneWeights = _originalMesh.boneWeights;
        // 复制原始网格数据
        newBodyMesh.vertices = _originalMesh.vertices;
        newBodyMesh.boneWeights = boneWeights;
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
                    BoneWeight w = boneWeights[index];

                    if (IsBoneInArm(partBonesList, w.boneIndex0) ||
                       IsBoneInArm(partBonesList, w.boneIndex1) ||
                       IsBoneInArm(partBonesList, w.boneIndex2) ||
                       IsBoneInArm(partBonesList, w.boneIndex3))
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
        newBodyMesh.uv = _originalMesh.uv;
        newBodyMesh.uv2 = _originalMesh.uv2;
        newBodyMesh.uv3 = _originalMesh.uv3;
        newBodyMesh.uv4 = _originalMesh.uv4;
        newBodyMesh.RecalculateNormals();

        // 关键：保持原始骨骼配置
        _bodySMR.sharedMesh = newBodyMesh;
        _bodySMR.bones = _originalBones; // 保持原始骨骼数组
        _bodySMR.rootBone = _originalRootBone; // 新增类字段记录原始根骨骼

        // 重置动画
        GetComponent<Animation>().Play();

        _originalMesh = newBodyMesh;
    }

    void AddPhysicsToSeveredArm(GameObject severedArm)
    {
        Rigidbody rb = severedArm.AddComponent<Rigidbody>();
        rb.mass = 3f;
        rb.AddForce(Random.onUnitSphere * _severForce, ForceMode.Impulse);

        MeshCollider collider = severedArm.AddComponent<MeshCollider>();
        collider.convex = true;
        collider.sharedMesh = severedArm.GetComponent<SkinnedMeshRenderer>().sharedMesh;

        //severedArm.AddComponent<BoneFreezer>();
    }

    void CreateWoundEffect(Transform original)
    {
        GameObject wound = new GameObject("Wound");
        wound.transform.SetParent(original.parent);
        wound.transform.position = original.position;
        wound.transform.rotation = original.rotation;

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
            _initialPositions[i] = _bones[i].position;
            _initialRotations[i] = _bones[i].rotation;
        }
    }

    void LateUpdate()
    {
        for(int i=0; i<_bones.Length; i++)
        {
            _bones[i].position = _initialPositions[i];
            _bones[i].rotation = _initialRotations[i];
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

        Gizmos.color = Color.yellow;
        foreach (var bone in smr.bones)
        {
            if (bone == null) continue;
            Gizmos.DrawSphere(bone.position, 0.03f);
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