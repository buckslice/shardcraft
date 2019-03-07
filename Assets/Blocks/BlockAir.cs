using UnityEngine;
using System.Collections;
using System;

public class BlockAir : BlockType {

    public override bool IsSolid(Dir dir) {
        return false;
    }

    public override void AddData(Chunk chunk, int x, int y, int z, MeshData meshData) {
    }

    public override bool ColliderSolid() {
        return false;
    }

}