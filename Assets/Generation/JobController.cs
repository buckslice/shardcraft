#define GEN_COLLIDERS

using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using System.Threading.Tasks;


[BurstCompile]
public struct GenerationJob : IJob {

    public Vector3 chunkWorldPos;
    public NativeArray<Block> blocks;

    public void Execute() {

        WorldGenerator.Generate(chunkWorldPos, blocks);

    }
}

//[BurstCompile] // need to get rid of all class references for burst...??? yeaaa...
public struct MeshJob : IJob {

    // this class is somethin else... somethin else...

    public const int s = Chunk.SIZE;

    [ReadOnly] public NativeArray<Block> blocks; // my blocks
    [ReadOnly] public NativeArray<Block> wB;
    [ReadOnly] public NativeArray<Block> dB;
    [ReadOnly] public NativeArray<Block> sB;
    [ReadOnly] public NativeArray<Block> eB;
    [ReadOnly] public NativeArray<Block> uB;
    [ReadOnly] public NativeArray<Block> nB;
    [ReadOnly] public NativeArray<Block> uwB;
    [ReadOnly] public NativeArray<Block> usB;
    [ReadOnly] public NativeArray<Block> ueB;
    [ReadOnly] public NativeArray<Block> unB;
    [ReadOnly] public NativeArray<Block> swB;
    [ReadOnly] public NativeArray<Block> seB;
    [ReadOnly] public NativeArray<Block> nwB;
    [ReadOnly] public NativeArray<Block> neB;
    [ReadOnly] public NativeArray<Block> dwB;
    [ReadOnly] public NativeArray<Block> dsB;
    [ReadOnly] public NativeArray<Block> deB;
    [ReadOnly] public NativeArray<Block> dnB;
    [ReadOnly] public NativeArray<Block> uswB;
    [ReadOnly] public NativeArray<Block> useB;
    [ReadOnly] public NativeArray<Block> unwB;
    [ReadOnly] public NativeArray<Block> uneB;
    [ReadOnly] public NativeArray<Block> dswB;
    [ReadOnly] public NativeArray<Block> dseB;
    [ReadOnly] public NativeArray<Block> dnwB;
    [ReadOnly] public NativeArray<Block> dneB;

    public NativeArray<byte> light; // my light
    public NativeArray<byte> wL;
    public NativeArray<byte> dL;
    public NativeArray<byte> sL;
    public NativeArray<byte> eL;
    public NativeArray<byte> uL;
    public NativeArray<byte> nL;
    public NativeArray<byte> uwL;
    public NativeArray<byte> usL;
    public NativeArray<byte> ueL;
    public NativeArray<byte> unL;
    public NativeArray<byte> dwL;
    public NativeArray<byte> dsL;
    public NativeArray<byte> deL;
    public NativeArray<byte> dnL;
    public NativeArray<byte> swL;
    public NativeArray<byte> seL;
    public NativeArray<byte> nwL;
    public NativeArray<byte> neL;
    public NativeArray<byte> uswL;
    public NativeArray<byte> useL;
    public NativeArray<byte> unwL;
    public NativeArray<byte> uneL;
    public NativeArray<byte> dswL;
    public NativeArray<byte> dseL;
    public NativeArray<byte> dnwL;
    public NativeArray<byte> dneL;

    public NativeList<Vector3> vertices;
    public NativeList<Vector3> uvs;
    public NativeList<int> triangles;

#if GEN_COLLIDERS
    public NativeList<Vector3> colliderVerts;
    public NativeList<int> colliderTris;
#endif

    public NativeQueue<LightOp> lightOps;     // a list of placement and deletion operations to make within this chunk?
    public NativeQueue<int> lightBFS;
    // also record list of who needs to update after this (if u edit their light)

    public void Execute() {

        NativeMeshData data = new NativeMeshData(this, vertices, uvs, triangles);

        // do lighting update, somehow atomic lock the other chunks native arrays?

        //LightCalculator.ProcessLights(this, lightOps, lightBFS);

        // unlock them 

        MeshBuilder.BuildNaive(data);

#if GEN_COLLIDERS
        MeshBuilder.BuildGreedyCollider(data, colliderVerts, colliderTris);
#endif

    }

    public Block GetBlock(int x, int y, int z) {
        if (y < 0) {
            if (z < 0) {
                if (x < 0) {
                    return dswB[(x + s) + (z + s) * s + (y + s) * s * s];
                } else if (x >= s) {
                    return dseB[(x - s) + (z + s) * s + (y + s) * s * s];
                } else {
                    return dsB[x + (z + s) * s + (y + s) * s * s];
                }
            } else if (z >= s) {
                if (x < 0) {
                    return dnwB[(x + s) + (z - s) * s + (y + s) * s * s];
                } else if (x >= s) {
                    return dneB[(x - s) + (z - s) * s + (y + s) * s * s];
                } else {
                    return dnB[x + (z - s) * s + (y + s) * s * s];
                }
            } else {
                if (x < 0) {
                    return dwB[(x + s) + z * s + (y + s) * s * s];
                } else if (x >= s) {
                    return deB[(x - s) + z * s + (y + s) * s * s];
                } else {
                    return dB[x + z * s + (y + s) * s * s];
                }
            }
        } else if (y >= s) {
            if (z < 0) {
                if (x < 0) {
                    return uswB[(x + s) + (z + s) * s + (y - s) * s * s];
                } else if (x >= s) {
                    return useB[(x - s) + (z + s) * s + (y - s) * s * s];
                } else {
                    return usB[x + (z + s) * s + (y - s) * s * s];
                }
            } else if (z >= s) {
                if (x < 0) {
                    return unwB[(x + s) + (z - s) * s + (y - s) * s * s];
                } else if (x >= s) {
                    return uneB[(x - s) + (z - s) * s + (y - s) * s * s];
                } else {
                    return unB[x + (z - s) * s + (y - s) * s * s];
                }
            } else {
                if (x < 0) {
                    return uwB[(x + s) + z * s + (y - s) * s * s];
                } else if (x >= s) {
                    return ueB[(x - s) + z * s + (y - s) * s * s];
                } else {
                    return uB[x + z * s + (y - s) * s * s];
                }
            }
        } else {
            if (z < 0) {
                if (x < 0) {
                    return swB[(x + s) + (z + s) * s + y * s * s];
                } else if (x >= s) {
                    return seB[(x - s) + (z + s) * s + y * s * s];
                } else {
                    return sB[x + (z + s) * s + y * s * s];
                }
            } else if (z >= s) {
                if (x < 0) {
                    return nwB[(x + s) + (z - s) * s + y * s * s];
                } else if (x >= s) {
                    return neB[(x - s) + (z - s) * s + y * s * s];
                } else {
                    return nB[x + (z - s) * s + y * s * s];
                }
            } else {
                if (x < 0) {
                    return wB[(x + s) + z * s + y * s * s];
                } else if (x >= s) {
                    return eB[(x - s) + z * s + y * s * s];
                } else {
                    return blocks[x + z * s + y * s * s];
                }
            }
        }
    }

    // using local coordinates of this chunk
    public byte GetLight(int x, int y, int z) {
        if (y < 0) {
            if (z < 0) {
                if (x < 0) {
                    return dswL[(x + s) + (z + s) * s + (y + s) * s * s];
                } else if (x >= s) {
                    return dseL[(x - s) + (z + s) * s + (y + s) * s * s];
                } else {
                    return dsL[x + (z + s) * s + (y + s) * s * s];
                }
            } else if (z >= s) {
                if (x < 0) {
                    return dnwL[(x + s) + (z - s) * s + (y + s) * s * s];
                } else if (x >= s) {
                    return dneL[(x - s) + (z - s) * s + (y + s) * s * s];
                } else {
                    return dnL[x + (z - s) * s + (y + s) * s * s];
                }
            } else {
                if (x < 0) {
                    return dwL[(x + s) + z * s + (y + s) * s * s];
                } else if (x >= s) {
                    return deL[(x - s) + z * s + (y + s) * s * s];
                } else {
                    return dL[x + z * s + (y + s) * s * s];
                }
            }
        } else if (y >= s) {
            if (z < 0) {
                if (x < 0) {
                    return uswL[(x + s) + (z + s) * s + (y - s) * s * s];
                } else if (x >= s) {
                    return useL[(x - s) + (z + s) * s + (y - s) * s * s];
                } else {
                    return usL[x + (z + s) * s + (y - s) * s * s];
                }
            } else if (z >= s) {
                if (x < 0) {
                    return unwL[(x + s) + (z - s) * s + (y - s) * s * s];
                } else if (x >= s) {
                    return uneL[(x - s) + (z - s) * s + (y - s) * s * s];
                } else {
                    return unL[x + (z - s) * s + (y - s) * s * s];
                }
            } else {
                if (x < 0) {
                    return uwL[(x + s) + z * s + (y - s) * s * s];
                } else if (x >= s) {
                    return ueL[(x - s) + z * s + (y - s) * s * s];
                } else {
                    return uL[x + z * s + (y - s) * s * s];
                }
            }
        } else {
            if (z < 0) {
                if (x < 0) {
                    return swL[(x + s) + (z + s) * s + y * s * s];
                } else if (x >= s) {
                    return seL[(x - s) + (z + s) * s + y * s * s];
                } else {
                    return sL[x + (z + s) * s + y * s * s];
                }
            } else if (z >= s) {
                if (x < 0) {
                    return nwL[(x + s) + (z - s) * s + y * s * s];
                } else if (x >= s) {
                    return neL[(x - s) + (z - s) * s + y * s * s];
                } else {
                    return nL[x + (z - s) * s + y * s * s];
                }
            } else {
                if (x < 0) {
                    return wL[(x + s) + z * s + y * s * s];
                } else if (x >= s) {
                    return eL[(x - s) + z * s + y * s * s];
                } else {
                    return light[x + z * s + y * s * s];
                }
            }
        }
    }

    // using local coordinates of this chunk
    public void SetLight(int x, int y, int z, byte v) {
        if (y < 0) {
            if (z < 0) {
                if (x < 0) {
                    dswL[(x + s) + (z + s) * s + (y + s) * s * s] = v;
                } else if (x >= s) {
                    dseL[(x - s) + (z + s) * s + (y + s) * s * s] = v;
                } else {
                    dsL[x + (z + s) * s + (y + s) * s * s] = v;
                }
            } else if (z >= s) {
                if (x < 0) {
                    dnwL[(x + s) + (z - s) * s + (y + s) * s * s] = v;
                } else if (x >= s) {
                    dneL[(x - s) + (z - s) * s + (y + s) * s * s] = v;
                } else {
                    dnL[x + (z - s) * s + (y + s) * s * s] = v;
                }
            } else {
                if (x < 0) {
                    dwL[(x + s) + z * s + (y + s) * s * s] = v;
                } else if (x >= s) {
                    deL[(x - s) + z * s + (y + s) * s * s] = v;
                } else {
                    dL[x + z * s + (y + s) * s * s] = v;
                }
            }
        } else if (y >= s) {
            if (z < 0) {
                if (x < 0) {
                    uswL[(x + s) + (z + s) * s + (y - s) * s * s] = v;
                } else if (x >= s) {
                    useL[(x - s) + (z + s) * s + (y - s) * s * s] = v;
                } else {
                    usL[x + (z + s) * s + (y - s) * s * s] = v;
                }
            } else if (z >= s) {
                if (x < 0) {
                    unwL[(x + s) + (z - s) * s + (y - s) * s * s] = v;
                } else if (x >= s) {
                    uneL[(x - s) + (z - s) * s + (y - s) * s * s] = v;
                } else {
                    unL[x + (z - s) * s + (y - s) * s * s] = v;
                }
            } else {
                if (x < 0) {
                    uwL[(x + s) + z * s + (y - s) * s * s] = v;
                } else if (x >= s) {
                    ueL[(x - s) + z * s + (y - s) * s * s] = v;
                } else {
                    uL[x + z * s + (y - s) * s * s] = v;
                }
            }
        } else {
            if (z < 0) {
                if (x < 0) {
                    swL[(x + s) + (z + s) * s + y * s * s] = v;
                } else if (x >= s) {
                    seL[(x - s) + (z + s) * s + y * s * s] = v;
                } else {
                    sL[x + (z + s) * s + y * s * s] = v;
                }
            } else if (z >= s) {
                if (x < 0) {
                    nwL[(x + s) + (z - s) * s + y * s * s] = v;
                } else if (x >= s) {
                    neL[(x - s) + (z - s) * s + y * s * s] = v;
                } else {
                    nL[x + (z - s) * s + y * s * s] = v;
                }
            } else {
                if (x < 0) {
                    wL[(x + s) + z * s + y * s * s] = v;
                } else if (x >= s) {
                    eL[(x - s) + z * s + y * s * s] = v;
                } else {
                    light[x + z * s + y * s * s] = v;
                }
            }
        }
    }

}

//public struct LightingJob : IJob { // might need to be apart of mesh job tho because its using vertex colors...
//    public void Execute() {
//        throw new System.NotImplementedException();
//    }
//}

public class GenJobInfo {
    public JobHandle handle;

    Chunk chunk;
    NativeArray<Block> blocks;

    public GenJobInfo(Chunk chunk) {
        this.chunk = chunk;
        blocks = chunk.blocks;

        GenerationJob job = new GenerationJob {
            blocks = blocks,
            chunkWorldPos = chunk.GetWorldPos(),
        };

        handle = job.Schedule();

        //Debug.Assert(!genInfo.handle.IsCompleted);
    }

    public void Finish() {
        chunk.SetLoaded();
        chunk.update = true;
        chunk.needToUpdateSave = true; // save should be updated since this was newly generated
    }
}

public class MeshJobInfo {
    public JobHandle handle;

    Chunk chunk;

    NativeList<Vector3> vertices;
    NativeList<Vector3> uvs;
    NativeList<int> triangles;

#if GEN_COLLIDERS
    NativeList<Vector3> colliderVerts;
    NativeList<int> colliderTris;
#endif

    NativeQueue<LightOp> lightOps;
    NativeQueue<int> lightBFS;

    public MeshJobInfo(Chunk chunk) {
        this.chunk = chunk;

        MeshJob job = new MeshJob();
        job.blocks = chunk.blocks;
        job.wB = chunk.neighbors[Dirs.WEST].blocks;
        job.dB = chunk.neighbors[Dirs.DOWN].blocks;
        job.sB = chunk.neighbors[Dirs.SOUTH].blocks;
        job.eB = chunk.neighbors[Dirs.EAST].blocks;
        job.uB = chunk.neighbors[Dirs.UP].blocks;
        job.nB = chunk.neighbors[Dirs.NORTH].blocks;
        job.uwB = chunk.neighbors[Dirs.UP].neighbors[Dirs.WEST].blocks;
        job.usB = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].blocks;
        job.ueB = chunk.neighbors[Dirs.UP].neighbors[Dirs.EAST].blocks;
        job.unB = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].blocks;
        job.dwB = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].blocks;
        job.dsB = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].blocks;
        job.deB = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].blocks;
        job.dnB = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].blocks;
        job.swB = chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].blocks;
        job.seB = chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].blocks;
        job.nwB = chunk.neighbors[Dirs.NORTH].neighbors[Dirs.WEST].blocks;
        job.neB = chunk.neighbors[Dirs.NORTH].neighbors[Dirs.EAST].blocks;
        job.uswB = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].blocks;
        job.useB = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].blocks;
        job.unwB = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].blocks;
        job.uneB = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].blocks;
        job.dswB = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].blocks;
        job.dseB = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].blocks;
        job.dnwB = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].blocks;
        job.dneB = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].blocks;

        job.light = chunk.light;
        job.wL = chunk.neighbors[Dirs.WEST].light;
        job.dL = chunk.neighbors[Dirs.DOWN].light;
        job.sL = chunk.neighbors[Dirs.SOUTH].light;
        job.eL = chunk.neighbors[Dirs.EAST].light;
        job.uL = chunk.neighbors[Dirs.UP].light;
        job.nL = chunk.neighbors[Dirs.NORTH].light;
        job.uwL = chunk.neighbors[Dirs.UP].neighbors[Dirs.WEST].light;
        job.usL = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].light;
        job.ueL = chunk.neighbors[Dirs.UP].neighbors[Dirs.EAST].light;
        job.unL = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].light;
        job.dwL = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].light;
        job.dsL = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].light;
        job.deL = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].light;
        job.dnL = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].light;
        job.swL = chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].light;
        job.seL = chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].light;
        job.nwL = chunk.neighbors[Dirs.NORTH].neighbors[Dirs.WEST].light;
        job.neL = chunk.neighbors[Dirs.NORTH].neighbors[Dirs.EAST].light;
        job.uswL = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].light;
        job.useL = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].light;
        job.unwL = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].light;
        job.uneL = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].light;
        job.dswL = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].light;
        job.dseL = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].light;
        job.dnwL = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].light;
        job.dneL = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].light;

        chunk.LockData();
        chunk.neighbors[Dirs.WEST].LockData();
        chunk.neighbors[Dirs.DOWN].LockData();
        chunk.neighbors[Dirs.SOUTH].LockData();
        chunk.neighbors[Dirs.EAST].LockData();
        chunk.neighbors[Dirs.UP].LockData();
        chunk.neighbors[Dirs.NORTH].LockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.WEST].LockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].LockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.EAST].LockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].LockData();
        chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockData();
        chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockData();
        chunk.neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockData();
        chunk.neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockData();

        vertices = Pools.v3Pool.Get();
        uvs = Pools.v3Pool.Get();
        triangles = Pools.intPool.Get();
        vertices.Clear();
        uvs.Clear();
        triangles.Clear();

        job.vertices = vertices;
        job.uvs = uvs;
        job.triangles = triangles;

#if GEN_COLLIDERS
        colliderVerts = Pools.v3Pool.Get();
        colliderTris = Pools.intPool.Get();
        colliderVerts.Clear();
        colliderTris.Clear();

        job.colliderVerts = colliderVerts;
        job.colliderTris = colliderTris;
#endif

        lightOps = Pools.loQPool.Get();
        lightBFS = Pools.intQPool.Get();
        lightOps.Clear();
        lightBFS.Clear();

        job.lightOps = lightOps;
        job.lightBFS = lightBFS; // dont really need a ref to this in my class but whatever

        handle = job.Schedule();

    }

    public void Finish() {
        chunk.UnlockData();
        chunk.neighbors[Dirs.WEST].UnlockData();
        chunk.neighbors[Dirs.DOWN].UnlockData();
        chunk.neighbors[Dirs.SOUTH].UnlockData();
        chunk.neighbors[Dirs.EAST].UnlockData();
        chunk.neighbors[Dirs.UP].UnlockData();
        chunk.neighbors[Dirs.NORTH].UnlockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.WEST].UnlockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].UnlockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.EAST].UnlockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].UnlockData();
        chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockData();
        chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockData();
        chunk.neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockData();
        chunk.neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockData();
        chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockData();

        chunk.UpdateMeshNative(vertices, uvs, triangles);

        Pools.v3Pool.Return(vertices);
        Pools.v3Pool.Return(uvs);
        Pools.intPool.Return(triangles);

#if GEN_COLLIDERS
        chunk.UpdateColliderNative(colliderVerts, colliderTris);
        Pools.v3Pool.Return(colliderVerts);
        Pools.intPool.Return(colliderTris);
#endif

        Pools.loQPool.Return(lightOps);
        Pools.intQPool.Return(lightBFS);
    }
}


public class JobController : MonoBehaviour {

    static List<GenJobInfo> genJobInfos = new List<GenJobInfo>();

    static List<MeshJobInfo> meshJobInfos = new List<MeshJobInfo>();

    //static List<Task<Chunk>> genTasks = new List<Task<Chunk>>();

    World world;

    // Start is called before the first frame update
    void Start() {
        world = FindObjectOfType<World>();
    }

    static bool shutDown = false;
    public static void FinishJobs() {
        shutDown = true;
        for (int i = 0; i < genJobInfos.Count; ++i) {
            genJobInfos[i].handle.Complete();
            genJobInfos[i].Finish();
        }

        for (int i = 0; i < meshJobInfos.Count; ++i) {
            meshJobInfos[i].handle.Complete();
            meshJobInfos[i].Finish();
        }

    }

    // Update is called once per frame
    void Update() {

        for (int i = 0; i < genJobInfos.Count; ++i) {
            if (genJobInfos[i].handle.IsCompleted) {
                genJobInfos[i].handle.Complete();

                genJobInfos[i].Finish();
                genJobFinished++;
                genJobInfos.SwapAndPop(i);
                --i;
            }
        }

        int meshFinishedPer = 0;
        for (int i = 0; i < meshJobInfos.Count; ++i) {
            if (meshJobInfos[i].handle.IsCompleted) {
                meshJobInfos[i].handle.Complete();

                meshJobInfos[i].Finish();
                meshFinishedPer++;
                meshJobFinished++;

                meshJobInfos.SwapAndPop(i);
                --i;

                if (meshFinishedPer >= LoadChunks.meshLoadsPerFrame) {
                    return;
                }

            }
        }

    }

    public static int meshJobScheduled = 0;
    public static int meshJobFinished = 0;
    public static int genJobScheduled = 0;
    public static int genJobFinished = 0;

    public static void StartGenerationJob(Chunk chunk) {
        Debug.Assert(!shutDown);
        GenJobInfo info = new GenJobInfo(chunk);

        genJobInfos.Add(info);

        genJobScheduled++;


    }

    public static void StartMeshJob(Chunk chunk) {
        Debug.Assert(!shutDown);
        MeshJobInfo info = new MeshJobInfo(chunk);

        meshJobInfos.Add(info);

        meshJobScheduled++;

    }

    public static int GetRunningJobs() {
        return genJobInfos.Count;

    }


}
