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

    public Vector3 chunkPos;
    public NativeArray<Block> blocks;

    public void Execute() {

        WorldGenerator.Generate(chunkPos, blocks);

    }
}

//[BurstCompile]
// need to get rid of all class references for burst...??? yeaaa...
public struct MeshJob : IJob {

    [ReadOnly]
    public NativeArray<Block> blocks;
    [ReadOnly]
    public NativeArray<Block> west;
    [ReadOnly]
    public NativeArray<Block> down;
    [ReadOnly]
    public NativeArray<Block> south;
    [ReadOnly]
    public NativeArray<Block> east;
    [ReadOnly]
    public NativeArray<Block> up;
    [ReadOnly]
    public NativeArray<Block> north;

    // these last ones are for grass block texturing only at the moment
    // could switch to nativehashmap or whatever maybe...
    // this whole strat might be dumb tho i dunno
    [ReadOnly]
    public NativeArray<Block> downWest;
    [ReadOnly]
    public NativeArray<Block> downSouth;
    [ReadOnly]
    public NativeArray<Block> downEast;
    [ReadOnly]
    public NativeArray<Block> downNorth;


    public NativeList<Vector3> vertices;
    public NativeList<int> triangles;
    public NativeList<Vector2> uvs;

#if GEN_COLLIDERS
    public NativeList<Vector3> colliderVerts;
    public NativeList<int> colliderTris;
#endif
    //public NativeList<Vector2> uvs;

    public const int s = Chunk.SIZE;

    public void Execute() {

        NativeMeshData data = new NativeMeshData(this, vertices, triangles, uvs);

        MeshBuilder.BuildNaive(data);

#if GEN_COLLIDERS
        MeshBuilder.BuildGreedyCollider(data, colliderVerts, colliderTris);
#endif

    }

    // only lets get one block into edge of neighbor for now
    // only will work if accessing from above listed neighbors which is an incomplete set
    public Block GetBlock(int x, int y, int z) {
        if (y < 0) {
            // havent accounted for option where both of these are false, aka corner neighbors
            Debug.Assert(x >= 0 && x < s || z >= 0 && z < s);
            if (x < 0) {
                return downWest[(s - 1) + z * s + (s - 1) * s * s];
            } else if (x >= s) {
                return downEast[0 + z * s + (s - 1) * s * s];
            } else if (z < 0) {
                return downSouth[x + (s - 1) * s + (s - 1) * s * s];
            } else if (z >= s) {
                return downNorth[x + 0 + (s - 1) * s * s];
            }
            return down[x + z * s + (s - 1) * s * s];
        }
        if (x < 0) {
            return west[(s - 1) + z * s + y * s * s];
        } else if (z < 0) {
            return south[x + (s - 1) * s + y * s * s];
        } else if (x >= s) {
            return east[0 + z * s + y * s * s];
        } else if (y >= s) {
            return up[x + z * s + 0];
        } else if (z >= s) {
            return north[x + 0 + y * s * s];
        } else {
            return blocks[x + z * s + y * s * s];
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
            chunkPos = chunk.wp.ToVector3()
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
    NativeList<int> triangles;
    NativeList<Vector2> uvs;

#if GEN_COLLIDERS
    NativeList<Vector3> colliderVerts;
    NativeList<int> colliderTris;
#endif

    public MeshJobInfo(Chunk chunk) {

        vertices = Pools.v3Pool.Get();
        triangles = Pools.intPool.Get();
        uvs = Pools.v2Pool.Get();

        vertices.Clear();
        triangles.Clear();
        uvs.Clear();

        this.chunk = chunk;

        MeshJob job = new MeshJob();
        job.blocks = chunk.blocks;
        job.west = chunk.neighbors[Dirs.WEST].blocks;
        job.down = chunk.neighbors[Dirs.DOWN].blocks;
        job.south = chunk.neighbors[Dirs.SOUTH].blocks;
        job.east = chunk.neighbors[Dirs.EAST].blocks;
        job.up = chunk.neighbors[Dirs.UP].blocks;
        job.north = chunk.neighbors[Dirs.NORTH].blocks;

        job.downWest = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].blocks;
        job.downEast = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].blocks;
        job.downSouth = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].blocks;
        job.downNorth = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].blocks;

        chunk.LockData();
        for (int i = 0; i < 6; ++i) {
            chunk.neighbors[i].LockData();
        }
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].LockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].LockData();


        job.vertices = vertices;
        job.triangles = triangles;
        job.uvs = uvs;

#if GEN_COLLIDERS
        colliderVerts = Pools.v3Pool.Get();
        colliderTris = Pools.intPool.Get();
        colliderVerts.Clear();
        colliderTris.Clear();
        job.colliderVerts = colliderVerts;
        job.colliderTris = colliderTris;
#endif

        handle = job.Schedule();

    }

    public void Finish() {
        chunk.UnlockData();
        for (int i = 0; i < 6; ++i) {
            chunk.neighbors[i].UnlockData();
        }
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].UnlockData();
        chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].UnlockData();

        chunk.UpdateMeshNative(vertices, triangles, uvs);

        Pools.v3Pool.Return(vertices);
        Pools.intPool.Return(triangles);
        Pools.v2Pool.Return(uvs);

#if GEN_COLLIDERS
        chunk.UpdateColliderNative(colliderVerts, colliderTris);
        Pools.v3Pool.Return(colliderVerts);
        Pools.intPool.Return(colliderTris);
#endif
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
