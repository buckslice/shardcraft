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
        return (Dir)Opp((int)dir);
    }

    public static int Opp(int dir) {
        return (dir + 3) % 6;
    }

}


