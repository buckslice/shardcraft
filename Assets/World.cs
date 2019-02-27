using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour {

    public string worldName = "world";

    public GameObject chunkPrefab;

    public Material TileMat;
    public Material TileMatGreedy;

    public Dictionary<Vector3i, Chunk> chunks = new Dictionary<Vector3i, Chunk>();

    public bool loadPlayerSave = false;

    // Use this for initialization
    void Start() {

        if (loadPlayerSave) {
            Serialization.LoadPlayer();
        }
        Dirs.Test();
        Debug.Assert(Chunk.CHUNK_HEIGHT >= Chunk.CHUNK_WIDTH);
    }

    public void OnApplicationQuit() {
        // save all chunks
        foreach (KeyValuePair<Vector3i, Chunk> entry in chunks) {
            Serialization.SaveChunk(entry.Value);
        }

        Serialization.SavePlayer();

    }

    public void CreateChunk(int x, int y, int z) {
        Debug.Assert(x % Chunk.SIZE == 0 && y % Chunk.SIZE == 0 && z % Chunk.SIZE == 0);

        Vector3i worldPos = new Vector3i(x, y, z);

        //Instantiate the chunk at the coordinates using the chunk prefab
        GameObject newChunkObject = Instantiate(chunkPrefab, new Vector3(x, y, z), Quaternion.Euler(Vector3.zero), transform) as GameObject;
        newChunkObject.name = "Chunk " + (worldPos / Chunk.SIZE).ToString();
        Chunk newChunk = newChunkObject.GetComponent<Chunk>();

        newChunk.pos = worldPos;
        newChunk.world = this;

        JobController.StartGenerationJob(newChunk);
        //JobController.StartGenerationTask(newChunk);

        //Add it to the chunks dictionary with the position as the key
        chunks.Add(worldPos, newChunk);

    }

    public void DestroyChunk(int x, int y, int z) {
        Chunk chunk = null;
        if (chunks.TryGetValue(new Vector3i(x, y, z), out chunk)) {
            Serialization.SaveChunk(chunk);
            Destroy(chunk.gameObject);
            chunks.Remove(new Vector3i(x, y, z));
        }
    }

    // gets chunk using world coordinates
    public Chunk GetChunk(int x, int y, int z) {
        chunks.TryGetValue(Chunk.GetChunkPosition(new Vector3(x, y, z)), out Chunk containerChunk);
        return containerChunk;
    }
    public Chunk GetChunk(Vector3i p) {
        chunks.TryGetValue(Chunk.GetChunkPosition(p.ToVector3()), out Chunk containerChunk);
        return containerChunk;
    }

    public Block GetBlock(int x, int y, int z) {
        Chunk containerChunk = GetChunk(x, y, z);

        if (containerChunk != null) {
            Block block = containerChunk.GetBlock(x - containerChunk.pos.x, y - containerChunk.pos.y, z - containerChunk.pos.z);
            return block;
        } else {
            return Blocks.AIR;
        }

    }

    // sets block using world coordinates
    public void SetBlock(int x, int y, int z, Block block) {
        Chunk chunk = GetChunk(x, y, z);

        if (chunk != null) {
            chunk.SetBlock(x - chunk.pos.x, y - chunk.pos.y, z - chunk.pos.z, block);
            chunk.update = true;

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
            Chunk chunk = GetChunk(pos.x, pos.y, pos.z);
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
