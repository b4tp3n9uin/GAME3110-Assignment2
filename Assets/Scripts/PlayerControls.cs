using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControls : MonoBehaviour
{
    public float speed = 5.0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float h_Movement = Input.GetAxisRaw("Horizontal");
        float v_Movement = Input.GetAxisRaw("Vertical");

        transform.Translate(new Vector3(h_Movement * Time.deltaTime * speed, v_Movement * Time.deltaTime * speed, 0.0f));
    }
}
