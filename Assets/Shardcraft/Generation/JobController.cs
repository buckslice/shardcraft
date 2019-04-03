#define GEN_COLLIDERS
//#define _DEBUG

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

    public NativeArray3x3<Light> lights;

    public NativeList<Vector3> vertices;
    public NativeList<Vector3> uvs;
    public NativeList<Color32> colors;
    public NativeList<int> triangles;

#if GEN_COLLIDERS
    public bool genCollider;
    public NativeList<Vector3> colliderVerts;
    public NativeList<int> colliderTris;
#endif

    public NativeList<Face> faces; // save face data for easier light updates

    public bool calcInitialLight;
    public NativeQueue<LightOp> lightOps;     // a list of placement and deletion operations to make within this chunk?
    public NativeQueue<int> lightBFS;
    public NativeQueue<LightRemovalNode> lightRBFS;
    // also record list of who needs to update after this (if u edit their light)

    public void Execute() {
        // lighting is only reason we need to lock all other chunks rather than just ourselves... but i dunno
        // its pretty convenient to have it in the same job because were rebuilding the mesh anyways
        // also since were passing in all these references if we split these 3 up into their own jobs
        // would have to do that 3 times instead #puke

#if _DEBUG
        long initLightTime = 0;
        long processLightTime = 0;
        long meshingTime = 0;
        long colliderTime = 0;
        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        watch.Restart();
        UnityEngine.Profiling.Profiler.BeginSample("Lighting");
#endif

        // if chunk hasnt been rendered before then check each block to see if it has any lights
        if (calcInitialLight) {
            LightCalculator.CalcInitialLightOps(blocks.c, lightOps);
#if _DEBUG
            initLightTime = watch.ElapsedMilliseconds;
            watch.Restart();
#endif
        }
        int lightFlags = LightCalculator.ProcessLightOps(ref lights, ref blocks, lightOps, lightBFS, lightRBFS);
        Debug.Assert(lightBFS.Count == 0 && lightRBFS.Count == 0);
        lightBFS.Enqueue(lightFlags); // kinda stupid way to do this, but so job handle can check which chunks had their lights set

#if _DEBUG
        UnityEngine.Profiling.Profiler.EndSample();
        processLightTime = watch.ElapsedMilliseconds;
        watch.Restart();
        UnityEngine.Profiling.Profiler.BeginSample("Meshing");
#endif

        NativeMeshData data = new NativeMeshData(vertices, uvs, colors, triangles);
        MeshBuilder.BuildNaive(data, ref blocks, ref lights, faces);

#if _DEBUG
        meshingTime = watch.ElapsedMilliseconds;
        watch.Restart();
        UnityEngine.Profiling.Profiler.EndSample();
#endif

#if GEN_COLLIDERS
#if _DEBUG
        UnityEngine.Profiling.Profiler.BeginSample("Collider");
#endif
        if (genCollider) {
            MeshBuilder.BuildGreedyCollider(ref blocks, colliderVerts, colliderTris);
        }
#if _DEBUG
        colliderTime = watch.ElapsedMilliseconds;
        UnityEngine.Profiling.Profiler.EndSample();
#endif
#endif

#if _DEBUG
        lightBFS.Enqueue((int)initLightTime);
        lightBFS.Enqueue((int)processLightTime);
        lightBFS.Enqueue((int)meshingTime);
        lightBFS.Enqueue((int)colliderTime);
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

    NativeList<Face> faces;

    NativeQueue<LightOp> lightOps;
    NativeQueue<int> lightBFS;
    NativeQueue<LightRemovalNode> lightRBFS;

    public MeshJobInfo(Chunk chunk) {
        this.chunk = chunk;

        chunk.LockLocalGroup();

        MeshJob job;
        job.blocks = chunk.GetLocalBlocks();
        job.lights = chunk.GetLocalLights();

        vertices = Pools.v3Pool.Get();
        uvs = Pools.v3Pool.Get();
        colors = Pools.c32Pool.Get();
        triangles = Pools.intPool.Get();

        job.vertices = vertices;
        job.uvs = uvs;
        job.colors = colors;
        job.triangles = triangles;

#if GEN_COLLIDERS
        colliderVerts = Pools.v3Pool.Get();
        colliderTris = Pools.intPool.Get();

        job.colliderVerts = colliderVerts;
        job.colliderTris = colliderTris;

        // only generate a new collider if there has been a block change (on lighting change dont need to remake collider)
        // this is one good reason to split the mesh job into multiple jobs maybe...
        job.genCollider = chunk.needNewCollider;
#endif

        chunk.faces.Clear();
        job.faces = chunk.faces;

        lightOps = Pools.loQPool.Get();
        lightBFS = Pools.intQPool.Get();
        lightRBFS = Pools.lrnQPool.Get();

        while (chunk.lightOps.Count > 0) {
            lightOps.Enqueue(chunk.lightOps.Dequeue());
        }

        job.lightOps = lightOps;
        job.lightBFS = lightBFS;
        job.lightRBFS = lightRBFS;

        // if chunk hasnt been rendered then it needs to check for existing lights
        job.calcInitialLight = !chunk.rendered;

        handle = job.Schedule();

    }

#if _DEBUG
    public static int totalsIters = 1;
    public static int initLightTime = 0;
    public static int processLightTime = 0;
    public static int meshingTime = 0;
    public static int colliderTime = 0;
    public static bool tracking = true;
#endif

    public void Finish() {
        chunk.UnlockLocalGroup();

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

#if _DEBUG
        if (lightBFS.Count > 0) {
            if (tracking) {
                totalsIters++;
                initLightTime += lightBFS.Dequeue();
                processLightTime += lightBFS.Dequeue();
                meshingTime += lightBFS.Dequeue();
                colliderTime += lightBFS.Dequeue();

                string output = string.Format("initLight:{0:0.0}, lighting:{1:0.0}, meshing:{2:0.0}, collider:{3:0.0}",
                    initLightTime / (float)totalsIters,
                    processLightTime / (float)totalsIters,
                    meshingTime / (float)totalsIters,
                    colliderTime / (float)totalsIters);

                Debug.Log(output);
            }
        }
#endif


        Pools.loQPool.Return(lightOps);
        Pools.intQPool.Return(lightBFS);
        Pools.lrnQPool.Return(lightRBFS);


        // notify neighbors whom should update based on set light flags
        LightCalculator.CheckNeighborLightUpdate(chunk, lightFlags);

    }
}

public struct LightUpdateJob : IJob {

    // self and 6 neighbor lights, actually will prob need full local group eventually
    // once do smooth lighting i think
    //[ReadOnly] public NativeArrayC6<byte> lights;

    [ReadOnly] public NativeArray3x3<Light> lights;
    [ReadOnly] public NativeList<Face> faces;

    public NativeList<Color32> colors;

    public void Execute() {

        LightCalculator.LightUpdate(ref lights, faces, colors);

    }
}

public class LightJobInfo {

    public JobHandle handle;
    Chunk chunk;

    NativeList<Color32> colors;

    public LightJobInfo(Chunk chunk) {
        this.chunk = chunk;

        chunk.LockLocalGroup();

        LightUpdateJob job;

        job.lights = chunk.GetLocalLights();
        job.faces = chunk.faces;

        colors = Pools.c32Pool.Get();
        job.colors = colors;

        handle = job.Schedule();

    }

    public void Finish() {
        chunk.UnlockLocalGroup();

        chunk.UpdateMeshLight(colors);

        Pools.c32Pool.Return(colors);
    }

}

// make the jobinfo handling into a class u moron
public class JobController : MonoBehaviour {

    static List<GenJobInfo> genJobInfos = new List<GenJobInfo>();

    static List<MeshJobInfo> meshJobInfos = new List<MeshJobInfo>();

    static List<LightJobInfo> lightJobInfos = new List<LightJobInfo>();

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

        for (int i = 0; i < lightJobInfos.Count; ++i) {
            lightJobInfos[i].handle.Complete();
            lightJobInfos[i].Finish();
        }
    }

    public static int meshJobScheduled = 0;
    public static int meshJobFinished = 0;
    public static int genJobScheduled = 0;
    public static int genJobFinished = 0;
    public static int lightJobScheduled = 0;
    public static int lightJobFinished = 0;

    // Update is called once per frame
    void Update() {

#if _DEBUG
        if (Input.GetKeyDown(KeyCode.P)) {
            MeshJobInfo.tracking = !MeshJobInfo.tracking;
            MeshJobInfo.totalsIters = 1;
            MeshJobInfo.initLightTime = 0;
            MeshJobInfo.processLightTime = 0;
            MeshJobInfo.meshingTime = 0;
            MeshJobInfo.colliderTime = 0;
        }
#endif

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
                    break;
                }

            }
        }

        for (int i = 0; i < lightJobInfos.Count; ++i) {
            if (lightJobInfos[i].handle.IsCompleted) {
                lightJobInfos[i].handle.Complete();

                lightJobInfos[i].Finish();
                lightJobFinished++;
                lightJobInfos.SwapAndPop(i);
                --i;
            }
        }

    }

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

    public static void StartLightUpdateJob(Chunk chunk) {
        Debug.Assert(!chunk.update);

        LightJobInfo info = new LightJobInfo(chunk);

        lightJobInfos.Add(info);

        lightJobScheduled++;
    }

    public static int GetRunningJobs() {
        return genJobInfos.Count;

    }


}
