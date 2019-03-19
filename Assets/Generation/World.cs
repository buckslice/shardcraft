using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class World : MonoBehaviour {

    public string worldName = "world";

    public GameObject chunkPrefab;

    public Material TileMat;
    public Material TileMatGreedy;

    public Dictionary<Vector3i, Chunk> chunks = new Dictionary<Vector3i, Chunk>();

    public bool loadPlayerSave = false;

    public int seed; // todo: make world generator use seed with some offset or something

    // Use this for initialization
    void Start() {
        Debug.Assert(Chunk.CHUNK_HEIGHT >= Chunk.CHUNK_WIDTH);

        Serialization.StartThread();

        if (loadPlayerSave) {
            Serialization.LoadPlayer();
        }

        Tests.Run();


        seed = 1000;
    }

    public void OnApplicationQuit() {
        // save all chunks
        foreach (KeyValuePair<Vector3i, Chunk> entry in chunks) {
            Serialization.SaveChunk(entry.Value);
        }

        //foreach (KeyValuePair<Vector3i, Chunk> entry in chunks) {
        //    JobController.StartSaveJob(entry.Value);
        //}

        JobController.FinishJobs();

        Serialization.SavePlayer();

        Serialization.KillThread();

    }

    void Update() {
        Serialization.CheckNewLoaded();
    }

    public void CreateChunk(int x, int y, int z) {
        Debug.Assert(GetChunk(x, y, z) == null);

        Vector3i chunkPos = new Vector3i(x, y, z);
        Vector3i worldPos = chunkPos * Chunk.SIZE;

        //Instantiate the chunk at the coordinates using the chunk prefab
        GameObject newChunkObject = Instantiate(chunkPrefab, worldPos.ToVector3(), Quaternion.Euler(Vector3.zero), transform) as GameObject;
        newChunkObject.name = "Chunk " + chunkPos.ToString();
        Chunk chunk = new Chunk(this, worldPos, newChunkObject);

        string saveFile = Serialization.SaveFileName(chunk);
        if (File.Exists(saveFile)) {

            Serialization.LoadChunk(chunk);
            //JobController.StartLoadJob(chunk);

            //Serialization.LoadChunk(chunk); // should become load job, also do region system and RLE this is so slow lol
        } else {
            JobController.StartGenerationJob(chunk);
        }

        //Add it to the chunks dictionary with the position as the key
        chunks.Add(chunkPos, chunk);

        // setup chunk neighbors
        SetNeighbor(chunk, GetChunk(x - 1, y, z), Dir.west);
        SetNeighbor(chunk, GetChunk(x, y - 1, z), Dir.down);
        SetNeighbor(chunk, GetChunk(x, y, z - 1), Dir.south);
        SetNeighbor(chunk, GetChunk(x + 1, y, z), Dir.east);
        SetNeighbor(chunk, GetChunk(x, y + 1, z), Dir.up);
        SetNeighbor(chunk, GetChunk(x, y, z + 1), Dir.north);
    }

    void SetNeighbor(Chunk c, Chunk n, Dir dir) {
        if (n == null) {
            return;
        }
        int d = (int)dir;
        c.neighbors[d] = n;
        n.neighbors[Dirs.Opp(d)] = c;
    }

    public void DestroyChunk(int x, int y, int z) {
        Chunk chunk = GetChunk(x, y, z);
        if (chunk != null) {
            Serialization.SaveChunk(chunk);

            //JobController.StartSaveJob(chunk);

            // notify neighbors
            for (int i = 0; i < 6; ++i) {
                Chunk n = chunk.neighbors[i];
                if (n != null) {
                    n.neighbors[Dirs.Opp(i)] = null;
                }
            }

            Destroy(chunk.gameObject);
            chunks.Remove(new Vector3i(x, y, z));
        } else {
            Debug.LogWarning("trying to destroy chunk that doesn't exist...");
        }

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
            return containerChunk.GetBlock(x - containerChunk.pos.x, y - containerChunk.pos.y, z - containerChunk.pos.z);
        } else {
            return Blocks.AIR;
        }

    }

    // sets block using world coordinates
    public void SetBlock(int x, int y, int z, Block block) {
        Chunk chunk = GetChunkByWorldPos(x, y, z);

        if (chunk != null) {
            chunk.SetBlock(x - chunk.pos.x, y - chunk.pos.y, z - chunk.pos.z, block);

            // if block is on a chunk edge then update neighbor chunks
            UpdateIfEqual(x - chunk.pos.x, 0, new Vector3i(x - 1, y, z));
            UpdateIfEqual(x - chunk.pos.x, Chunk.SIZE - 1, new Vector3i(x + 1, y, z));
            UpdateIfEqual(y - chunk.pos.y, 0, new Vector3i(x, y - 1, z));
            UpdateIfEqual(y - chunk.pos.y, Chunk.SIZE - 1, new Vector3i(x, y + 1, z));
            UpdateIfEqual(z - chunk.pos.z, 0, new Vector3i(x, y, z - 1));
            UpdateIfEqual(z - chunk.pos.z, Chunk.SIZE - 1, new Vector3i(x, y, z + 1));
        }
    }


    void UpdateIfEqual(int value1, int value2, Vector3i pos) {
        if (value1 == value2) {
            Chunk chunk = GetChunkByWorldPos(pos.x, pos.y, pos.z);
            if (chunk != null) {
                chunk.update = true;
            }
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
