using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// the ordering matters i think so dont change
public enum Dir : byte {
    west = 0,   //back faces
    down = 1,
    south = 2,
    east = 3,   //front faces
    up = 4,
    north = 5,
};


public static class Dirs {
    public static Dir Opp(Dir dir) {
        return (Dir)(((int)dir + 3) % 6);
    }

    public static void Test() {
        Debug.Assert(Opp(Dir.west) == Dir.east);
        Debug.Assert(Opp(Dir.down) == Dir.up);
        Debug.Assert(Opp(Dir.south) == Dir.north);
        Debug.Assert(Opp(Dir.east) == Dir.west);
        Debug.Assert(Opp(Dir.up) == Dir.down);
        Debug.Assert(Opp(Dir.north) == Dir.south);
    }
}


