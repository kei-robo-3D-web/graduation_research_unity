using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test_transform : MonoBehaviour
{
    public GameObject[] body = new GameObject[14];
    Transform[] bodyTransform = new Transform[14];
    Vector3 bodyPos;

    // Start is called before the first frame update
    //égÇ§É{Å[ÉìÇÕ11,12,13,14,15,16,23,24,25,26,27,28,31,32ÇÃ14å¬

    public enum landmarks { 
        sholderL,
        sholderR,
        elbowL,
        elbowR,
        handL,
        handR,
        hipL,
        hipR,
        kneeL,
        kneeR,
        ankleL,
        ankleR,
        footL,
        footR
    }

    void Start()
    {
        body[(int)landmarks.sholderL] = GameObject.Find("body.011");
        body[(int)landmarks.sholderR] = GameObject.Find("body.012");

        body[(int)landmarks.  elbowL] = GameObject.Find("body.013");
        body[(int)landmarks.  elbowR] = GameObject.Find("body.014");

        body[(int)landmarks.   handL] = GameObject.Find("body.015");
        body[(int)landmarks.   handR] = GameObject.Find("body.016");

        body[(int)landmarks.    hipL] = GameObject.Find("body.023");
        body[(int)landmarks.    hipR] = GameObject.Find("body.024");

        body[(int)landmarks.   kneeL] = GameObject.Find("body.025");
        body[(int)landmarks.   kneeR] = GameObject.Find("body.026");

        body[(int)landmarks.  ankleL] = GameObject.Find("body.027");
        body[(int)landmarks.  ankleR] = GameObject.Find("body.028");

        body[(int)landmarks.   footL] = GameObject.Find("body.027_end");
        body[(int)landmarks.   footR] = GameObject.Find("body.028_end");


        for (int i = 0; i < 14; i++)
        {
            bodyTransform[i] = body[i].transform;
        }
    }

    // Update is called once per frame
    void Update()
    {
        //handTransform.localRotation = Quaternion.Euler(45,0,0);
        for (int i = 0; i < 14; i++) {
            bodyTransform[i].localPosition = new Vector3(0, 0.01f, 0);
        }
        
    }
}
