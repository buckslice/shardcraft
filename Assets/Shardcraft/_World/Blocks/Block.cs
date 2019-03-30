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
    public static readonly Block BIRCH = new Block(3);
    public static readonly Block LEAF = new Block(4);

    //public static readonly Block TORCH = new Block();
}

public static class BlockTypes {

    private static BlockType[] types = new BlockType[] {
        new AirBlock(),
        new StoneBlock(),
        new GrassBlock(),
        new BirchBlock(),
        new LeafBlock(),
        //new BlockTorch(),
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

    //public virtual Tile TexturePosition(Dir dir, int x, int y, int z, NativeMeshData data) {
    //    return new Tile() { x = 0, y = 0 };
    //}

    public virtual int GetTextureIndex(Dir dir, int x, int y, int z, NativeMeshData data) {
        return 0;
    }

    public virtual void AddDataNative(int x, int y, int z, NativeMeshData data) {
        if (!data.GetBlock(x - 1, y, z).IsSolid(Dir.east)) {
            FaceDataWestNative(x, y, z, data);
        }
        if (!data.GetBlock(x, y - 1, z).IsSolid(Dir.up)) {
            FaceDataDownNative(x, y, z, data);
        }
        if (!data.GetBlock(x, y, z - 1).IsSolid(Dir.north)) {
            FaceDataSouthNative(x, y, z, data);
        }
        if (!data.GetBlock(x + 1, y, z).IsSolid(Dir.west)) {
            FaceDataEastNative(x, y, z, data);
        }
        if (!data.GetBlock(x, y + 1, z).IsSolid(Dir.down)) {
            FaceDataUpNative(x, y, z, data);
        }
        if (!data.GetBlock(x, y, z + 1).IsSolid(Dir.south)) {
            FaceDataNorthNative(x, y, z, data);
        }
    }

    static Color32 GetColorFromLight(byte light) {
        float fl = (float)light / LightCalculator.MAX_LIGHT;
        return new Color(fl, fl, fl, 1.0f);
    }

    static float calcAO(int side1, int side2, NativeMeshData data, int c1, int c2, int c3) {
        if (side1 + side2 == 2) {
            return 0.2f;
        }
        return (3.0f - side1 - side2 - GetOpacity(data, c1, c2, c3)) / 3.0f * 0.8f + 0.2f;
    }

    static int GetOpacity(NativeMeshData data, int x, int y, int z) {
        return data.GetBlock(x, y, z) != Blocks.AIR ? 1 : 0;
    }

    protected virtual void FaceDataWestNative(int x, int y, int z, NativeMeshData data) {
        Color c = GetColorFromLight(data.GetLight(x - 1, y, z));

        int up = GetOpacity(data, x - 1, y + 1, z);
        int down = GetOpacity(data, x - 1, y - 1, z);
        int north = GetOpacity(data, x - 1, y, z + 1);
        int south = GetOpacity(data, x - 1, y, z - 1);

        float a0 = calcAO(down, north, data, x - 1, y - 1, z + 1);
        float a1 = calcAO(up, north, data, x - 1, y + 1, z + 1);
        float a2 = calcAO(up, south, data, x - 1, y + 1, z - 1);
        float a3 = calcAO(down, south, data, x - 1, y - 1, z - 1);

        c.a = a0;
        data.AddVertex(new Vector3(x, y, z + 1.0f) / Chunk.BPU, c);
        c.a = a1;
        data.AddVertex(new Vector3(x, y + 1.0f, z + 1.0f) / Chunk.BPU, c);
        c.a = a2;
        data.AddVertex(new Vector3(x, y + 1.0f, z) / Chunk.BPU, c);
        c.a = a3;
        data.AddVertex(new Vector3(x, y, z) / Chunk.BPU, c);

        // do anisotropy flip
        if (a0 + a2 > a1 + a3) {
            data.AddQuadTriangles();
        } else {
            data.AddFlippedQuadTriangles();
        }

        data.AddFaceUVs(GetTextureIndex(Dir.west, x, y, z, data));
    }
    protected virtual void FaceDataDownNative(int x, int y, int z, NativeMeshData data) {
        Color c = GetColorFromLight(data.GetLight(x, y - 1, z));

        int north = GetOpacity(data, x, y - 1, z + 1);
        int south = GetOpacity(data, x, y - 1, z - 1);
        int east = GetOpacity(data, x + 1, y - 1, z);
        int west = GetOpacity(data, x - 1, y - 1, z);

        float a0 = calcAO(south, west, data, x - 1, y - 1, z - 1);
        float a1 = calcAO(south, east, data, x + 1, y - 1, z - 1);
        float a2 = calcAO(north, east, data, x + 1, y - 1, z + 1);
        float a3 = calcAO(north, west, data, x - 1, y - 1, z + 1);

        c.a = a0;
        data.AddVertex(new Vector3(x, y, z) / Chunk.BPU, c);
        c.a = a1;
        data.AddVertex(new Vector3(x + 1.0f, y, z) / Chunk.BPU, c);
        c.a = a2;
        data.AddVertex(new Vector3(x + 1.0f, y, z + 1.0f) / Chunk.BPU, c);
        c.a = a3;
        data.AddVertex(new Vector3(x, y, z + 1.0f) / Chunk.BPU, c);

        // do anisotropy flip
        if (a0 + a2 > a1 + a3) {
            data.AddQuadTriangles();
        } else {
            data.AddFlippedQuadTriangles();
        }

        data.AddFaceUVs(GetTextureIndex(Dir.down, x, y, z, data));
    }
    protected virtual void FaceDataSouthNative(int x, int y, int z, NativeMeshData data) {
        Color c = GetColorFromLight(data.GetLight(x, y, z - 1));

        int up = GetOpacity(data, x, y + 1, z - 1);
        int down = GetOpacity(data, x, y - 1, z - 1);
        int east = GetOpacity(data, x + 1, y, z - 1);
        int west = GetOpacity(data, x - 1, y, z - 1);

        float a0 = calcAO(down, west, data, x - 1, y - 1, z - 1);
        float a1 = calcAO(up, west, data, x - 1, y + 1, z - 1);
        float a2 = calcAO(up, east, data, x + 1, y + 1, z - 1);
        float a3 = calcAO(down, east, data, x + 1, y - 1, z - 1);

        c.a = a0;
        data.AddVertex(new Vector3(x, y, z) / Chunk.BPU, c);
        c.a = a1;
        data.AddVertex(new Vector3(x, y + 1.0f, z) / Chunk.BPU, c);
        c.a = a2;
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z) / Chunk.BPU, c);
        c.a = a3;
        data.AddVertex(new Vector3(x + 1.0f, y, z) / Chunk.BPU, c);

        // do anisotropy flip
        if (a0 + a2 > a1 + a3) {
            data.AddQuadTriangles();
        } else {
            data.AddFlippedQuadTriangles();
        }

        data.AddFaceUVs(GetTextureIndex(Dir.south, x, y, z, data));
    }

    protected virtual void FaceDataEastNative(int x, int y, int z, NativeMeshData data) {
        Color c = GetColorFromLight(data.GetLight(x + 1, y, z));

        int up = GetOpacity(data, x + 1, y + 1, z);
        int down = GetOpacity(data, x + 1, y - 1, z);
        int north = GetOpacity(data, x + 1, y, z + 1);
        int south = GetOpacity(data, x + 1, y, z - 1);

        float a0 = calcAO(down, south, data, x + 1, y - 1, z - 1);
        float a1 = calcAO(up, south, data, x + 1, y + 1, z - 1);
        float a2 = calcAO(up, north, data, x + 1, y + 1, z + 1);
        float a3 = calcAO(down, north, data, x + 1, y - 1, z + 1);

        c.a = a0;
        data.AddVertex(new Vector3(x + 1.0f, y, z) / Chunk.BPU, c);
        c.a = a1;
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z) / Chunk.BPU, c);
        c.a = a2;
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f) / Chunk.BPU, c);
        c.a = a3;
        data.AddVertex(new Vector3(x + 1.0f, y, z + 1.0f) / Chunk.BPU, c);

        // do anisotropy flip
        if (a0 + a2 > a1 + a3) {
            data.AddQuadTriangles();
        } else {
            data.AddFlippedQuadTriangles();
        }

        data.AddFaceUVs(GetTextureIndex(Dir.east, x, y, z, data));
    }
    protected virtual void FaceDataUpNative(int x, int y, int z, NativeMeshData data) {
        Color c = GetColorFromLight(data.GetLight(x, y + 1, z));

        int north = GetOpacity(data, x, y + 1, z + 1);
        int south = GetOpacity(data, x, y + 1, z - 1);
        int east = GetOpacity(data, x + 1, y + 1, z);
        int west = GetOpacity(data, x - 1, y + 1, z);

        float a0 = calcAO(north, west, data, x - 1, y + 1, z + 1);
        float a1 = calcAO(north, east, data, x + 1, y + 1, z + 1);
        float a2 = calcAO(south, east, data, x + 1, y + 1, z - 1);
        float a3 = calcAO(south, west, data, x - 1, y + 1, z - 1);

        c.a = a0;
        data.AddVertex(new Vector3(x, y + 1.0f, z + 1.0f) / Chunk.BPU, c);
        c.a = a1;
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f) / Chunk.BPU, c);
        c.a = a2;
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z) / Chunk.BPU, c);
        c.a = a3;
        data.AddVertex(new Vector3(x, y + 1.0f, z) / Chunk.BPU, c);

        // do anisotropy flip
        if (a0 + a2 > a1 + a3) {
            data.AddQuadTriangles();
        } else {
            data.AddFlippedQuadTriangles();
        }

        data.AddFaceUVs(GetTextureIndex(Dir.up, x, y, z, data));
    }
    protected virtual void FaceDataNorthNative(int x, int y, int z, NativeMeshData data) {
        Color c = GetColorFromLight(data.GetLight(x, y, z + 1));

        int up = GetOpacity(data, x, y + 1, z + 1);
        int down = GetOpacity(data, x, y - 1, z + 1);
        int east = GetOpacity(data, x + 1, y, z + 1);
        int west = GetOpacity(data, x - 1, y, z + 1);

        float a0 = calcAO(down, east, data, x + 1, y - 1, z + 1);
        float a1 = calcAO(up, east, data, x + 1, y + 1, z + 1);
        float a2 = calcAO(up, west, data, x - 1, y + 1, z + 1);
        float a3 = calcAO(down, west, data, x - 1, y - 1, z + 1);

        c.a = a0;
        data.AddVertex(new Vector3(x + 1.0f, y, z + 1.0f) / Chunk.BPU, c);
        c.a = a1;
        data.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f) / Chunk.BPU, c);
        c.a = a2;
        data.AddVertex(new Vector3(x, y + 1.0f, z + 1.0f) / Chunk.BPU, c);
        c.a = a3;
        data.AddVertex(new Vector3(x, y, z + 1.0f) / Chunk.BPU, c);

        // do anisotropy flip
        if (a0 + a2 > a1 + a3) {
            data.AddQuadTriangles();
        } else {
            data.AddFlippedQuadTriangles();
        }

        data.AddFaceUVs(GetTextureIndex(Dir.north, x, y, z, data));
    }

    // todo: translate to native array job option
    //public virtual void FaceUVsGreedy(Dir dir, MeshData data, int w, int h) {
    //    Tile tp = TexturePosition(dir, data);

    //    // store the offset
    //    data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
    //    data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
    //    data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
    //    data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);

    //    // then add width and height in uv2, shader will calculate coordinate from this
    //    data.uv2.Add(new Vector2(0, 0));
    //    data.uv2.Add(new Vector2(h, 0));
    //    data.uv2.Add(new Vector2(0, w));
    //    data.uv2.Add(new Vector2(h, w));
    //}

}


//public struct Tile {
//    public const float SIZE = 0.125f; // set equal to 1 / number of tiles on sprite sheet 
//    public int x;
//    public int y;

//    public Tile(int x, int y) {
//        this.x = x;
//        this.y = y;
//    }
//}

public class StoneBlock : BlockType {

    // test smiley texture
    //public override Tile TexturePosition(Dir dir) {
    //    return new Tile(1, 1);
    //}
}
