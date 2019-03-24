using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkPool {

    List<Chunk> chunks = new List<Chunk>();

    int free = 0;

    Func<Chunk> buildChunkFunction;

    public ChunkPool(Func<Chunk> f) {
        buildChunkFunction = f;
    }

    public void Return(Chunk chunk) {
        if (free <= 0) {
            throw new InvalidOperationException();
        }
        chunks[--free] = chunk;
    }

    public Chunk Get() {
        if (free >= chunks.Count) {
            chunks.Add(buildChunkFunction());
        }
        return chunks[free++];
    }

    public void DisposeChunks() {
        for (int i = 0; i < chunks.Count; ++i) {
            chunks[i].blocks.Dispose();
        }
    }

    public int Count() {
        return chunks.Count;
    }
    public int CountFree() {
        return chunks.Count - free;
    }

}
