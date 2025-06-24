using UnityEngine;
using System.Collections;

public class IKTest : MonoBehaviour
{
    public GameObject[] IKObject = new GameObject[8];
    public Transform[] IKTransform = new Transform[8];
    public enum IKtarget
    {
        handL,
        handR,
        elbowL,
        elbowR,
        footL,
        footR,
        kneeL,
        kneeR
    }


    void Start()
    {
        IKObject[(int)IKtarget.handL] = GameObject.Find("LeftHandTarget ");
        IKObject[(int)IKtarget.handR] = GameObject.Find("RightHandTarget");

        IKObject[(int)IKtarget.elbowL] = GameObject.Find("LeftHintElbow" );
        IKObject[(int)IKtarget.elbowR] = GameObject.Find("RightHintElbow");

        IKObject[(int)IKtarget.footL] = GameObject.Find("LeftFootTarget" );
        IKObject[(int)IKtarget.footR] = GameObject.Find("RightFootTarget");

        IKObject[(int)IKtarget.kneeL] = GameObject.Find("LeftHintKnee" );
        IKObject[(int)IKtarget.kneeR] = GameObject.Find("RightHintKnee");

        for (int i = 0; i < 8; i++) {
            IKTransform[i] = IKObject[i].transform;
        }


    }


}

