using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// turn this into component later when try ECS
public class PhysicsMover {

    public Transform transform;
    public Vector3 pos;
    public AABB shape; // shape around transform, centered on xz with y at bottom
    public Vector3 vel;
    public bool obeysGravity = true;

    public AABB GetWorldAABB() {
        AABB b;

        b.minX = shape.minX + pos.x;
        b.maxX = shape.maxX + pos.x;
        b.minY = shape.minY + pos.y;
        b.maxY = shape.maxY + pos.y;
        b.minZ = shape.minZ + pos.z;
        b.maxZ = shape.maxZ + pos.z;

        return b;

    }

}
