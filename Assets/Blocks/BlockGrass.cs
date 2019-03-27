

public class BlockGrass : BlockType {
    public override bool IsSolid(Dir dir) {
        return true;
    }

    public override Tile TexturePosition(Dir dir, int x, int y, int z, NativeMeshData data) {
        switch (dir) {
            case Dir.up:
                return new Tile { x = 2, y = 0 };
            case Dir.down:
                return new Tile { x = 1, y = 0 };
        }

        if (data.GetBlock(x, y + 1, z) != Blocks.AIR) {
            return new Tile { x = 1, y = 0 };
        }

        if (y == 0) {   // only have 6 neighbors, this would need more southern neighbors
            return new Tile { x = 3, y = 0 };
        }

        switch (dir) {
            case Dir.west:
                if (data.GetBlock(x - 1, y - 1, z) == Blocks.GRASS) {
                    return new Tile { x = 2, y = 0 };
                }
                break;
            case Dir.east:
                if (data.GetBlock(x + 1, y - 1, z) == Blocks.GRASS) {
                    return new Tile { x = 2, y = 0 };
                }
                break;
            case Dir.south:
                if (data.GetBlock(x, y - 1, z - 1) == Blocks.GRASS) {
                    return new Tile { x = 2, y = 0 };
                }
                break;
            case Dir.north:
                if (data.GetBlock(x, y - 1, z + 1) == Blocks.GRASS) {
                    return new Tile { x = 2, y = 0 };
                }
                break;
        }

        return new Tile { x = 3, y = 0 };
    }
}