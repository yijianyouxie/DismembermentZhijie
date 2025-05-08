using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class VertexNumberDisplay : MonoBehaviour
{
    [Header("显示设置")]
    public bool showVertexNumbers = true;  // 是否显示顶点编号
    [Range(0.1f, 2f)]
    public float textScale = 1f;  // 文字大小
    public Color textColor = Color.cyan;    // 文字颜色
    public Vector3 textOffset = new Vector3(0.0f, 0.0f, 0);  // 文字偏移

    [Header("过滤设置")]
    public bool enableDistanceFilter = false;  // 启用距离过滤
    [Range(1, 50)]
    public float maxViewDistance = 10f;  // 最大显示距离

    private Mesh mesh;
    private SkinnedMeshRenderer skinnedMeshRenderer;

    void OnDrawGizmos()
    {
        if (!showVertexNumbers) return;

        // 获取网格数据
        GetMeshData();

        if (mesh == null) return;

        // 获取顶点世界坐标
        Vector3[] vertices = mesh.vertices;
        Transform transform = GetTransform();

        // 设置文字样式
        GUIStyle style = new GUIStyle();
        style.normal.textColor = textColor;
        style.fontSize = Mathf.RoundToInt(12 * textScale);
        style.alignment = TextAnchor.LowerLeft;

        // 遍历所有顶点
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(vertices[i]);
            //worldPos.y = worldPos.y - style.fontSize / 2f;

            // 距离过滤
            if (enableDistanceFilter &&
                Vector3.Distance(worldPos, SceneView.currentDrawingSceneView.camera.transform.position) > maxViewDistance)
            {
                continue;
            }

            // 在OnDrawGizmos中调用：
            Camera sceneCam = SceneView.currentDrawingSceneView.camera;
            Vector3 screenOffset = CalculateScreenSpaceOffset(sceneCam, worldPos, style, i.ToString());
            // 绘制顶点编号
            Handles.Label(worldPos + screenOffset + textOffset, i.ToString(), style);
        }
    }

    // 在原有代码基础上增加3D对齐计算
    Vector3 CalculateScreenSpaceOffset(Camera cam, Vector3 worldPos, GUIStyle style, string text)
    {
        // 将世界坐标转换为屏幕坐标
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // 计算文本尺寸
        Vector2 textSize = style.CalcSize(new GUIContent(text));

        // 计算3D空间中的偏移量
        Vector3 offset = cam.ScreenToWorldPoint(
            new Vector3(
                screenPos.x - textSize.x * 0.5f,
                screenPos.y - textSize.y * 0.5f,
                screenPos.z
            )
        ) - worldPos;

        return offset;
    }

    // 获取网格数据
    void GetMeshData()
    {
        // 优先获取SkinnedMeshRenderer
        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer != null)
        {
            mesh = skinnedMeshRenderer.sharedMesh;
            return;
        }

        // 如果不存在则获取普通MeshFilter
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            mesh = meshFilter.sharedMesh;
        }
    }

    // 获取正确的变换组件
    Transform GetTransform()
    {
        return skinnedMeshRenderer != null ?
            skinnedMeshRenderer.transform :
            transform;
    }

    // 在Inspector添加快捷按钮
    [CustomEditor(typeof(VertexNumberDisplay))]
    public class VertexNumberDisplayEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("切换显示状态"))
            {
                VertexNumberDisplay t = (VertexNumberDisplay)target;
                t.showVertexNumbers = !t.showVertexNumbers;
            }
        }
    }
}