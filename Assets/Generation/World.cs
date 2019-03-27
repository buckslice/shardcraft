using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class World : MonoBehaviour {

    // can add support for multiple worlds down the line. will need to change saving and loading a bit
    public const string worldName = "world";

    public GameObject chunkPrefab;

    public Material TileMat;
    public Material TileMatGreedy;

    public Dictionary<Vector3i, Chunk> chunks = new Dictionary<Vector3i, Chunk>();

    public Pool<Chunk> chunkPool;

    public bool loadPlayerSave = false;

    public int seed; // todo: make world generator use seed with some offset or something

    // Use this for initialization
    void Start() {
        Debug.Assert(Chunk.CHUNK_HEIGHT >= Chunk.CHUNK_WIDTH);

        Chunk InstantiateChunk() {
            GameObject newChunkObject = Instantiate(chunkPrefab, Vector3.zero, Quaternion.identity, transform) as GameObject;
            return new Chunk(newChunkObject);
        }
        void ChunkDispose(Chunk chunk) {
            chunk.blocks.Dispose();
        }
        chunkPool = new Pool<Chunk>(InstantiateChunk, ChunkDispose);

        Serialization.StartThread();

        if (loadPlayerSave) {
            Serialization.LoadPlayer();
        }

        Tests.Run();


        seed = 1000;
    }



    public void CreateChunk(int x, int y, int z) {
        Debug.Assert(GetChunk(x, y, z) == null);

        Vector3i chunkPos = new Vector3i(x, y, z);
        Vector3i worldPos = chunkPos * Chunk.SIZE;

        Chunk chunk = chunkPool.Get();
        chunk.Initialize(this, worldPos, chunkPos);

        Serialization.LoadChunk(chunk);

        //Add it to the chunks dictionary with the position as the key
        chunks.Add(chunkPos, chunk);

        // setup chunk neighbors (need to do after add it to dict)
        ConnectNeighbors(chunk, GetChunk(x - 1, y, z), Dir.west);
        ConnectNeighbors(chunk, GetChunk(x, y - 1, z), Dir.down);
        ConnectNeighbors(chunk, GetChunk(x, y, z - 1), Dir.south);
        ConnectNeighbors(chunk, GetChunk(x + 1, y, z), Dir.east);
        ConnectNeighbors(chunk, GetChunk(x, y + 1, z), Dir.up);
        ConnectNeighbors(chunk, GetChunk(x, y, z + 1), Dir.north);
    }

    void ConnectNeighbors(Chunk c, Chunk n, Dir dir) {
        if (n == null) {
            return;
        }
        int d = (int)dir;
        c.neighbors[d] = n;
        n.neighbors[Dirs.Opp(d)] = c;
    }

    public bool DestroyChunk(int x, int y, int z) {
        Chunk chunk = GetChunk(x, y, z);
        if (chunk != null) {
            // notify neighbors
            for (int i = 0; i < 6; ++i) {
                Chunk n = chunk.neighbors[i];
                if (n != null) {
                    n.neighbors[Dirs.Opp(i)] = null;
                }
            }

            chunk.gameObject.SetActive(false);
            chunk.ClearMeshes();
            chunks.Remove(new Vector3i(x, y, z));
            Serialization.SaveChunk(chunk);
            return true;
        }
        return false;
    }

    public void SaveChunks() {
        foreach (KeyValuePair<Vector3i, Chunk> entry in chunks) {
            Serialization.SaveChunk(entry.Value, true);
        }
        Serialization.SetNewWork();
    }

    public void DisposeChunks() {
        // first return all the chunks so the chunkPool contains one reference to every chunk it made
        foreach (KeyValuePair<Vector3i, Chunk> entry in chunks) {
            chunkPool.Return(entry.Value);
        }
        chunkPool.Dispose();
    }

    // gets chunk using world coordinates
    public Chunk GetChunkByWorldPos(int x, int y, int z) {
        chunks.TryGetValue(Chunk.GetChunkPosition(new Vector3(x, y, z)), out Chunk chunk);
        return chunk;
    }
    // gets chunk using chunk coords
    public Chunk GetChunk(int x, int y, int z) {
        chunks.TryGetValue(new Vector3i(x, y, z), out Chunk chunk);
        return chunk;
    }
    public Chunk GetChunk(Vector3i chunkPos) {
        chunks.TryGetValue(chunkPos, out Chunk chunk);
        return chunk;
    }


    public Block GetBlock(int x, int y, int z) {
        Chunk containerChunk = GetChunkByWorldPos(x, y, z);

        if (containerChunk != null && containerChunk.loaded) {
            return containerChunk.GetBlock(x - containerChunk.wp.x, y - containerChunk.wp.y, z - containerChunk.wp.z);
        } else {
            return Blocks.AIR;
        }

    }

    // sets block using world coordinates
    public void SetBlock(int x, int y, int z, Block block) {
        Chunk chunk = GetChunkByWorldPos(x, y, z);

        if (chunk != null) {
            chunk.SetBlock(x - chunk.wp.x, y - chunk.wp.y, z - chunk.wp.z, block);
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


}
