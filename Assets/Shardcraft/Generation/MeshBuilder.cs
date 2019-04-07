using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

public struct Face {
    public ushort pos;
    public Dir dir;
}

public static class MeshBuilder {

    const int S = Chunk.SIZE;

    public static void BuildNaive(ref NativeMeshData meshData, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeArray<BlockData> blockData) {

        for (int y = 0; y < S; y++) {
            for (int z = 0; z < S; z++) {
                for (int x = 0; x < S; x++) {
                    //blocks.c[x + z * S + y * S * S].GetType().AddDataNative(x, y, z, ref data, ref blocks, ref lights, faces);

                    BlockData bd = blockData[blocks.c[x + z * S + y * S * S].type];

                    if (bd.renderType > 0) {
                        AddDataNative(x, y, z, ref meshData, ref blocks, ref lights, blockData);
                    }

                }
            }
        }
    }

    static void AddUVs(ref NativeMeshData data, ref NativeArray3x3<Block> blocks, NativeArray<BlockData> blockData, Dir dir, int x, int y, int z) {

        BlockData bd = blockData[blocks.Get(x, y, z).type];
        if (bd.renderType == 1) {
            if (bd.texture < 0) { // dynamic, depends on nearby blocks
                data.AddFaceUVs(GetTextureIndex(dir, x, y, z, ref blocks));
            } else {
                data.AddFaceUVs(bd.texture);
            }
        } else if (bd.renderType == 2) {
            if (bd.texture < 0) {
                int texture = GetTileTextureIndex(dir, x, y, z, ref blocks);
                data.AddTileUvs(texture, dir, x, y, z, ref blocks, blockData);
            } else {
                data.AddTileUvs(bd.texture, dir, x, y, z, ref blocks, blockData);
            }
        }
    }

    static int GetTileTextureIndex(Dir dir, int x, int y, int z, ref NativeArray3x3<Block> blocks) {
        Block b = blocks.Get(x, y, z);

        if (b == Blocks.GRASS) {
            switch (dir) {
                case Dir.up:
                    return 2;
                case Dir.down:
                    return 3;
            }

            if (blocks.Get(x, y + 1, z) != Blocks.AIR) {
                return 3;
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
        } else if (b == Blocks.STONE) {
            switch (dir) {
                case Dir.up:
                case Dir.down:
                    return 0;
                default:
                    return 1;
            }
        }else if(b== Blocks.PINE) {
            switch (dir) {
                case Dir.up:
                case Dir.down:
                    return 5;
                default:
                    return 4;
            }
        }

        return -1;
    }

    static int GetTextureIndex(Dir dir, int x, int y, int z, ref NativeArray3x3<Block> blocks) {
        Block b = blocks.Get(x, y, z);

        if (b == Blocks.GRASS) {
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
        } else if (b == Blocks.PINE) {
            switch (dir) {
                case Dir.up:
                case Dir.down:
                    return 5;
                default:
                    return 4;
            }
        }

        return -1; // shouldnt ever reach this point
    }

    static void AddDataNative(int x, int y, int z, ref NativeMeshData meshData, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeArray<BlockData> blockData) {
        if (!BlockData.RenderSolid(blockData, blocks.Get(x - 1, y, z), Dir.east)) {
            FaceDataWestNative(x, y, z, ref meshData, ref blocks, ref lights, blockData);
            AddUVs(ref meshData, ref blocks, blockData, Dir.west, x, y, z);
            meshData.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.west });
        }
        if (!BlockData.RenderSolid(blockData, blocks.Get(x, y - 1, z), Dir.up)) {
            FaceDataDownNative(x, y, z, ref meshData, ref blocks, ref lights, blockData);
            AddUVs(ref meshData, ref blocks, blockData, Dir.down, x, y, z);
            meshData.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.down });
        }
        if (!BlockData.RenderSolid(blockData, blocks.Get(x, y, z - 1), Dir.north)) {
            FaceDataSouthNative(x, y, z, ref meshData, ref blocks, ref lights, blockData);
            AddUVs(ref meshData, ref blocks, blockData, Dir.south, x, y, z);
            meshData.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.south });
        }
        if (!BlockData.RenderSolid(blockData, blocks.Get(x + 1, y, z), Dir.west)) {
            FaceDataEastNative(x, y, z, ref meshData, ref blocks, ref lights, blockData);
            AddUVs(ref meshData, ref blocks, blockData, Dir.east, x, y, z);
            meshData.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.east });
        }
        if (!BlockData.RenderSolid(blockData, blocks.Get(x, y + 1, z), Dir.down)) {
            FaceDataUpNative(x, y, z, ref meshData, ref blocks, ref lights, blockData);
            AddUVs(ref meshData, ref blocks, blockData, Dir.up, x, y, z);
            meshData.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.up });
        }
        if (!BlockData.RenderSolid(blockData, blocks.Get(x, y, z + 1), Dir.south)) {
            FaceDataNorthNative(x, y, z, ref meshData, ref blocks, ref lights, blockData);
            AddUVs(ref meshData, ref blocks, blockData, Dir.north, x, y, z);
            meshData.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.north });
        }
    }

    const float AOMIN = 0.2f;
    static float CalcAO(int side1, int side2, ref NativeArray3x3<Block> blocks, NativeArray<BlockData> blockData, int c1, int c2, int c3) {
        if (side1 + side2 == 2) {
            return AOMIN;
        }
        return (3.0f - side1 - side2 - GetOpacity(ref blocks, blockData, c1, c2, c3)) / 3.0f * (1.0f - AOMIN) + AOMIN;
    }

    static int GetOpacity(ref NativeArray3x3<Block> blocks, NativeArray<BlockData> blockData, int x, int y, int z) {
        return BlockData.RenderSolid(blockData, blocks.Get(x, y, z), Dir.none) ? 1 : 0; // dir.none for now since all blocks are either transparent or not
    }

    static void FaceDataWestNative(int x, int y, int z, ref NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeArray<BlockData> blockData) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x - 1, y, z));

        int up = GetOpacity(ref blocks, blockData, x - 1, y + 1, z);
        int down = GetOpacity(ref blocks, blockData, x - 1, y - 1, z);
        int north = GetOpacity(ref blocks, blockData, x - 1, y, z + 1);
        int south = GetOpacity(ref blocks, blockData, x - 1, y, z - 1);

        float a0 = CalcAO(down, north, ref blocks, blockData, x - 1, y - 1, z + 1);
        float a1 = CalcAO(up, north, ref blocks, blockData, x - 1, y + 1, z + 1);
        float a2 = CalcAO(up, south, ref blocks, blockData, x - 1, y + 1, z - 1);
        float a3 = CalcAO(down, south, ref blocks, blockData, x - 1, y - 1, z - 1);

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

    }

    static void FaceDataDownNative(int x, int y, int z, ref NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeArray<BlockData> blockData) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y - 1, z));

        int north = GetOpacity(ref blocks, blockData, x, y - 1, z + 1);
        int south = GetOpacity(ref blocks, blockData, x, y - 1, z - 1);
        int east = GetOpacity(ref blocks, blockData, x + 1, y - 1, z);
        int west = GetOpacity(ref blocks, blockData, x - 1, y - 1, z);

        float a0 = CalcAO(south, west, ref blocks, blockData, x - 1, y - 1, z - 1);
        float a1 = CalcAO(south, east, ref blocks, blockData, x + 1, y - 1, z - 1);
        float a2 = CalcAO(north, east, ref blocks, blockData, x + 1, y - 1, z + 1);
        float a3 = CalcAO(north, west, ref blocks, blockData, x - 1, y - 1, z + 1);

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

    }

    static void FaceDataSouthNative(int x, int y, int z, ref NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeArray<BlockData> blockData) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y, z - 1));

        int up = GetOpacity(ref blocks, blockData, x, y + 1, z - 1);
        int down = GetOpacity(ref blocks, blockData, x, y - 1, z - 1);
        int east = GetOpacity(ref blocks, blockData, x + 1, y, z - 1);
        int west = GetOpacity(ref blocks, blockData, x - 1, y, z - 1);

        float a0 = CalcAO(down, west, ref blocks, blockData, x - 1, y - 1, z - 1);
        float a1 = CalcAO(up, west, ref blocks, blockData, x - 1, y + 1, z - 1);
        float a2 = CalcAO(up, east, ref blocks, blockData, x + 1, y + 1, z - 1);
        float a3 = CalcAO(down, east, ref blocks, blockData, x + 1, y - 1, z - 1);

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

    }

    static void FaceDataEastNative(int x, int y, int z, ref NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeArray<BlockData> blockData) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x + 1, y, z));

        int up = GetOpacity(ref blocks, blockData, x + 1, y + 1, z);
        int down = GetOpacity(ref blocks, blockData, x + 1, y - 1, z);
        int north = GetOpacity(ref blocks, blockData, x + 1, y, z + 1);
        int south = GetOpacity(ref blocks, blockData, x + 1, y, z - 1);

        float a0 = CalcAO(down, south, ref blocks, blockData, x + 1, y - 1, z - 1);
        float a1 = CalcAO(up, south, ref blocks, blockData, x + 1, y + 1, z - 1);
        float a2 = CalcAO(up, north, ref blocks, blockData, x + 1, y + 1, z + 1);
        float a3 = CalcAO(down, north, ref blocks, blockData, x + 1, y - 1, z + 1);

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

    }

    static void FaceDataUpNative(int x, int y, int z, ref NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeArray<BlockData> blockData) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y + 1, z));

        int north = GetOpacity(ref blocks, blockData, x, y + 1, z + 1);
        int south = GetOpacity(ref blocks, blockData, x, y + 1, z - 1);
        int east = GetOpacity(ref blocks, blockData, x + 1, y + 1, z);
        int west = GetOpacity(ref blocks, blockData, x - 1, y + 1, z);

        float a0 = CalcAO(north, west, ref blocks, blockData, x - 1, y + 1, z + 1);
        float a1 = CalcAO(north, east, ref blocks, blockData, x + 1, y + 1, z + 1);
        float a2 = CalcAO(south, east, ref blocks, blockData, x + 1, y + 1, z - 1);
        float a3 = CalcAO(south, west, ref blocks, blockData, x - 1, y + 1, z - 1);

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

    }

    static void FaceDataNorthNative(int x, int y, int z, ref NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeArray<BlockData> blockData) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y, z + 1));

        int up = GetOpacity(ref blocks, blockData, x, y + 1, z + 1);
        int down = GetOpacity(ref blocks, blockData, x, y - 1, z + 1);
        int east = GetOpacity(ref blocks, blockData, x + 1, y, z + 1);
        int west = GetOpacity(ref blocks, blockData, x - 1, y, z + 1);

        float a0 = CalcAO(down, east, ref blocks, blockData, x + 1, y - 1, z + 1);
        float a1 = CalcAO(up, east, ref blocks, blockData, x + 1, y + 1, z + 1);
        float a2 = CalcAO(up, west, ref blocks, blockData, x - 1, y + 1, z + 1);
        float a3 = CalcAO(down, west, ref blocks, blockData, x - 1, y - 1, z + 1);

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

    }

    //public const int VOXEL_SIZE = 1;
    //https://github.com/roboleary/GreedyMesh/blob/master/src/mygame/Main.java
    //https://github.com/darkedge/starlight/blob/master/starlight/starlight_game.cpp

    public static void BuildGreedyCollider(ref NativeArray3x3<Block> blocks, NativeList<Vector3> vertices, NativeList<int> triangles, NativeArray<BlockData> blockData) {

        void AddQuadTrianglesGreedy(bool clockwise) {
            if (!clockwise) {
                triangles.Add(vertices.Length - 2);  // 2
                triangles.Add(vertices.Length - 4);  // 0
                triangles.Add(vertices.Length - 3);  // 1

                triangles.Add(vertices.Length - 3);  // 1
                triangles.Add(vertices.Length - 1);  // 3
                triangles.Add(vertices.Length - 2);  // 2
            } else {
                triangles.Add(vertices.Length - 2);  // 2
                triangles.Add(vertices.Length - 1);  // 3
                triangles.Add(vertices.Length - 3);  // 1

                triangles.Add(vertices.Length - 3);  // 1
                triangles.Add(vertices.Length - 4);  // 0
                triangles.Add(vertices.Length - 2);  // 2
            }
        }

        // setup variables for algo
        int i, j, k, l, w, h, d1, d2, n = 0;
        Dir side = Dir.south;

        int[] x = new int[] { 0, 0, 0 };
        int[] q = new int[] { 0, 0, 0 };
        int[] du = new int[] { 0, 0, 0 };
        int[] dv = new int[] { 0, 0, 0 };

        // slice will contain groups of matching blocks as we proceed through chunk in 6 directions, onces for each face
        Block[] slice = new Block[Chunk.CHUNK_WIDTH * Chunk.CHUNK_HEIGHT];

        int[] maxDim = new int[] { Chunk.CHUNK_WIDTH, Chunk.CHUNK_HEIGHT, Chunk.CHUNK_WIDTH };

        // sweep over six dimensions
        for (int dim = 0; dim < 6; ++dim) {
            int d0 = dim % 3;
            d1 = (dim + 1) % 3; // u
            d2 = (dim + 2) % 3; // v
            // when going thru z dimension, make x d1 and y d2 so makes more sense for uvs
            if (d0 == 2) {
                d1 = 1;
                d2 = 0;
            }

            int bf = dim / 3 * 2 - 1; // -1 -1 -1 +1 +1 +1
            bool backFace = bf < 0;

            x[0] = 0;
            x[1] = 0;
            x[2] = 0;

            // set the direction vector from dimension
            q[0] = 0;
            q[1] = 0;
            q[2] = 0;
            q[d0] = 1;

            side = (Dir)dim;

            // move through dimension from front to back
            for (x[d0] = -1; x[d0] < maxDim[d0];) {
                // compute mask (which is a slice)
                n = 0;
                for (x[d2] = 0; x[d2] < maxDim[d2]; x[d2]++) {
                    for (x[d1] = 0; x[d1] < maxDim[d1]; x[d1]++) {

                        // the second part of the ors are to make sure you dont add collision data for other chunk block faces on your borders
                        Block block1 = (backFace || x[d0] >= 0) ? blocks.Get(x[0], x[1], x[2]) : Blocks.AIR; // block were at
                        Block block2 = (!backFace || x[d0] < S - 1) ? blocks.Get(x[0] + q[0], x[1] + q[1], x[2] + q[2]) : Blocks.AIR;
                        slice[n++] = BlockData.ColliderSolid(blockData, block1) && BlockData.ColliderSolid(blockData, block2) ? Blocks.AIR : backFace ? block2 : block1;

                        // saving this for when porting back to meshing
                        //slice[n++] = block1.IsSolid(side) && block2.IsSolid(Dirs.Opp(side)) ?
                        //    Blocks.AIR : backFace ? block2 : block1;
                    }
                }

                // i think the current dimension we are slicing thru is incremented here so the blocks
                // will have the correct placement coordinate
                x[d0]++;

                // generate mesh for the mask
                n = 0;
                for (j = 0; j < maxDim[d2]; ++j) {
                    for (i = 0; i < maxDim[d1];) {
                        if (!BlockData.ColliderSolid(blockData, slice[n])) {
                            ++i;
                            ++n;
                            continue;
                        }

                        // normal equality check can split on type and more like AO and stuff later if want to change this back
                        // just need to change this below line and the line like 8 lines down

                        // compute width
                        for (w = 1; i + w < maxDim[d1] && BlockData.ColliderSolid(blockData, slice[n + w]) == BlockData.ColliderSolid(blockData, slice[n]); ++w) { }

                        // compute height
                        bool done = false;
                        for (h = 1; j + h < maxDim[d2]; ++h) {
                            for (k = 0; k < w; ++k) {
                                if (BlockData.ColliderSolid(blockData, slice[n + k + h * maxDim[d1]]) != BlockData.ColliderSolid(blockData, slice[n])) {
                                    done = true;
                                    break;
                                }
                            }
                            if (done) {
                                break;
                            }
                        }

                        x[d1] = i;
                        x[d2] = j;

                        du[0] = 0;
                        du[1] = 0;
                        du[2] = 0;
                        du[d1] = w;

                        dv[0] = 0;
                        dv[1] = 0;
                        dv[2] = 0;
                        dv[d2] = h;

                        int s = (int)side;
                        Vector3 botLeft = new Vector3(x[0], x[1], x[2]) + MeshUtils.padOffset[s][0];
                        Vector3 botRight = new Vector3(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]) + MeshUtils.padOffset[s][1];
                        Vector3 topLeft = new Vector3(x[0] + du[0], x[1] + du[1], x[2] + du[2]) + MeshUtils.padOffset[s][2];
                        Vector3 topRight = new Vector3(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]) + MeshUtils.padOffset[s][3];

                        botLeft /= Chunk.BPU;
                        topLeft /= Chunk.BPU;
                        topRight /= Chunk.BPU;
                        botRight /= Chunk.BPU;

                        vertices.Add(botLeft);
                        vertices.Add(botRight);
                        vertices.Add(topLeft);
                        vertices.Add(topRight);

                        AddQuadTrianglesGreedy(d0 == 2 ? backFace : !backFace);

                        // zero out the quad in the mask
                        for (l = 0; l < h; ++l) {
                            for (k = 0; k < w; ++k) {
                                slice[n + k + l * maxDim[d1]] = Blocks.AIR;
                            }
                        }

                        // increment counters and continue
                        i += w;
                        n += w;

                    }
                }
            }
        }
    }


    // some crap just so i can reuse current block face functions
    // those will prob get rewritten later so i can redo this then too

    static NativeArray3x3<Block> blockArray;
    static NativeArray3x3<Light> lightArray;
    static NativeList<Face> faceList;

    public static void PrimeBasicBlock() {
        blockArray.c = new NativeArray<Block>(S * S * S, Allocator.Persistent);
        lightArray.c = new NativeArray<Light>(S * S * S, Allocator.Persistent);
        faceList = new NativeList<Face>(Allocator.Persistent);

    }

    public static void DestroyBasicBlock() {
        blockArray.c.Dispose();
        lightArray.c.Dispose();
        faceList.Dispose();
    }

    public static void GetBlockMesh(Block block, MeshFilter filter) {

        var blockData = JobController.instance.blockData;

        BlockData bd = blockData[block.type];

        var vertices = Pools.v3Pool.Get();
        var uvs = Pools.v3Pool.Get();
        var uv2s = Pools.v3Pool.Get();
        var colors = Pools.c32Pool.Get();
        var triangles = Pools.intPool.Get();

        NativeMeshData data = new NativeMeshData(vertices, uvs, uv2s, colors, triangles, faceList);

        const int x = 1;
        const int y = 1;
        const int z = 1;
        blockArray.c[x + z * S + y * S * S] = block;

        ushort light = bd.light;
        if (light == 0) {
            light = ushort.MaxValue;
        }

        lightArray.c[x + z * S + y * S * S] = new Light { torch = light };
        lightArray.c[x - 1 + z * S + y * S * S] = new Light { torch = light };
        lightArray.c[x + (z - 1) * S + y * S * S] = new Light { torch = light };
        lightArray.c[x + z * S + (y - 1) * S * S] = new Light { torch = light };
        lightArray.c[x + 1 + z * S + y * S * S] = new Light { torch = light };
        lightArray.c[x + (z + 1) * S + y * S * S] = new Light { torch = light };
        lightArray.c[x + z * S + (y + 1) * S * S] = new Light { torch = light };

        AddDataNative(x, y, z, ref data, ref blockArray, ref lightArray, blockData);

        for (int i = 0; i < vertices.Length; ++i) {
            vertices[i] = (vertices[i] - (Vector3.one * 0.75f)) * 2.0f;
        }

        filter.mesh.Clear();
        filter.mesh.vertices = vertices.ToArray();
        filter.mesh.SetUVs(0, new List<Vector3>(uvs.ToArray()));
        filter.mesh.SetUVs(1, new List<Vector3>(uv2s.ToArray()));
        filter.mesh.colors32 = colors.ToArray();

        filter.mesh.triangles = triangles.ToArray();
        filter.mesh.RecalculateNormals();

        Pools.v3Pool.Return(vertices);
        Pools.v3Pool.Return(uvs);
        Pools.v3Pool.Return(uv2s);
        Pools.c32Pool.Return(colors);
        Pools.intPool.Return(triangles);

    }


}
