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
    //public Array3<Block> blocks;

    public void Execute() {

        WorldGenerator.Generate(chunkPos, blocks);

    }
}

//[BurstCompile]
public struct MeshJob : IJob {

    public NativeArray<Block> blocks; // has 1 block padding on edges

    public NativeList<Vector3> vertices;
    public NativeList<int> triangles;
    public NativeList<Vector2> uvs;

    public void Execute() {

        const int s2 = Chunk.SIZE + 2;

        for (int z = 1; z < s2 - 1; z++) {
            for (int y = 1; y < s2 - 1; y++) {
                for (int x = 1; x < s2 - 1; x++) {
                    Block b = blocks[x + y * s2 + z * s2 * s2];
                    if (b == Blocks.AIR) {
                        continue;
                    }
                    BlockType bt = b.GetBlockType();

                    if (!blocks[(x + 1) + y * s2 + z * s2 * s2].IsSolid(Dir.west)) {
                        vertices.Add(new Vector3(x + 1.0f, y, z));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x + 1.0f, y, z + 1.0f));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.east);
                    }
                    if (!blocks[x + (y + 1) * s2 + z * s2 * s2].IsSolid(Dir.down)) {
                        vertices.Add(new Vector3(x, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z));
                        vertices.Add(new Vector3(x, y + 1.0f, z));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.up);
                    }
                    if (!blocks[x + y * s2 + (z + 1) * s2 * s2].IsSolid(Dir.south)) {
                        vertices.Add(new Vector3(x + 1.0f, y, z + 1.0f));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x, y, z + 1.0f));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.north);
                    }

                    if (!blocks[(x - 1) + y * s2 + z * s2 * s2].IsSolid(Dir.east)) {
                        vertices.Add(new Vector3(x, y, z + 1.0f));
                        vertices.Add(new Vector3(x, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x, y + 1.0f, z));
                        vertices.Add(new Vector3(x, y, z));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.west);

                    }
                    if (!blocks[x + (y - 1) * s2 + z * s2 * s2].IsSolid(Dir.up)) {
                        vertices.Add(new Vector3(x, y, z));
                        vertices.Add(new Vector3(x + 1.0f, y, z));
                        vertices.Add(new Vector3(x + 1.0f, y, z + 1.0f));
                        vertices.Add(new Vector3(x, y, z + 1.0f));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.down);
                    }


                    if (!blocks[x + y * s2 + (z - 1) * s2 * s2].IsSolid(Dir.north)) {
                        vertices.Add(new Vector3(x, y, z));
                        vertices.Add(new Vector3(x, y + 1.0f, z));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z));
                        vertices.Add(new Vector3(x + 1.0f, y, z));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.south);
                    }


                }
            }
        }

        //vertices = data.vertices.ToArray();
    }

    public void AddQuadTriangles() {
        triangles.Add(vertices.Length - 4);  // 0
        triangles.Add(vertices.Length - 3);  // 1
        triangles.Add(vertices.Length - 2);  // 2

        triangles.Add(vertices.Length - 4);  // 0
        triangles.Add(vertices.Length - 2);  // 2
        triangles.Add(vertices.Length - 1);  // 3

    }

    public void AddFaceUVs(BlockType bt, Dir dir) {
        Tile tp = bt.TexturePosition(dir);

        uvs.Add(new Vector2(tp.x + 1, tp.y) * Tile.SIZE);
        uvs.Add(new Vector2(tp.x + 1, tp.y + 1) * Tile.SIZE);
        uvs.Add(new Vector2(tp.x, tp.y + 1) * Tile.SIZE);
        uvs.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
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

    public MeshJobInfo(Chunk chunk) {
        const int s = Chunk.SIZE;
        const int s1 = s + 1;
        const int s2 = s + 2;

        blocks = new NativeArray<Block>(s2 * s2 * s2, Allocator.TempJob);
        vertices = new NativeList<Vector3>(Allocator.TempJob);
        triangles = new NativeList<int>(Allocator.TempJob);
        uvs = new NativeList<Vector2>(Allocator.TempJob);

        this.chunk = chunk;

        // west neighbor
        //d s , e u n

        // west
        for (int z = 0; z < s; ++z) {
            for (int y = 0; y < s; ++y) {
                blocks[0 + (y + 1) * s2 + (z + 1) * s2 * s2] = chunk.neighbors[0].blocks[(s - 1) + y * s + z * s * s];
            }
        }
        // down
        for (int z = 0; z < s; ++z) {
            for (int x = 0; x < s; ++x) {
                blocks[(x + 1) + 0 + (z + 1) * s2 * s2] = chunk.neighbors[1].blocks[x + (s - 1) * s + z * s * s];
            }
        }

        // south
        for (int y = 0; y < s; ++y) {
            for (int x = 0; x < s; ++x) {
                blocks[(x + 1) + (y + 1) * s2 + 0] = chunk.neighbors[2].blocks[x + y * s + (s - 1) * s * s];
            }
        }

        // east
        for (int z = 0; z < s; ++z) {
            for (int y = 0; y < s; ++y) {
                blocks[s1 + (y + 1) * s2 + (z + 1) * s2 * s2] = chunk.neighbors[3].blocks[0 + y * s + z * s * s];
            }
        }
        // up
        for (int z = 0; z < s; ++z) {
            for (int x = 0; x < s; ++x) {
                blocks[(x + 1) + s1 * s2 + (z + 1) * s2 * s2] = chunk.neighbors[4].blocks[x + 0 + z * s * s];
            }
        }

        // north
        for (int y = 0; y < s; ++y) {
            for (int x = 0; x < s; ++x) {
                blocks[(x + 1) + (y + 1) * s2 + s1 * s2 * s2] = chunk.neighbors[5].blocks[x + y * s + 0];
            }
        }

        // fill blocks array with padding
        for (int z = 1; z < s1; z++) {
            for (int y = 1; y < s1; y++) {
                for (int x = 1; x < s1; x++) {
                    blocks[x + y * s2 + z * s2 * s2] = chunk.blocks[(x - 1) + (y - 1) * s + (z - 1) * s * s];
                }
            }
        }
    }

    public void Finish() {
        chunk.UpdateMeshNative(vertices, triangles, uvs);

        chunk.waitingForMesh = false;
        chunk.rendered = true;

        blocks.Dispose();
        vertices.Dispose();
        triangles.Dispose();
        uvs.Dispose();
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
