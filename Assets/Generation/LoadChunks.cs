using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadChunks : MonoBehaviour {

    int loadRadius = 8; // render radius will be 1 minus this
    World world;

    Vector3i[] neighborChunks; // list of chunk offsets to generate in order of closeness

    public const int maxUpdatesPerFrame = 20;

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

        Debug.Log(neighborChunks.Length);
        //Debug.Assert(SumNeighborChunks() == Vector3i.zero);

        //for (int i = 0; i < neighborChunks.Length; ++i) {
        //    Debug.Log(neighborChunks[i]);
        //}
    }

    Vector3i SumNeighborChunks() {
        Vector3i val = new Vector3i();
        for (int i = 0; i < neighborChunks.Length; ++i) {
            val += neighborChunks[i];
        }
        return val;
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
    }

    public static int needUpdates = 0;
    public static int needGeneration = 0;
    void LateUpdate() {

        UpdateChunks();

        needUpdates = 0;
        needGeneration = 0;
        foreach (var chunk in world.chunks.Values) {
            if (chunk.update && chunk.generated) {
                needUpdates++;
            }
            if (!chunk.generated) {
                needGeneration++;
            }
        }

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

    // generates all chunks that need to be generated
    void GenerateChunks() {
        //const int pad = 1;

        Vector3i playerChunk = Chunk.GetChunkPosition(transform.position);

        for (int i = 0; i < neighborChunks.Length; ++i) {
            Vector3i p = playerChunk + neighborChunks[i];

            //for (int x = -pad; x <= pad; ++x) {
            //    for (int y = -pad; y <= pad; ++y) {
            //        for (int z = -pad; z <= pad; ++z) {
            //            // dont overload job system, not sure if this is needed tho...
            //            // still good to reduce new gameobject creation maybe
            //            if (JobController.GetRunningJobs() >= 16) {
            //                return;
            //            }
            //            // add offset
            //            Vector3i o = new Vector3i(x, y, z) * Chunk.SIZE;
            //            p += o;
            //            Chunk chunk = world.GetChunk(p);
            //            if (chunk == null) {
            //                world.CreateChunk(p.x, p.y, p.z);
            //            }
            //            p -= o;
            //        }
            //    }
            //}

            if (JobController.GetRunningJobs() >= 32) {
                return;
            }
            // add offset
            Chunk chunk = world.GetChunk(p);
            if (chunk == null) {
                world.CreateChunk(p.x, p.y, p.z);
            }

        }

    }

    // todo: change to just delete if chunk offset is not in the neighbors set
    // or at least turn off renderer then delete chunk later actually
    void DeleteFarChunks() {
        float maxDist = (loadRadius * 4) * Chunk.SIZE;

        List<Vector3i> chunksToDelete = new List<Vector3i>();
        foreach (var chunk in world.chunks) {
            float sqrDist = Vector3.SqrMagnitude(chunk.Value.pos.ToVector3() + Vector3.one * Chunk.SIZE - transform.position);

            if (sqrDist > maxDist * maxDist) {
                chunksToDelete.Add(chunk.Key);
            }
        }

        // cant delete in upper loop cuz iterating over dict
        foreach (var cp in chunksToDelete) {
            world.DestroyChunk(cp.x, cp.y, cp.z);
        }
        if (chunksToDelete.Count > 0) {
            Debug.Log("deleted: " + chunksToDelete.Count);
        }
    }



    void TestFillChunk(Vector3i chunkPos, Block toFill) {
        Chunk c = world.GetChunk(chunkPos);
        if (c == null) {
            Debug.Log("no chunk here");
            return;
        }

        for (int x = 0; x < Chunk.SIZE; ++x) {
            for (int y = 0; y < Chunk.SIZE; ++y) {
                for (int z = 0; z < Chunk.SIZE; ++z) {
                    c.SetBlock(x, y, z, Blocks.STONE);
                }
            }
        }
    }

}
