using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.UI;

public class LoadChunks : MonoBehaviour {

    int loadRadius = 4; // render radius will be 1 minus this
    World world;

    Vector3i[] neighborChunks; // list of chunk offsets to generate in order of closeness

    public const int maxUpdatesPerFrame = 8; // how many mesh jobs are sent among other things
    public const int genJobLimit = 16; // limit on number of active generation jobs
    public const int meshLoadsPerFrame = 8; // number of mesh datas and colliders uploaded

    public Text text;

    // Start is called before the first frame update
    void Start() {
        world = FindObjectOfType<World>();
        GenerateNeighborChunks(loadRadius);
    }

    // im too netarded to try to figure out this pattern so writing some trash flood fill algo lol
    void GenerateNeighborChunks(int radius) {
        int size = radius * 2 + 1;
        neighborChunks = new Vector3i[size * size * size];
        neighborChunks[0] = new Vector3i(0, 0, 0);

        HashSet<Vector3i> visited = new HashSet<Vector3i>();
        visited.Add(new Vector3i(0, 0, 0));

        Vector3i[] neighbors = new Vector3i[] {
            Vector3i.forward, Vector3i.right, Vector3i.up,
            Vector3i.back, Vector3i.left, Vector3i.down
        };

        bool TooGreat(Vector3i v) {
            return Mathf.Abs(v.x) > radius || Mathf.Abs(v.y) > radius || Mathf.Abs(v.z) > radius;
        }

        int end = 0;
        for (int i = 0; i < neighborChunks.Length; ++i) {
            Vector3i cur = neighborChunks[i];
            for (int n = 0; n < 6; ++n) {
                Vector3i next = cur + neighbors[n];
                if (!visited.Contains(next) && !TooGreat(next)) {
                    neighborChunks[++end] = next;
                    visited.Add(next);
                }
            }

        }

        Debug.Log(neighborChunks.Length + " chunks nearby");
        //Debug.Assert(SumNeighborChunks() == Vector3i.zero);

        //for (int i = 0; i < neighborChunks.Length; ++i) {
        //    Debug.Log(neighborChunks[i]);
        //}
    }

    public void OnApplicationQuit() {
        JobController.FinishJobs();

        // save all chunks
        world.SaveChunks();

        Serialization.SavePlayer();

        Serialization.KillThread();

        Serialization.thread.Join();
        Debug.Log("main thread joined!");

        world.DisposeChunks();

        Pools.v3Pool.Dispose();
        Pools.v2Pool.Dispose();
        Pools.intPool.Dispose();

    }


    // Update is called once per frame
    int timer = 0;
    void Update() {
        if (++timer < 10) {
            GenerateChunks();
        } else {
            DeleteFarChunks();
            timer = 0;
        }

        if (Input.GetKeyDown(KeyCode.F)) {
            TestFillChunk(new Vector3i(-2, 0, 2), Blocks.STONE);
        }

        JobHandle.ScheduleBatchedJobs();
    }

    static int chunksLoaded = 0;

    Queue<Chunk> chunkGenQueue = new Queue<Chunk>();

    void LateUpdate() {
        // check how many chunks loaded and queue the ones that couldnt up for generation
        chunksLoaded += Serialization.CheckNewLoaded(chunkGenQueue);

        Serialization.FreeSavedChunks(world.chunkPool);

        UnityEngine.Profiling.Profiler.BeginSample("Update Chunks");
        UpdateChunks();
        UnityEngine.Profiling.Profiler.EndSample();

        JobHandle.ScheduleBatchedJobs();

        text.text = string.Format(
            "Generat: {0}/{1}\n" +
            "Meshing: {2}/{3}\n" +
            "Free/C : {4}/{5}\n" +
            "Chunks:  {6}\n" +
            "Loaded:  {7}\n" +
            "Greedy:  {8}\n" +
            "v3Pool:  {9}/{10}\n" +
            "v2Pool:  {11}/{12}\n" +
            "intPool: {13}/{14}\n",
            JobController.genJobFinished, JobController.genJobScheduled,
            JobController.meshJobFinished, JobController.meshJobScheduled,
            world.chunkPool.CountFree(), world.chunkPool.Count(),
            world.chunks.Count, chunksLoaded, Chunk.beGreedy,
            Pools.v3Pool.CountFree(), Pools.v3Pool.Count(),
            Pools.v2Pool.CountFree(), Pools.v2Pool.Count(),
            Pools.intPool.CountFree(), Pools.intPool.Count()
            );


    }

    void UpdateChunks() {
        int updates = 0;

        Vector3i playerChunk = Chunk.GetChunkPosition(transform.position);
        for (int i = 0; i < neighborChunks.Length && updates < maxUpdatesPerFrame; ++i) {
            Vector3i p = playerChunk + neighborChunks[i];
            Chunk chunk = world.GetChunk(p);
            if (chunk != null) {
                if (chunk.update) {
                    updates += chunk.UpdateChunk() ? 1 : 0;
                }
            }
        }

    }

    int neighborIndex = 0;
    Vector3i lastPlayerChunk = new Vector3i(1000000, 1000000, 1000000);

    // generates all chunks that need to be generated
    void GenerateChunks() {
        //const int pad = 1;

        Vector3i playerChunk = Chunk.GetChunkPosition(transform.position);
        if (playerChunk != lastPlayerChunk) {
            neighborIndex = 0;
        }
        lastPlayerChunk = playerChunk;

        // queue up chunks that failed to load
        while (JobController.GetRunningJobs() < genJobLimit && chunkGenQueue.Count > 0) {
            JobController.StartGenerationJob(chunkGenQueue.Dequeue());
        }

        while (neighborIndex < neighborChunks.Length) {
            if (JobController.GetRunningJobs() >= genJobLimit) {
                return;
            }

            Vector3i p = playerChunk + neighborChunks[neighborIndex];

            // add offset
            Chunk chunk = world.GetChunk(p);
            if (chunk == null) {
                world.CreateChunk(p.x, p.y, p.z);
            }

            neighborIndex++;
        }

    }

    // todo: change to just delete if chunk offset is not in the neighbors set
    // or at least turn off renderer then delete chunk later actually
    void DeleteFarChunks() {
        float maxDist = (loadRadius * 4) * Chunk.SIZE;

        List<Vector3i> chunksToDelete = new List<Vector3i>();
        foreach (var chunk in world.chunks) {
            float sqrDist = Vector3.SqrMagnitude(chunk.Value.wp.ToVector3() + Vector3.one * Chunk.SIZE - transform.position);

            if (sqrDist > maxDist * maxDist) {
                chunksToDelete.Add(chunk.Key);
            }
        }

        // cant delete in upper loop cuz iterating over dict
        int destroyed = 0;
        foreach (var cp in chunksToDelete) {
            destroyed += world.DestroyChunk(cp.x, cp.y, cp.z) ? 1 : 0;
        }
        if (chunksToDelete.Count > 0) {
            Debug.Log("destroyed: " + destroyed);
        }
    }

    // nice do one in checkerboard pattern too for testing encoding and sector stuff
    void TestFillChunk(Vector3i chunkPos, Block toFill) {
        Chunk c = world.GetChunk(chunkPos);
        if (c == null) {
            Debug.Log("no chunk here");
            return;
        }

        for (int x = 0; x < Chunk.SIZE; ++x) {
            for (int y = 0; y < Chunk.SIZE; ++y) {
                for (int z = 0; z < Chunk.SIZE; ++z) {
                    //c.SetBlock(x, y, z, Blocks.STONE);

                    // checkerboard
                    if ((x + y + z) % 2 == 0) {
                        c.SetBlock(x, y, z, Blocks.STONE);
                    } else {
                        c.SetBlock(x, y, z, Blocks.AIR);
                    }
                }
            }
        }
    }


    Vector3i SumNeighborChunks() {
        Vector3i val = new Vector3i();
        for (int i = 0; i < neighborChunks.Length; ++i) {
            val += neighborChunks[i];
        }
        return val;
    }
}
