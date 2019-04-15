using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyCam : MonoBehaviour {
    public float sensitivity = 1.0f;
    public float moveSpeed = 8.0f;

    public float pitch;
    public float yaw;

    const bool flyMode = false;

    PhysicsMover mover;

    void Start() {
        if (!flyMode) {
            mover = new PhysicsMover();
            mover.transform = transform;

            float s = 0.35f;
            // .7 wide 1.4 tall
            // so 2 blocks wide, 3 tall
            AABB shape;
            shape.minX = -s;
            shape.minZ = -s;
            shape.maxX = s;
            shape.maxZ = s;
            shape.minY = -1.0f;
            shape.maxY = 0.4f;
            mover.shape = shape;

            BlonkPhysics.AddMover(mover);
        }
    }


    void Update() {

        // basically copying minecraft flying mode behaviour cuz its nice

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

        if (flyMode) {
            float upDir = 0.0f;
            if (Input.GetKey(KeyCode.Space)) {
                upDir += 1.0f;
            }
            if (Input.GetKey(KeyCode.LeftShift)) {
                upDir -= 1.0f;
            }
            move += upDir * Vector3.up * speed;

            transform.position += move * Time.deltaTime;
        } else {
            mover.vel.x = move.x;
            mover.vel.z = move.z;
            if (Input.GetKey(KeyCode.Space)) {
                mover.vel.y = 4.0f;
            }
        }
    }
}
