using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.Assertions;

public class World : MonoBehaviour {

    // can add support for multiple worlds down the line. will need to change saving and loading a bit
    public const string worldName = "world";

    public GameObject chunkPrefab;

    public Material TileMat;
    public Material TileMatGreedy;

    public Dictionary<Vector3i, Chunk> chunks = new Dictionary<Vector3i, Chunk>();

    public Pool<Chunk> chunkPool;

    public Queue<Chunk> destroyQueue = new Queue<Chunk>();

    public bool loadPlayerSave = false;

    public int seed; // todo: make world generator use seed with some offset or something

    // Use this for initialization
    void Start() {
        Assert.IsTrue(Chunk.CHUNK_HEIGHT >= Chunk.CHUNK_WIDTH);

        Chunk InstantiateChunk() {
            GameObject newChunkObject = Instantiate(chunkPrefab, Vector3.zero, Quaternion.identity, transform) as GameObject;
            return new Chunk(newChunkObject);
        }
        void ChunkDispose(Chunk chunk) {
            chunk.blocks.Dispose();
            chunk.lights.Dispose();
            chunk.faces.Dispose();
        }
        chunkPool = new Pool<Chunk>(InstantiateChunk, null, ChunkDispose);

        Serialization.StartThread();

        if (loadPlayerSave) {
            Serialization.LoadPlayer();
        }

        Tests.Run();

        seed = 1000;
    }

    public void CreateChunk(int x, int y, int z) {
        Assert.IsTrue(GetChunk(x, y, z) == null);

        Vector3i chunkPos = new Vector3i(x, y, z);
        Vector3i blockPos = chunkPos * Chunk.SIZE;

        Chunk chunk = chunkPool.Get();
        //Add it to the chunks dictionary with the position as the key
        chunks.Add(chunkPos, chunk);
        chunk.Initialize(this, blockPos, chunkPos);

        Serialization.LoadChunk(chunk);

        // setup chunk neighbors (need to do after add it to dict)
        ConnectNeighbors(chunk, GetChunk(x - 1, y, z), Dir.west);
        ConnectNeighbors(chunk, GetChunk(x, y - 1, z), Dir.down);
        ConnectNeighbors(chunk, GetChunk(x, y, z - 1), Dir.south);
        ConnectNeighbors(chunk, GetChunk(x + 1, y, z), Dir.east);
        ConnectNeighbors(chunk, GetChunk(x, y + 1, z), Dir.up);
        ConnectNeighbors(chunk, GetChunk(x, y, z + 1), Dir.north);

        SetLoadedNeighbors(chunk);
    }

    void ConnectNeighbors(Chunk c, Chunk n, Dir dir) {
        if (n == null) {
            return;
        }
        int d = (int)dir;
        c.neighbors[d] = n;
        n.neighbors[Dirs.Opp(d)] = c;
    }

    // based on chunk pos coordinate, increase or decrease nearby chunks loaded count based on loaded or unloaded
    public void UpdateNeighborsLoadedNeighbors(Vector3i cp, bool loaded) {
        int i = loaded ? 1 : -1;
        for (int y = -1; y <= 1; ++y) {
            for (int z = -1; z <= 1; ++z) {
                for (int x = -1; x <= 1; ++x) {
                    if (x == 0 && y == 0 && z == 0) {
                        continue;
                    }
                    Chunk neighbor = GetChunk(cp + new Vector3i(x, y, z));
                    if (neighbor != null) {
                        neighbor.loadedNeighbors += i;
                    }
                }
            }
        }
    }
    // sets a new chunks loaded neighbor count
    void SetLoadedNeighbors(Chunk chunk) {
        for (int y = -1; y <= 1; ++y) {
            for (int z = -1; z <= 1; ++z) {
                for (int x = -1; x <= 1; ++x) {
                    if (x == 0 && y == 0 && z == 0) {
                        continue;
                    }
                    Chunk neighbor = GetChunk(chunk.cp + new Vector3i(x, y, z));
                    if (neighbor != null && neighbor.loaded) {
                        chunk.loadedNeighbors++;
                    }
                }
            }
        }
    }

    // this will save all chunks and return them to the pool
    // should basically only be called when quitting, if want to save all while running
    // maybe just save then mark all chunks as not needing the saves or something and free chunk some other way
    public void SaveChunks() {
        foreach (KeyValuePair<Vector3i, Chunk> entry in chunks) {
            Serialization.SaveChunk(entry.Value, false);
        }
        Serialization.SetNewWork();
    }

    // gets chunk using chunk coords
    public Chunk GetChunk(int x, int y, int z) {
        chunks.TryGetValue(new Vector3i(x, y, z), out Chunk chunk);
        return chunk;
    }
    // pretty sure this is still creating garbage somehow... changing the hashcode generator on Vector3i
    // changes the garbage allocation readout in unity profiler. it implements IEquatable tho!??
    public Chunk GetChunk(Vector3i chunkPos) {
        chunks.TryGetValue(chunkPos, out Chunk chunk);
        return chunk;
    }

    // gets chunk using world block coordinates
    public Chunk GetChunkByWorldBlockPos(int x, int y, int z) {
        chunks.TryGetValue(WorldUtils.GetChunkPosFromBlockPos(x, y, z), out Chunk chunk);
        return chunk;
    }

    // gets block using world block coordinates (as opposed to local/chunk block coordinates)
    public Block GetBlock(int x, int y, int z) {
        Chunk containerChunk = GetChunkByWorldBlockPos(x, y, z);
        if (containerChunk != null && containerChunk.loaded) {
            return containerChunk.GetBlock(x - containerChunk.bp.x, y - containerChunk.bp.y, z - containerChunk.bp.z);
        } else {
            return Blocks.AIR;
        }

    }

    // sets block using world block coordinates (as opposed to local/chunk block coordinates)
    public void SetBlock(int x, int y, int z, Block block) {
        Chunk chunk = GetChunkByWorldBlockPos(x, y, z);

        if (chunk != null) {
            chunk.SetBlock(x - chunk.bp.x, y - chunk.bp.y, z - chunk.bp.z, block);
        }
    }

    public void SetBlock(Vector3i bp, Block block) {
        Chunk chunk = GetChunkByWorldBlockPos(bp.x, bp.y, bp.z);

        if (chunk != null) {
            chunk.SetBlock(bp.x - chunk.bp.x, bp.y - chunk.bp.y, bp.z - chunk.bp.z, block);
        }
    }

    public void SwapGreedy() {
        Chunk.beGreedy = !Chunk.beGreedy;

        foreach (Chunk c in chunks.Values) {
            c.mr.material = Chunk.beGreedy ? TileMatGreedy : TileMat;
            if (c.rendered) {
                c.update = true;
            }
        }
    }


    // adds to queue to destroy it when proper time
    public void DestroyChunk(Chunk chunk) {
        if (chunk != null && !chunk.dying) {
            destroyQueue.Enqueue(chunk);
            chunk.dying = true;
        }
    }

    public void DestroyChunks() {
        int destroyed = 0;
        int qlen = destroyQueue.Count;
        while (--qlen >= 0) {
            Chunk chunk = destroyQueue.Dequeue();
            // chunk has to be loaded and none of the data can be in use
            if (!chunk.loaded || chunk.IsAnyDataInUse()) {
                destroyQueue.Enqueue(chunk); // put it back and try next time
            } else { // destroy the chunk
                // notify neighbors
                for (int i = 0; i < 6; ++i) {
                    Chunk n = chunk.neighbors[i];
                    if (n != null) {
                        n.neighbors[Dirs.Opp(i)] = null;
                    }
                    chunk.neighbors[i] = null;
                }

                UpdateNeighborsLoadedNeighbors(chunk.cp, false);

                chunk.gameObject.SetActive(false);
                chunk.ClearMeshes();
                chunks.Remove(chunk.cp);
                Serialization.SaveChunk(chunk);
                destroyed++;
            }
        }

        //if (destroyed > 0) {
        //    Debug.Log("destroyed: " + destroyed);
        //}
    }

}
