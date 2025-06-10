using UnityEngine;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(Animation))]
public class DynamicPartSever : MonoBehaviour
{
    [Header("Settings")]
    //[SerializeField] private Transform _shoulderBone;  // 肩膀骨骼（分离点）
    //[SerializeField] private Transform _headBone;  // 肩膀骨骼（分离点）
    [SerializeField] private List<Transform> dismemberBoneList;  // 肩膀骨骼（分离点）
    [SerializeField]
    private List<SkinnedMeshRenderer> subSkinnedMeshRenderList;
    private Dictionary<Transform, Transform[]> subSkinBonesDic = new Dictionary<Transform, Transform[]>(4);
    [SerializeField] private Material _woundMaterial;  // 截面材质
    [SerializeField] private float _severForce = 5f;   // 分离力度

    private SkinnedMeshRenderer _bodySMR;
    private Transform _bodySMRTr;
    private Material[] _bodySMRSharedMaterials;
    //[SerializeField]
    //private AnimationClip _idleAnimation;
    private Mesh _originalMesh;
    //private Mesh _originalBodyMesh;
    private Transform _originalRootBone;
    private Transform[] _originalBones;
    //整个骨骼的tr和index字典
    private Dictionary<Transform, int> _boneTr2IndexDic = new Dictionary<Transform, int>(32);
    private Dictionary<int, Transform> _boneIndex2TrDic = new Dictionary<int, Transform>(32);
    //private Material[] _severedMaterials;
    //private bool _isSevered;

    //value表示的是，此肢解部分的各个骨骼在原整体骨骼中的index
    private Dictionary<Transform, HashSet<int>> _partBonesDic = new Dictionary<Transform, HashSet<int>>(4);
    //value表示的是，此肢解部分的各个骨骼
    private Dictionary<Transform, Transform[]> _partBonesTrDic = new Dictionary<Transform, Transform[]>(4);
    //此部分是否被肢解的标记
    private HashSet<int> partSeverdSet = new HashSet<int>();
    //各个部分的骨头在原骨骼中的index
    private List<int> _partBonesOriIndexList = new List<int>(8);
    private float boneWeightThreshold = 0.3f;

    //新建肢解部分的时候用到
    private Mesh bakedMesh;
    private Dictionary<Transform, Mesh> newMeshDic = new Dictionary<Transform, Mesh>(4);
    private Dictionary<Transform, SkinnedMeshRenderer> smrDic = new Dictionary<Transform, SkinnedMeshRenderer>(4);
    private Dictionary<Transform, Transform> smrTrDic = new Dictionary<Transform, Transform>(4);

    //复制骨头的时候用到
    private Queue<GameObject> newBoneGOQueue = new Queue<GameObject>(8);
    private Dictionary<GameObject, Transform> newBoneGO2Tr = new Dictionary<GameObject, Transform>(8);

    //key，此部分；value：此部分下的三角形顶点索引hash
    private Dictionary<Transform, Dictionary<int, int>> partTr2TriIndexHashDic = new Dictionary<Transform, Dictionary<int, int>>(4);

    private Mesh newBodyMesh;

    private RagdollEnabler rde;

    void Start()
    {
        //UnityEngine.Profiling.Profiler.BeginSample("====Start");
        _bodySMR = GetComponentInChildren<SkinnedMeshRenderer>();
        _bodySMRTr = _bodySMR.transform;
        _bodySMRSharedMaterials = _bodySMR.sharedMaterials;
        _originalMesh = Instantiate(_bodySMR.sharedMesh); // 创建网格副本

        oriBindposeList.Clear();
        _originalMesh.GetBindposes(oriBindposeList);
        var bindePoseCount = oriBindposeList.Count;
        oriBindposes = new Matrix4x4[bindePoseCount];
        for(int i = 0; i < bindePoseCount; i++)
        {
            oriBindposes[i] = oriBindposeList[i];
        }

        _oriMeshBoneWeights.Clear();
        _originalMesh.GetBoneWeights(_oriMeshBoneWeights);
        var boneWeightCount = _oriMeshBoneWeights.Count;
        _oriMeshBoneWeightArray = new BoneWeight[boneWeightCount];
        for (int i = 0; i < boneWeightCount; i++)
        {
            _oriMeshBoneWeightArray[i] = _oriMeshBoneWeights[i];
        }

        GetOriginalInfos();
        //_originalBodyMesh = _bodySMR.sharedMesh;
        _originalRootBone = _bodySMR.rootBone;
        _originalBones = _bodySMR.bones;

        _boneTr2IndexDic.Clear();
        _boneIndex2TrDic.Clear();
        var len = _originalBones.Length;
        for (int i = 0; i < len; i++)
        {
            _boneTr2IndexDic[_originalBones[i]] = i;
            _boneIndex2TrDic[i] = _originalBones[i];
        }
        
        Transform tr;
        for(int i = 0; i < dismemberBoneList.Count; i++)
        {
            tr = dismemberBoneList[i];
            if(null != tr)
            {
                CollectPartBones(tr);
            }

            GameObject obj = new GameObject("Skin");
            var smr = obj.AddComponent<SkinnedMeshRenderer>();
            obj.SetActive(false);
            smrDic[tr] = smr;
            smrTrDic[tr] = smr.transform;

            //两者长度一致
            var subSkinMR = subSkinnedMeshRenderList[i];
            if(null != subSkinMR)
            {
                subSkinBonesDic[tr] = subSkinMR.bones;
            }
        }

        bakedMesh = new Mesh();

        GameObject newBone;
        for(int i = 0; i < 8; i++)
        {
            newBone = new GameObject();
            var rb = newBone.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var mc = newBone.AddComponent<MeshCollider>();
            mc.enabled = false;
            newBoneGOQueue.Enqueue(newBone);
            newBoneGO2Tr[newBone] = newBone.transform;
        }

        newBodyMesh = new Mesh();

        rde = GetComponent<RagdollEnabler>();

        //UnityEngine.Profiling.Profiler.EndSample();
    }
    private List<Vector3> _oriMeshVertices = new List<Vector3>(2048);
    private List<BoneWeight> _oriMeshBoneWeights = new List<BoneWeight>(2048);
    private BoneWeight[] _oriMeshBoneWeightArray;
    private List<Vector2> _oriUVs = new List<Vector2>(2048);
    //private List<Vector2> _oriUVs2 = new List<Vector2>(512);
    //private List<Vector2> _oriUVs3 = new List<Vector2>(512);
    //private List<Vector2> _oriUVs4 = new List<Vector2>(512);
    //private List<Color> _oriColors = new List<Color>(512);
    private int _oriUVsCount = 0;
    //private int _oriUVs2Count = 0;
    //private int _oriUVs3Count = 0;
    //private int _oriUVs4Count = 0;
    //private int _oriColorsCount = 0;
    private int _originaleSubmeshCount = 0;
    private Dictionary<int, List<int>> oriTriangles = new Dictionary<int, List<int>>(2);
    private Matrix4x4[] oriBindposes;
    private List<Matrix4x4> oriBindposeList = new List<Matrix4x4>();

    private void GetOriginalInfos()
    {
        _oriMeshVertices.Clear();
        _oriUVs.Clear();
        //_oriUVs2.Clear();
        //_oriUVs3.Clear();
        //_oriUVs4.Clear();
        //_oriColors.Clear();

        _originalMesh.GetVertices(_oriMeshVertices);

        _originalMesh.GetUVs(0, _oriUVs);
        //_originalMesh.GetUVs(1, _oriUVs2);
        //_originalMesh.GetUVs(2, _oriUVs3);
        //_originalMesh.GetUVs(3, _oriUVs4);
        //_originalMesh.GetColors(_oriColors);
        _oriUVsCount = _oriUVs.Count;
        //_oriUVs2Count = _oriUVs2.Count;
        //_oriUVs3Count = _oriUVs3.Count;
        //_oriUVs4Count = _oriUVs4.Count;
        //_oriColorsCount = _oriColors.Count;

        _originaleSubmeshCount = _originalMesh.subMeshCount;
        //UnityEngine.Profiling.Profiler.BeginSample("====GetOriginalInfos2");
        List<int> subTriangles;
        for (int subMesh = 0; subMesh < _originaleSubmeshCount; subMesh++)
        {
            if(oriTriangles.TryGetValue(subMesh, out subTriangles))
            {
                subTriangles.Clear();
                _originalMesh.GetTriangles(subTriangles, subMesh);
            }
            else
            {
                subTriangles = new List<int>(8192);
                _originalMesh.GetTriangles(subTriangles, subMesh);
                oriTriangles[subMesh] = subTriangles;
            }            
        }

        
        //UnityEngine.Profiling.Profiler.EndSample();
    }

    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.S) )
        {
            if(partSeverdSet.Contains(0))
            {
                return;
            }
            SeverPart(dismemberBoneList[0], subSkinnedMeshRenderList[0]);
            partSeverdSet.Add(0);
        }

        if(Input.GetKeyDown(KeyCode.A))
        {
            if (partSeverdSet.Contains(1))
            {
                return;
            }
            SeverPart(dismemberBoneList[1], subSkinnedMeshRenderList[1]);
            partSeverdSet.Add(1);
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            if (partSeverdSet.Contains(2))
            {
                return;
            }
            SeverPart(dismemberBoneList[2], subSkinnedMeshRenderList[2]);
            partSeverdSet.Add(2);
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if(null != rde)
            {
                rde.ActivateRagdoll(UnityEngine.Random.onUnitSphere * _severForce * 10);
            }
        }
    }

    void TraverseChild(Transform tr, HashSet<int> bones)
    {
        if(null != tr)
        {
            int boneIndex = -1;
            if (_boneTr2IndexDic.TryGetValue(tr, out boneIndex))
            {
                bones.Add(boneIndex);
                _partBonesOriIndexList.Add(boneIndex);
            }

            if (tr.childCount > 0)
            {
                for (int i = 0; i < tr.childCount; i++)
                {
                    var current = tr.GetChild(i);
                    if(null != current)
                    {
                        boneIndex = -1;
                        if(_boneTr2IndexDic.TryGetValue(current, out boneIndex))
                        {
                            bones.Add(boneIndex);
                            _partBonesOriIndexList.Add(boneIndex);
                        }

                        TraverseChild(current, bones);
                    }
                }
            }
        }
    }
    // 收集手臂骨骼链
    void CollectPartBones(Transform startBone)
    {
        HashSet<int> bones = new HashSet<int>();
        _partBonesDic[startBone] = bones;
        bones.Clear();
        Transform current = startBone;
        TraverseChild(current, bones);
        //while (current != null)
        //{
        //    bones.Add(_boneTr2IndexDic[current]);
        //    _partBonesOriIndexList.Add(_boneTr2IndexDic[current]);
        //    if (current.childCount > 0) current = current.GetChild(0);
        //    else break;
        //}
        //获取长度后，开辟数组
        var bonesLen = bones.Count;
        var bonesArray = new Transform[bonesLen];
        var index = 0;
        foreach(var i in bones)
        {
            bonesArray[index] = _boneIndex2TrDic[i];
            index++;
        }
        _partBonesTrDic[startBone] = bonesArray;

        //UnityEngine.Profiling.Profiler.BeginSample("====triOriIndexHash");
        var triOriIndexHash = new Dictionary<int, int>(512);
        partTr2TriIndexHashDic[startBone] = triOriIndexHash;
        for (int subMesh = 0; subMesh < _originaleSubmeshCount; subMesh++)
        {
            var triangles = oriTriangles[subMesh];
            var trianglesCount = triangles.Count;
            for (int i = 0; i < trianglesCount; i++)
            {
                int originalIndex = triangles[i];
                if(triOriIndexHash.ContainsKey(originalIndex))
                {
                    continue;
                }
                BoneWeight weight = _oriMeshBoneWeights[originalIndex];

                // 检查顶点是否属于目标骨骼
                bool isTargetVertex = (weight.weight0 >= boneWeightThreshold && IsBoneInPart(bones, weight.boneIndex0)) ||
                                      (weight.weight1 >= boneWeightThreshold && IsBoneInPart(bones, weight.boneIndex1)) ||
                                      (weight.weight2 >= boneWeightThreshold && IsBoneInPart(bones, weight.boneIndex2)) ||
                                      (weight.weight3 >= boneWeightThreshold && IsBoneInPart(bones, weight.boneIndex3));

                if (isTargetVertex)
                {
                    triOriIndexHash[originalIndex] = 1;
                }
            }
        }
        //UnityEngine.Profiling.Profiler.EndSample();

        var newMesh = new Mesh();
        //预先赋值，消除后续extractmesh的gc开销
        newVertices.Clear();
        newTriangles.Clear();
        newUV.Clear();
        newMesh.SetVertices(newVertices);
        newMesh.SetTriangles(newTriangles, 0);
        newMesh.SetUVs(0, newUV);
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        newMesh.RecalculateTangents();
        newMeshDic[startBone] = newMesh;
    }

    public void SeverPart(Transform tr, SkinnedMeshRenderer subSmr)
    {
        //if (_isSevered) return;

        //Animation ani = GetComponent<Animation>();
        //ani.Sample();

        // 步骤1：复制手臂骨骼
        Transform severedRoot = DuplicateBoneHierarchy(tr);


        var triOriIndexHash = partTr2TriIndexHashDic[tr];
        // 步骤2：创建手臂网格
        CreateSeveredPartMesh(triOriIndexHash, tr, severedRoot, subSmr);

        // 步骤3：更新身体网格
        UpdateBodyMesh(triOriIndexHash, tr);

        // 步骤4：添加物理效果
        AddPhysicsToSeveredPart(severedRoot.gameObject);

        // 步骤5：创建伤口
        //CreateWoundEffect(tr);

        // 重置身体动画
        //Animation anim = GetComponent<Animation>();
        //anim.Stop();
        //anim.Play();

        //_isSevered = true;
    }

    private GameObject GetNewBone()
    {
        if(newBoneGOQueue.Count > 0)
        {
            return newBoneGOQueue.Dequeue();
        }else
        {
            var newBone = new GameObject();
            var rb = newBone.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var mc = newBone.AddComponent<MeshCollider>();
            mc.enabled = false;       
            newBoneGO2Tr[newBone] = newBone.transform;
            return newBone;
        }
    }
    private void RecycleNewBone(GameObject newBone)
    {
        if(null != newBone)
        {
            newBoneGOQueue.Enqueue(newBone);
        }
    }
    //key是原始整个骨骼里的索引，value是在新骨骼中的索引
    public static Dictionary<int, int> partOriginalBoneIndexMap = new Dictionary<int, int>(8);
    private Dictionary<Transform, Transform> boneMap = new Dictionary<Transform, Transform>(8);
    private Stack<Transform> stack = new Stack<Transform>(4);
    private List<Transform> newBoneList = new List<Transform>(8);
    Transform DuplicateBoneHierarchy(Transform original)
    {
        //UnityEngine.Profiling.Profiler.BeginSample("====DuplicateBoneHierarchy1");
        partOriginalBoneIndexMap.Clear(); // 清空旧的映射

        //GameObject newRoot = new GameObject(original.name); // 保持相同名称
        GameObject newRoot = GetNewBone();
#if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.BeginSample("DuplicateBoneHierarchy.GC only run in Unity Editor.");
        newRoot.name = original.name;
        UnityEngine.Profiling.Profiler.EndSample();
#endif
        //newRoot.transform.SetPositionAndRotation(original.position, original.rotation);
        //Debug.LogError("====original.position:"+ original.position + " newRoot.transform:"+ newRoot.transform.position);
        // 使用当前动画姿势的位置和旋转
        var newRootTr = newBoneGO2Tr[newRoot];
        newRootTr.position = original.position;
        newRootTr.rotation = original.rotation;
        newRootTr.localScale = original.localScale;

        Transform[] subSkinBones;
        int subBoneIndex = -1;
        if (subSkinBonesDic.TryGetValue(original, out subSkinBones))
        {
            subBoneIndex = Array.IndexOf(subSkinBones, original);
            if (subBoneIndex >= 0)
            {
                subSkinBones[subBoneIndex] = newRootTr;
            }        
        }

        // 使用字典映射原始骨骼和新骨骼
        boneMap.Clear();
        boneMap.Add(original, newRootTr);

        // 使用栈进行非递归遍历
        stack.Clear();
        stack.Push(original);
        //UnityEngine.Profiling.Profiler.EndSample();

        //UnityEngine.Profiling.Profiler.BeginSample("====DuplicateBoneHierarchy2");
        while (stack.Count > 0)
        {
            Transform current = stack.Pop();
            var childLen = current.childCount;
            //foreach (Transform child in current)
            for(int i = 0; i < childLen; i++)
            {
                var child = current.GetChild(i);
                // 创建新骨骼

                GameObject newBone = GetNewBone();
#if UNITY_EDITOR
                UnityEngine.Profiling.Profiler.BeginSample("DuplicateBoneHierarchy.GC only run in Unity Editor.");
                newBone.name = child.name;
                UnityEngine.Profiling.Profiler.EndSample();
#endif
                var newBoneTr = newBoneGO2Tr[newBone];
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
                newBoneTr.SetParent(boneMap[current], false);
                newBoneTr.localPosition = child.localPosition;
                newBoneTr.localRotation = child.localRotation;
                newBoneTr.localScale = child.localScale;

                if(null != subSkinBones)
                {
                    subBoneIndex = Array.IndexOf(subSkinBones, child);
                    if (subBoneIndex >= 0)
                    {
                        subSkinBones[subBoneIndex] = newBoneTr;
                    }
                }

                boneMap.Add(child, newBoneTr);
                stack.Push(child);
            }
        }

        // 在遍历骨骼时记录索引
        newBoneList.Clear();
        var partBonesList = _partBonesDic[original];
        foreach (var bone in partBonesList)
        {
            Transform newBone;
            if (boneMap.TryGetValue(_boneIndex2TrDic[bone], out newBone))
            {
                newBoneList.Add(newBone);
            }
        }

        var newBoneListLen = newBoneList.Count;
        for (int i = 0; i < newBoneListLen; i++)
        {
            partOriginalBoneIndexMap[_partBonesOriIndexList[i]] = i;
        }

        //此处不能重复利用啊，boneMap里存的是有用的
        //Transform tr;
        ////直接foreach dic有box gc.下边的写法直接获取迭代器也同样还是会产生32B+32B的gc
        //foreach (var item in boneMap)
        //{
        //    tr = item.Value;
        //    RecycleNewBone(tr.gameObject);
        //}
        //var iterator = boneMap.GetEnumerator();
        //while (iterator.MoveNext())
        //{
        //    tr = iterator.Current.Value;
        //    if (null != tr)
        //    {
        //        RecycleNewBone(tr.gameObject);
        //    }
        //}
        boneMap.Clear();

        //UnityEngine.Profiling.Profiler.EndSample();
        return newRootTr;
    }

    void CreateSeveredPartMesh(Dictionary<int, int> triOriIndexHash, Transform original, Transform newRoot, SkinnedMeshRenderer subSmr)
    {
        var partBonesList = _partBonesDic[original];
        var newMesh = newMeshDic[original];

        //var partBoneTrArray = _partBonesTrDic[original];

        // 获取手臂部分的网格数据
        Mesh severedMesh = ExtractSubMesh(triOriIndexHash, partBonesList, newRoot, newMesh);

        // 创建新渲染器
        SkinnedMeshRenderer newSMR = smrDic[original];// newRoot.gameObject.AddComponent<SkinnedMeshRenderer>();
        newSMR.gameObject.SetActive(true);
        smrTrDic[original].SetParent(newRoot, false);
        //newSMR.transform.localPosition = Vector3.zero;
        //newSMR.transform.localRotation = Quaternion.identity;

        newSMR.sharedMesh = severedMesh;
        //newSMR.materials = _bodySMR.materials;

        //// 重新绑定骨骼（关键修正）
        //List<Transform> newBones = new List<Transform>();
        //foreach (int boneIndex in partBonesList)
        //{
        //    var bone = _boneIndex2TrDic[boneIndex];
        //    // 注意：从newRoot开始查找（包含自身）
        //    Transform newBone = newRoot.FindDeepChild(bone.name);

        //    //// 调试日志
        //    //if (newBone == null)
        //    //    Debug.LogError("找不到骨骼:bone.name:" + bone.name);
        //    //else
        //    //    Debug.Log("成功绑定骨骼:bone.name:" + bone.name);

        //    newBones.Add(newBone);
        //}

        //// 重新计算绑定姿势
        //Matrix4x4[] bindPoses = new Matrix4x4[newBones.Count];
        //for (int i = 0; i < newBones.Count; i++)
        //{
        //    // 计算相对根骨骼的变换矩阵
        //    bindPoses[i] = newBones[i].worldToLocalMatrix * newRoot.localToWorldMatrix;
        //}
        //severedMesh.bindposes = bindPoses;

        //newSMR.bones = newBones.ToArray();
        //newSMR.rootBone = newBones[0];

        // 修改材质设置方式
        //newSMR.sharedMaterials = _severedMaterials;
        newSMR.sharedMaterials = _bodySMRSharedMaterials;

        //// 添加调试可视化
        //newRoot.gameObject.AddComponent<BoneVisualizer>();
        Transform[] subSkinBones;
        if(subSkinBonesDic.TryGetValue(original, out subSkinBones))
        {
            subSmr.bones = subSkinBonesDic[original];
        }
    }

    private List<Vector3> currVertices = new List<Vector3>(2048);
    // 顶点映射字典（旧索引 -> 新索引）
    //private Dictionary<int, int> vertexMap = new Dictionary<int, int>(2048);
    private int[] vertexMap = new int[2048];
    private List<Vector3> newVertices = new List<Vector3>(512);
    //private List<BoneWeight> newBoneWeights = new List<BoneWeight>(512);
    private List<int> newTriangles = new List<int>(2048);

    // 第一步：收集顶点及相关属性
    private List<Vector2> newUV = new List<Vector2>(512);
    //private List<Vector2> newUV2 = new List<Vector2>(512);
    //private List<Vector2> newUV3 = new List<Vector2>(512);
    //private List<Vector2> newUV4 = new List<Vector2>(512);
    //private List<Color> newColors = new List<Color>(512);
    Mesh ExtractSubMesh(Dictionary<int, int> triOriIndexHash, HashSet<int> targetBones, Transform newRoot, Mesh newMesh)
    {
        bakedMesh.Clear();
        //Mesh bakedMesh = new Mesh();
        _bodySMR.BakeMesh(bakedMesh);
        currVertices.Clear();
        //var currVertices = currentMesh.vertices;
        bakedMesh.GetVertices(currVertices);

        //newMesh.Clear();
        //Mesh newMesh = new Mesh();
        //BoneWeight[] weights = _originalMesh.boneWeights;
        //var weights = _oriMeshBoneWeights;

        //// 获取目标骨骼的索引
        //HashSet<int> targetBoneIndices = new HashSet<int>();
        //for (int i = 0; i < _originalBones.Length; i++)
        //{
        //    if (targetBones.Contains(_originalBones[i]))
        //        targetBoneIndices.Add(i);
        //}

        // 顶点映射字典（旧索引 -> 新索引）
        //vertexMap.Clear();
        if(vertexMap.Length < currVertices.Count)
        {
            vertexMap = new int[currVertices.Count];
        }
        vertexMap.SetAll<int>(-1);
        newVertices.Clear();
        //newBoneWeights.Clear();
        newTriangles.Clear();

        // 第一步：收集顶点及相关属性
        newUV.Clear();
        //newUV2.Clear();
        //newUV3.Clear();
        //newUV4.Clear();
        //newColors.Clear();
        //var oriUV = _originalMesh.uv;
        //var oriUV2 = _originalMesh.uv2;
        //var oriUV3 = _originalMesh.uv3;
        //var oriUV4 = _originalMesh.uv4;
        //var oriColors = _originalMesh.colors;
        //Dictionary<int, int[]> oriTriangles = new Dictionary<int, int[]>(4);

        //// 获取坐标转换矩阵
        //Matrix4x4 originalRootWorldMatrix = _bodySMR.rootBone.localToWorldMatrix;
        //Matrix4x4 newRootWorldMatrix = newRoot.localToWorldMatrix;

        // 第一步：收集所有相关顶点
        //for (int subMesh = 0; subMesh < _originaleSubmeshCount; subMesh++)
        //{
        //    //int[] triangles = _originalMesh.GetTriangles(subMesh);
        //    //oriTriangles[subMesh] = triangles;
        //    var triangles = oriTriangles[subMesh];
        //    var trianglesCount = triangles.Count;
        //    Debug.LogError("========trianglesCount:"+ trianglesCount + " currVertices.Count:" + currVertices.Count);
            var len = currVertices.Count;
            for (int i = 0; i < len; i++)
            {
                //UnityEngine.Profiling.Profiler.BeginSample("====triangles");
                //int originalIndex = triangles[i];//这里边的是有可能重复的
                int originalIndex = i;//这里边的是有可能重复的
                //BoneWeight weight = weights[originalIndex];
                //UnityEngine.Profiling.Profiler.EndSample();

                // 检查顶点是否属于目标骨骼
                //bool isTargetVertex = (weight.weight0 >= boneWeightThreshold && IsBoneInPart(targetBones, weight.boneIndex0)) ||
                //                      (weight.weight1 >= boneWeightThreshold && IsBoneInPart(targetBones, weight.boneIndex1)) ||
                //                      (weight.weight2 >= boneWeightThreshold && IsBoneInPart(targetBones, weight.boneIndex2)) ||
                //                      (weight.weight3 >= boneWeightThreshold && IsBoneInPart(targetBones, weight.boneIndex3));
                bool isTargetVertex = triOriIndexHash.ContainsKey(originalIndex);

                //if (isTargetVertex && !vertexMap.ContainsKey(originalIndex))
                if (isTargetVertex && vertexMap[originalIndex] <= 0)
                {
                    //UnityEngine.Profiling.Profiler.BeginSample("====isTargetVertex");
                    //Debug.LogError(string.Format("====权重{0}|{1}|{2}|{3}", weight.weight0, weight.weight1, weight.weight2, weight.weight3));
                    //Debug.LogError(string.Format("====Bone{0}|{1}|{2}|{3}", _originalBones[weight.boneIndex0].name, _originalBones[weight.boneIndex1].name, _originalBones[weight.boneIndex2].name, _originalBones[weight.boneIndex3].name));
                    //// 坐标转换
                    //Vector3 worldPos = originalRootWorldMatrix.MultiplyPoint3x4(_originalMesh.vertices[originalIndex]);
                    //Vector3 localPos = newRootWorldMatrix.inverse.MultiplyPoint3x4(worldPos);
                    // 获取顶点在世界空间的位置
                    Vector3 worldPos = _bodySMRTr.TransformPoint(currVertices[originalIndex]);
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

                    //BoneWeight newWeight = new BoneWeight();

                    //// 转换每个骨骼索引
                    //newWeight.boneIndex0 = GetMappedBoneIndex(weight.boneIndex0);
                    //newWeight.boneIndex1 = GetMappedBoneIndex(weight.boneIndex1);
                    //newWeight.boneIndex2 = GetMappedBoneIndex(weight.boneIndex2);
                    //newWeight.boneIndex3 = GetMappedBoneIndex(weight.boneIndex3);

                    //newWeight.weight0 = weight.weight0;
                    //newWeight.weight1 = weight.weight1;
                    //newWeight.weight2 = weight.weight2;
                    //newWeight.weight3 = weight.weight3;
                    //newBoneWeights.Add(newWeight);

                    // 收集UV和颜色
                    if (_oriUVsCount > originalIndex)
                        newUV.Add(_oriUVs[originalIndex]);
                    //if (_oriUVs2Count > originalIndex)
                    //    newUV2.Add(_oriUVs2[originalIndex]);
                    //if (_oriUVs3Count > originalIndex)
                    //    newUV3.Add(_oriUVs3[originalIndex]);
                    //if (_oriUVs4Count > originalIndex)
                    //    newUV4.Add(_oriUVs4[originalIndex]);
                    //if (_oriColorsCount > originalIndex)
                    //    newColors.Add(_oriColors[originalIndex]);
                    //UnityEngine.Profiling.Profiler.EndSample();
                }
            }
        //}

        // 第二步：重新构建三角形
        for (int subMesh = 0; subMesh < _originaleSubmeshCount; subMesh++)
        {
            //UnityEngine.Profiling.Profiler.BeginSample("====triangles2");
            var triangles = oriTriangles[subMesh];
            var trianglesCount = triangles.Count;
            for (int i = 0; i < trianglesCount; i += 3)
            {
                // 必须三个顶点都有效
                if (i + 2 >= trianglesCount) continue;

                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                //if (vertexMap.TryGetValue(i0, out i0) &&
                //    vertexMap.TryGetValue(i1, out i1) &&
                //    vertexMap.TryGetValue(i2, out i2))
                //{
                //    newTriangles.Add(i0);
                //    newTriangles.Add(i1);
                //    newTriangles.Add(i2);
                //}
                var bi0 = vertexMap[i0];
                var bi1 = vertexMap[i1];
                var bi2 = vertexMap[i2];
                if (bi0 >= 0 &&
                    bi1 >= 0 &&
                    bi2 >= 0)
                {
                    newTriangles.Add(bi0);
                    newTriangles.Add(bi1);
                    newTriangles.Add(bi2);
                }
            }
            //UnityEngine.Profiling.Profiler.EndSample();
        }

        //// 设置网格数据
        //newMesh.vertices = newVertices.ToArray();
        //newMesh.boneWeights = newBoneWeights.ToArray();
        //newMesh.triangles = newTriangles.ToArray();

        //// 添加材质和UV处理
        //newMesh.uv = newUV.ToArray();
        //if (newUV2.Count == newVertices.Count) newMesh.uv2 = newUV2.ToArray();
        //if (newUV3.Count == newVertices.Count) newMesh.uv3 = newUV3.ToArray();
        //if (newUV4.Count == newVertices.Count) newMesh.uv4 = newUV4.ToArray();
        //if (newColors.Count == newVertices.Count) newMesh.colors = newColors.ToArray();

        newMesh.SetVertices(newVertices);
        //newMesh.boneWeights = newBoneWeights.ToArray();
        newMesh.SetTriangles(newTriangles, 0);
        newMesh.SetUVs(0, newUV);
        //newMesh.SetUVs(1, newUV2);
        //newMesh.SetUVs(2, newUV3);
        //newMesh.SetUVs(3, newUV4);
        //newMesh.SetColors(newColors);

        //// 处理子网格材质
        //List<Material> materials = new List<Material>(4);
        //newMesh.subMeshCount = _originalMesh.subMeshCount;

        //for (int i = 0; i < _originalMesh.subMeshCount; i++)
        //{
        //    List<int> subTriangles = new List<int>(1024);
        //    int[] triangles = oriTriangles[i];

        //    for (int j = 0; j < triangles.Length; j += 3)
        //    {
        //        if (j + 2 >= triangles.Length) continue;

        //        int i0 = triangles[j];
        //        int i1 = triangles[j + 1];
        //        int i2 = triangles[j + 2];

        //        if (vertexMap.TryGetValue(i0, out i0) &&
        //           vertexMap.TryGetValue(i1, out i1) &&
        //           vertexMap.TryGetValue(i2, out i2))
        //        {
        //            subTriangles.Add(i0);
        //            subTriangles.Add(i1);
        //            subTriangles.Add(i2);
        //        }
        //    }

        //    newMesh.SetTriangles(subTriangles, i);
        //    materials.Add(_bodySMR.sharedMaterials[i]);
        //}

        //// 保存材质信息
        //newMesh.name = "SeveredMesh";
        //_severedMaterials = materials.ToArray(); // 新增类字段 private Material[] _severedMaterials;


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

        //Transform originalBone = _originalBones[originalBoneIndex];
        if (partOriginalBoneIndexMap.ContainsKey(originalBoneIndex))
            return partOriginalBoneIndexMap[originalBoneIndex];

        return 0; // 默认指向根骨骼
    }

    // 新增辅助方法
    bool IsBoneInPart(HashSet<int> partBonesList, int boneIndex)
    {
        if (boneIndex < 0 || boneIndex >= _originalBones.Length) return false;
        //return partBonesList.Contains(boneIndex);
        return partBonesList.Contains(boneIndex);
    }
    private List<int> validTriangles = new List<int>(9192);
    void UpdateBodyMesh(Dictionary<int, int> triOriIndexHash, Transform original)
    {
        var partBonesList = _partBonesDic[original];
        // 创建新的身体网格（保持原始骨骼结构）
        //Mesh newBodyMesh = new Mesh();
        newBodyMesh.Clear();

        var boneWeights = _oriMeshBoneWeightArray;// _originalMesh.boneWeights;
        // 复制原始网格数据
        //newBodyMesh.vertices = _originalMesh.vertices;
        newBodyMesh.SetVertices(_oriMeshVertices);
        
        newBodyMesh.boneWeights = boneWeights;
        newBodyMesh.bindposes = oriBindposes;// _originalMesh.bindposes;
        //newBodyMesh.triangles = _originalMesh.triangles;

        validTriangles.Clear();
        var oriVeticesCount = _oriMeshVertices.Count;
        var sunMeshCount = _originalMesh.subMeshCount;
        // 仅过滤掉手臂相关三角形        
        for (int subMesh = 0; subMesh < sunMeshCount; subMesh++)
        {

            var triangles = oriTriangles[subMesh];// _originalMesh.GetTriangles(subMesh);
            var len = triangles.Count;
            //var len = triangles.Length;
            for (int i = 0; i < len; i += 3)
            {
                bool keepTriangle = true;
                for (int j = 0; j < 3; j++)
                {
                    int index = triangles[i + j];
                    //BoneWeight w = boneWeights[index];

                    //bool isPart = (w.weight0 >= boneWeightThreshold && IsBoneInPart(partBonesList, w.boneIndex0)) ||
                    //                    (w.weight1 >= boneWeightThreshold && IsBoneInPart(partBonesList, w.boneIndex1)) ||
                    //                    (w.weight2 >= boneWeightThreshold && IsBoneInPart(partBonesList, w.boneIndex2)) ||
                    //                    (w.weight3 >= boneWeightThreshold && IsBoneInPart(partBonesList, w.boneIndex3));
                    bool isPart = triOriIndexHash.ContainsKey(index);
                    if (isPart)
                    {
                        keepTriangle = false; // 该顶点属于目标骨骼，标记三角形为可剔除
                    }
                    else
                    {
                        keepTriangle = true; // 至少一个顶点属于身体，保留三角形
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

        //newBodyMesh.triangles = validTriangles.ToArray();
        newBodyMesh.SetTriangles(validTriangles, 0);
        //newBodyMesh.uv = _originalMesh.uv;
        newBodyMesh.SetUVs(0, _oriUVs);
        //newBodyMesh.uv2 = _originalMesh.uv2;
        //newBodyMesh.uv3 = _originalMesh.uv3;
        //newBodyMesh.uv4 = _originalMesh.uv4;
        newBodyMesh.RecalculateNormals();

        // 关键：保持原始骨骼配置
        _bodySMR.sharedMesh = newBodyMesh;
        _bodySMR.bones = _originalBones; // 保持原始骨骼数组
        _bodySMR.rootBone = _originalRootBone; // 新增类字段记录原始根骨骼

        // 重置动画
        //GetComponent<Animation>().Play();

        _originalMesh = newBodyMesh;
        GetOriginalInfos();
    }

    void AddPhysicsToSeveredPart(GameObject severedPart)
    {
        //Rigidbody rb = severedPart.AddComponent<Rigidbody>();
        Rigidbody rb = severedPart.GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = 10f;
        rb.drag = 1f;
        rb.AddForce(UnityEngine.Random.onUnitSphere * _severForce, ForceMode.Impulse);

        //MeshCollider collider = severedPart.AddComponent<MeshCollider>();
        MeshCollider collider = severedPart.GetComponent<MeshCollider>();
        collider.enabled = true;
        collider.convex = true;
        collider.sharedMesh = severedPart.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;

        //severedPart.AddComponent<BoneFreezer>();
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

    private void OnDestroy()
    {
        
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

public static class ArrayExtensions
{
    public static void SetAll<T>(this T[] array, T value)
    {
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = value;
        }
    }
}