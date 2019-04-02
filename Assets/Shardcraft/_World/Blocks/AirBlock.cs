using UnityEngine;
using System.Collections;
using System;
using Unity.Collections;

public class AirBlock : BlockType {

    public override bool IsSolid(Dir dir) {
        return false;
    }

    public override void AddDataNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> light, NativeList<Face> faces) {
    }

    public override bool ColliderSolid() {
        return false;
    }

}