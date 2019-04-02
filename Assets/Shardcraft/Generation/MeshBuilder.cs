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

    public static void BuildNaive(NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeList<Face> faces) {

        for (int y = 0; y < S; y++) {
            for (int z = 0; z < S; z++) {
                for (int x = 0; x < S; x++) {
                    blocks.c[x + z * S + y * S * S].GetType().AddDataNative(x, y, z, data, ref blocks, ref lights, faces);
                }
            }
        }
    }

    //public const int VOXEL_SIZE = 1;
    //https://github.com/roboleary/GreedyMesh/blob/master/src/mygame/Main.java
    //https://github.com/darkedge/starlight/blob/master/starlight/starlight_game.cpp

    public static void BuildGreedyCollider(ref NativeArray3x3<Block> blocks, NativeList<Vector3> vertices, NativeList<int> triangles) {

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
                        slice[n++] = block1.ColliderSolid() && block2.ColliderSolid() ? Blocks.AIR : backFace ? block2 : block1;

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
                        if (!slice[n].ColliderSolid()) {
                            ++i;
                            ++n;
                            continue;
                        }

                        // normal equality check can split on type and more like AO and stuff later if want to change this back
                        // just need to change this below line and the line like 8 lines down

                        // compute width
                        for (w = 1; i + w < maxDim[d1] && slice[n + w].ColliderSolid() == slice[n].ColliderSolid(); ++w) { }

                        // compute height
                        bool done = false;
                        for (h = 1; j + h < maxDim[d2]; ++h) {
                            for (k = 0; k < w; ++k) {
                                if (slice[n + k + h * maxDim[d1]].ColliderSolid() != slice[n].ColliderSolid()) {
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

                        // not using for now
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

        BlockType bt = block.GetType();

        var vertices = Pools.v3Pool.Get();
        var uvs = Pools.v3Pool.Get();
        var colors = Pools.c32Pool.Get();
        var triangles = Pools.intPool.Get();
        vertices.Clear();
        uvs.Clear();
        colors.Clear();
        triangles.Clear();

        NativeMeshData data = new NativeMeshData(vertices, uvs, colors, triangles);

        const int x = 1;
        const int y = 1;
        const int z = 1;
        blockArray.c[x + z * S + y * S * S] = block;

        ushort light = bt.GetLight();
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

        bt.AddDataNative(x, y, z, data, ref blockArray, ref lightArray, faceList);

        for (int i = 0; i < vertices.Length; ++i) {
            vertices[i] = (vertices[i] - (Vector3.one * 0.75f)) * 2.0f;
        }

        filter.mesh.Clear();
        filter.mesh.vertices = vertices.ToArray();
        filter.mesh.SetUVs(0, new List<Vector3>(uvs.ToArray()));
        filter.mesh.colors32 = colors.ToArray();

        filter.mesh.triangles = triangles.ToArray();
        filter.mesh.RecalculateNormals();

        Pools.v3Pool.Return(vertices);
        Pools.v3Pool.Return(uvs);
        Pools.c32Pool.Return(colors);
        Pools.intPool.Return(triangles);

    }


}
