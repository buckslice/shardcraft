

public class BlockGrass : BlockType {
    public override bool IsSolid(Dir dir) {
        return true;
    }

    public override Tile TexturePosition(Dir dir) {
        Tile tile = new Tile();

        switch (dir) {
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