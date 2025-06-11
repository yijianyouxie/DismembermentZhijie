using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour {

    public GameObject bossRes;
    private GameObject bossGO;

    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if(null != bossGO)
            {
                Destroy(bossGO);
            }

            if (null != bossRes)
            {
                bossGO = GameObject.Instantiate(bossRes, new Vector3(2.079f, 0, 0.08f), Quaternion.identity);
            }
        }
    }
}
