using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;

//[BurstCompile]
public struct GenerationJob : IJob {

    public Vector3 chunkPos;
    public NativeArray<Block> blocks;
    //public Array3<Block> blocks;

    public void Execute() {

        WorldGenerator.Generate(chunkPos, blocks);

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

public class JobController : MonoBehaviour {

    static List<GenJobInfo> genInfos = new List<GenJobInfo>();

    //static List<Task<Chunk>> genTasks = new List<Task<Chunk>>();

    public ShadowText text;
    
    World world;

    // Start is called before the first frame update
    void Start() {
        world = FindObjectOfType<World>();
    }

    public void OnApplicationQuit() {

        for (int i = 0; i < genInfos.Count; ++i) {
            genInfos[i].handle.Complete();
            genInfos[i].Finish();
        }
    }

    // Update is called once per frame
    void Update() {

        for (int i = 0; i < genInfos.Count; ++i) {
            if (genInfos[i].handle.IsCompleted) {
                genInfos[i].handle.Complete();

                genInfos[i].Finish();
                finished++;

                // swap and pop
                genInfos[i] = genInfos[genInfos.Count - 1];
                genInfos.RemoveAt(genInfos.Count - 1);
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

        GenJobInfo genInfo = new GenJobInfo(chunk);

        GenerationJob job = new GenerationJob();
        job.blocks = genInfo.blocks;
        job.chunkPos = chunk.pos.ToVector3();

        genInfo.handle = job.Schedule();

        Debug.Assert(!genInfo.handle.IsCompleted);

        genInfos.Add(genInfo);

        scheduled++;

    }

    public static int GetRunningJobs() {
        return genInfos.Count;
    }

    //public static void StartGenerationTask(Chunk chunk) {

    //    Task<Chunk> t = Task<Chunk>.Factory.StartNew(() => {

    //        WorldGenerator.Generate(chunk);

    //        return chunk;
    //    }, TaskCreationOptions.PreferFairness);

    //    genTasks.Add(t);
    //}

}
