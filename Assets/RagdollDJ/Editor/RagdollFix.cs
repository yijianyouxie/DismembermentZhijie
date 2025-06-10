using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

public class FixRagdollTool : MonoBehaviour
{
    private static Rigidbody[] _rigidBodys;

    [MenuItem("Tools/ModelMaterialClean")]
    private static void ModelMaterialClean()
    {
        if (Selection.objects.Length == 0)
            return;

        List<Renderer> smr = new List<Renderer>();
        for (int j = 0; j < Selection.objects.Length; j++)
        {
            GameObject obj = Selection.objects[j] as GameObject;
            if (obj == null)
                continue;

            for (int i = 0; i < obj.transform.childCount; i++)
            {
                Renderer r = obj.transform.GetChild(i).GetComponent<Renderer>();
                if (r != null)
                    smr.Add(r);
            }
        }

        for (int i = 0; i < smr.Count; i++)
        {
            Material[] mats = smr[i].sharedMaterials;
            for (int j = 0; j < mats.Length; j++)
            {
                if (mats[j] == null)
                {
                    Debug.Log(smr[i].name);
                    continue;
                }

                if (mats[j].HasProperty("_IceTex"))
                {
                    Texture tex = mats[j].GetTexture("_IceTex");
                    if (tex != null && tex.name == "Ice")
                    {
                        mats[j].SetTexture("_IceTex", null);
                    }
                }
                if (mats[j].HasProperty("_DissolveSrc"))
                {
                    Texture tex = mats[j].GetTexture("_DissolveSrc");
                    if (tex != null && tex.name == "DissolveMap")
                    {
                        mats[j].SetTexture("_DissolveSrc", null);
                    }
                }

                mats[j].DisableKeyword("RIMLIGHT_ON");
                mats[j].DisableKeyword("IceEffect_ON");
                mats[j].DisableKeyword("DissolveEffect_ON");
            }
        }
    }

    [MenuItem("Tools/FixRagdoll")]
    private static void FixRagdoll()
    {
        if (Selection.objects.Length == 0)
            return;

        for (int j = 0; j < Selection.objects.Length; j++)
        {
            GameObject obj = Selection.objects[j] as GameObject;
            
            _rigidBodys = obj.transform.GetComponentsInChildren<Rigidbody>();
            for (int i = 0; i < _rigidBodys.Length; ++i)
            {
                //_rigidBodys[i].detectCollisions = false;
                _rigidBodys[i].isKinematic = true;

                Collider[] bcs = _rigidBodys[i].transform.GetComponents<BoxCollider>();
                if (bcs.Length == 2)
                    DestroyImmediate(bcs[1]);

                Collider[] ccs = _rigidBodys[i].transform.GetComponents<CapsuleCollider>();
                if (ccs.Length == 2)
                    DestroyImmediate(ccs[1]);

                Collider[] scs = _rigidBodys[i].transform.GetComponents<SphereCollider>();
                if (scs.Length == 2)
                    DestroyImmediate(scs[1]);

                Collider collider = _rigidBodys[i].GetComponent<Collider>();
                if (collider != null)
                    collider.enabled = false;

                CharacterJoint characterJoint = _rigidBodys[i].GetComponent<CharacterJoint>();
                if (characterJoint != null)
                {
                    characterJoint.enableProjection = true;
                }
            }
        }

    }

    public static GameObject ClearRagdollComponents(GameObject target)
    {
        GameObject copy = Instantiate(target) as GameObject;
        copy.name = target.name;
        copy.hideFlags = HideFlags.HideInHierarchy;

        if (copy.CompareTag("Ragdoll"))
            return copy;

        CharacterJoint[] characterJoints = copy.transform.GetComponentsInChildren<CharacterJoint>();
        for (int i = characterJoints.Length - 1; i >= 0; i--)
            DestroyImmediate(characterJoints[i]);

        Rigidbody[] rigidBodys = copy.transform.GetComponentsInChildren<Rigidbody>();
        for (int i = rigidBodys.Length - 1; i >= 0; i--)
            DestroyImmediate(rigidBodys[i]);

        /*BoxCollider[] boxColliders = copy.transform.GetComponentsInChildren<BoxCollider>();
        for (int i = boxColliders.Length - 1; i >= 0; i--)
            DestroyImmediate(boxColliders[i]);

        CapsuleCollider[] boxColliders = copy.transform.GetComponentsInChildren<BoxCollider>();
        for (int i = boxColliders.Length - 1; i >= 0; i--)
            DestroyImmediate(boxColliders[i]);

        BoxCollider[] boxColliders = copy.transform.GetComponentsInChildren<BoxCollider>();
        for (int i = boxColliders.Length - 1; i >= 0; i--)
            DestroyImmediate(boxColliders[i]);*/
        Collider[] colliders = copy.transform.GetComponentsInChildren<Collider>();
        for (int i = colliders.Length - 1; i >= 0; i--)
            DestroyImmediate(colliders[i]);

        return copy;
    }
}