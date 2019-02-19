using UnityEngine;
using System.Collections;

public class CamModify : MonoBehaviour {

    public float sensitivity = 1.0f;
    public float moveSpeed = 5.0f;

    public float pitch;
    public float yaw;

    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, 100)) {
                Terrain.SetBlock(hit, new BlockAir());
            }
        }

        if (Input.GetMouseButtonDown(1)) {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, 100)) {
                Terrain.SetBlock(hit, new BlockGrass(), true);
            }
        }

        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch += Input.GetAxis("Mouse Y") * sensitivity;

        pitch = Mathf.Clamp(pitch, -90, 90);

        transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up);
        transform.localRotation *= Quaternion.AngleAxis(pitch, Vector3.left);

        Vector3 move = transform.forward * Input.GetAxisRaw("Vertical") + transform.right * Input.GetAxisRaw("Horizontal");
        move = move.normalized * moveSpeed;

        float upDir = 0.0f;
        if (Input.GetKey(KeyCode.Space)) {
            upDir += 1.0f;
        }
        if (Input.GetKey(KeyCode.LeftShift)) {
            upDir -= 1.0f;
        }
        move += upDir * Vector3.up * moveSpeed;

        transform.position += move * Time.deltaTime;
    }
}