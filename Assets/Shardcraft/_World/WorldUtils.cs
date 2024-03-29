﻿using UnityEngine;
using System.Collections;

public static class WorldUtils {

    // given a position in world space get the world space block coordinates
    public static Vector3i GetBlockPos(Vector3 worldPos) {
        //return new Vector3i(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y), Mathf.RoundToInt(pos.z));
        //return new Vector3i((int)pos.x, (int)pos.y, (int)pos.z); // THIS TRUNCATES but we need FLOOORING REEEEEEEEEEEE
        worldPos *= Chunk.BPU;
        Vector3i v;
        v.x = Mathf.FloorToInt(worldPos.x);
        v.y = Mathf.FloorToInt(worldPos.y);
        v.z = Mathf.FloorToInt(worldPos.z);
        return v;
    }

    // gets chunk coordinate using world block coordinates
    public static Vector3i GetChunkPosFromBlockPos(int x, int y, int z) {
        Vector3i v;
        v.x = x >> 5;
        v.y = y >> 5;
        v.z = z >> 5;
        return v;
    }

    // regions are 16x16x16 chunks
    public static Vector3i GetRegionCoord(Vector3i cp) { // from chunk position
        Vector3i v;
        v.x = cp.x >> 4;
        v.y = cp.y >> 4;
        v.z = cp.z >> 4;
        return v;
    }

    // returns the chunk coord based on world position
    public static Vector3i GetChunkPosFromWorldPos(Vector3 worldPos) {
        worldPos *= Chunk.BPU;
        Vector3i v;
        v.x = Mathf.FloorToInt(worldPos.x / Chunk.SIZE);
        v.y = Mathf.FloorToInt(worldPos.y / Chunk.SIZE);
        v.z = Mathf.FloorToInt(worldPos.z / Chunk.SIZE);
        return v;
    }

    // raycast hit will usually be right on edge between two blocks so move point in or out a little
    // depending on if you want block you hit or adjacent block
    public static Vector3i GetBlockPos(RaycastHit hit, bool adjacent = false) {
        Vector3 p = hit.point;
        if (adjacent) {
            p += hit.normal / 10.0f;
        } else {
            p -= hit.normal / 10.0f;
        }
        return GetBlockPos(p);
    }

    public static bool SetBlock(World world, RaycastHit hit, Block block, bool adjacent = false) {
        if (!hit.collider.CompareTag(Tags.Terrain)) {
            return false;
        }

        Vector3i bp = GetBlockPos(hit, adjacent);

        world.SetBlock(bp.x, bp.y, bp.z, block);

        return true;
    }

    public static Block GetBlock(World world, RaycastHit hit, bool adjacent = false) {
        if (!hit.collider.CompareTag(Tags.Terrain)) {
            return Blocks.AIR;
        }

        Vector3i bp = GetBlockPos(hit, adjacent);

        return world.GetBlock(bp.x, bp.y, bp.z);

    }

    public static void FillChunk(World world, Vector3i chunkPos, Block toFill) {
        Chunk c = world.GetChunk(chunkPos);
        if (c == null) {
            Debug.Log("no chunk here");
            return;
        }

        for (int y = 0; y < Chunk.SIZE; ++y) {
            for (int z = 0; z < Chunk.SIZE; ++z) {
                for (int x = 0; x < Chunk.SIZE; ++x) {
                    c.SetBlock(x, y, z, toFill);
                }
            }
        }

    }

    public static void CheckerboardChunk(World world, Vector3i chunkPos, Block toFill) {
        Chunk c = world.GetChunk(chunkPos);
        if (c == null) {
            Debug.Log("no chunk here");
            return;
        }

        for (int y = 0; y < Chunk.SIZE; ++y) {
            for (int z = 0; z < Chunk.SIZE; ++z) {
                for (int x = 0; x < Chunk.SIZE; ++x) {
                    // checkerboard
                    if ((x + y + z) % 2 == 0) {
                        c.SetBlock(x, y, z, toFill);
                    } else {
                        c.SetBlock(x, y, z, Blocks.AIR);
                    }
                }
            }
        }

    }

}

public struct BlockEdit {
    public int x; // local coordinates of block in chunk
    public int y;
    public int z;
    public Block block; // block to set there
}
