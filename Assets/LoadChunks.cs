using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadChunks : MonoBehaviour {

    int loadRadius = 8;
    World world;

    List<Vector3i> genList = new List<Vector3i>(); // list of chunk positions that should be built

    Vector3i[] neighborChunks; // list of chunk offsets to generate in order of closeness

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

        //for (int i = 0; i < neighborChunks.Length; ++i) {
        //    Debug.Log(neighborChunks[i]);
        //}
    }


    // Update is called once per frame
    int timer = 0;
    void Update() {
        FindChunksToLoad();

        if (++timer < 10) {
            GenerateChunks();
        } else {
            DeleteFarChunks();
            timer = 0;
        }
    }



    void FindChunksToLoad() {
        if (genList.Count == 0) {
            Vector3i playerPos = Chunk.GetChunkPosition(transform.position);
            int added = 0;

            for (int i = 0; i < neighborChunks.Length; ++i) {
                Vector3i p = playerPos + neighborChunks[i] * Chunk.SIZE;
                Chunk newChunk = world.GetChunk(p);

                if (newChunk != null && newChunk.rendered) {
                    continue;
                }

                genList.Add(p);

                // dont let list get too huge
                if (++added >= 1000) {
                    return;
                }
            }

        }

    }

    void GenerateChunks() {
        const int maxUpdatesPerFrame = 10;
        const int pad = Chunk.SIZE * 1;
        int updates = 0;

        while (genList.Count > 0) {
            Vector3i pos = genList[0];
            for (int x = pos.x - pad; x <= pos.x + pad; x += Chunk.SIZE) {
                for (int y = pos.y - pad; y <= pos.y + pad; y += Chunk.SIZE) {
                    for (int z = pos.z - pad; z <= pos.z + pad; z += Chunk.SIZE) {
                        if (JobController.GetRunningJobs() >= 16) {
                            return;
                        }

                        if (world.GetChunk(x, y, z) == null) {
                            world.CreateChunk(x, y, z);
                        }
                    }
                }
            }
            genList.RemoveAt(0);

            if (++updates <= maxUpdatesPerFrame) {
                world.GetChunk(pos).update = true;
            }

        }
    }

    // todo: change to just delete if chunk offset is not in the neighbors set
    // or at least turn off renderer then delete chunk later actually
    void DeleteFarChunks() {
        float maxDist = (loadRadius * 4) * Chunk.SIZE;

        List<Vector3i> chunksToDelete = new List<Vector3i>();
        foreach (var chunk in world.chunks) {
            Vector3i cp = chunk.Key;
            float sqrDist = Vector3.SqrMagnitude(cp.ToVector3() + Vector3.one * Chunk.SIZE - transform.position);

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

}
