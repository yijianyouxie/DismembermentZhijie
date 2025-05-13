using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestForeachDictionary : MonoBehaviour {

    private Dictionary<int, int> dic = new Dictionary<int, int>(4);
	// Use this for initialization
	void Start () {
        dic[1] = 100;
        dic[2] = 200;
        dic[3] = 300;
        dic[4] = 400;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            foreach(var item in dic)
            {
                Debug.LogError("====================");
            }
        }
    }
}
