using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test_transform : MonoBehaviour
{
    public GameObject hand;
    Transform handTransform;
    Vector3 handPos;
    // Start is called before the first frame update
    void Start()
    {
        handTransform = hand.transform;
        handPos = handTransform.localEulerAngles;
    }

    // Update is called once per frame
    void Update()
    {
        //handTransform.localRotation = Quaternion.Euler(45,0,0);
        handTransform.localPosition = new Vector3(0, 0.01f, 0);
    }
}
