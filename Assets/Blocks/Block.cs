﻿using UnityEngine;
using System.Collections;
using System;

[Serializable]
public struct Block {

    public byte type;

    public byte changed;

    // byte light;

    public Block(byte type) {
        this.type = type;
        changed = 1;
    }

    public BlockType GetBlockType() {
        return BlockTypes.GetBlockType(type);
    }

    public bool IsSolid(Dir dir) {
        return BlockTypes.GetBlockType(type).IsSolid(dir);
    }

    public Tile TexturePosition(Dir dir) {
        return BlockTypes.GetBlockType(type).TexturePosition(dir);
    }

    public void AddData(Chunk chunk, int x, int y, int z, MeshData meshData) {
        BlockTypes.GetBlockType(type).AddData(chunk, x, y, z, meshData);
    }

    public static bool operator ==(Block a, Block b) {
        return a.type == b.type;
    }
    public static bool operator !=(Block a, Block b) {
        return !(a == b);
    }

}

// dont change the ordering
public enum Dir {
    west,
    down,
    south,
    east,
    up,
    north,
};

// make sure this matches array below
public static class Blocks {
    public static readonly Block AIR = new Block(0);
    public static readonly Block STONE = new Block(1);
    public static readonly Block GRASS = new Block(2);
}

public static class BlockTypes {

    private static BlockType[] types = new BlockType[] {
        new BlockAir(),
        new BlockStone(),
        new BlockGrass(),
    };

    public static BlockType GetBlockType(int type) {
        return types[type];
    }

}

public abstract class BlockType {

    public virtual bool IsSolid(Dir dir) {
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

    public virtual Tile TexturePosition(Dir dir) {
        return new Tile() { x = 0, y = 0 };
    }

    public virtual void AddData(Chunk chunk, int x, int y, int z, MeshData meshData) {
        if (!chunk.GetBlock(x, y + 1, z).IsSolid(Dir.down)) {
            FaceDataUp(chunk, x, y, z, meshData);
        }

        if (!chunk.GetBlock(x, y - 1, z).IsSolid(Dir.up)) {
            FaceDataDown(chunk, x, y, z, meshData);
        }

        if (!chunk.GetBlock(x, y, z + 1).IsSolid(Dir.south)) {
            FaceDataNorth(chunk, x, y, z, meshData);
        }

        if (!chunk.GetBlock(x, y, z - 1).IsSolid(Dir.north)) {
            FaceDataSouth(chunk, x, y, z, meshData);
        }

        if (!chunk.GetBlock(x + 1, y, z).IsSolid(Dir.west)) {
            FaceDataEast(chunk, x, y, z, meshData);
        }

        if (!chunk.GetBlock(x - 1, y, z).IsSolid(Dir.east)) {
            FaceDataWest(chunk, x, y, z, meshData);
        }
    }

    protected virtual void FaceDataUp(Chunk chunk, int x, int y, int z, MeshData meshData) {
        meshData.AddVertex(new Vector3(x, y + 1.0f, z + 1.0f));
        meshData.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
        meshData.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z));
        meshData.AddVertex(new Vector3(x, y + 1.0f, z));

        meshData.AddQuadTriangles();

        AddFaceUVs(Dir.up, meshData);
    }

    protected virtual void FaceDataDown(Chunk chunk, int x, int y, int z, MeshData meshData) {
        meshData.AddVertex(new Vector3(x, y, z));
        meshData.AddVertex(new Vector3(x + 1.0f, y, z));
        meshData.AddVertex(new Vector3(x + 1.0f, y, z + 1.0f));
        meshData.AddVertex(new Vector3(x, y, z + 1.0f));

        meshData.AddQuadTriangles();

        AddFaceUVs(Dir.down, meshData);
    }

    protected virtual void FaceDataNorth(Chunk chunk, int x, int y, int z, MeshData meshData) {
        meshData.AddVertex(new Vector3(x + 1.0f, y, z + 1.0f));
        meshData.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
        meshData.AddVertex(new Vector3(x, y + 1.0f, z + 1.0f));
        meshData.AddVertex(new Vector3(x, y, z + 1.0f));

        meshData.AddQuadTriangles();

        AddFaceUVs(Dir.north, meshData);
    }

    protected virtual void FaceDataEast(Chunk chunk, int x, int y, int z, MeshData meshData) {
        meshData.AddVertex(new Vector3(x + 1.0f, y, z));
        meshData.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z));
        meshData.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
        meshData.AddVertex(new Vector3(x + 1.0f, y, z + 1.0f));

        meshData.AddQuadTriangles();

        AddFaceUVs(Dir.east, meshData);
    }

    protected virtual void FaceDataSouth(Chunk chunk, int x, int y, int z, MeshData meshData) {
        meshData.AddVertex(new Vector3(x, y, z));
        meshData.AddVertex(new Vector3(x, y + 1.0f, z));
        meshData.AddVertex(new Vector3(x + 1.0f, y + 1.0f, z));
        meshData.AddVertex(new Vector3(x + 1.0f, y, z));

        meshData.AddQuadTriangles();

        AddFaceUVs(Dir.south, meshData);
    }

    protected virtual void FaceDataWest(Chunk chunk, int x, int y, int z, MeshData data) {
        data.AddVertex(new Vector3(x, y, z + 1.0f));
        data.AddVertex(new Vector3(x, y + 1.0f, z + 1.0f));
        data.AddVertex(new Vector3(x, y + 1.0f, z));
        data.AddVertex(new Vector3(x, y, z));

        data.AddQuadTriangles();

        AddFaceUVs(Dir.west, data);
    }

    public virtual void AddFaceUVs(Dir dir, MeshData data) {
        Tile tp = TexturePosition(dir);

        data.uv.Add(new Vector2(tp.x + 1, tp.y) * Tile.SIZE);
        data.uv.Add(new Vector2(tp.x + 1, tp.y + 1) * Tile.SIZE);
        data.uv.Add(new Vector2(tp.x, tp.y + 1) * Tile.SIZE);
        data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
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
