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
                        AddBlockFaces(x, y, z, ref meshData, ref blocks, ref lights, blockData);
                    }

                }
            }
        }
    }

    static void AddUVs(int x, int y, int z, ref NativeMeshData data, ref NativeArray3x3<Block> blocks, NativeArray<BlockData> blockData, Dir dir, bool isLight) {

        // calculate uv1s, xy is uv coordinates, z is texture type
        BlockData bd = blockData[blocks.Get(x, y, z).type];
        if (bd.renderType == 1) { // normal texture
            if (bd.texture < 0) { // dynamic, depends on nearby blocks
                data.AddFaceUVs(GetTextureIndex(dir, x, y, z, ref blocks));
            } else {
                data.AddFaceUVs(bd.texture);
            }
        } else if (bd.renderType == 2) { // using tiling textures
            if (bd.texture < 0) { // dynamic, depends on nearby blocks
                int texture = GetTileTextureIndex(dir, x, y, z, ref blocks);
                data.AddTileUvs(texture, dir, x, y, z, ref blocks, blockData);
            } else {
                data.AddTileUvs(bd.texture, dir, x, y, z, ref blocks, blockData);
            }
        }

        // now add uv2s, x is tiletype and y is ambient occlusion (z is unused for now)
        Vector3 uv2_0, uv2_1, uv2_2, uv2_3;
        uv2_0 = uv2_1 = uv2_2 = uv2_3 = default;
        uv2_0.x = uv2_1.x = uv2_2.x = uv2_3.x = bd.renderType == 1 ? 0.5f : 1.5f;
        if (isLight) {
            uv2_0.y = uv2_1.y = uv2_2.y = uv2_3.y = 1.0f; // dont add ao on the faces of lights, looks weird
        } else {
            switch (dir) {
                case Dir.west: {
                        int up = GetOpacity(ref blocks, blockData, x - 1, y + 1, z);
                        int down = GetOpacity(ref blocks, blockData, x - 1, y - 1, z);
                        int north = GetOpacity(ref blocks, blockData, x - 1, y, z + 1);
                        int south = GetOpacity(ref blocks, blockData, x - 1, y, z - 1);
                        uv2_0.y = CalcAO(down, north, ref blocks, blockData, x - 1, y - 1, z + 1);
                        uv2_1.y = CalcAO(up, north, ref blocks, blockData, x - 1, y + 1, z + 1);
                        uv2_2.y = CalcAO(up, south, ref blocks, blockData, x - 1, y + 1, z - 1);
                        uv2_3.y = CalcAO(down, south, ref blocks, blockData, x - 1, y - 1, z - 1);
                    }
                    break;
                case Dir.down: {
                        int north = GetOpacity(ref blocks, blockData, x, y - 1, z + 1);
                        int south = GetOpacity(ref blocks, blockData, x, y - 1, z - 1);
                        int east = GetOpacity(ref blocks, blockData, x + 1, y - 1, z);
                        int west = GetOpacity(ref blocks, blockData, x - 1, y - 1, z);
                        uv2_0.y = CalcAO(south, west, ref blocks, blockData, x - 1, y - 1, z - 1);
                        uv2_1.y = CalcAO(south, east, ref blocks, blockData, x + 1, y - 1, z - 1);
                        uv2_2.y = CalcAO(north, east, ref blocks, blockData, x + 1, y - 1, z + 1);
                        uv2_3.y = CalcAO(north, west, ref blocks, blockData, x - 1, y - 1, z + 1);
                    }
                    break;
                case Dir.south: {
                        int up = GetOpacity(ref blocks, blockData, x, y + 1, z - 1);
                        int down = GetOpacity(ref blocks, blockData, x, y - 1, z - 1);
                        int east = GetOpacity(ref blocks, blockData, x + 1, y, z - 1);
                        int west = GetOpacity(ref blocks, blockData, x - 1, y, z - 1);
                        uv2_0.y = CalcAO(down, west, ref blocks, blockData, x - 1, y - 1, z - 1);
                        uv2_1.y = CalcAO(up, west, ref blocks, blockData, x - 1, y + 1, z - 1);
                        uv2_2.y = CalcAO(up, east, ref blocks, blockData, x + 1, y + 1, z - 1);
                        uv2_3.y = CalcAO(down, east, ref blocks, blockData, x + 1, y - 1, z - 1);
                    }
                    break;
                case Dir.east: {
                        int up = GetOpacity(ref blocks, blockData, x + 1, y + 1, z);
                        int down = GetOpacity(ref blocks, blockData, x + 1, y - 1, z);
                        int north = GetOpacity(ref blocks, blockData, x + 1, y, z + 1);
                        int south = GetOpacity(ref blocks, blockData, x + 1, y, z - 1);
                        uv2_0.y = CalcAO(down, south, ref blocks, blockData, x + 1, y - 1, z - 1);
                        uv2_1.y = CalcAO(up, south, ref blocks, blockData, x + 1, y + 1, z - 1);
                        uv2_2.y = CalcAO(up, north, ref blocks, blockData, x + 1, y + 1, z + 1);
                        uv2_3.y = CalcAO(down, north, ref blocks, blockData, x + 1, y - 1, z + 1);
                    }
                    break;
                case Dir.up: {
                        int north = GetOpacity(ref blocks, blockData, x, y + 1, z + 1);
                        int south = GetOpacity(ref blocks, blockData, x, y + 1, z - 1);
                        int east = GetOpacity(ref blocks, blockData, x + 1, y + 1, z);
                        int west = GetOpacity(ref blocks, blockData, x - 1, y + 1, z);
                        uv2_0.y = CalcAO(north, west, ref blocks, blockData, x - 1, y + 1, z + 1);
                        uv2_1.y = CalcAO(north, east, ref blocks, blockData, x + 1, y + 1, z + 1);
                        uv2_2.y = CalcAO(south, east, ref blocks, blockData, x + 1, y + 1, z - 1);
                        uv2_3.y = CalcAO(south, west, ref blocks, blockData, x - 1, y + 1, z - 1);
                    }
                    break;
                case Dir.north: {
                        int up = GetOpacity(ref blocks, blockData, x, y + 1, z + 1);
                        int down = GetOpacity(ref blocks, blockData, x, y - 1, z + 1);
                        int east = GetOpacity(ref blocks, blockData, x + 1, y, z + 1);
                        int west = GetOpacity(ref blocks, blockData, x - 1, y, z + 1);
                        uv2_0.y = CalcAO(down, east, ref blocks, blockData, x + 1, y - 1, z + 1);
                        uv2_1.y = CalcAO(up, east, ref blocks, blockData, x + 1, y + 1, z + 1);
                        uv2_2.y = CalcAO(up, west, ref blocks, blockData, x - 1, y + 1, z + 1);
                        uv2_3.y = CalcAO(down, west, ref blocks, blockData, x - 1, y - 1, z + 1);
                    }
                    break;
                default:
                    break;
            }
        }

        data.uv2s.Add(uv2_0);
        data.uv2s.Add(uv2_1);
        data.uv2s.Add(uv2_2);
        data.uv2s.Add(uv2_3);

        // do anisotropy flip
        if (uv2_0.y + uv2_2.y > uv2_1.y + uv2_3.y) {
            data.AddQuadTriangles();
        } else {
            data.AddFlippedQuadTriangles();
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
        } else if (b == Blocks.PINE) {
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

    static void AddBlockFaces(int x, int y, int z, ref NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeArray<BlockData> blockData) {

        bool isLight = LightCalculator.GetIsLight(lights.Get(x, y, z).torch);

        if (!BlockData.RenderSolid(blockData, blocks.Get(x - 1, y, z), Dir.east)) {
            AddWestFace(x, y, z, ref data);
            AddUVs(x, y, z, ref data, ref blocks, blockData, Dir.west, isLight);
            data.AddFaceColor(LightCalculator.GetColorFromLight(lights.Get(x - 1, y, z)));
            data.AddFaceNormal(Dirs.norm3f[Dirs.WEST]);
            data.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.west });
        }
        if (!BlockData.RenderSolid(blockData, blocks.Get(x, y - 1, z), Dir.up)) {
            AddDownFace(x, y, z, ref data);
            AddUVs(x, y, z, ref data, ref blocks, blockData, Dir.down, isLight);
            data.AddFaceColor(LightCalculator.GetColorFromLight(lights.Get(x, y - 1, z)));
            data.AddFaceNormal(Dirs.norm3f[Dirs.DOWN]);
            data.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.down });
        }
        if (!BlockData.RenderSolid(blockData, blocks.Get(x, y, z - 1), Dir.north)) {
            AddSouthFace(x, y, z, ref data);
            AddUVs(x, y, z, ref data, ref blocks, blockData, Dir.south, isLight);
            data.AddFaceColor(LightCalculator.GetColorFromLight(lights.Get(x, y, z - 1)));
            data.AddFaceNormal(Dirs.norm3f[Dirs.SOUTH]);
            data.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.south });
        }
        if (!BlockData.RenderSolid(blockData, blocks.Get(x + 1, y, z), Dir.west)) {
            AddEastFace(x, y, z, ref data);
            AddUVs(x, y, z, ref data, ref blocks, blockData, Dir.east, isLight);
            data.AddFaceColor(LightCalculator.GetColorFromLight(lights.Get(x + 1, y, z)));
            data.AddFaceNormal(Dirs.norm3f[Dirs.EAST]);
            data.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.east });
        }
        if (!BlockData.RenderSolid(blockData, blocks.Get(x, y + 1, z), Dir.down)) {
            AddUpFace(x, y, z, ref data);
            AddUVs(x, y, z, ref data, ref blocks, blockData, Dir.up, isLight);
            data.AddFaceColor(LightCalculator.GetColorFromLight(lights.Get(x, y + 1, z)));
            data.AddFaceNormal(Dirs.norm3f[Dirs.UP]);
            data.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.up });
        }
        if (!BlockData.RenderSolid(blockData, blocks.Get(x, y, z + 1), Dir.south)) {
            AddNorthFace(x, y, z, ref data);
            AddUVs(x, y, z, ref data, ref blocks, blockData, Dir.north, isLight);
            data.AddFaceColor(LightCalculator.GetColorFromLight(lights.Get(x, y, z + 1)));
            data.AddFaceNormal(Dirs.norm3f[Dirs.NORTH]);
            data.faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.north });
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

    static void AddWestFace(int x, int y, int z, ref NativeMeshData data) {
        Vector3 v;
        v.x = x;
        v.y = y;
        v.z = z + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.y = y + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.z = z;
        data.vertices.Add(v / Chunk.BPU);
        v.y = y;
        data.vertices.Add(v / Chunk.BPU);
    }

    static void AddDownFace(int x, int y, int z, ref NativeMeshData data) {
        Vector3 v;
        v.x = x;
        v.y = y;
        v.z = z;
        data.vertices.Add(v / Chunk.BPU);
        v.x = x + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.z = z + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.x = x;
        data.vertices.Add(v / Chunk.BPU);
    }

    static void AddSouthFace(int x, int y, int z, ref NativeMeshData data) {
        Vector3 v;
        v.x = x;
        v.y = y;
        v.z = z;
        data.vertices.Add(v / Chunk.BPU);
        v.y = y + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.x = x + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.y = y;
        data.vertices.Add(v / Chunk.BPU);
    }

    static void AddEastFace(int x, int y, int z, ref NativeMeshData data) {
        Vector3 v;
        v.x = x + 1.0f;
        v.y = y;
        v.z = z;
        data.vertices.Add(v / Chunk.BPU);
        v.y = y + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.z = z + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.y = y;
        data.vertices.Add(v / Chunk.BPU);
    }

    static void AddUpFace(int x, int y, int z, ref NativeMeshData data) {
        Vector3 v;
        v.x = x;
        v.y = y + 1.0f;
        v.z = z + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.x = x + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.z = z;
        data.vertices.Add(v / Chunk.BPU);
        v.x = x;
        data.vertices.Add(v / Chunk.BPU);
    }

    static void AddNorthFace(int x, int y, int z, ref NativeMeshData data) {
        Vector3 v;
        v.x = x + 1.0f;
        v.y = y;
        v.z = z + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.y = y + 1.0f;
        data.vertices.Add(v / Chunk.BPU);
        v.x = x;
        data.vertices.Add(v / Chunk.BPU);
        v.y = y;
        data.vertices.Add(v / Chunk.BPU);
    }

    // updates and returns nativeLists back to pools
    public static void UpdateMeshFilter(MeshFilter filter, NativeList<Vector3> vertices, NativeList<Vector3> normals, NativeList<Vector3> uvs, NativeList<Vector3> uv2s, NativeList<Color32> colors, NativeList<int> triangles) {
        filter.mesh.Clear();

        if (triangles.Length < ushort.MaxValue) {
            filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        } else {
            filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        // old bad allocaty way
        //filter.mesh.vertices = vertices.ToArray();
        //filter.mesh.SetUVs(0, new List<Vector3>(uvs.ToArray()));
        //filter.mesh.SetUVs(1, new List<Vector3>(uv2s.ToArray()));
        //filter.mesh.colors32 = colors.ToArray();
        //filter.mesh.triangles = triangles.ToArray();

        var vertL = Pools.v3.Get();
        var normL = Pools.v3.Get();
        var uvL = Pools.v3.Get();
        var uv2L = Pools.v3.Get();
        var colorL = Pools.c32.Get();
        var intL = Pools.i.Get();

        UnsafeCopy.CopyVectors(vertices, vertL);
        UnsafeCopy.CopyVectors(normals, normL);
        UnsafeCopy.CopyVectors(uvs, uvL);
        UnsafeCopy.CopyVectors(uv2s, uv2L);
        UnsafeCopy.CopyColors(colors, colorL);
        UnsafeCopy.CopyIntegers(triangles, intL);

        filter.mesh.SetVertices(vertL);
        filter.mesh.SetNormals(normL);
        filter.mesh.SetUVs(0, uvL);
        filter.mesh.SetUVs(1, uv2L);
        filter.mesh.SetColors(colorL);
        filter.mesh.SetTriangles(intL, 0);

        Pools.v3.Return(vertL);
        Pools.v3.Return(normL);
        Pools.v3.Return(uvL);
        Pools.v3.Return(uv2L);
        Pools.c32.Return(colorL);
        Pools.i.Return(intL);

        // and then return the native lists too
        Pools.v3N.Return(vertices);
        Pools.v3N.Return(normals);
        Pools.v3N.Return(uvs);
        Pools.v3N.Return(uv2s);
        Pools.c32N.Return(colors);
        Pools.intN.Return(triangles);

    }


    // below is trash way to render a single blocks mesh
    // did this way to be able to reuse the current functions

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

        var vertices = Pools.v3N.Get();
        var normals = Pools.v3N.Get();
        var uvs = Pools.v3N.Get();
        var uv2s = Pools.v3N.Get();
        var colors = Pools.c32N.Get();
        var triangles = Pools.intN.Get();

        NativeMeshData data = new NativeMeshData(vertices, normals, uvs, uv2s, colors, triangles, faceList);

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

        AddBlockFaces(x, y, z, ref data, ref blockArray, ref lightArray, blockData);

        // to correct x,y,z offset
        for (int i = 0; i < vertices.Length; ++i) {
            vertices[i] = (vertices[i] - (Vector3.one * 0.75f)) * 2.0f;
        }

        UpdateMeshFilter(filter, vertices, normals, uvs, uv2s, colors, triangles);

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
}
