using UnityEngine;
using System.Collections;
using System;

[Serializable]
public struct Block : IEquatable<Block> {

    public byte type;

    // byte light; // prob should be separate array in chunk actually

    public Block(byte type) {
        this.type = type;
    }

    public BlockType GetBlockType() {
        return BlockTypes.GetBlockType(type);
    }

    // returns whether the block has a solid face from this direction
    // consider stairs, bottom and back are solid but other sides aren't completely
    // air is not solid on any sides, also torches because they dont cover any whole face
    public bool IsSolid(Dir dir) {
        return BlockTypes.GetBlockType(type).IsSolid(dir);
    }

    // todo: add way to specify custom collider data for different shapes
    public bool ColliderSolid() {
        return BlockTypes.GetBlockType(type).ColliderSolid();
    }

    public Tile TexturePosition(Dir dir) {
        return BlockTypes.GetBlockType(type).TexturePosition(dir);
    }

    public static bool operator ==(Block a, Block b) {
        return a.type == b.type;
    }
    public static bool operator !=(Block a, Block b) {
        return !(a == b);
    }

    public bool Equals(Block other) {
        return this == other;
    }

    public override bool Equals(object other) {
        if (!(other is Block)) {
            return false;
        }
        return this == (Block)other;
    }

    public override int GetHashCode() {
        return type;
    }

}

// make sure this matches array below
public static class Blocks {
    public static readonly Block AIR = new Block(0);
    public static readonly Block STONE = new Block(1);
    public static readonly Block GRASS = new Block(2);
    public static readonly Block TORCH = new Block(3);
}

public static class BlockTypes {

    private static BlockType[] types = new BlockType[] {
        new BlockAir(),
        new BlockStone(),
        new BlockGrass(),
        new BlockTorch(),
    };

    public static BlockType GetBlockType(int type) {
        return types[type];
    }

}

public abstract class BlockType {

    public virtual bool IsSolid(Dir dir) { // checking if this side is opaque basically
        switch (dir) {
            case Dir.north:
                return true;
            case Dir.east:
                return true;
            case Dir.south:
                return true;
            case Dir.west:
                return true;
            case Dir.up:
                return true;
            case Dir.down:
                return true;
        }
        return false;
    }

    // not sure how to handle above IsSolid cases, seems more for blocks with visually transparent faces but should still have a collider there
    public virtual bool ColliderSolid() {
        return true;
    }

    public virtual Tile TexturePosition(Dir dir) {
        return new Tile() { x = 0, y = 0 };
    }

    public virtual void AddDataNative(int x, int y, int z, NativeMeshData data) {
        if (!data.GetBlock(x + 1, y, z).IsSolid(Dir.west)) {
            FaceDataEastNative(x, y, z, data);
        }
        if (!data.GetBlock(x, y + 1, z).IsSolid(Dir.down)) {
            FaceDataUpNative(x, y, z, data);
        }
        if (!data.GetBlock(x, y, z + 1).IsSolid(Dir.south)) {
            FaceDataNorthNative(x, y, z, data);
        }
        if (!data.GetBlock(x - 1, y, z).IsSolid(Dir.east)) {
            FaceDataWestNative(x, y, z, data);
        }
        if (!data.GetBlock(x, y - 1, z).IsSolid(Dir.up)) {
            FaceDataDownNative(x, y, z, data);
        }
        if (!data.GetBlock(x, y, z - 1).IsSolid(Dir.north)) {
            FaceDataSouthNative(x, y, z, data);
        }
    }


    protected virtual void FaceDataWestNative(int x, int y, int z, NativeMeshData data) {
        data.AddVertex(new Vector3(x, y, z + 1.0f));
        data.AddVertex(new Vector3(x, y + 1.0f, z + 1.0f));
        data.AddVertex(new Vector3(x, y + 1.0f, z));
        data.AddVertex(new Vector3(x, y, z));

        data.AddQuadTriangles();

        data.AddFaceUVs(TexturePosition(Dir.west));
    }
    protected virtual void FaceDataDownNative(int x, int y, int z, NativeMeshData data) {
        data.AddVertex(new Vector3(x, y, z));
        data.AddVertex(new Vector3(x + 1.0f, y, z));
        data.AddVertex(new Vector3(x + 1.0f, y, z + 1.0f));
        data.AddVertex(new Vector3(x, y, z + 1.0f));

        data.AddQuadTriangles();

        data.AddFaceUVs(TexturePosition(Dir.down));
    }
    protected virtual void FaceDataSouthNative(int x, int y, int z, NativeMeshData data) {
        data.AddVertex(new Vector3(x, y, z));
        data.AddVertex(new Vector3(x, y + 1.0f, z));
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z));
        data.AddVertex(new Vector3(x + 1.0f, y, z));

        data.AddQuadTriangles();

        data.AddFaceUVs(TexturePosition(Dir.south));
    }

    protected virtual void FaceDataEastNative(int x, int y, int z, NativeMeshData data) {
        data.AddVertex(new Vector3(x + 1.0f, y, z));
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z));
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
        data.AddVertex(new Vector3(x + 1.0f, y, z + 1.0f));

        data.AddQuadTriangles();

        data.AddFaceUVs(TexturePosition(Dir.east));
    }
    protected virtual void FaceDataUpNative(int x, int y, int z, NativeMeshData data) {
        data.AddVertex(new Vector3(x, y + 1.0f, z + 1.0f));
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z));
        data.AddVertex(new Vector3(x, y + 1.0f, z));

        data.AddQuadTriangles();

        data.AddFaceUVs(TexturePosition(Dir.up));
    }
    protected virtual void FaceDataNorthNative(int x, int y, int z, NativeMeshData data) {
        data.AddVertex(new Vector3(x + 1.0f, y, z + 1.0f));
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
        data.AddVertex(new Vector3(x, y + 1.0f, z + 1.0f));
        data.AddVertex(new Vector3(x, y, z + 1.0f));

        data.AddQuadTriangles();

        data.AddFaceUVs(TexturePosition(Dir.north));
    }

    public virtual void FaceUVsGreedy(Dir dir, MeshData data, int w, int h) {
        Tile tp = TexturePosition(dir);

        // store the offset
        data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
        data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
        data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
        data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);

        // then add width and height in uv2, shader will calculate coordinate from this
        data.uv2.Add(new Vector2(0, 0));
        data.uv2.Add(new Vector2(h, 0));
        data.uv2.Add(new Vector2(0, w));
        data.uv2.Add(new Vector2(h, w));
    }

}


public struct Tile {
    public const float SIZE = 0.25f; // set equal to 1 / number of tiles on sprite sheet 
    public int x;
    public int y;

    public Tile(int x, int y) {
        this.x = x;
        this.y = y;
    }
}

public class BlockStone : BlockType {

    // test smiley texture
    //public override Tile TexturePosition(Dir dir) {
    //    return new Tile(1, 1);
    //}
}
