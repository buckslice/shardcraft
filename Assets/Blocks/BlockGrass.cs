using UnityEngine;
using System.Collections;
using System;

[Serializable]
public class BlockGrass : Block {

    public BlockGrass() : base() {

    }

    public override Tile TexturePosition(Dir direction) {
        Tile tile = new Tile();

        switch (direction) {
            case Dir.up:
                tile.x = 2;
                tile.y = 0;
                return tile;
            case Dir.down:
                tile.x = 1;
                tile.y = 0;
                return tile;
        }

        tile.x = 3;
        tile.y = 0;

        return tile;
    }
}