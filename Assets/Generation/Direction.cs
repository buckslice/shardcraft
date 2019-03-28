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

    public const int WEST = 0;
    public const int DOWN = 1;
    public const int SOUTH = 2;
    public const int EAST = 3;
    public const int UP = 4;
    public const int NORTH = 5;

    public static Dir Opp(Dir dir) {
        return (Dir)Opp((int)dir);
    }

    public static int Opp(int dir) {
        return (dir + 3) % 6;
    }

}


