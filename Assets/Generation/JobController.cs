using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;

[BurstCompile]
public struct GenerationJob : IJob {

    public Vector3 chunkPos;
    public NativeArray<Block> blocks;

    public void Execute() {

        WorldGenerator.Generate(chunkPos, blocks);

    }
}

//[BurstCompile]
// need to get rid of all class references for burst...??? frick
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

public struct LightingJob : IJob {
    public void Execute() {
        throw new System.NotImplementedException();
    }
}

public struct SerializationJob : IJob {
    public void Execute() {
        throw new System.NotImplementedException();
    }
}


public class GenJobInfo {
    public JobHandle handle;
    public Chunk chunk;
    public NativeArray<Block> blocks;

    public GenJobInfo(Chunk chunk) {
        this.chunk = chunk;
        blocks = new NativeArray<Block>(Chunk.SIZE * Chunk.SIZE * Chunk.SIZE, Allocator.TempJob);
    }

    public void Finish() {

        blocks.CopyTo(chunk.blocks.GetData());

        chunk.generated = true;

        Serialization.LoadChunk(chunk);

        chunk.update = true;

        blocks.Dispose();
    }
}

public class MeshJobInfo {
    public JobHandle handle;
    public Chunk chunk;

    public NativeArray<Block> blocks; // has 1 block padding on edges

    public NativeList<Vector3> vertices;
    public NativeList<int> triangles;
    public NativeList<Vector2> uvs;

    public NativeList<Vector3> colliderVerts;
    public NativeList<int> colliderTris;

    public MeshJobInfo(Chunk chunk) {
        blocks = MeshBuilder.BuildPaddedBlockArray(chunk);

        vertices = new NativeList<Vector3>(Allocator.TempJob);
        triangles = new NativeList<int>(Allocator.TempJob);
        uvs = new NativeList<Vector2>(Allocator.TempJob);

        colliderVerts = new NativeList<Vector3>(Allocator.TempJob);
        colliderTris = new NativeList<int>(Allocator.TempJob);

        this.chunk = chunk;

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

    public ShadowText text;

    World world;

    // Start is called before the first frame update
    void Start() {
        world = FindObjectOfType<World>();
    }

    public void OnApplicationQuit() {

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
                finished++;

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


        text.SetText(string.Format(
            "Scheduled: {0}\n" +
            "Finished:  {1}\n" +
            "Chunks:    {2}\n" +
            "ToUpdate:  {3}\n" +
            "NeedGen:   {4}\n" +
            "Greedy:    {5}",
            scheduled, finished, world.chunks.Count, LoadChunks.needUpdates, LoadChunks.needGeneration, Chunk.beGreedy));

    }

    static int scheduled = 0;
    static int finished = 0;

    public static void StartGenerationJob(Chunk chunk) {

        GenJobInfo info = new GenJobInfo(chunk);

        GenerationJob job = new GenerationJob();
        job.blocks = info.blocks;
        job.chunkPos = chunk.pos.ToVector3();

        info.handle = job.Schedule();

        //Debug.Assert(!genInfo.handle.IsCompleted);

        genJobInfos.Add(info);


    }

    public static void StartMeshJob(Chunk chunk) {

        MeshJobInfo info = new MeshJobInfo(chunk);

        MeshJob job = new MeshJob();
        job.blocks = info.blocks;
        job.vertices = info.vertices;
        job.triangles = info.triangles;
        job.uvs = info.uvs;

        job.colliderVerts = info.colliderVerts;
        job.colliderTris = info.colliderTris;

        info.handle = job.Schedule();

        meshJobInfos.Add(info);

        scheduled++;

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
