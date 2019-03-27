﻿using System.Collections;
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

    public Queue<Chunk> destroyQueue = new Queue<Chunk>();

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
        Vector3i worldPos = chunkPos * Chunk.SIZE / 2;

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

    // adds to queue to destroy it when proper time
    public void DestroyChunk(Vector3i cp) {
        Chunk chunk = GetChunk(cp);
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
            if (chunk.IsDataLocked()) {
                destroyQueue.Enqueue(chunk); // put it back and try next time
            } else { // destroy the chunk
                // notify neighbors
                for (int i = 0; i < 6; ++i) {
                    Chunk n = chunk.neighbors[i];
                    if (n != null) {
                        n.neighbors[Dirs.Opp(i)] = null;
                    }
                }

                chunk.gameObject.SetActive(false);
                chunk.ClearMeshes();
                chunks.Remove(chunk.cp);
                Serialization.SaveChunk(chunk);
                destroyed++;
            }
        }

        if (destroyed > 0) {
            Debug.Log("destroyed: " + destroyed);
        }
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

    // gets chunk using chunk coords
    public Chunk GetChunk(int x, int y, int z) {
        chunks.TryGetValue(new Vector3i(x, y, z), out Chunk chunk);
        return chunk;
    }
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
            return containerChunk.GetBlock(x - containerChunk.wp.x, y - containerChunk.wp.y, z - containerChunk.wp.z);
        } else {
            return Blocks.AIR;
        }

    }

    // sets block using world block coordinates (as opposed to local/chunk block coordinates)
    public void SetBlock(int x, int y, int z, Block block) {
        Chunk chunk = GetChunkByWorldBlockPos(x, y, z);

        if (chunk != null) {
            chunk.SetBlock(x - chunk.wp.x * Chunk.BPU, y - chunk.wp.y * Chunk.BPU, z - chunk.wp.z * Chunk.BPU, block);
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
