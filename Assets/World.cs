using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour {

    public string worldName = "world";

    public GameObject chunkPrefab;

    public Dictionary<Vector3i, Chunk> chunks = new Dictionary<Vector3i, Chunk>();

    // Use this for initialization
    void Start() {
        for (int x = -4; x < 4; x++) {
            for (int y = -1; y < 3; y++) {
                for (int z = -4; z < 4; z++) {
                    CreateChunk(x * 16, y * 16, z * 16);
                }
            }
        }

        Serialization.LoadPlayer();
    }

    // Update is called once per frame
    void Update() {

    }

    public void OnApplicationQuit() {
        // save all chunks
        foreach(KeyValuePair<Vector3i, Chunk> entry in chunks) {
            Serialization.SaveChunk(entry.Value);
        }

        Serialization.SavePlayer();

    }

    public void CreateChunk(int x, int y, int z) {
        Vector3i worldPos = new Vector3i(x, y, z);

        //Instantiate the chunk at the coordinates using the chunk prefab
        GameObject newChunkObject = Instantiate(chunkPrefab, new Vector3(x, y, z), Quaternion.Euler(Vector3.zero)) as GameObject;

        Chunk newChunk = newChunkObject.GetComponent<Chunk>();

        newChunk.pos = worldPos;
        newChunk.world = this;

        //Add it to the chunks dictionary with the position as the key
        chunks.Add(worldPos, newChunk);

        WorldGenerator.Generate(newChunk);

        // could also set unmodified after loading and then if theres no new modified blocks
        // leave save file alone
        newChunk.SetBlocksUnmodified();

        Serialization.LoadChunk(newChunk);
    }

    public void DestroyChunk(int x, int y, int z) {
        Chunk chunk = null;
        if (chunks.TryGetValue(new Vector3i(x, y, z), out chunk)) {
            Serialization.SaveChunk(chunk);
            Object.Destroy(chunk.gameObject);
            chunks.Remove(new Vector3i(x, y, z));
        }
    }

    public Chunk GetChunk(int x, int y, int z) {
        Vector3i pos = new Vector3i();
        float fSIZE = Chunk.SIZE;
        pos.x = Mathf.FloorToInt(x / fSIZE) * Chunk.SIZE;
        pos.y = Mathf.FloorToInt(y / fSIZE) * Chunk.SIZE;
        pos.z = Mathf.FloorToInt(z / fSIZE) * Chunk.SIZE;

        chunks.TryGetValue(pos, out Chunk containerChunk);

        return containerChunk;
    }

    public Block GetBlock(int x, int y, int z) {
        Chunk containerChunk = GetChunk(x, y, z);

        if (containerChunk != null) {
            Block block = containerChunk.GetBlock(x - containerChunk.pos.x, y - containerChunk.pos.y, z - containerChunk.pos.z);
            return block;
        } else {
            return new BlockAir();
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



}
