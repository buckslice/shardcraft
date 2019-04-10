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

                    //if (blocks.Get(x, y, z) == Blocks.AIR && blocks.Get(x, y - 1, z) == Blocks.STONE) {
                    //    blocks.Set(x, y, z, Blocks.GRASS);
                    //    blocks.Set(x, y - 1, z, Blocks.GRASS);
                    //}

                    // random chance to try to spawn tree
                    if (urand.NextFloat() < 0.01f) {
                        TrySpawnGoodTree(ref blocks, urand, x, y, z);
                    }

                    // try to spawn gems
                    if (blocks.Get(x, y, z) == Blocks.AIR) {
                        float gemChance = Mth.Blend(0.02f, 0.0f, y + chunkBlockPos.y, -400, -100);

                        if (urand.NextFloat() < gemChance) {
                            TrySpawnGemstone(ref blocks, urand, x, y, z);
                        }
                    }

                }
            }
        }

    }

    static void TrySpawnGemstone(ref NativeArray3x3<Block> blocks, Random urand, int x, int y, int z) {

        //int dir = urand.NextInt(0, 6);
        int3 pos = new int3(x, y, z);
        int3 dir;
        bool stalag = urand.NextFloat() < 0.5f; // ones on ground pointing up
        if (stalag) { // changed to just be up and down for now see how that looks
            dir = new int3(0, 1, 0);
        } else {
            dir = new int3(0, -1, 0);
        }

        if (blocks.Get(x - dir.x, y - dir.y, z - dir.z) == Blocks.AIR) {
            return;
        }

        int len = urand.NextInt(2, 8 + (int)(math.abs(y) * 0.01f));
        len /= stalag ? 1 : 2; // stalags should be twice as tall as stalacts (ceiling ones)
        len = math.clamp(len, 2, 20); // make sure dont get too crazy long

        int sides, xdir, zdir;
        if (len > 2) { // if taller than this have some secondary columns with random lengths
            sides = urand.NextInt(0, 4);
            xdir = urand.NextInt(2, len - 1);   // size of secondary columns
            zdir = urand.NextInt(2, len - 1);
        } else {
            xdir = zdir = sides = 0;
        }
        int gemType = urand.NextInt(0, 3);
        Block gemBlock;
        if (gemType == 0) {
            gemBlock = Blocks.RUBY;
        } else if (gemType == 1) {
            gemBlock = Blocks.EMERALD;
        } else {
            gemBlock = Blocks.SAPPHIRE;
        }

        for (int i = 0; i < len; ++i) {
            int3 p = pos + dir * i;
            if (blocks.Get(p.x, p.y, p.z) != Blocks.AIR) {
                return;
            }
            blocks.Set(p.x, p.y, p.z, gemBlock);

            if (xdir-- > 0) {
                if (sides / 2 == 0) { // west
                    if (blocks.Get(p.x - 1, p.y, p.z) == Blocks.AIR) {
                        blocks.Set(p.x - 1, p.y, p.z, gemBlock);
                    }
                } else { // east
                    if (blocks.Get(p.x + 1, p.y, p.z) == Blocks.AIR) {
                        blocks.Set(p.x + 1, p.y, p.z, gemBlock);
                    }
                }
            }
            if (zdir-- > 0) {
                if (sides % 2 == 0) { // south
                    if (blocks.Get(p.x, p.y, p.z - 1) == Blocks.AIR) {
                        blocks.Set(p.x, p.y, p.z - 1, gemBlock);
                    }
                } else { // north
                    if (blocks.Get(p.x, p.y, p.z + 1) == Blocks.AIR) {
                        blocks.Set(p.x, p.y, p.z + 1, gemBlock);
                    }
                }
            }

        }

    }


    // this is a pine tree routine
    static void TrySpawnGoodTree(ref NativeArray3x3<Block> blocks, Random urand, int x, int y, int z) {

        float width = urand.NextFloat(0.5f, 2f);

        int height = 0;
        height = (int)(28 * width / 2.0f) + urand.NextInt(-3, 3);
        math.clamp(height, 5, 29);

        // make sure space is kinda clear for tree height at least
        int wi = (int)width + 1;
        for (int u = -wi; u <= wi; ++u) {
            for (int v = -wi; v <= wi; ++v) {
                if (math.lengthsq(new float2(u, v)) < wi * wi) {
                    if (blocks.Get(x + u, y, z + v) != Blocks.GRASS) {
                        return;
                    }
                }
            }
        }
        for (int i = 1; i <= height; ++i) {
            if (blocks.Get(x, y + i, z) != Blocks.AIR) {
                return;
            }
        }

        int nextLeafLevel = urand.NextInt(3, 5);
        for (int i = 1; i <= height; ++i) {

            float hp = (float)i / height;
            float curWidth = math.lerp(width, width / 2.0f, hp);
            float leafEdge = 0.0f;

            //bool leafLayer = (i >= 3 && i % 2 == 0) || i >= height;
            bool leafLayer = i == nextLeafLevel || i == height;
            if (leafLayer) {
                leafEdge = math.lerp(width * 4.0f, 1.5f, Mth.QuadraticEaseOut(hp));
                float r = urand.NextFloat();
                if (r < 0.1f) {
                    nextLeafLevel += 1;
                } else if (r < 0.75f || height < 20) {
                    nextLeafLevel += 2;
                } else { // taller trees have small chance of large gaps in leaves
                    nextLeafLevel += 3;
                }
                if (i == height) {
                    leafEdge = 1.0f;
                } else if (urand.NextFloat() < .2f) {
                    leafEdge++;
                }
            }
            int cwi = (int)(curWidth + leafEdge) + 1;

            for (int u = -cwi; u <= cwi; ++u) {
                for (int v = -cwi; v <= cwi; ++v) {
                    float len = math.lengthsq(new float2(u, v));
                    if (len < curWidth * curWidth) {
                        blocks.Set(x + u, y + i, z + v, Blocks.PINE);
                    } else if (leafLayer && len < (curWidth + leafEdge) * (curWidth + leafEdge)) {
                        if (blocks.Get(x + u, y + i, z + v) == Blocks.AIR) {
                            blocks.Set(x + u, y + i, z + v, Blocks.PINELEAF);
                        }
                    }
                }
            }

        }

        // add leaves to the top
        int topper = height > 20 ? 2 : 1;
        for (int i = height + 1; i < height + 1 + topper; ++i) {
            if (blocks.Get(x, y + i, z) == Blocks.AIR) {
                blocks.Set(x, y + i, z, Blocks.PINELEAF);
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
                        blocks.Set(x + u, y + i, z + v, Blocks.PINE);
                    } else if (i >= width * 2 && i % 2 == 0 || i > height - 1) { // otherwise leaves
                        if (blocks.Get(x + u, y + i, z + v) == Blocks.AIR) {
                            blocks.Set(x + u, y + i, z + v, Blocks.PINELEAF);
                        }
                    }
                }
            }
        }

    }

    public static void CheckNeighborNeedUpdate(Chunk chunk, int flags) {
        if ((flags & 0x1) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].BlocksWereUpdated();
        if ((flags & 0x2) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].BlocksWereUpdated();
        if ((flags & 0x4) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].BlocksWereUpdated();
        if ((flags & 0x8) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].BlocksWereUpdated();
        if ((flags & 0x10) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].BlocksWereUpdated();
        if ((flags & 0x20) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].BlocksWereUpdated();
        if ((flags & 0x40) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].BlocksWereUpdated();
        if ((flags & 0x80) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].BlocksWereUpdated();
        if ((flags & 0x100) != 0)
            chunk.neighbors[Dirs.DOWN].BlocksWereUpdated();
        if ((flags & 0x200) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].BlocksWereUpdated();
        if ((flags & 0x400) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].BlocksWereUpdated();
        if ((flags & 0x800) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].BlocksWereUpdated();
        if ((flags & 0x1000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].BlocksWereUpdated();
        if ((flags & 0x2000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].BlocksWereUpdated();
        if ((flags & 0x4000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].BlocksWereUpdated();
        if ((flags & 0x8000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.WEST].BlocksWereUpdated();
        if ((flags & 0x10000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.EAST].BlocksWereUpdated();
        if ((flags & 0x20000) != 0)
            chunk.neighbors[Dirs.UP].BlocksWereUpdated();
        if ((flags & 0x40000) != 0)
            chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].BlocksWereUpdated();
        if ((flags & 0x80000) != 0)
            chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].BlocksWereUpdated();
        if ((flags & 0x100000) != 0)
            chunk.neighbors[Dirs.SOUTH].BlocksWereUpdated();
        if ((flags & 0x200000) != 0)
            chunk.neighbors[Dirs.NORTH].neighbors[Dirs.WEST].BlocksWereUpdated();
        if ((flags & 0x400000) != 0)
            chunk.neighbors[Dirs.NORTH].neighbors[Dirs.EAST].BlocksWereUpdated();
        if ((flags & 0x800000) != 0)
            chunk.neighbors[Dirs.NORTH].BlocksWereUpdated();
        if ((flags & 0x1000000) != 0)
            chunk.neighbors[Dirs.WEST].BlocksWereUpdated();
        if ((flags & 0x2000000) != 0)
            chunk.neighbors[Dirs.EAST].BlocksWereUpdated();
    }

}