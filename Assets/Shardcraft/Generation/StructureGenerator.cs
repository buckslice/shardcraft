using System.Collections;
using System.Collections.Generic;
//using UnityEngine;
using Unity.Mathematics;

public static class StructureGenerator {

    // called only once all neighbors are generated
    public static void BuildStructures(Vector3i chunkBlockPos, int seed, ref NativeArray3x3<Block> blocks) {
        // not sure here lol
        Random urand = new Random((uint)(chunkBlockPos.GetHashCode() + seed));        

        for (int y = 0; y < Chunk.SIZE; ++y) {
            for (int z = 0; z < Chunk.SIZE; ++z) {
                for (int x = 0; x < Chunk.SIZE; ++x) {
                    // random chance to try to spawn tree
                    if (urand.NextFloat() < 0.01f) {
                        TrySpawnTree(ref blocks, urand, x, y, z);
                    }

                }
            }
        }

    }

    static void TrySpawnTree(ref NativeArray3x3<Block> blocks, Random urand, int x, int y, int z) {
        int width = urand.NextInt(1, 4);

        int height = 0;
        if (width == 1) {
            height = urand.NextInt(3, 10);
            for (int i = 0; i <= height; ++i) {
                Block b = blocks.Get(x, y + i, z);
                if (i == 0) {
                    if (b != Blocks.GRASS) return;
                } else {
                    if (b != Blocks.AIR) return;
                }
            }

        } else if (width == 2) {
            height = urand.NextInt(8, 20);
            for (int i = 0; i <= height; ++i) {
                for (int u = 0; u <= 1; ++u) {
                    for (int v = 0; v <= 1; ++v) {
                        Block b = blocks.Get(x + u, y + i, z + v);
                        if (i == 0) {
                            if (b != Blocks.GRASS) return;
                        } else {
                            if (b != Blocks.AIR) return;
                        }
                    }
                }
            }

        } else if (width == 3) {
            height = urand.NextInt(16, 30);
            for (int i = 0; i <= height; ++i) {
                for (int u = -1; u <= 1; ++u) {
                    for (int v = -1; v <= 1; ++v) {
                        Block b = blocks.Get(x + u, y + i, z + v);
                        if (i == 0) {
                            if (b != Blocks.GRASS) return;
                        } else {
                            if (b != Blocks.AIR) return;
                        }
                    }
                }
            }
        }

        for (int i = 1; i <= height + 1; ++i) {
            float f = (height - i);
            float hf = (float)i / height;
            hf = 1.0f - hf;
            hf *= hf;
            int s = (int)(f * hf);
            if (s <= 0) {
                s = 1;
            }
            s += urand.NextInt(-1, 2);
            int us = -s;
            int vs = -s;
            if (width == 3) {
                us -= 1;
                vs -= 1;
            }

            for (int u = us; u <= s + width - 1; ++u) {
                for (int v = vs; v <= s + width - 1; ++v) {
                    // spawn trunk if near center
                    if (width == 1 && u == 0 && v == 0 ||
                       width == 2 && (u >= 0 && u <= 1) && (v >= 0 && v <= 1) ||
                       width == 3 && (u >= -1 && u <= 1) && (v >= -1 && v <= 1)) {
                        blocks.Set(x + u, y + i, z + v, Blocks.BIRCH);
                    } else if (i >= width * 2 && i % 2 == 0 || i > height - 1) { // otherwise leaves
                        blocks.Set(x + u, y + i, z + v, Blocks.LEAF);
                    }
                }
            }
        }

    }

}