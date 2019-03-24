using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.IO;

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

    public NativeList<Vector3> vertices;
    public NativeList<int> triangles;
    public NativeList<Vector2> uvs;

    public NativeList<Vector3> colliderVerts;
    public NativeList<int> colliderTris;
    //public NativeList<Vector2> uvs;

    public void Execute() {

        MeshBuilder.BuildNaive(blocks, vertices, triangles, uvs);

        MeshBuilder.BuildGreedyCollider(blocks, colliderVerts, colliderTris);
    }

}

public struct LightingJob : IJob { // might need to be apart of mesh job tho because its using vertex colors...
    public void Execute() {
        throw new System.NotImplementedException();
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
            chunkPos = chunk.wp.ToVector3()
        };

        handle = job.Schedule();

        //Debug.Assert(!genInfo.handle.IsCompleted);
    }

    public void Finish() {

        chunk.loaded = true;
        chunk.update = true;
        chunk.needToUpdateSave = true; // save should be updated since this was newly generated

    }
}

public class MeshJobInfo {
    public JobHandle handle;

    Chunk chunk;

    NativeArray<Block> blocks; // has 1 block padding on edges

    NativeList<Vector3> vertices;
    NativeList<int> triangles;
    NativeList<Vector2> uvs;

    NativeList<Vector3> colliderVerts;
    NativeList<int> colliderTris;

    public MeshJobInfo(Chunk chunk) {
        UnityEngine.Profiling.Profiler.BeginSample("BuildPaddedBlockArray");
        blocks = MeshBuilder.BuildPaddedBlockArray(chunk);
        UnityEngine.Profiling.Profiler.EndSample();

        vertices = new NativeList<Vector3>(Allocator.TempJob);
        triangles = new NativeList<int>(Allocator.TempJob);
        uvs = new NativeList<Vector2>(Allocator.TempJob);

        colliderVerts = new NativeList<Vector3>(Allocator.TempJob);
        colliderTris = new NativeList<int>(Allocator.TempJob);

        this.chunk = chunk;

        MeshJob job = new MeshJob();
        job.blocks = blocks;
        job.vertices = vertices;
        job.triangles = triangles;
        job.uvs = uvs;

        job.colliderVerts = colliderVerts;
        job.colliderTris = colliderTris;

        handle = job.Schedule();

    }

    public void Finish() {
        chunk.UpdateMeshNative(vertices, triangles, uvs);

        chunk.UpdateColliderNative(colliderVerts, colliderTris);

        chunk.waitingForMesh = false;
        chunk.rendered = true;

        blocks.Dispose();
        vertices.Dispose();
        triangles.Dispose();
        uvs.Dispose();

        colliderVerts.Dispose();
        colliderTris.Dispose();
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


    public static void FinishJobs() {
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

                if (meshFinishedPer >= 20) {
                    return;
                }

            }
        }

        //for (int i = 0; i < genTasks.Count; ++i) {
        //    if (genTasks[i].IsCompleted) {
        //        Chunk chunk = genTasks[i].Result;
        //        chunk.generated = true;
        //        // could also set unmodified after loading and then if theres no new modified blocks leave save file alone
        //        chunk.SetBlocksUnmodified();
        //        Serialization.LoadChunk(chunk);

        //        chunk.update = true;

        //        totalGen++;

        //        //genTasks[i].Dispose();
        //        genTasks[i] = genTasks[genTasks.Count - 1];
        //        genTasks.RemoveAt(genTasks.Count - 1);
        //        --i;
        //    }

        //}
    }

    public static int meshJobScheduled = 0;
    public static int meshJobFinished = 0;
    public static int genJobScheduled = 0;
    public static int genJobFinished = 0;

    public static void StartGenerationJob(Chunk chunk) {

        GenJobInfo info = new GenJobInfo(chunk);

        genJobInfos.Add(info);

        genJobScheduled++;


    }

    public static void StartMeshJob(Chunk chunk) {

        MeshJobInfo info = new MeshJobInfo(chunk);

        meshJobInfos.Add(info);

        meshJobScheduled++;

    }

    public static int GetRunningJobs() {
        return genJobInfos.Count;

    }

    //public static void StartGenerationTask(Chunk chunk) {

    //    Task<Chunk> t = Task<Chunk>.Factory.StartNew(() => {

    //        WorldGenerator.Generate(chunk);

    //        return chunk;
    //    }, TaskCreationOptions.PreferFairness);

    //    genTasks.Add(t);
    //}

}
