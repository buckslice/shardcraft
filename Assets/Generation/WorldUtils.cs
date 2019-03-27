using UnityEngine;
using System.Collections;

public static class WorldUtils {

    // given a position in world space get the world space block coordinates
    public static Vector3i GetBlockPos(Vector3 pos) {
        //return new Vector3i(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y), Mathf.RoundToInt(pos.z));
        //return new Vector3i((int)pos.x, (int)pos.y, (int)pos.z); // THIS TRUNCATES but we need FLOOORING REEEEEEEEEEEE
        return new Vector3i(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
    }

    public static Vector3i GetBlockPos(RaycastHit hit, bool adjacent = false) {
        Vector3 p = hit.point;
        if (adjacent) {
            p += hit.normal / 10.0f;
        } else {
            p -= hit.normal / 10.0f;
        }
        return GetBlockPos(p);
    }

    // raycast hit will usually be right on edge between two blocks so move point in or out
    // depending on if you want block you hit or adjacent block
    static float MoveWithinBlock(float pos, float norm, bool adjacent = false) {
        if (adjacent) {
            pos += (norm / 2);
        } else {
            pos -= (norm / 2);
        }

        return pos;
    }

    public static bool SetBlock(World world, RaycastHit hit, Block block, bool adjacent = false) {
        if (!hit.collider.CompareTag(Tags.Terrain)) {
            return false;
        }

        Vector3i pos = GetBlockPos(hit, adjacent);

        world.SetBlock(pos.x, pos.y, pos.z, block);

        return true;
    }

    public static Block GetBlock(World world, RaycastHit hit, bool adjacent = false) {
        if (!hit.collider.CompareTag(Tags.Terrain)) {
            return Blocks.AIR;
        }

        Vector3i pos = GetBlockPos(hit, adjacent);

        Block block = world.GetBlock(pos.x, pos.y, pos.z);

        return block;
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