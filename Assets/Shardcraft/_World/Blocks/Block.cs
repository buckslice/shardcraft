using UnityEngine;
using System.Collections;
using System;
using Unity.Collections;

[Serializable]
public struct Block : IEquatable<Block> {

    public byte type;

    public Block(byte type) {
        this.type = type;
    }

    public new BlockType GetType() {
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

// make sure this matches types array below
public static class Blocks {
    public static readonly Block AIR = new Block(0);
    public static readonly Block STONE = new Block(1);
    public static readonly Block GRASS = new Block(2);
    public static readonly Block BIRCH = new Block(3);
    public static readonly Block LEAF = new Block(4);
    public static readonly Block TORCH = new Block(5);
    public static readonly Block TORCH_R = new Block(6);
    public static readonly Block TORCH_G = new Block(7);
    public static readonly Block TORCH_B = new Block(8);
    public static readonly Block TORCH_T = new Block(9);
}

public static class BlockTypes {
    // make sure these match Blocks above
    private static BlockType[] types = new BlockType[] {
        new AirBlock(),
        new StoneBlock(),
        new GrassBlock(),
        new BirchBlock(),
        new LeafBlock(),
        new BlockTorch(31,31,31),
        new BlockTorch(31,0,0),
        new BlockTorch(0,31,0),
        new BlockTorch(0,0,31),
        new BlockTorch(31,31,15),
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

    public virtual ushort GetLight() {
        return 0;
    }


    public virtual int GetTextureIndex(Dir dir, int x, int y, int z, ref NativeArray3x3<Block> blocks) {
        return 0;
    }

    public virtual void AddDataNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeList<Face> faces) {
        if (!blocks.Get(x - 1, y, z).IsSolid(Dir.east)) {
            FaceDataWestNative(x, y, z, data, ref blocks, ref lights);
            faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.west });
        }
        if (!blocks.Get(x, y - 1, z).IsSolid(Dir.up)) {
            FaceDataDownNative(x, y, z, data, ref blocks, ref lights);
            faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.down });
        }
        if (!blocks.Get(x, y, z - 1).IsSolid(Dir.north)) {
            FaceDataSouthNative(x, y, z, data, ref blocks, ref lights);
            faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.south });
        }
        if (!blocks.Get(x + 1, y, z).IsSolid(Dir.west)) {
            FaceDataEastNative(x, y, z, data, ref blocks, ref lights);
            faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.east });
        }
        if (!blocks.Get(x, y + 1, z).IsSolid(Dir.down)) {
            FaceDataUpNative(x, y, z, data, ref blocks, ref lights);
            faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.up });
        }
        if (!blocks.Get(x, y, z + 1).IsSolid(Dir.south)) {
            FaceDataNorthNative(x, y, z, data, ref blocks, ref lights);
            faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.north });
        }
    }

    const float AOMIN = 0.2f;
    static float CalcAO(int side1, int side2, ref NativeArray3x3<Block> blocks, int c1, int c2, int c3) {
        if (side1 + side2 == 2) {
            return AOMIN;
        }
        return (3.0f - side1 - side2 - GetOpacity(ref blocks, c1, c2, c3)) / 3.0f * (1.0f - AOMIN) + AOMIN;
    }

    static int GetOpacity(ref NativeArray3x3<Block> blocks, int x, int y, int z) {
        return blocks.Get(x, y, z) != Blocks.AIR ? 1 : 0;
    }

    protected virtual void FaceDataWestNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x - 1, y, z));

        int up = GetOpacity(ref blocks, x - 1, y + 1, z);
        int down = GetOpacity(ref blocks, x - 1, y - 1, z);
        int north = GetOpacity(ref blocks, x - 1, y, z + 1);
        int south = GetOpacity(ref blocks, x - 1, y, z - 1);

        float a0 = CalcAO(down, north, ref blocks, x - 1, y - 1, z + 1);
        float a1 = CalcAO(up, north, ref blocks, x - 1, y + 1, z + 1);
        float a2 = CalcAO(up, south, ref blocks, x - 1, y + 1, z - 1);
        float a3 = CalcAO(down, south, ref blocks, x - 1, y - 1, z - 1);

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

        data.AddFaceUVs(GetTextureIndex(Dir.west, x, y, z, ref blocks));
    }

    protected virtual void FaceDataDownNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y - 1, z));

        int north = GetOpacity(ref blocks, x, y - 1, z + 1);
        int south = GetOpacity(ref blocks, x, y - 1, z - 1);
        int east = GetOpacity(ref blocks, x + 1, y - 1, z);
        int west = GetOpacity(ref blocks, x - 1, y - 1, z);

        float a0 = CalcAO(south, west, ref blocks, x - 1, y - 1, z - 1);
        float a1 = CalcAO(south, east, ref blocks, x + 1, y - 1, z - 1);
        float a2 = CalcAO(north, east, ref blocks, x + 1, y - 1, z + 1);
        float a3 = CalcAO(north, west, ref blocks, x - 1, y - 1, z + 1);

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

        data.AddFaceUVs(GetTextureIndex(Dir.down, x, y, z, ref blocks));
    }

    protected virtual void FaceDataSouthNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y, z - 1));

        int up = GetOpacity(ref blocks, x, y + 1, z - 1);
        int down = GetOpacity(ref blocks, x, y - 1, z - 1);
        int east = GetOpacity(ref blocks, x + 1, y, z - 1);
        int west = GetOpacity(ref blocks, x - 1, y, z - 1);

        float a0 = CalcAO(down, west, ref blocks, x - 1, y - 1, z - 1);
        float a1 = CalcAO(up, west, ref blocks, x - 1, y + 1, z - 1);
        float a2 = CalcAO(up, east, ref blocks, x + 1, y + 1, z - 1);
        float a3 = CalcAO(down, east, ref blocks, x + 1, y - 1, z - 1);

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

        data.AddFaceUVs(GetTextureIndex(Dir.south, x, y, z, ref blocks));
    }

    protected virtual void FaceDataEastNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x + 1, y, z));

        int up = GetOpacity(ref blocks, x + 1, y + 1, z);
        int down = GetOpacity(ref blocks, x + 1, y - 1, z);
        int north = GetOpacity(ref blocks, x + 1, y, z + 1);
        int south = GetOpacity(ref blocks, x + 1, y, z - 1);

        float a0 = CalcAO(down, south, ref blocks, x + 1, y - 1, z - 1);
        float a1 = CalcAO(up, south, ref blocks, x + 1, y + 1, z - 1);
        float a2 = CalcAO(up, north, ref blocks, x + 1, y + 1, z + 1);
        float a3 = CalcAO(down, north, ref blocks, x + 1, y - 1, z + 1);

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

        data.AddFaceUVs(GetTextureIndex(Dir.east, x, y, z, ref blocks));
    }

    protected virtual void FaceDataUpNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y + 1, z));

        int north = GetOpacity(ref blocks, x, y + 1, z + 1);
        int south = GetOpacity(ref blocks, x, y + 1, z - 1);
        int east = GetOpacity(ref blocks, x + 1, y + 1, z);
        int west = GetOpacity(ref blocks, x - 1, y + 1, z);

        float a0 = CalcAO(north, west, ref blocks, x - 1, y + 1, z + 1);
        float a1 = CalcAO(north, east, ref blocks, x + 1, y + 1, z + 1);
        float a2 = CalcAO(south, east, ref blocks, x + 1, y + 1, z - 1);
        float a3 = CalcAO(south, west, ref blocks, x - 1, y + 1, z - 1);

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

        data.AddFaceUVs(GetTextureIndex(Dir.up, x, y, z, ref blocks));
    }

    protected virtual void FaceDataNorthNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y, z + 1));

        int up = GetOpacity(ref blocks, x, y + 1, z + 1);
        int down = GetOpacity(ref blocks, x, y - 1, z + 1);
        int east = GetOpacity(ref blocks, x + 1, y, z + 1);
        int west = GetOpacity(ref blocks, x - 1, y, z + 1);

        float a0 = CalcAO(down, east, ref blocks, x + 1, y - 1, z + 1);
        float a1 = CalcAO(up, east, ref blocks, x + 1, y + 1, z + 1);
        float a2 = CalcAO(up, west, ref blocks, x - 1, y + 1, z + 1);
        float a3 = CalcAO(down, west, ref blocks, x - 1, y - 1, z + 1);

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

        data.AddFaceUVs(GetTextureIndex(Dir.north, x, y, z, ref blocks));
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
