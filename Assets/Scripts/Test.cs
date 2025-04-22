using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour {

    private LimbDissection ld;
	// Use this for initialization
	void Start () {
        ld = GetComponent<LimbDissection>();

    }

    private void OnGUI()
    {
        if(GUI.Button(new Rect(50, 50, 100, 50), "创建残肢"))
        {
            ld.CreateStump();
        }

        if (GUI.Button(new Rect(50, 150, 100, 50), "创建被肢解部分"))
        {
            ld.CreateDismemberedPart();
        }
    }
}
