using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 用于测试将skinnedMeshRenderer的bones数组中
/// 移除一部分后的表现是什么样子的
/// </summary>
public class TestSkinBones : MonoBehaviour {

    public SkinnedMeshRenderer smr;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    private void OnGUI()
    {
        if(GUI.Button(new Rect(0,0,100,50), "RemoveBones"))
        {
            Transform[] newBones = new Transform[0];
            smr.bones = newBones;
        }
    }
}
