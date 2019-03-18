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

public struct LightingJob : IJob { // might need to be apart of mesh job tho because its using vertex colors...
    public void Execute() {
        throw new System.NotImplementedException();
    }
}

public struct SaveJob : IJob {

    [ReadOnly]
    public NativeArray<Block> blocks;
    [ReadOnly]
    public NativeArray<char> saveChars; // strings dont work in job system yay

    public void Execute() {
        // do runlength encoding later...
        // also might be better for this to be on just a single separate thread once region system is added

        string saveFile = new string(saveChars.ToArray());
        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream(saveFile, FileMode.Create, FileAccess.Write, FileShare.None);
        formatter.Serialize(stream, blocks);
        stream.Close();
    }
}

public struct LoadJob : IJob {

    public NativeArray<Block> blocks;
    [ReadOnly]
    public NativeArray<char> saveChars; // strings dont work in job system yay

    public void Execute() {
        // do runlength encoding later...
        // also might be better for this to be on just a single separate thread once region system is added

        string saveFile = new string(saveChars.ToArray());

        Debug.Assert(File.Exists(saveFile));

        IFormatter formatter = new BinaryFormatter();
        FileStream stream = new FileStream(saveFile, FileMode.Open);

        blocks = (NativeArray<Block>)formatter.Deserialize(stream);

        stream.Close();
    }
}

public class SaveJobInfo {
    public JobHandle handle;
    public NativeArray<Block> blocks;
    public NativeArray<char> saveChars;

    public SaveJobInfo(Chunk chunk) {
        blocks = new NativeArray<Block>(Chunk.SIZE * Chunk.SIZE * Chunk.SIZE, Allocator.TempJob);
        blocks.CopyFrom(chunk.blocks.data);

        string saveFile = Serialization.SaveFileName(chunk);

        saveChars = new NativeArray<char>(saveFile.ToCharArray(), Allocator.TempJob);

        SaveJob saveJob = new SaveJob {
            blocks = blocks,
            saveChars = saveChars,
        };

        handle = saveJob.Schedule();
    }

    public void Finish() {
        blocks.Dispose();
        saveChars.Dispose();
        return;
    }

}

public class LoadJobInfo {
    public JobHandle handle;
    public Chunk chunk;
    public NativeArray<Block> blocks;
    public NativeArray<char> saveChars;

    public LoadJobInfo(Chunk chunk) {
        this.chunk = chunk;
        blocks = new NativeArray<Block>(Chunk.SIZE * Chunk.SIZE * Chunk.SIZE, Allocator.TempJob);

        string saveFile = Serialization.SaveFileName(chunk);
        saveChars = new NativeArray<char>(saveFile.ToCharArray(), Allocator.TempJob);

        LoadJob loadJob = new LoadJob {
            blocks = blocks,
            saveChars = saveChars,
        };

        handle = loadJob.Schedule();
    }

    public void Finish() {
        chunk.blocks.data = blocks.ToArray();
        chunk.generated = true;

        blocks.Dispose();
        saveChars.Dispose();
        return;
    }

}


public class GenJobInfo {
    public JobHandle handle;

    Chunk chunk;
    NativeArray<Block> blocks;

    public GenJobInfo(Chunk chunk) {
        this.chunk = chunk;
        blocks = new NativeArray<Block>(Chunk.SIZE * Chunk.SIZE * Chunk.SIZE, Allocator.TempJob);

        GenerationJob job = new GenerationJob {
            blocks = blocks,
            chunkPos = chunk.pos.ToVector3()
        };

        handle = job.Schedule();

        //Debug.Assert(!genInfo.handle.IsCompleted);
    }

    public void Finish() {

        //blocks.CopyTo(chunk.blocks.data);

        chunk.blocks = new Array3<Block>(blocks.ToArray(), Chunk.SIZE);

        chunk.generated = true;

        chunk.update = true;

        blocks.Dispose();
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
        blocks = MeshBuilder.BuildPaddedBlockArray(chunk);

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

    static List<SaveJobInfo> saveJobInfos = new List<SaveJobInfo>();
    static List<LoadJobInfo> loadJobInfos = new List<LoadJobInfo>();

    //static List<Task<Chunk>> genTasks = new List<Task<Chunk>>();

    public ShadowText text;

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

        for (int i = 0; i < saveJobInfos.Count; ++i) {
            saveJobInfos[i].handle.Complete();
            saveJobInfos[i].Finish();
        }

        for (int i = 0; i < loadJobInfos.Count; ++i) {
            loadJobInfos[i].handle.Complete();
            loadJobInfos[i].Finish();
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

        for (int i = 0; i < saveJobInfos.Count; ++i) {
            if (saveJobInfos[i].handle.IsCompleted) {
                saveJobInfos[i].handle.Complete();

                saveJobInfos[i].Finish();

                saveJobInfos.SwapAndPop(i);
                --i;
            }
        }

        for (int i = 0; i < loadJobInfos.Count; ++i) {
            if (loadJobInfos[i].handle.IsCompleted) {
                loadJobInfos[i].handle.Complete();

                loadJobInfos[i].Finish();

                loadJobInfos.SwapAndPop(i);
                --i;
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

        genJobInfos.Add(info);


    }

    public static void StartMeshJob(Chunk chunk) {

        MeshJobInfo info = new MeshJobInfo(chunk);

        meshJobInfos.Add(info);

        scheduled++;

    }

    public static void StartSaveJob(Chunk chunk) {
        SaveJobInfo info = new SaveJobInfo(chunk);

        saveJobInfos.Add(info);

    }

    public static void StartLoadJob(Chunk chunk) {
        LoadJobInfo info = new LoadJobInfo(chunk);

        loadJobInfos.Add(info);
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
