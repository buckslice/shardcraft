using UnityEngine;

public struct AABB {
    public float minX; // could make these readonly
    public float minY;
    public float minZ;
    public float maxX;
    public float maxY;
    public float maxZ;

    //public AABB(float minX, float minY, float minZ, float maxX, float maxY, float maxZ) {
    //    this.minX = minX;
    //    this.minY = minY;
    //    this.minZ = minZ;
    //    this.maxX = maxX;
    //    this.maxY = maxY;
    //    this.maxZ = maxZ;
    //}

    public static AABB GetSwept(AABB b, Vector3 vel) {
        AABB swept;

        swept.minX = vel.x > 0 ? b.minX : b.minX + vel.x;
        swept.minY = vel.y > 0 ? b.minY : b.minY + vel.y;
        swept.minZ = vel.z > 0 ? b.minZ : b.minZ + vel.z;

        swept.maxX = vel.x > 0 ? b.maxX + vel.x : b.maxX;
        swept.maxY = vel.y > 0 ? b.maxY + vel.y : b.maxY;
        swept.maxZ = vel.z > 0 ? b.maxZ + vel.z : b.maxZ;

        return swept;
    }

    public override string ToString() {
        return string.Format("({0}, {1}, {2}, {3}, {4}, {5})", minX, minY, minZ, maxX, maxY, maxZ);
    }

    //https://www.gamedev.net/articles/programming/general-and-gameplay-programming/swept-aabb-collision-detection-and-response-r3084/
    // also based off dtb source
    //public static float SweepTest(AABB b1, AABB b2, Vector3 vel, out Vector3 norm) {
    //    float invEntrX;
    //    float invEntrY;
    //    float invEntrZ;
    //    float invExitX;
    //    float invExitY;
    //    float invExitZ;

    //    if (vel.x > 0.0f) {
    //        invEntrX = b2.minX - b1.maxX;
    //        invExitX = b2.maxX - b1.minX;
    //    } else {
    //        invEntrX = b2.maxX - b1.minX;
    //        invExitX = b2.minX - b1.maxX;
    //    }
    //    if (vel.y > 0.0f) {
    //        invEntrY = b2.minY - b1.maxY;
    //        invExitY = b2.maxY - b1.minY;
    //    } else {
    //        invEntrY = b2.maxY - b1.minY;
    //        invExitY = b2.minY - b1.maxY;
    //    }
    //    if (vel.z > 0.0f) {
    //        invEntrZ = b2.minZ - b1.maxZ;
    //        invExitZ = b2.maxZ - b1.minZ;
    //    } else {
    //        invEntrZ = b2.maxZ - b1.minZ;
    //        invExitZ = b2.minZ - b1.maxZ;
    //    }

    //    float entrX;
    //    float entrY;
    //    float entrZ;
    //    float exitX;
    //    float exitY;
    //    float exitZ;

    //    if (vel.x == 0.0f) {
    //        entrX = float.NegativeInfinity;
    //        exitX = float.PositiveInfinity;
    //    } else {
    //        entrX = invEntrX / vel.x;
    //        exitX = invExitX / vel.x;
    //    }
    //    if (vel.y == 0.0f) {
    //        entrY = float.NegativeInfinity;
    //        exitY = float.PositiveInfinity;
    //    } else {
    //        entrY = invEntrY / vel.y;
    //        exitY = invExitY / vel.y;
    //    }
    //    if (vel.z == 0.0f) {
    //        entrZ = float.NegativeInfinity;
    //        exitZ = float.PositiveInfinity;
    //    } else {
    //        entrZ = invEntrZ / vel.z;
    //        exitZ = invExitZ / vel.z;
    //    }

    //    float entrTime = Mathf.Max(entrX, Mathf.Max(entrY, entrZ));
    //    float exitTime = Mathf.Min(exitX, Mathf.Min(exitY, exitZ));

    //    // check if no collision
    //    if (entrTime > exitTime ||
    //        entrX < 0.0f && entrY < 0.0f && entrZ < 0.0f ||
    //        entrX > 1.0f || entrY > 1.0f || entrZ > 1.0f) {
    //        norm = Vector3.zero;
    //        return 1.0f;
    //    }

    //    if (entrX > entrY) {
    //        if (entrX > entrZ) {
    //            norm = new Vector3(invEntrX < 0.0f ? 1.0f : -1.0f, 0.0f, 0.0f);
    //        } else {
    //            norm = new Vector3(0.0f, 0.0f, invEntrZ < 0.0f ? 1.0f : -1.0f);
    //        }
    //    } else {
    //        if (entrY > entrZ) {
    //            norm = new Vector3(0.0f, invEntrY < 0.0f ? 1.0f : -1.0f, 0.0f);
    //        } else {
    //            norm = new Vector3(0.0f, 0.0f, invEntrZ < 0.0f ? 1.0f : -1.0f);
    //        }
    //    }

    //    return entrTime;
    //}

    public static int SweepTest2(AABB dynamicBox, AABB staticBox, Vector3 vel, out float dtime) {
        // find distance between objects on near and far sides
        float invEntrX;
        float invEntrY;
        float invEntrZ;
        float invExitX;
        float invExitY;
        float invExitZ;

        if (vel.x > 0.0f) {
            invEntrX = staticBox.minX - dynamicBox.maxX;
            invExitX = staticBox.maxX - dynamicBox.minX;
        } else {
            invEntrX = staticBox.maxX - dynamicBox.minX;
            invExitX = staticBox.minX - dynamicBox.maxX;
        }
        if (vel.y > 0.0f) {
            invEntrY = staticBox.minY - dynamicBox.maxY;
            invExitY = staticBox.maxY - dynamicBox.minY;
        } else {
            invEntrY = staticBox.maxY - dynamicBox.minY;
            invExitY = staticBox.minY - dynamicBox.maxY;
        }
        if (vel.z > 0.0f) {
            invEntrZ = staticBox.minZ - dynamicBox.maxZ;
            invExitZ = staticBox.maxZ - dynamicBox.minZ;
        } else {
            invEntrZ = staticBox.maxZ - dynamicBox.minZ;
            invExitZ = staticBox.minZ - dynamicBox.maxZ;
        }

        float entrX;
        float entrY;
        float entrZ;
        float exitX;
        float exitY;
        float exitZ;

        if (vel.x == 0.0f) {
            entrX = float.NegativeInfinity;
            exitX = float.PositiveInfinity;
        } else {
            entrX = invEntrX / vel.x;
            exitX = invExitX / vel.x;
        }
        if (vel.y == 0.0f) {
            entrY = float.NegativeInfinity;
            exitY = float.PositiveInfinity;
        } else {
            entrY = invEntrY / vel.y;
            exitY = invExitY / vel.y;
        }
        if (vel.z == 0.0f) {
            entrZ = float.NegativeInfinity;
            exitZ = float.PositiveInfinity;
        } else {
            entrZ = invEntrZ / vel.z;
            exitZ = invExitZ / vel.z;
        }

        float entrTime = Mathf.Max(entrX, Mathf.Max(entrY, entrZ));
        float exitTime = Mathf.Min(exitX, Mathf.Min(exitY, exitZ));

        dtime = entrTime;
        // check if no collision
        if (entrTime > exitTime ||
            entrX < 0.0f && entrY < 0.0f && entrZ < 0.0f ||
            entrX > 1.0f || entrY > 1.0f || entrZ > 1.0f) {
            return -1;
        }

        if (entrX > entrY) {
            if (entrX > entrZ) {
                return 0;
            } else {
                return 2;
            }
        } else {
            if (entrY > entrZ) {
                return 1;
            } else {
                return 2;
            }
        }

    }


    // based on this source from minetest. seems to be good, has a lot less branching then dtb version
    //https://github.com/minetest/minetest/blob/master/src/collision.cpp
    // returns -1 if no collision, 0 if against x axis, 1 on y, 2 on z
    // also returns time of collision based on amount of velocity
    public const float d = 0.01f; // some sort of rounding bs constant
    public static int SweepTest(AABB dynamicBox, AABB staticBox, Vector3 vel, out float time) {
        const float COL_ZERO = 0; // also

        float sizeX = staticBox.maxX - staticBox.minX;
        float sizeY = staticBox.maxY - staticBox.minY;
        float sizeZ = staticBox.maxZ - staticBox.minZ;

        AABB rel;   // relative aabb
        rel.minX = dynamicBox.minX - staticBox.minX;
        rel.minY = dynamicBox.minY - staticBox.minY;
        rel.minZ = dynamicBox.minZ - staticBox.minZ;
        rel.maxX = dynamicBox.maxX - staticBox.minX;
        rel.maxY = dynamicBox.maxY - staticBox.minY;
        rel.maxZ = dynamicBox.maxZ - staticBox.minZ;

        time = 0.0f;

        // checking y first because prob happens most often due to gravity
        if (vel.y > 0) {
            if (rel.maxY <= d) {
                time = -rel.maxY / vel.y;
                if ((rel.minX + vel.x * time < sizeX) &&
                    (rel.maxX + vel.x * time > COL_ZERO) &&
                    (rel.minZ + vel.z * time < sizeZ) &&
                    (rel.maxZ + vel.z * time > COL_ZERO)) {
                    return 1;
                }
            } else if (rel.minY > sizeY) {
                return -1;
            }
        } else if (vel.y < 0) {
            if (rel.maxY >= sizeY - d) {
                time = (sizeY - rel.minY) / vel.y;
                if ((rel.minX + vel.x * time < sizeX) &&
                    (rel.maxX + vel.x * time > COL_ZERO) &&
                    (rel.minZ + vel.z * time < sizeZ) &&
                    (rel.maxZ + vel.z * time > COL_ZERO)) {
                    return 1;
                }
            } else if (rel.maxY < 0) {
                return -1;
            }
        }

        if (vel.x > 0) {
            if (rel.maxX <= d) {
                time = -rel.maxX / vel.x;
                if ((rel.minY + vel.y * time < sizeY) &&
                    (rel.maxY + vel.y * time > COL_ZERO) &&
                    (rel.minZ + vel.z * time < sizeZ) &&
                    (rel.maxZ + vel.z * time > COL_ZERO)) {
                    return 0;
                }
            } else if (rel.minX > sizeX) {
                return -1;
            }
        } else if (vel.x < 0) {
            if (rel.maxX >= sizeX - d) {
                time = (sizeX - rel.minX) / vel.x;
                if ((rel.minY + vel.y * time < sizeY) &&
                    (rel.maxY + vel.y * time > COL_ZERO) &&
                    (rel.minZ + vel.z * time < sizeZ) &&
                    (rel.maxZ + vel.z * time > COL_ZERO)) {
                    return 0;
                }
            } else if (rel.maxX < 0) {
                return -1;
            }
        }

        if (vel.z > 0) {
            if (rel.maxZ <= d) {
                time = -rel.maxZ / vel.z;
                if ((rel.minX + vel.x * time < sizeX) &&
                    (rel.maxX + vel.x * time > COL_ZERO) &&
                    (rel.minY + vel.y * time < sizeY) &&
                    (rel.maxY + vel.y * time > COL_ZERO)) {
                    return 2;
                }
            }// dont need another else because were gona return -1 anyways
        } else if (vel.z < 0) {
            if (rel.maxZ >= sizeZ - d) {
                time = (sizeZ - rel.minZ) / vel.z;
                if ((rel.minX + vel.x * time < sizeX) &&
                    (rel.maxX + vel.x * time > COL_ZERO) &&
                    (rel.minY + vel.y * time < sizeY) &&
                    (rel.maxY + vel.y * time > COL_ZERO)) {
                    return 2;
                }
            }// dont need another else because were gona return -1 anyways
        }

        return -1;
    }

}