using System.Collections;
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

        string msg = string.Format("{0}/{1} tests passed", passes, passes + failures);
        if (failures == 0) {
            Debug.Log(string.Format("<color=#00FF00><b>{0}</b></color>", msg));
        } else {
            Debug.Log(msg);
        }
    }
}
