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

        // make this single loop instead and calculate x,y,z from index i
        for (int x = 0; x < Chunk.SIZE; ++x) {
            for (int y = 0; y < Chunk.SIZE; ++y) {
                for (int z = 0; z < Chunk.SIZE; ++z) {
                    Vector3 wp = new Vector3(x, y, z) + chunkPos;
                    float n = 0.0f;

                    // experiment with catlike coding noise some more
                    //NoiseSample samp = Noise.Sum(Noise.Simplex3D, wp, 0.015f, 5, 2.0f, 0.5f);
                    //float n = samp.value * 3.0f;

                    // TODO: convert shapes.cginc into c# equiv, and or get gen going on multiple thread (try job system!!!)
                    n -= Vector3.Dot(wp, Vector3.up) * 0.05f;

                    n += Noise.Fractal(wp, 5, 0.01f);

                    if (n > 0.3f) {
                        blocks[x + y * Chunk.SIZE + z * Chunk.SIZE * Chunk.SIZE] = Blocks.STONE;
                    } else if (n > 0.15f) {
                        blocks[x + y * Chunk.SIZE + z * Chunk.SIZE * Chunk.SIZE] = Blocks.GRASS;

                        // trying to make grass not spawn on cliff edge...
                        //if (Mathf.Abs(samp.derivative.normalized.y) < 0.4f) {
                        //    chunk.SetBlock(x, y, z, new BlockGrass());
                        //} else {
                        //    chunk.SetBlock(x, y, z, new Block());
                        //}
                    } else {
                        blocks[x + y * Chunk.SIZE + z * Chunk.SIZE * Chunk.SIZE] = Blocks.AIR;
                    }

                }
            }
        }

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

        // could also set unmodified after loading and then if theres no new modified blocks leave save file alone
        chunk.SetBlocksUnmodified();

        Serialization.LoadChunk(chunk);

        blocks.Dispose();
    }
}

public class JobController : MonoBehaviour {

    static List<GenJobInfo> genInfos = new List<GenJobInfo>();

    static List<Task<Chunk>> genTasks = new List<Task<Chunk>>();

    public ShadowText text;

    int totalGen = 0;

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
                totalGen++;

                // swap and pop
                genInfos[i] = genInfos[genInfos.Count - 1];
                genInfos.RemoveAt(genInfos.Count - 1);
                --i;
            }
        }

        for (int i = 0; i < genTasks.Count; ++i) {
            if (genTasks[i].IsCompleted) {
                Chunk chunk = genTasks[i].Result;

                chunk.generated = true;
                // could also set unmodified after loading and then if theres no new modified blocks leave save file alone
                chunk.SetBlocksUnmodified();
                Serialization.LoadChunk(chunk);

                totalGen++;

                //genTasks[i].Dispose();
                genTasks[i] = genTasks[genTasks.Count - 1];
                genTasks.RemoveAt(genTasks.Count - 1);
                --i;
            }

        }


        text.SetText(string.Format(
            "Gen: {0}\n" +
            "Chunks: {1}\n" +
            "Greedy: {2}",
            Mathf.Max(genInfos.Count, genTasks.Count), world.chunks.Count, Chunk.beGreedy));

    }


    public static void StartGenerationJob(Chunk chunk) {

        GenJobInfo genInfo = new GenJobInfo(chunk);

        GenerationJob job = new GenerationJob();
        job.blocks = genInfo.blocks;
        job.chunkPos = chunk.pos.ToVector3();

        genInfo.handle = job.Schedule();

        genInfos.Add(genInfo);

    }

    public static int GetRunningJobs() {
        return Mathf.Max(genTasks.Count, genInfos.Count);
    }

    public static void StartGenerationTask(Chunk chunk) {

        Task<Chunk> t = Task<Chunk>.Factory.StartNew(() => {

            WorldGenerator.Generate(chunk);

            return chunk;
        }, TaskCreationOptions.PreferFairness);

        genTasks.Add(t);
    }

}
