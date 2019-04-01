

public class GrassBlock : BlockType {
    public override bool IsSolid(Dir dir) {
        return true;
    }

    public override int GetTextureIndex(Dir dir, int x, int y, int z, ref NativeArray3x3<Block> blocks) {
        switch (dir) {
            case Dir.up:
                return 2;
            case Dir.down:
                return 1;
        }

        if (blocks.Get(x, y + 1, z) != Blocks.AIR) {
            return 1;
        }

        switch (dir) {
            case Dir.west:
                if (blocks.Get(x - 1, y - 1, z) == Blocks.GRASS) {
                    return 2;
                }
                break;
            case Dir.east:
                if (blocks.Get(x + 1, y - 1, z) == Blocks.GRASS) {
                    return 2;
                }
                break;
            case Dir.south:
                if (blocks.Get(x, y - 1, z - 1) == Blocks.GRASS) {
                    return 2;
                }
                break;
            case Dir.north:
                if (blocks.Get(x, y - 1, z + 1) == Blocks.GRASS) {
                    return 2;
                }
                break;
        }

        return 3;
    }
}