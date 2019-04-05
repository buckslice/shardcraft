#if UNITY_EDITOR
#define _DEBUG
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct AABB {
    public readonly float minX;
    public readonly float minY;
    public readonly float minZ;
    public readonly float maxX;
    public readonly float maxY;
    public readonly float maxZ;

    public AABB(float x1, float y1, float z1, float x2, float y2, float z2) {
        minX = x1;
        minY = y1;
        minZ = z1;
        maxX = x2;
        maxY = y2;
        maxZ = z2;
    }

    public bool IsInside(float x, float y, float z) {
        return x > minX && x < maxX &&
               y > minY && y < maxY &&
               z > minY && z < maxZ;
    }

    public bool IsInside(ref Vector3 pos) {
        return pos.x > minX && pos.x < maxX &&
               pos.y > minY && pos.y < maxY &&
               pos.z > minZ && pos.z < maxZ;
    }
}

public struct RaycastVoxelHit {
    public Vector3i bpos; // world space block position
    public Dir dir;
}

public class BlonkPhysics : MonoBehaviour {
    // Start is called before the first frame update
    World world;
    void Start() {
        world = GetComponent<World>();
    }

    void FixedUpdate() {

    }

#if _DEBUG
    static List<Vector3i> posAlong = new List<Vector3i>();
    static Vector3 lastOrigin;
    static Vector3 lastDir;
#endif

    const int SELECT_RADIUS = 100;

    //private static float ceil()

    // based on this source https://github.com/camthesaxman/cubecraft/blob/master/source/field.c#L78    
    // and fixes from this  https://gamedev.stackexchange.com/questions/47362/cast-ray-to-select-block-in-voxel-game
    public static bool RaycastVoxel(World world, Vector3 origin, Vector3 dir, out RaycastVoxelHit hit) {

        if (dir == Vector3.zero) {
            Debug.LogError("RaycastVoxel dir is zero!");
            hit.bpos = Vector3i.zero;
            hit.dir = Dir.none;
            return false;
        }

        Vector3i pos = WorldUtils.GetBlockPos(origin);

#if _DEBUG
        lastOrigin = origin;
        lastDir = dir;
        posAlong.Clear();
        posAlong.Add(pos);
#endif

        // how far we are from origin in blocks
        Vector3i radius = Vector3i.zero;

        // direction to increment when stepping
        Vector3i step = Vector3i.zero;

        // ray distance it takes to equal one block unit in each direction (this one doesnt change in loop)
        Vector3 tDelta = Vector3.positiveInfinity;

        // ray distance it takes to move to next block boundary in each direction (this changes)
        Vector3 tMax = Vector3.positiveInfinity;

        // theres a couple edge cases with this but it works pretty good
        // at exact integer positions and exact PI/2 rotations theres basically a 2x2 of blocks in front of you
        // it will choose one steadily (not flipping between two) but it will depend on the rotation and be arbitrary
        // the commented lines are alternative options that im committing for science

        // edge case where xyz elemnt of origin is at 0
        float ceil(float s) { if (s == 0f) return 1f; else return Mathf.Ceil(s); }

        if (dir.x > 0.0f) {
            step.x = 1;
            tDelta.x = Chunk.BLOCK_SIZE / dir.x;
            //tMax.x = (Mathf.Ceil(origin.x * Chunk.BPU) / Chunk.BPU - origin.x) / dir.x;
            tMax.x = (ceil(origin.x * Chunk.BPU) / Chunk.BPU - origin.x) / dir.x;
        } else if (dir.x < 0.0f) {
            step.x = -1;
            tDelta.x = -Chunk.BLOCK_SIZE / dir.x;
            //tMax.x = -(origin.x - Mathf.Floor(origin.x * Chunk.BPU) / Chunk.BPU) / dir.x;
            //bool atBoundary = Mathf.Round(origin.x * Chunk.BPU) == origin.x * Chunk.BPU;
            bool atBoundary = Mth.Mod(origin.x, Chunk.BPU) == 0.0f;
            tMax.x = atBoundary ? 0 : -(origin.x - Mathf.Floor(origin.x * Chunk.BPU) / Chunk.BPU) / dir.x;
        }

        if (dir.y > 0.0f) {
            step.y = 1;
            tDelta.y = Chunk.BLOCK_SIZE / dir.y;
            //tMax.y = (Mathf.Ceil(origin.y * Chunk.BPU) / Chunk.BPU - origin.y) / dir.y;
            tMax.y = (ceil(origin.y * Chunk.BPU) / Chunk.BPU - origin.y) / dir.y;
        } else if (dir.y < 0.0f) {
            step.y = -1;
            tDelta.y = -Chunk.BLOCK_SIZE / dir.y;
            //tMax.y = -(origin.y - Mathf.Floor(origin.y * Chunk.BPU) / Chunk.BPU) / dir.y;
            //bool atBoundary = Mathf.Round(origin.y * Chunk.BPU) == origin.y * Chunk.BPU;
            bool atBoundary = Mth.Mod(origin.y, Chunk.BPU) == 0.0f;
            tMax.y = atBoundary ? 0 : -(origin.y - Mathf.Floor(origin.y * Chunk.BPU) / Chunk.BPU) / dir.y;
        }

        if (dir.z > 0.0f) {
            step.z = 1;
            tDelta.z = Chunk.BLOCK_SIZE / dir.z;
            //tMax.z = (Mathf.Ceil(origin.z * Chunk.BPU) / Chunk.BPU - origin.z) / dir.z;
            tMax.z = (ceil(origin.z * Chunk.BPU) / Chunk.BPU - origin.z) / dir.z;
        } else if (dir.z < 0.0f) {
            step.z = -1;
            tDelta.z = -Chunk.BLOCK_SIZE / dir.z;
            //tMax.z = -(origin.z - Mathf.Floor(origin.z * Chunk.BPU) / Chunk.BPU) / dir.z;
            //bool atBoundary = Mathf.Round(origin.z * Chunk.BPU) == origin.z * Chunk.BPU;
            bool atBoundary = Mth.Mod(origin.z, Chunk.BPU) == 0.0f;
            tMax.z = atBoundary ? 0 : -(origin.z - Mathf.Floor(origin.z * Chunk.BPU) / Chunk.BPU) / dir.z;
        }

        Debug.Assert(tDelta.x != 0 || tDelta.y != 0 || tDelta.z != 0);

        while (radius.x * radius.x + radius.y * radius.y + radius.z * radius.z < SELECT_RADIUS * SELECT_RADIUS) {
            if (tMax.x < tMax.y) {
                if (tMax.x < tMax.z) {
                    // increment x
                    tMax.x += tDelta.x;
                    pos.x += step.x;
                    radius.x++;
                    hit.dir = step.x > 0 ? Dir.west : Dir.east;
                } else {
                    // increment z
                    tMax.z += tDelta.z;
                    pos.z += step.z;
                    radius.z++;
                    hit.dir = step.z > 0 ? Dir.south : Dir.north;
                }
            } else {
                if (tMax.y < tMax.z) {
                    // increment y
                    tMax.y += tDelta.y;
                    pos.y += step.y;
                    radius.y++;
                    hit.dir = step.y > 0 ? Dir.down : Dir.up;
                } else { // duplicated from above
                    // increment z
                    tMax.z += tDelta.z;
                    pos.z += step.z;
                    radius.z++;
                    hit.dir = step.z > 0 ? Dir.south : Dir.north;
                }
            }

#if _DEBUG
            posAlong.Add(pos);
#endif
            // hit a solid block so return!
            if (world.GetBlock(pos.x, pos.y, pos.z).ColliderSolid()) {
                hit.bpos = pos;
                return true;
            }

        }

        hit.bpos = Vector3i.zero;
        hit.dir = Dir.none;
        return false;

    }

#if _DEBUG
    private void OnDrawGizmos() {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(lastOrigin, lastOrigin + lastDir * (SELECT_RADIUS / 2.0f + 1.0f)); // cus its like manhattan radius

        Gizmos.color = Color.magenta;
        for (int i = 0; i < posAlong.Count; ++i) {
            if (i == 0) {
                Gizmos.color = Color.yellow;
            } else {
                Gizmos.color = new Color(1.0f - (float)i / posAlong.Count, 0, 1.0f);
            }

            Gizmos.DrawWireCube(posAlong[i].ToVector3() / 2.0f + Vector3.one * 0.25f, Vector3.one * 0.5f);
        }
    }
#endif




}
