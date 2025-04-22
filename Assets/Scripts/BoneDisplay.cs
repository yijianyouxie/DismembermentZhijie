using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

[ExecuteInEditMode]
public class BoneDisplay : MonoBehaviour
{
    public bool showHierarchyAlways = true;
    public bool showSelectedBoneName = true;
    public Color boneColor = Color.white;
    public Color selectedBoneColor = Color.red;
    public Color boneNameColor = Color.cyan;
    public float jointSize = 0.0066f;

    SkinnedMeshRenderer[] m_Renderers;
    GUIStyle m_BoneNameStyle;

    void Start()
    {
        m_Renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        if (m_Renderers == null || m_Renderers.Length == 0)
        {
            Debug.LogWarning("No skinned mesh renderer found, script removed");
        }
    }

    void OnDrawGizmos()
    {
        if (showHierarchyAlways == false && UnityEditor.Selection.activeGameObject.GetComponentInParent<BoneDisplay>() != this)
            return;

        if (m_Renderers == null)
            return;

        if (m_BoneNameStyle == null)
        {
            m_BoneNameStyle = new GUIStyle(GUI.skin.GetStyle("label"));
        }

        foreach (var render in m_Renderers)
        {
            var bones = render.bones;
            foreach (var b in bones)
            {
                if (b.parent == null)
                    continue;

                bool selfSelected = (UnityEditor.Selection.activeGameObject != null && b.name == UnityEditor.Selection.activeGameObject.name);
                bool parentSelected = (UnityEditor.Selection.activeGameObject != null && b.parent.name == UnityEditor.Selection.activeGameObject.name);

                if (!showSelectedBoneName || selfSelected || parentSelected)
                {
                    m_BoneNameStyle.normal.textColor = boneNameColor;
                    UnityEditor.Handles.Label(selfSelected ? b.position : b.parent.position, selfSelected ? b.name : b.parent.name, m_BoneNameStyle);
                }

                var color = Gizmos.color;
                if (parentSelected)
                {
                    Gizmos.color = selectedBoneColor;
                    Gizmos.DrawWireSphere(b.parent.position, jointSize);
                    Gizmos.color = boneColor;
                }
                Gizmos.color = selfSelected ? selectedBoneColor : boneColor;
                Gizmos.DrawWireSphere(b.position, jointSize);
                Gizmos.DrawLine(b.position, b.parent.position);
                Gizmos.color = color;
            }
        }
    }
}
#endif