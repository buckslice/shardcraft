using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SunController : MonoBehaviour {

    public Transform sunAnchor;
    public float rotationSpeed = 1.0f; // in degrees per second

    //public AnimationCurve intensityCurve;

    UnityEngine.Light sun;

    // Start is called before the first frame update
    void Awake() {
        sun = GetComponent<UnityEngine.Light>();
    }

    // Update is called once per frame
    void LateUpdate() {
        transform.Rotate(new Vector3(rotationSpeed, 0, 0) * Time.deltaTime);
        sunAnchor.rotation = transform.rotation;

        //Debug.Log(sunAnchor.rotation.eulerAngles.x);
        
    }
}
