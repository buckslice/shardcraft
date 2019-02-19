using UnityEngine;
using System.Collections;
using System;

[Serializable]
public class BlockAir : Block {
    public BlockAir() : base() {

    }

    public override void AddData(Chunk chunk, int x, int y, int z, MeshData meshData) {
    }

    public override bool IsSolid(Block.Dir direction) {
        return false;
    }
}