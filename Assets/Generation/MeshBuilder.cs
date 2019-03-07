using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

public static class MeshBuilder {


    public static void BuildNaive(NativeArray<Block> blocks, NativeList<Vector3> vertices, NativeList<int> triangles, NativeList<Vector2> uvs) {
        void AddQuadTriangles() {
            triangles.Add(vertices.Length - 4);  // 0
            triangles.Add(vertices.Length - 3);  // 1
            triangles.Add(vertices.Length - 2);  // 2

            triangles.Add(vertices.Length - 4);  // 0
            triangles.Add(vertices.Length - 2);  // 2
            triangles.Add(vertices.Length - 1);  // 3

        }

        void AddFaceUVs(BlockType bt, Dir dir) {
            Tile tp = bt.TexturePosition(dir);

            uvs.Add(new Vector2(tp.x + 1, tp.y) * Tile.SIZE);
            uvs.Add(new Vector2(tp.x + 1, tp.y + 1) * Tile.SIZE);
            uvs.Add(new Vector2(tp.x, tp.y + 1) * Tile.SIZE);
            uvs.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
        }

        const int s = Chunk.SIZE;
        const int s2 = s + 2;

        for (int z = 0; z < s; z++) {
            for (int y = 0; y < s; y++) {
                for (int x = 0; x < s; x++) {
                    int xx = x + 1;
                    int yy = y + 1;
                    int zz = z + 1;

                    Block b = blocks[xx + yy * s2 + zz * s2 * s2];
                    if (b == Blocks.AIR) {
                        continue;
                    }
                    BlockType bt = b.GetBlockType();

                    if (!blocks[(xx + 1) + yy * s2 + zz * s2 * s2].IsSolid(Dir.west)) {
                        vertices.Add(new Vector3(x + 1.0f, y, z));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x + 1.0f, y, z + 1.0f));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.east);
                    }
                    if (!blocks[xx + (yy + 1) * s2 + zz * s2 * s2].IsSolid(Dir.down)) {
                        vertices.Add(new Vector3(x, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z));
                        vertices.Add(new Vector3(x, y + 1.0f, z));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.up);
                    }
                    if (!blocks[xx + yy * s2 + (zz + 1) * s2 * s2].IsSolid(Dir.south)) {
                        vertices.Add(new Vector3(x + 1.0f, y, z + 1.0f));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x, y, z + 1.0f));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.north);
                    }

                    if (!blocks[(xx - 1) + yy * s2 + zz * s2 * s2].IsSolid(Dir.east)) {
                        vertices.Add(new Vector3(x, y, z + 1.0f));
                        vertices.Add(new Vector3(x, y + 1.0f, z + 1.0f));
                        vertices.Add(new Vector3(x, y + 1.0f, z));
                        vertices.Add(new Vector3(x, y, z));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.west);

                    }
                    if (!blocks[xx + (yy - 1) * s2 + zz * s2 * s2].IsSolid(Dir.up)) {
                        vertices.Add(new Vector3(x, y, z));
                        vertices.Add(new Vector3(x + 1.0f, y, z));
                        vertices.Add(new Vector3(x + 1.0f, y, z + 1.0f));
                        vertices.Add(new Vector3(x, y, z + 1.0f));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.down);
                    }


                    if (!blocks[xx + yy * s2 + (zz - 1) * s2 * s2].IsSolid(Dir.north)) {
                        vertices.Add(new Vector3(x, y, z));
                        vertices.Add(new Vector3(x, y + 1.0f, z));
                        vertices.Add(new Vector3(x + 1.0f, y + 1.0f, z));
                        vertices.Add(new Vector3(x + 1.0f, y, z));

                        AddQuadTriangles();

                        AddFaceUVs(bt, Dir.south);
                    }


                }
            }
        }
    }

    public static void BuildGreedyCollider(NativeArray<Block> blocks, NativeList<Vector3> vertices, NativeList<int> triangles) {

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


        int s2 = Chunk.SIZE + 2;

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
            for (x[d0] = 0; x[d0] < maxDim[d0];) {

                // compute mask (which is a slice)
                n = 0;
                for (x[d2] = 0; x[d2] < maxDim[d2]; x[d2]++) {
                    for (x[d1] = 0; x[d1] < maxDim[d1]; x[d1]++) {
                        Block block1 = blocks[(x[0] + 1) + (x[1] + 1) * s2 + (x[2] + 1) * s2 * s2]; // block were at
                        Block block2 = blocks[(x[0] + 1 + q[0]) + (x[1] + 1 + q[1]) * s2 + (x[2] + 1 + q[2]) * s2 * s2]; // block were going to

                        // this isSolid is probably wrong in some cases but no blocks use yet cuz i dont rly get so figure out later lol
                        slice[n++] = block1.IsSolid(side) && block2.IsSolid(Dirs.Opp(side)) ?
                            Blocks.AIR : backFace ? block2 : block1;
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
                        //botLeft *= VOXEL_SIZE;
                        //topLeft *= VOXEL_SIZE;
                        //topRight *= VOXEL_SIZE;
                        //botRight *= VOXEL_SIZE;

                        vertices.Add(botLeft);
                        vertices.Add(botRight);
                        vertices.Add(topLeft);
                        vertices.Add(topRight);

                        AddQuadTrianglesGreedy(d0 == 2 ? backFace : !backFace);

                        //if (!forCollision) {
                        //    slice[n].GetBlockType().FaceUVsGreedy(side, data, w, h);
                        //}

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

    // this is probably slow as crap but whatever
    public static NativeArray<Block> BuildPaddedBlockArray(Chunk chunk) {
        const int s = Chunk.SIZE;
        const int s1 = s + 1;
        const int s2 = s + 2;

        NativeArray<Block> blocks = new NativeArray<Block>(s2 * s2 * s2, Allocator.TempJob);

        // west
        for (int z = 0; z < s; ++z) {
            for (int y = 0; y < s; ++y) {
                blocks[0 + (y + 1) * s2 + (z + 1) * s2 * s2] = chunk.neighbors[0].blocks[(s - 1) + y * s + z * s * s];
            }
        }
        // down
        for (int z = 0; z < s; ++z) {
            for (int x = 0; x < s; ++x) {
                blocks[(x + 1) + 0 + (z + 1) * s2 * s2] = chunk.neighbors[1].blocks[x + (s - 1) * s + z * s * s];
            }
        }

        // south
        for (int y = 0; y < s; ++y) {
            for (int x = 0; x < s; ++x) {
                blocks[(x + 1) + (y + 1) * s2 + 0] = chunk.neighbors[2].blocks[x + y * s + (s - 1) * s * s];
            }
        }

        // east
        for (int z = 0; z < s; ++z) {
            for (int y = 0; y < s; ++y) {
                blocks[s1 + (y + 1) * s2 + (z + 1) * s2 * s2] = chunk.neighbors[3].blocks[0 + y * s + z * s * s];
            }
        }
        // up
        for (int z = 0; z < s; ++z) {
            for (int x = 0; x < s; ++x) {
                blocks[(x + 1) + s1 * s2 + (z + 1) * s2 * s2] = chunk.neighbors[4].blocks[x + 0 + z * s * s];
            }
        }

        // north
        for (int y = 0; y < s; ++y) {
            for (int x = 0; x < s; ++x) {
                blocks[(x + 1) + (y + 1) * s2 + s1 * s2 * s2] = chunk.neighbors[5].blocks[x + y * s + 0];
            }
        }

        // fill blocks array with padding
        for (int z = 1; z < s1; z++) {
            for (int y = 1; y < s1; y++) {
                for (int x = 1; x < s1; x++) {
                    blocks[x + y * s2 + z * s2 * s2] = chunk.blocks[(x - 1) + (y - 1) * s + (z - 1) * s * s];
                }
            }
        }

        return blocks;

    }


}
