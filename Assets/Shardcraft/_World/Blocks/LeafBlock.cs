

public class LeafBlock : BlockType {
    public override bool IsSolid(Dir dir) {
        return false;
    }

    public override int GetTextureIndex(Dir dir, int x, int y, int z, NativeMeshData data) {
        return 6;
    }
}