using UnityEngine;
using System.Collections;

public static class WorldUtils {


    public static Vector3i GetBlockPos(Vector3 pos) {
        //return new Vector3i(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y), Mathf.RoundToInt(pos.z));
        //return new Vector3i((int)pos.x, (int)pos.y, (int)pos.z); // THIS TRUNCATES but we need FLOOORING REEEEEEEEEEEE
        return new Vector3i((int)Mathf.Floor(pos.x), (int)Mathf.Floor(pos.y), (int)Mathf.Floor(pos.z));
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
}