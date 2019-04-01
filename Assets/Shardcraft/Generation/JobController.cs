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

// this whole file is somethin else... somethin else...

//[BurstCompile] // need to get rid of all class references for burst...??? yeaaa...
public struct MeshJob : IJob {

    [ReadOnly]
    public NativeArray3x3<Block> blocks;

    public NativeArray3x3<byte> light;

    public NativeList<Vector3> vertices;
    public NativeList<Vector3> uvs;
    public NativeList<Color32> colors;
    public NativeList<int> triangles;

#if GEN_COLLIDERS
    public bool genCollider;
    public NativeList<Vector3> colliderVerts;
    public NativeList<int> colliderTris;
#endif

    public NativeQueue<LightOp> lightOps;     // a list of placement and deletion operations to make within this chunk?
    public NativeQueue<int> lightBFS;
    // also record list of who needs to update after this (if u edit their light)

    int lightFlags;
    public void Execute() {
        // lighting is only reason we need to lock all other chunks rather than just ourselves... but i dunno
        // its pretty convenient to have it in the same job because were rebuilding the mesh anyways
        // also since were passing in all these references if we split these 3 up into their own jobs
        // would have to do that 3 times instead #puke
        int lightFlags = LightCalculator.ProcessLightOps(ref light, ref blocks, lightOps, lightBFS);
        Debug.Assert(lightBFS.Count == 0);
        lightBFS.Enqueue(lightFlags); // kinda stupid way to do this, but so job handle can check which chunks had their lights set

        NativeMeshData data = new NativeMeshData(vertices, uvs, colors, triangles);
        MeshBuilder.BuildNaive(data, ref blocks, ref light);

#if GEN_COLLIDERS
        if (genCollider) {
            MeshBuilder.BuildGreedyCollider(ref blocks, colliderVerts, colliderTris);
        }
#endif

    }

}

public class MeshJobInfo {
    public JobHandle handle;

    Chunk chunk;

    NativeList<Vector3> vertices;
    NativeList<Vector3> uvs;
    NativeList<Color32> colors;
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
        job.blocks = new NativeArray3x3<Block> {
            c = chunk.blocks,
            w = chunk.neighbors[Dirs.WEST].blocks,
            d = chunk.neighbors[Dirs.DOWN].blocks,
            s = chunk.neighbors[Dirs.SOUTH].blocks,
            e = chunk.neighbors[Dirs.EAST].blocks,
            u = chunk.neighbors[Dirs.UP].blocks,
            n = chunk.neighbors[Dirs.NORTH].blocks,
            uw = chunk.neighbors[Dirs.UP].neighbors[Dirs.WEST].blocks,
            us = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].blocks,
            ue = chunk.neighbors[Dirs.UP].neighbors[Dirs.EAST].blocks,
            un = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].blocks,
            dw = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].blocks,
            ds = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].blocks,
            de = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].blocks,
            dn = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].blocks,
            sw = chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].blocks,
            se = chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].blocks,
            nw = chunk.neighbors[Dirs.NORTH].neighbors[Dirs.WEST].blocks,
            ne = chunk.neighbors[Dirs.NORTH].neighbors[Dirs.EAST].blocks,
            usw = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].blocks,
            use = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].blocks,
            unw = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].blocks,
            une = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].blocks,
            dsw = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].blocks,
            dse = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].blocks,
            dnw = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].blocks,
            dne = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].blocks,
        };

        job.light = new NativeArray3x3<byte> {
            c = chunk.light,
            w = chunk.neighbors[Dirs.WEST].light,
            d = chunk.neighbors[Dirs.DOWN].light,
            s = chunk.neighbors[Dirs.SOUTH].light,
            e = chunk.neighbors[Dirs.EAST].light,
            u = chunk.neighbors[Dirs.UP].light,
            n = chunk.neighbors[Dirs.NORTH].light,
            uw = chunk.neighbors[Dirs.UP].neighbors[Dirs.WEST].light,
            us = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].light,
            ue = chunk.neighbors[Dirs.UP].neighbors[Dirs.EAST].light,
            un = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].light,
            dw = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].light,
            ds = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].light,
            de = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].light,
            dn = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].light,
            sw = chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].light,
            se = chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].light,
            nw = chunk.neighbors[Dirs.NORTH].neighbors[Dirs.WEST].light,
            ne = chunk.neighbors[Dirs.NORTH].neighbors[Dirs.EAST].light,
            usw = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].light,
            use = chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].light,
            unw = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].light,
            une = chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].light,
            dsw = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].light,
            dse = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].light,
            dnw = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].light,
            dne = chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].light,
        };

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
        colors = Pools.c32Pool.Get();
        triangles = Pools.intPool.Get();
        vertices.Clear();
        uvs.Clear();
        colors.Clear();
        triangles.Clear();

        job.vertices = vertices;
        job.uvs = uvs;
        job.colors = colors;
        job.triangles = triangles;

#if GEN_COLLIDERS
        colliderVerts = Pools.v3Pool.Get();
        colliderTris = Pools.intPool.Get();
        colliderVerts.Clear();
        colliderTris.Clear();

        job.colliderVerts = colliderVerts;
        job.colliderTris = colliderTris;

        // only generate a new collider if there has been a block change (on lighting change dont need to remake collider)
        // this is one good reason to split the mesh job into multiple jobs maybe...
        job.genCollider = chunk.needNewCollider;
#endif

        lightOps = Pools.loQPool.Get();
        lightBFS = Pools.intQPool.Get();
        lightOps.Clear();
        lightBFS.Clear();

        while (chunk.lightOps.Count > 0) {
            lightOps.Enqueue(chunk.lightOps.Dequeue());
        }

        job.lightOps = lightOps;
        job.lightBFS = lightBFS;

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

        chunk.UpdateMeshNative(vertices, uvs, colors, triangles);

        Pools.v3Pool.Return(vertices);
        Pools.v3Pool.Return(uvs);
        Pools.c32Pool.Return(colors);
        Pools.intPool.Return(triangles);

#if GEN_COLLIDERS
        if (chunk.needNewCollider) {
            chunk.UpdateColliderNative(colliderVerts, colliderTris);
        }
        Pools.v3Pool.Return(colliderVerts);
        Pools.intPool.Return(colliderTris);
#endif

        int lightFlags = lightBFS.Dequeue();

        Pools.loQPool.Return(lightOps);
        Pools.intQPool.Return(lightBFS);

        //Debug.Log(lightFlags);

        // notify neighbors whom should update based on set light flags
        LightCalculator.CheckNeighborLightUpdate(chunk, lightFlags);

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
