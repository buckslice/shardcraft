using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

[Serializable]
public class ChunkSave {

    // TODO switch to manual serialization instead of using this
    public Dictionary<ushort, Block> blocks = new Dictionary<ushort, Block>();

    public ChunkSave(Chunk chunk) {

        foreach (ushort i in chunk.modifiedBlockIndices) {
            blocks.Add(i, chunk.blocks[i]);
        }

    }

}
