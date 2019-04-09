using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.UI;

public class LoadChunks : MonoBehaviour {

    World world;

    Vector3i[] neighborChunks; // list of chunk offsets to generate in order of closeness

    public const int loadRadius = 8; // render radius will be 1 minus this
    public const int maxUpdatesPerFrame = 8; // number of update per frame (sends mesh jobs among other things)
    public const int genJobLimit = 16; // limit on number of active generation jobs
    public const int meshLoadsPerFrame = 16; // number of meshes and colliders uploaded per frame
    public const int maxActiveLightJobs = 0;

    // default settings for above
    //8 8 16 16 10

    public Text text;

    public Vector3i editChunk;

    // Start is called before the first frame update
    void Awake() {
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
        //Assert.IsTrue(SumNeighborChunks() == Vector3i.zero);

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

        Serialization.FreeSavedChunks(world.chunkPool);

        world.chunkPool.Dispose();

        Pools.Dispose();

        JobController.instance.blockData.Dispose();
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
            WorldUtils.FillChunk(world, editChunk, Blocks.STONE);
        }
        if (Input.GetKeyDown(KeyCode.G)) {
            WorldUtils.FillChunk(world, editChunk, Blocks.AIR);
        }
        if (Input.GetKeyDown(KeyCode.C)) {
            WorldUtils.CheckerboardChunk(world, editChunk, Blocks.STONE);
        }

        //if (Input.GetKeyDown(KeyCode.P)) {
        //    Debug.Log(world.IsAnyChunksLocked());
        //}

        JobHandle.ScheduleBatchedJobs();
    }

    static int chunksLoaded = 0;
    public static bool drawDebug = true;
    public static bool updateChunks = true;

    Queue<Chunk> chunkGenQueue = new Queue<Chunk>();

    void LateUpdate() {

        // check how many chunks loaded and queue the ones that couldnt up for generation
        chunksLoaded += Serialization.CheckNewLoaded(chunkGenQueue);

        Serialization.FreeSavedChunks(world.chunkPool);

        UnityEngine.Profiling.Profiler.BeginSample("Update Chunks");
        if (updateChunks) {
            UpdateChunks();
        }
        UnityEngine.Profiling.Profiler.EndSample();

        JobHandle.ScheduleBatchedJobs();

        Vector3i pbp = WorldUtils.GetBlockPos(transform.position);
        Vector3i pcp = WorldUtils.GetChunkPosFromBlockPos(pbp.x, pbp.y, pbp.z);

        if (drawDebug) {
            text.gameObject.SetActive(true);
            text.text = string.Format(
                "block x:{0} y:{1} z:{2}\n" +
                "chunk x:{3} y:{4} z:{5}\n" +
                "Generat: {6}/{7}\n" +
                "Structr: {8}/{9}\n" +
                "Meshing: {10}/{11}\n" +
                "Light  : {12}/{13}\n" +
                "Free/C : {14}/{15}\n" +
                "Chunks:  {16}\n" +
                "Loaded:  {17}\n" +
                "Greedy:  {18}\n" +
                "v3Pool:  {19}/{20}\n" +
                "intPool: {21}/{22}\n",
                pbp.x, pbp.y, pbp.z, pcp.x, pcp.y, pcp.z,
                JobController.genJobFinished, JobController.genJobScheduled,
                JobController.structureJobFinished, JobController.structureJobScheduled,
                JobController.meshJobFinished, JobController.meshJobScheduled,
                JobController.lightJobFinished, JobController.lightJobScheduled,
                world.chunkPool.CountFree(), world.chunkPool.Count(),
                world.chunks.Count, chunksLoaded, Chunk.beGreedy,
                Pools.v3N.CountFree(), Pools.v3N.Count(),
                Pools.intN.CountFree(), Pools.intN.Count()
            );
        } else {
            text.gameObject.SetActive(false);
        }
    }

    void UpdateChunks() {
        int updates = 0;

        // calls update on nearby block of chunks
        // if others are loaded but too far out of range they won't get updated until you come closer
        Vector3i playerChunk = WorldUtils.GetChunkPosFromWorldPos(transform.position);
        for (int i = 0; i < neighborChunks.Length && updates < maxUpdatesPerFrame; ++i) {
            Vector3i p = playerChunk + neighborChunks[i];
            Chunk chunk = world.GetChunk(p);
            if (chunk != null) {
                updates += chunk.UpdateChunk() ? 1 : 0;
            }
        }

    }

    int neighborIndex = 0;
    Vector3i lastPlayerChunk = new Vector3i(1000000, 1000000, 1000000);

    // generates all chunks that need to be generated
    void GenerateChunks() {
        //const int pad = 1;

        Vector3i playerChunk = WorldUtils.GetChunkPosFromWorldPos(transform.position);
        if (playerChunk != lastPlayerChunk) {
            neighborIndex = 0;
        }
        lastPlayerChunk = playerChunk;

        // queue up chunks that failed to load (no save entry)
        while (JobController.GetGenJobCount() < genJobLimit && chunkGenQueue.Count > 0) {
            JobController.StartGenerationJob(chunkGenQueue.Dequeue());
        }

        while (neighborIndex < neighborChunks.Length) {
            if (JobController.GetGenJobCount() >= genJobLimit) {
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

        Vector3i playerChunk = WorldUtils.GetChunkPosFromWorldPos(transform.position);

        foreach (var chunk in world.chunks.Values) {
            if (chunk.dying) {
                continue;
            }

            if (Mathf.Abs(playerChunk.x - chunk.cp.x) > (loadRadius + 1) ||
               Mathf.Abs(playerChunk.y - chunk.cp.y) > (loadRadius + 1) ||
               Mathf.Abs(playerChunk.z - chunk.cp.z) > (loadRadius + 1)) {
                world.DestroyChunk(chunk);
            }
        }

        world.DestroyChunks();
    }

    Vector3i SumNeighborChunks() {
        Vector3i val = new Vector3i();
        for (int i = 0; i < neighborChunks.Length; ++i) {
            val += neighborChunks[i];
        }
        return val;
    }
}
