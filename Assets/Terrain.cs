﻿using UnityEngine;
using System.Collections;

public static class Terrain {


    public static Vector3i GetBlockPos(Vector3 pos) {
        return new Vector3i(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y), Mathf.RoundToInt(pos.z));
    }

    public static Vector3i GetBlockPos(RaycastHit hit, bool adjacent = false) {
        Vector3 pos = new Vector3(
            MoveWithinBlock(hit.point.x, hit.normal.x, adjacent),
            MoveWithinBlock(hit.point.y, hit.normal.y, adjacent),
            MoveWithinBlock(hit.point.z, hit.normal.z, adjacent)
        );

        return GetBlockPos(pos);
    }

    // raycast hit will usually be right on edge between two blocks so move point in or out
    // depending on if you want block you hit or adjacent block
    static float MoveWithinBlock(float pos, float norm, bool adjacent = false) {
        if (pos - (int)pos == 0.5f || pos - (int)pos == -0.5f) {
            if (adjacent) {
                pos += (norm / 2);
            } else {
                pos -= (norm / 2);
            }
        }

        return (float)pos;
    }

    public static bool SetBlock(RaycastHit hit, Block block, bool adjacent = false) {
        Chunk chunk = hit.collider.GetComponent<Chunk>();
        if (chunk == null) {
            return false;
        }

        Vector3i pos = GetBlockPos(hit, adjacent);

        chunk.world.SetBlock(pos.x, pos.y, pos.z, block);

        return true;
    }

    public static Block GetBlock(RaycastHit hit, bool adjacent = false) {
        Chunk chunk = hit.collider.GetComponent<Chunk>();
        if (chunk == null)
            return null;

        Vector3i pos = GetBlockPos(hit, adjacent);

        Block block = chunk.world.GetBlock(pos.x, pos.y, pos.z);

        return block;
    }
}