using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContinueIcon : MonoBehaviour
{

    public float speed = 2f;
    Vector3 startPos;
    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {

        float y = Mathf.Sin(Time.time * speed)* 5f;

        transform.position =startPos +new Vector3(0, y, 0);

    }
}
