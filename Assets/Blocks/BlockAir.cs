﻿using UnityEngine;
using System.Collections;
using System;

public class BlockAir : BlockType {

    public override bool IsSolid(Dir dir) {
        return false;
    }

    public override void AddDataNative(int x, int y, int z, NativeMeshData data) {
    }

    public override bool ColliderSolid() {
        return false;
    }

}