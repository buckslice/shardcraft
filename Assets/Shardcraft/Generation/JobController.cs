#if UNITY_EDITOR
//#define _DEBUG
#endif
//#define GEN_COLLIDERS

using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.Assertions;

[BurstCompile]
public struct GenerationJob : IJob {

    public Vector3 chunkWorldPos;
    public NativeArray<Block> blocks;
    public NativeArray<Light> lights;

    public void Execute() {

        WorldGenerator.Generate(chunkWorldPos, blocks, lights);

    }
}

public class GenJobInfo {
    public JobHandle handle;
    Chunk chunk;

    public GenJobInfo(Chunk chunk) {
        this.chunk = chunk;

        GenerationJob job = new GenerationJob {
            blocks = chunk.blocks,
            lights = chunk.lights,
            chunkWorldPos = chunk.GetWorldPos(),
        };

        handle = job.Schedule();

        //Assert.IsTrue(!genInfo.handle.IsCompleted);
    }

    public void Finish() {
        chunk.SetLoaded();
        chunk.update = true;
        chunk.needToUpdateSave = true; // save should be updated since this was newly generated
    }
}

[BurstCompile]
public struct StructureJob : IJob {
    public NativeArray3x3<Block> blocks;
    public Vector3i chunkBlockPos;
    public int seed;
    public NativeQueue<int> flagHolder;

    public void Execute() {
        StructureGenerator.BuildStructures(chunkBlockPos, seed, ref blocks);
        flagHolder.Enqueue(blocks.flags);
    }
}

public class StructureJobInfo {
    public JobHandle handle;
    Chunk chunk;
    NativeQueue<int> flagHolder;

    public StructureJobInfo(Chunk chunk) {
        this.chunk = chunk;
        chunk.LockLocalGroupForStructuring();

        flagHolder = Pools.intQN.Get();

        StructureJob job = new StructureJob {
            blocks = chunk.GetLocalBlocks(),
            chunkBlockPos = chunk.bp,
            seed = chunk.world.seed,
            flagHolder = flagHolder,
        };

        handle = job.Schedule();

    }

    public void Finish() {
        chunk.UnlockLocalGroupForStructuring();

        chunk.BlocksWereUpdated();
        StructureGenerator.CheckNeighborNeedUpdate(chunk, flagHolder.Dequeue());
        Pools.intQN.Return(flagHolder);

        chunk.builtStructures = true;
    }

}


[BurstCompile]
public struct MeshJob : IJob {

    [ReadOnly]
    public NativeArray<BlockData> blockData;

    [ReadOnly]
    public NativeArray3x3<Block> blocks;

    public NativeArray3x3<Light> lights;

    public NativeList<Vector3> vertices;
    public NativeList<Vector3> normals;
    public NativeList<Vector3> uvs;
    public NativeList<Vector3> uv2s;
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
            LightCalculator.CalcInitialLightOps(blocks.c, blockData, lightOps);
#if _DEBUG
            initLightTime = watch.ElapsedMilliseconds;
            watch.Restart();
#endif
        }
        LightCalculator.ProcessTorchLightOps(ref lights, ref blocks, blockData, lightOps, lightBFS, lightRBFS);
        Assert.IsTrue(lightBFS.Count == 0 && lightRBFS.Count == 0);
        lightBFS.Enqueue(lights.flags); // kinda stupid way to do this, but so job handle can check which chunks had their lights set

#if _DEBUG
        UnityEngine.Profiling.Profiler.EndSample();
        processLightTime = watch.ElapsedMilliseconds;
        watch.Restart();
        UnityEngine.Profiling.Profiler.BeginSample("Meshing");
#endif

        NativeMeshData meshData = new NativeMeshData(vertices, normals, uvs, uv2s, colors, triangles, faces); // add faces to this
        MeshBuilder.BuildNaive(ref meshData, ref blocks, ref lights, blockData);

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
    NativeList<Vector3> normals;
    NativeList<Vector3> uvs;
    NativeList<Vector3> uv2s;
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

    NativeArray3x3<Light> lights;

    public MeshJobInfo(Chunk chunk) {
        this.chunk = chunk;

        chunk.LockLocalGroupForMeshing();

        MeshJob job;
        job.blockData = JobController.instance.blockData;
        job.blocks = chunk.GetLocalBlocks();
        job.lights = chunk.GetLocalLights();

        vertices = Pools.v3N.Get();
        normals = Pools.v3N.Get();
        uvs = Pools.v3N.Get();
        uv2s = Pools.v3N.Get();
        colors = Pools.c32N.Get();
        triangles = Pools.intN.Get();

        job.vertices = vertices;
        job.normals = normals;
        job.uvs = uvs;
        job.uv2s = uv2s;
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

        lightOps = Pools.loQN.Get();
        lightBFS = Pools.intQN.Get();
        lightRBFS = Pools.lrnQN.Get();

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
        chunk.UnlockLocalGroupForMeshing();

        chunk.UpdateMesh(vertices, normals, uvs, uv2s, colors, triangles);

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

        Pools.loQN.Return(lightOps);
        Pools.intQN.Return(lightBFS);
        Pools.lrnQN.Return(lightRBFS);

        // notify neighbors whom should update based on set light flags
        LightCalculator.CheckNeighborLightUpdate(chunk, lightFlags);

    }
}

[BurstCompile]
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

        chunk.LockLocalGroupForLightUpdate();

        LightUpdateJob job;

        job.lights = chunk.GetLocalLights();
        job.faces = chunk.faces;

        colors = Pools.c32N.Get();
        job.colors = colors;

        handle = job.Schedule();

    }

    public void Finish() {
        chunk.UnlockLocalGroupForLightUpdate();

        chunk.UpdateMeshLight(colors);

        Pools.c32N.Return(colors);
    }

}

// make the jobinfo handling into a class u moron
public class JobController : MonoBehaviour {

    static List<GenJobInfo> genJobInfos = new List<GenJobInfo>();

    static List<MeshJobInfo> meshJobInfos = new List<MeshJobInfo>();

    static List<LightJobInfo> lightJobInfos = new List<LightJobInfo>();

    static List<StructureJobInfo> structureJobInfos = new List<StructureJobInfo>();

    public NativeArray<BlockData> blockData;

    //static List<Task<Chunk>> genTasks = new List<Task<Chunk>>();

    public static JobController instance;

    World world;

    // Start is called before the first frame update
    void Awake() {
        if (instance == null) {
            instance = this;
        } else {
            Assert.IsTrue(false);
        }

        world = FindObjectOfType<World>();

        blockData = BlockDatas.InitBlockData();
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

        for (int i = 0; i < structureJobInfos.Count; ++i) {
            structureJobInfos[i].handle.Complete();
            structureJobInfos[i].Finish();
        }

    }

    public static int genJobScheduled = 0;
    public static int genJobFinished = 0;
    public static int structureJobScheduled = 0;
    public static int structureJobFinished = 0;

    public static int meshJobScheduled = 0;
    public static int meshJobFinished = 0;
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

        for (int i = 0; i < structureJobInfos.Count; ++i) {
            if (structureJobInfos[i].handle.IsCompleted) {
                structureJobInfos[i].handle.Complete();

                structureJobInfos[i].Finish();
                structureJobFinished++;
                structureJobInfos.SwapAndPop(i);
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
        Assert.IsTrue(!shutDown);
        GenJobInfo info = new GenJobInfo(chunk);

        genJobInfos.Add(info);

        genJobScheduled++;


    }

    public static void StartStructureJob(Chunk chunk) {
        Assert.IsTrue(!chunk.builtStructures);
        StructureJobInfo info = new StructureJobInfo(chunk);

        structureJobInfos.Add(info);

        structureJobScheduled++;
    }

    public static void StartMeshJob(Chunk chunk) {
        Assert.IsTrue(!shutDown);
        MeshJobInfo info = new MeshJobInfo(chunk);

        meshJobInfos.Add(info);

        meshJobScheduled++;

    }

    public static void StartLightUpdateJob(Chunk chunk) {
        Assert.IsTrue(!chunk.update);

        LightJobInfo info = new LightJobInfo(chunk);

        lightJobInfos.Add(info);

        lightJobScheduled++;
    }

    public static int GetGenJobCount() {
        return genJobInfos.Count;
    }
    public static bool CanStartLightJob() {
        return lightJobInfos.Count < LoadChunks.maxActiveLightJobs;
    }


}
