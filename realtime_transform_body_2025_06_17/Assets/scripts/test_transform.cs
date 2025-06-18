using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test_transform : MonoBehaviour
{
    public GameObject[] hand = new GameObject[21];
    Transform[] handTransform = new Transform[21];
    Vector3 handPos;

    // Start is called before the first frame update
    //g‚¤ƒ{[ƒ“‚Í11,12,13,14,15,16,23,24,25,26,27,28,31,32‚Ì14ŒÂ

    void Start()
    {
        for (int i = 0; i < 21; i++)
        {
            //if (i != 4 && i != 8 && i != 12 && i != 16 && i != 20) {
            //    handTransform[i] = hand[i].transform;
            //}

            if (i == 4 || i == 8 || i == 12 || i == 16 || i == 20)
            {

                if (i < 10 && i != 0)
                {
                    hand[i] = GameObject.Find("hand.00" + (i - 1).ToString() + "_end");
                    Debug.Log("hand.00" + (i - 1).ToString() + "_end");
                }
                else
                {
                    hand[i] = GameObject.Find("hand.0" + (i - 1).ToString() + "_end");
                }
            }
            else
            {
                if (i < 10)
                {
                    hand[i] = GameObject.Find("hand.00" + (i).ToString());
                    Debug.Log("hand.00" + (i).ToString());
                }
                else
                {
                    hand[i] = GameObject.Find("hand.0" + (i).ToString());
                }
            }


            handTransform[i] = hand[i].transform;
        }
    }

    // Update is called once per frame
    void Update()
    {
        //handTransform.localRotation = Quaternion.Euler(45,0,0);
        for (int i = 0; i < 21; i++) {
            handTransform[i].localPosition = new Vector3(0, 0.01f, 0);
        }
        
    }
}
