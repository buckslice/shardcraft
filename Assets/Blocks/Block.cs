using UnityEngine;
using System.Collections;
using System;

[Serializable]
public class Block {

    public enum Dir { north, east, south, west, up, down };

    public bool changed = true;

    //Base block constructor
    public Block() {

    }

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
        meshData.AddVertex(new Vector3(x - 0.5f, y + 0.5f, z + 0.5f));
        meshData.AddVertex(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
        meshData.AddVertex(new Vector3(x + 0.5f, y + 0.5f, z - 0.5f));
        meshData.AddVertex(new Vector3(x - 0.5f, y + 0.5f, z - 0.5f));

        meshData.AddQuadTriangles();

        meshData.uv.AddRange(FaceUVs(Dir.up));
    }

    protected virtual void FaceDataDown(Chunk chunk, int x, int y, int z, MeshData meshData) {
        meshData.AddVertex(new Vector3(x - 0.5f, y - 0.5f, z - 0.5f));
        meshData.AddVertex(new Vector3(x + 0.5f, y - 0.5f, z - 0.5f));
        meshData.AddVertex(new Vector3(x + 0.5f, y - 0.5f, z + 0.5f));
        meshData.AddVertex(new Vector3(x - 0.5f, y - 0.5f, z + 0.5f));

        meshData.AddQuadTriangles();

        meshData.uv.AddRange(FaceUVs(Dir.down));
    }

    protected virtual void FaceDataNorth(Chunk chunk, int x, int y, int z, MeshData meshData) {
        meshData.AddVertex(new Vector3(x + 0.5f, y - 0.5f, z + 0.5f));
        meshData.AddVertex(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
        meshData.AddVertex(new Vector3(x - 0.5f, y + 0.5f, z + 0.5f));
        meshData.AddVertex(new Vector3(x - 0.5f, y - 0.5f, z + 0.5f));

        meshData.AddQuadTriangles();

        meshData.uv.AddRange(FaceUVs(Dir.north));
    }

    protected virtual void FaceDataEast(Chunk chunk, int x, int y, int z, MeshData meshData) {
        meshData.AddVertex(new Vector3(x + 0.5f, y - 0.5f, z - 0.5f));
        meshData.AddVertex(new Vector3(x + 0.5f, y + 0.5f, z - 0.5f));
        meshData.AddVertex(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
        meshData.AddVertex(new Vector3(x + 0.5f, y - 0.5f, z + 0.5f));

        meshData.AddQuadTriangles();

        meshData.uv.AddRange(FaceUVs(Dir.east));
    }

    protected virtual void FaceDataSouth(Chunk chunk, int x, int y, int z, MeshData meshData) {
        meshData.AddVertex(new Vector3(x - 0.5f, y - 0.5f, z - 0.5f));
        meshData.AddVertex(new Vector3(x - 0.5f, y + 0.5f, z - 0.5f));
        meshData.AddVertex(new Vector3(x + 0.5f, y + 0.5f, z - 0.5f));
        meshData.AddVertex(new Vector3(x + 0.5f, y - 0.5f, z - 0.5f));

        meshData.AddQuadTriangles();

        meshData.uv.AddRange(FaceUVs(Dir.south));
    }

    protected virtual void FaceDataWest(Chunk chunk, int x, int y, int z, MeshData meshData) {
        meshData.AddVertex(new Vector3(x - 0.5f, y - 0.5f, z + 0.5f));
        meshData.AddVertex(new Vector3(x - 0.5f, y + 0.5f, z + 0.5f));
        meshData.AddVertex(new Vector3(x - 0.5f, y + 0.5f, z - 0.5f));
        meshData.AddVertex(new Vector3(x - 0.5f, y - 0.5f, z - 0.5f));

        meshData.AddQuadTriangles();

        meshData.uv.AddRange(FaceUVs(Dir.west));
    }


    public struct Tile { public int x; public int y; }

    const float TILE_SIZE = 0.25f; // set equal to 1 / number of tiles on sprite sheet 

    public virtual Tile TexturePosition(Dir dir) {
        Tile tile = new Tile();
        tile.x = 0;
        tile.y = 0;

        return tile;
    }

    public virtual Vector2[] FaceUVs(Dir dir) {
        Vector2[] uvs = new Vector2[4];
        Tile tilePos = TexturePosition(dir);

        uvs[0] = new Vector2(TILE_SIZE * tilePos.x + TILE_SIZE, TILE_SIZE * tilePos.y);
        uvs[1] = new Vector2(TILE_SIZE * tilePos.x + TILE_SIZE, TILE_SIZE * tilePos.y + TILE_SIZE);
        uvs[2] = new Vector2(TILE_SIZE * tilePos.x, TILE_SIZE * tilePos.y + TILE_SIZE);
        uvs[3] = new Vector2(TILE_SIZE * tilePos.x, TILE_SIZE * tilePos.y);

        return uvs;
    }

}