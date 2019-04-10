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
    none = 6,
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

    public static readonly Vector3i[] norm3i = new Vector3i[] {
        new Vector3i(-1,0,0),
        new Vector3i(0,-1,0),
        new Vector3i(0,0,-1),
        new Vector3i(1,0,0),
        new Vector3i(0,1,0),
        new Vector3i(0,0,1),
        new Vector3i(0,0,0),
    };

    public static readonly Vector3[] norm3f = new Vector3[] {
        new Vector3(-1,0,0),
        new Vector3(0,-1,0),
        new Vector3(0,0,-1),
        new Vector3(1,0,0),
        new Vector3(0,1,0),
        new Vector3(0,0,1),
        new Vector3(0,0,0),
    };

    public static Vector3i GetNormal(Dir dir) {
        return norm3i[(int)dir];
    }

    public static int Opp(int dir) {
        return (dir + 3) % 6;
    }

}


