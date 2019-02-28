﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

[Serializable]
public class Save {

    public Dictionary<Vector3i, Block> blocks = new Dictionary<Vector3i, Block>();

    public Save(Chunk chunk) {
        for (int x = 0; x < Chunk.SIZE; x++) {
            for (int y = 0; y < Chunk.SIZE; y++) {
                for (int z = 0; z < Chunk.SIZE; z++) {
                    if (chunk.blocks[x, y, z].changed == 0)
                        continue;

                    Vector3i pos = new Vector3i(x, y, z);
                    blocks.Add(pos, chunk.blocks[x, y, z]);
                }
            }
        }
    }

}
