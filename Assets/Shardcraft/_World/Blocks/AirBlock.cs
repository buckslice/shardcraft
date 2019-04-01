using UnityEngine;
using System.Collections;
using System;

public class AirBlock : BlockType {

    public override bool IsSolid(Dir dir) {
        return false;
    }

    public override void AddDataNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<byte> light) {
    }

    public override bool ColliderSolid() {
        return false;
    }

}