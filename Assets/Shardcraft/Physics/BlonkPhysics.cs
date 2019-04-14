#if UNITY_EDITOR
#define _DEBUG
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public struct RaycastVoxelHit {
    public Vector3i bpos; // world space block position
    public Dir dir;
}

public class BlonkPhysics : MonoBehaviour {

    public Vector3 gravity = Vector3.down * 15.0f;

    World world;
    static List<PhysicsMover> movers = new List<PhysicsMover>();
    List<AABB> boxes = new List<AABB>();

    void Start() {
        world = GetComponent<World>();
    }

    // add object to physics simulation
    public static void AddMover(PhysicsMover mover) {
        movers.Add(mover);
    }


    void FixedUpdate() {

        for (int moverIndex = 0; moverIndex < movers.Count; ++moverIndex) {
            PhysicsMover mover = movers[moverIndex];
            mover.pos = mover.transform.position;
            if (mover.obeysGravity) {
                mover.vel += gravity * Time.deltaTime;
            }

            // loop thru and build list of aabbs with all possible blocks mover could collide with this frame
            // take into account velocity
            AABB swept = AABB.GetSwept(mover.GetWorldAABB(), mover.vel * Time.deltaTime);
            // cast min and max to block positions and add all nearby block AABBs to boxes list
            const float bs = 0.01f;
            Vector3i minBP = WorldUtils.GetBlockPos(new Vector3(swept.minX - bs, swept.minY - bs, swept.minZ - bs));
            Vector3i maxBP = WorldUtils.GetBlockPos(new Vector3(swept.maxX + bs, swept.maxY + bs, swept.maxZ + bs));
            Assert.IsTrue(maxBP.x >= minBP.x && maxBP.y >= minBP.y && maxBP.z >= minBP.z);
            boxes.Clear();
            for (int y = minBP.y; y <= maxBP.y; ++y) {
                for (int z = minBP.z; z <= maxBP.z; ++z) {
                    for (int x = minBP.x; x <= maxBP.x; ++x) {
                        if (world.GetBlock(x, y, z).ColliderSolid()) {
                            AABB b;
                            b.minX = x * Chunk.BLOCK_SIZE;
                            b.minY = y * Chunk.BLOCK_SIZE;
                            b.minZ = z * Chunk.BLOCK_SIZE;
                            b.maxX = (x + 1) * Chunk.BLOCK_SIZE;
                            b.maxY = (y + 1) * Chunk.BLOCK_SIZE;
                            b.maxZ = (z + 1) * Chunk.BLOCK_SIZE;
                            boxes.Add(b);
                        }
                    }
                }
            }
            //Debug.Log(boxes.Count);

            // do a sweeptest against each one and find closest time of collision if any
            // collide against that and zero out velocity on that axis (or bounce)
            // keep going until remainingDelta is very small
            float remainingDelta = Time.deltaTime;
            int loopCount = 0;
            while (remainingDelta > 0.0001f) {
                if (++loopCount > 100) {
                    Debug.LogWarning("physics loop count exceeded!");
                    break;
                }

                float nearestTime = remainingDelta;
                int nearestIndex = -1;
                int nearestAxis = -1;
                for (int i = 0; i < boxes.Count; ++i) {
                    AABB box = boxes[i];

                    int axis = AABB.SweepTest2(box, mover.GetWorldAABB(), mover.vel * remainingDelta, out float t);

                    if (axis == -1 || t >= nearestTime) {
                        continue;
                    }
                    Assert.IsTrue(t <= remainingDelta && t >= 0.0f);
                    nearestTime = t;
                    nearestIndex = i;
                    nearestAxis = axis;
                }

                if (nearestAxis == -1) { // no collision
                    mover.pos += mover.vel * remainingDelta;
                    remainingDelta = 0;
                } else { // collision!

                    // move to the point of collision and reduce time remaining
                    if (nearestTime < 0) { // handle negative nearest caused by d allowance
                        if (nearestAxis == 0) {
                            mover.pos.x += mover.vel.x * nearestTime;
                        } else if (nearestAxis == 1) {
                            mover.pos.y += mover.vel.y * nearestTime;
                        } else if (nearestAxis == 2) {
                            mover.pos.z += mover.vel.z * nearestTime;
                        }
                    } else {
                        mover.pos += mover.vel * nearestTime;
                        remainingDelta -= nearestTime;
                    }

                    // zero out velocity on collided axis
                    if (nearestAxis == 0) {
                        mover.vel.x = 0;
                    } else if (nearestAxis == 1) {
                        mover.vel.y = 0;
                    } else if (nearestAxis == 2) {
                        mover.vel.z = 0;
                    }

                }

            }

            mover.transform.position = mover.pos; // update transform

        }

    }



#if _DEBUG
    static List<Vector3i> posAlong = new List<Vector3i>();
    static Vector3 lastOrigin;
    static Vector3 lastDir;
#endif

    const int SELECT_RADIUS = 100;


    // based on this source https://github.com/camthesaxman/cubecraft/blob/master/source/field.c#L78    
    // and fixes from this  https://gamedev.stackexchange.com/questions/47362/cast-ray-to-select-block-in-voxel-game

    // todo: instead of all parkour fixes just do one mod and if its on block boundary just add 0.00001f to it lol
    public static bool RaycastVoxel(World world, Vector3 origin, Vector3 dir, out RaycastVoxelHit hit) {

        if (dir == Vector3.zero) {
            Debug.LogError("RaycastVoxel dir is zero!");
            hit.bpos = Vector3i.zero;
            hit.dir = Dir.none;
            return false;
        }

        // make sure origin never exactly lines up to block boundary
        // this is kinda filth but seems to work more reliably
        const float ff = 0.00001f;
        if (origin.x % Chunk.BLOCK_SIZE == 0) {
            origin.x += ff;
        }
        if (origin.y % Chunk.BLOCK_SIZE == 0) {
            origin.y += ff;
        }
        if (origin.z % Chunk.BLOCK_SIZE == 0) {
            origin.z += ff;
        }

        Vector3i pos = WorldUtils.GetBlockPos(origin);
        // check if inside a block already
        if (world.GetBlock(pos.x, pos.y, pos.z).ColliderSolid()) {
            hit.bpos = pos;
            hit.dir = Dir.none;
            return true;
        }

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

        if (dir.x > 0.0f) {
            step.x = 1;
            tDelta.x = Chunk.BLOCK_SIZE / dir.x;
            tMax.x = (Mathf.Ceil(origin.x * Chunk.BPU) / Chunk.BPU - origin.x) / dir.x;
            //tMax.x = (ceil(origin.x * Chunk.BPU) / Chunk.BPU - origin.x) / dir.x;
        } else if (dir.x < 0.0f) {
            step.x = -1;
            tDelta.x = -Chunk.BLOCK_SIZE / dir.x;
            tMax.x = -(origin.x - Mathf.Floor(origin.x * Chunk.BPU) / Chunk.BPU) / dir.x;
            //bool atBoundary = Mathf.Round(origin.x * Chunk.BPU) == origin.x * Chunk.BPU;
            //bool atBoundary = Mth.Mod(origin.x, Chunk.BPU) == 0.0f;
            //tMax.x = atBoundary ? 0 : -(origin.x - Mathf.Floor(origin.x * Chunk.BPU) / Chunk.BPU) / dir.x;
        }

        if (dir.y > 0.0f) {
            step.y = 1;
            tDelta.y = Chunk.BLOCK_SIZE / dir.y;
            tMax.y = (Mathf.Ceil(origin.y * Chunk.BPU) / Chunk.BPU - origin.y) / dir.y;
            //tMax.y = (ceil(origin.y * Chunk.BPU) / Chunk.BPU - origin.y) / dir.y;
        } else if (dir.y < 0.0f) {
            step.y = -1;
            tDelta.y = -Chunk.BLOCK_SIZE / dir.y;
            tMax.y = -(origin.y - Mathf.Floor(origin.y * Chunk.BPU) / Chunk.BPU) / dir.y;
            //bool atBoundary = Mathf.Round(origin.y * Chunk.BPU) == origin.y * Chunk.BPU;
            //bool atBoundary = Mth.Mod(origin.y, Chunk.BPU) == 0.0f;
            //tMax.y = atBoundary ? 0 : -(origin.y - Mathf.Floor(origin.y * Chunk.BPU) / Chunk.BPU) / dir.y;
        }

        if (dir.z > 0.0f) {
            step.z = 1;
            tDelta.z = Chunk.BLOCK_SIZE / dir.z;
            tMax.z = (Mathf.Ceil(origin.z * Chunk.BPU) / Chunk.BPU - origin.z) / dir.z;
            //tMax.z = (ceil(origin.z * Chunk.BPU) / Chunk.BPU - origin.z) / dir.z;
        } else if (dir.z < 0.0f) {
            step.z = -1;
            tDelta.z = -Chunk.BLOCK_SIZE / dir.z;
            tMax.z = -(origin.z - Mathf.Floor(origin.z * Chunk.BPU) / Chunk.BPU) / dir.z;
            //bool atBoundary = Mathf.Round(origin.z * Chunk.BPU) == origin.z * Chunk.BPU;
            //bool atBoundary = Mth.Mod(origin.z, Chunk.BPU) == 0.0f;
            //tMax.z = atBoundary ? 0 : -(origin.z - Mathf.Floor(origin.z * Chunk.BPU) / Chunk.BPU) / dir.z;
        }

        Assert.IsTrue(tDelta.x != 0 || tDelta.y != 0 || tDelta.z != 0);

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
            // if hit a solid block then return successfully!
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
