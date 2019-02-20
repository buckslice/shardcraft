using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadChunks : MonoBehaviour {

    int loadRadius = 2;
    public World world;

    //List<Vector3i> updateList = new List<Vector3i>(); // list of chunk positions that should be rendered
    List<Vector3i> genList = new List<Vector3i>(); // list of chunk positions that should be built


    // Start is called before the first frame update
    void Start() {

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
        Vector3i playerPos = Chunk.GetChunkPosition(transform.position);

        int added = 0;
        if (genList.Count == 0) {

            for (int s = 0; s <= loadRadius; ++s) {
                for (int x = -s; x <= s; ++x) {
                    for (int y = -s; y <= s; ++y) {
                        for (int z = -s; z <= s; ++z) {
                            if (x == -s || x == s || y == -s || y == s || z == -s || z == s) {
                                Vector3i p = playerPos + new Vector3i(x, y, z) * Chunk.SIZE;

                                Chunk newChunk = world.GetChunk(p.x, p.y, p.z);

                                if (newChunk != null && newChunk.rendered) {
                                    continue;
                                }

                                genList.Add(p);

                                // dont let list get too huge
                                if (++added >= 16) {
                                    return;
                                }
                            }
                        }
                    }
                }
            }


        }

    }

    void BuildChunk(Vector3i pos) {
        int pad = Chunk.SIZE * 1;
        for (int x = pos.x - pad; x <= pos.x + pad; x += Chunk.SIZE) {
            for (int y = pos.y - pad; y <= pos.y + pad; y += Chunk.SIZE) {
                for (int z = pos.z - pad; z <= pos.z + pad; z += Chunk.SIZE) {
                    if (world.GetChunk(x, y, z) == null) {
                        world.CreateChunk(x, y, z);
                    }
                }
            }
        }
        //if (world.GetChunk(pos.x, pos.y, pos.z) == null) {
        //    world.CreateChunk(pos.x, pos.y, pos.z);
        //}

        //updateList.Add(pos);
    }

    void GenerateChunks() {
        const int maxGensPerFrame = 4;
        const int pad = Chunk.SIZE * 1;
        int generated = 0;
        while (generated < maxGensPerFrame && genList.Count > 0) {
            Vector3i pos = genList[0];
            for (int x = pos.x - pad; x <= pos.x + pad; x += Chunk.SIZE) {
                for (int y = pos.y - pad; y <= pos.y + pad; y += Chunk.SIZE) {
                    for (int z = pos.z - pad; z <= pos.z + pad; z += Chunk.SIZE) {
                        if (world.GetChunk(x, y, z) == null) {
                            world.CreateChunk(x, y, z);
                            if (++generated > maxGensPerFrame) {
                                return;
                            }
                        }
                    }
                }
            }
            genList.RemoveAt(0);

            // THIS IS SETUP IN NEGATIVE SCRIPT EXECUTION ORDER so chunks will update in same frame
            world.GetChunk(pos).update = true;
        }
    }

    //void LoadAndRenderChunks() {
    //for (int i = 0; i < 1 && buildList.Count != 0; ++i) {
    //    BuildChunk(buildList[0]);
    //    buildList.RemoveAt(0);
    //}

    //for (int i = 0; i < updateList.Count; ++i) {
    //    Vector3i upos = updateList[i];
    //    Chunk chunk = world.GetChunk(upos.x, upos.y, upos.z);
    //    if (chunk != null) {
    //        chunk.update = true;
    //    }
    //}
    //updateList.Clear();
    //}

    void DeleteFarChunks() {
        float maxDist = (loadRadius * 3) * Chunk.SIZE;

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
