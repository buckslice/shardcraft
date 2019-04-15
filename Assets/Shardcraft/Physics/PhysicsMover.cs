using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// turn this into component later when try ECS
public class PhysicsMover {

    public Transform transform;
    public AABB shape; // shape around transform, centered on xz with y at bottom
    public Vector3 pos = Vector3.zero;
    public Vector3 vel = Vector3.zero;
    public bool obeysGravity = true;
    public bool simulate = true;
    public bool grounded = false;

    public AABB GetWorldAABB() {
        AABB b;

        b.minX = shape.minX + pos.x;
        b.minY = shape.minY + pos.y;
        b.minZ = shape.minZ + pos.z;
        b.maxX = shape.maxX + pos.x;
        b.maxY = shape.maxY + pos.y;
        b.maxZ = shape.maxZ + pos.z;

        return b;

    }

}
