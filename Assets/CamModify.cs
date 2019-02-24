using UnityEngine;
using System.Collections;

public class CamModify : MonoBehaviour {

    public float sensitivity = 1.0f;
    public float moveSpeed = 5.0f;

    public float pitch;
    public float yaw;

    void Update() {
        // left click delete
        if (Input.GetMouseButtonDown(0)) {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, 100)) {
                Terrain.SetBlock(hit, Blocks.AIR);
            }
        }

        // right click place
        if (Input.GetMouseButtonDown(1)) {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, 100)) {
                Terrain.SetBlock(hit, Blocks.GRASS, true);
            }
        }

        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch += Input.GetAxis("Mouse Y") * sensitivity;

        pitch = Mathf.Clamp(pitch, -89, 89);

        transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up);
        transform.localRotation *= Quaternion.AngleAxis(pitch, Vector3.left);

        Vector3 forward = transform.forward;
        forward.y = 0.0f;
        forward.Normalize();

        Vector3 move = forward * Input.GetAxisRaw("Vertical") + transform.right * Input.GetAxisRaw("Horizontal");

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftControl)) {
            speed *= 3;
        }

        move = move.normalized * speed;

        float upDir = 0.0f;
        if (Input.GetKey(KeyCode.Space)) {
            upDir += 1.0f;
        }
        if (Input.GetKey(KeyCode.LeftShift)) {
            upDir -= 1.0f;
        }
        move += upDir * Vector3.up * speed;

        transform.position += move * Time.deltaTime;
    }
}