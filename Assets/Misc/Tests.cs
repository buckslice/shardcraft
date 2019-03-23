﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Tests {

    static int passes = 0;
    static int failures = 0;

    static void TestEqual(object a, object b, string name) {
        if (!a.Equals(b)) {
            Debug.Log(string.Format("<color=#AA0000><b>Test '{0}' Failed</b></color>, {1} != {2}", name, a, b));
            failures++;
        } else {
            passes++;
        }

    }


    public static void Run() {

        passes = 0;
        failures = 0;

        {
            Vector3i v = new Vector3i(5, 0, 11);
            int i = Chunk.CoordToUint(v.x, v.y, v.z);
            Vector3i v2 = Chunk.IntToCoord(i);
            TestEqual(v, v2, "Int2Coord");

            v = new Vector3i(0, 0, 0);
            i = Chunk.CoordToUint(v.x, v.y, v.z);
            v2 = Chunk.IntToCoord(i);
            TestEqual(v, v2, "Int2Coord2");

        }

        {
            TestEqual(Dirs.Opp(Dir.west), Dir.east, "DirTest1");
            TestEqual(Dirs.Opp(Dir.down), Dir.up, "DirTest2");
            TestEqual(Dirs.Opp(Dir.south), Dir.north, "DirTest3");
            TestEqual(Dirs.Opp(Dir.east), Dir.west, "DirTest4");
            TestEqual(Dirs.Opp(Dir.up), Dir.down, "DirTest5");
            TestEqual(Dirs.Opp(Dir.north), Dir.south, "DirTest6");
        }

        {
            TestEqual(Serialization.GetRegionCoord(new Vector3i(0, 0, 0)), new Vector3i(0, 0, 0), "RegionCoordTest1");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(1, 0, 0)), new Vector3i(0, 0, 0), "RegionCoordTest2");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(5, 5, 5)), new Vector3i(0, 0, 0), "RegionCoordTest3");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(15, 15, 15)), new Vector3i(0, 0, 0), "RegionCoordTest4");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(16, 16, 16)), new Vector3i(1, 1, 1), "RegionCoordTest5");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(18, 5, 5)), new Vector3i(1, 0, 0), "RegionCoordTest6");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(-1, 0, 0)), new Vector3i(-1, 0, 0), "RegionCoordTest7");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(0, -1, 0)), new Vector3i(0, -1, 0), "RegionCoordTest8");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(0, 0, -1)), new Vector3i(0, 0, -1), "RegionCoordTest9");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(0, 0, -1)), new Vector3i(0, 0, -1), "RegionCoordTest10");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(14, 15, 15)), new Vector3i(0, 0, 0), "RegionCoordTest11");
            TestEqual(Serialization.GetRegionCoord(new Vector3i(17, 15, 15)), new Vector3i(1, 0, 0), "RegionCoordTest12");
        }

        {
            TestEqual(Chunk.GetChunkPosition(new Vector3(0, 0, 0)), new Vector3i(0, 0, 0), "ChunkPos1");
            TestEqual(Chunk.GetChunkPosition(new Vector3(-1.0f, 0, 0)), new Vector3i(-1, 0, 0), "ChunkPos2");
            //TestEqual(Chunk.GetChunkPosition(new Vector3(-16.01f, 0, 0)), new Vector3i(-2, 0, 0), "ChunkPos3");
            //TestEqual(Chunk.GetChunkPosition(new Vector3(25, 35, -5)), new Vector3i(1, 2, -1), "ChunkPos4");

        }

        string msg = string.Format("{0}/{1} tests passed", passes, passes + failures);
        if (failures == 0) {
            Debug.Log(string.Format("<color=#00FF00><b>{0}</b></color>", msg));
        } else {
            Debug.Log(msg);
        }
    }
}
