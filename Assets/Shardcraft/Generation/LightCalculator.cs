using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

public struct LightOp {
    public int x;
    public int y;
    public int z;
    public int val;
}

public static class LightCalculator {

    //public struct LightNode {
    //    short index; // x y z coordinate!
    //}

    public const byte MAX_LIGHT = 16;

    public static void ProcessLightOps(ref NativeArray3x3<byte> light, ref NativeArray3x3<Block> blocks, NativeQueue<LightOp> ops, NativeQueue<int> lbfs) {
        const int ww = 96; // because processing 3x3x3 block of 32x32x32 chunks

        int lightFlags = 0;

        while (ops.Count > 0) {
            LightOp op = ops.Dequeue();

            if (op.val > 0) { // light propagation
                Debug.Assert(lbfs.Count == 0);

                // set the light here
                lightFlags = SetLight(ref light, lightFlags, op.x, op.y, op.z, (byte)op.val);

                // ranging from -32 -> -1 , 0 -> 31 , 32 -> 63 , so add 32 to build index from 0-95
                int startIndex = (op.x + 32) + (op.z + 32) * ww + (op.y + 32) * ww * ww;
                lbfs.Enqueue(startIndex);

                while (lbfs.Count > 0) {
                    int index = lbfs.Dequeue();

                    // extract coords from index
                    int x = index % ww - 32;
                    int y = index / (ww * ww) - 32;
                    int z = (index % (ww * ww)) / ww - 32;

                    // get light level at this node
                    byte lightLevel = light.Get(x, y, z);

                    // check each neighbor if its air (should later be any transparent block)
                    // if neighbor light level is 2 or more levels less than this node, set them to this light -1 and add to queue
                    if (blocks.Get(x - 1, y, z) == Blocks.AIR && light.Get(x - 1, y, z) + 2 <= lightLevel) {
                        lightFlags = SetLight(ref light, lightFlags, x - 1, y, z, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x - 1 + 32 + (z + 32) * ww + (y + 32) * ww * ww);
                    }
                    if (blocks.Get(x, y - 1, z) == Blocks.AIR && light.Get(x, y - 1, z) + 2 <= lightLevel) {
                        lightFlags = SetLight(ref light, lightFlags, x, y - 1, z, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x + 32 + (z + 32) * ww + (y - 1 + 32) * ww * ww);
                    }
                    if (blocks.Get(x, y, z - 1) == Blocks.AIR && light.Get(x, y, z - 1) + 2 <= lightLevel) {
                        lightFlags = SetLight(ref light, lightFlags, x, y, z - 1, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x + 32 + (z - 1 + 32) * ww + (y + 32) * ww * ww);
                    }
                    if (blocks.Get(x + 1, y, z) == Blocks.AIR && light.Get(x + 1, y, z) + 2 <= lightLevel) {
                        lightFlags = SetLight(ref light, lightFlags, x + 1, y, z, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x + 1 + 32 + (z + 32) * ww + (y + 32) * ww * ww);
                    }
                    if (blocks.Get(x, y + 1, z) == Blocks.AIR && light.Get(x, y + 1, z) + 2 <= lightLevel) {
                        lightFlags = SetLight(ref light, lightFlags, x, y + 1, z, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x + 32 + (z + 32) * ww + (y + 1 + 32) * ww * ww);
                    }
                    if (blocks.Get(x, y, z + 1) == Blocks.AIR && light.Get(x, y, z + 1) + 2 <= lightLevel) {
                        lightFlags = SetLight(ref light, lightFlags, x, y, z + 1, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x + 32 + (z + 1 + 32) * ww + (y + 32) * ww * ww);
                    }

                }

            } else if (op.val == 0) { // light removal... todo

            } else { // op is -1 means a block was removed that wasn't a light. so update its light from surrounding values
                byte w = light.Get(op.x - 1, op.y, op.z);
                byte d = light.Get(op.x, op.y - 1, op.z);
                byte s = light.Get(op.x, op.y, op.z - 1);
                byte e = light.Get(op.x + 1, op.y, op.z);
                byte u = light.Get(op.x, op.y + 1, op.z);
                byte n = light.Get(op.x, op.y, op.z + 1);

                int max = Mathf.Max(w, d, s, e, u, n);
                lightFlags = SetLight(ref light, lightFlags, op.x, op.y, op.z, (byte)(max > 0 ? max - 1 : 0));
            }

        }

    }

    const int S = Chunk.SIZE;
    static int SetLight(ref NativeArray3x3<byte> light, int lightFlags, int x, int y, int z, byte v) {
        if (y < 0) {
            if (z < 0) {
                if (x < 0) {
                    light.dsw[(x + S) + (z + S) * S + (y + S) * S * S] = v;
                    return lightFlags | 0x1;
                } else if (x >= S) {
                    light.dse[(x - S) + (z + S) * S + (y + S) * S * S] = v;
                    return lightFlags | 0x2;
                } else {
                    light.ds[x + (z + S) * S + (y + S) * S * S] = v;
                    return lightFlags | 0x4;
                }
            } else if (z >= S) {
                if (x < 0) {
                    light.dnw[(x + S) + (z - S) * S + (y + S) * S * S] = v;
                    return lightFlags | 0x8;
                } else if (x >= S) {
                    light.dne[(x - S) + (z - S) * S + (y + S) * S * S] = v;
                    return lightFlags | 0x10;
                } else {
                    light.dn[x + (z - S) * S + (y + S) * S * S] = v;
                    return lightFlags | 0x20;
                }
            } else {
                if (x < 0) {
                    light.dw[(x + S) + z * S + (y + S) * S * S] = v;
                    return lightFlags | 0x40;
                } else if (x >= S) {
                    light.de[(x - S) + z * S + (y + S) * S * S] = v;
                    return lightFlags | 0x80;
                } else {
                    light.d[x + z * S + (y + S) * S * S] = v;
                    return lightFlags | 0x100;
                }
            }
        } else if (y >= S) {
            if (z < 0) {
                if (x < 0) {
                    light.usw[(x + S) + (z + S) * S + (y - S) * S * S] = v;
                    return lightFlags | 0x200;
                } else if (x >= S) {
                    light.use[(x - S) + (z + S) * S + (y - S) * S * S] = v;
                    return lightFlags | 0x400;
                } else {
                    light.us[x + (z + S) * S + (y - S) * S * S] = v;
                    return lightFlags | 0x800;
                }
            } else if (z >= S) {
                if (x < 0) {
                    light.unw[(x + S) + (z - S) * S + (y - S) * S * S] = v;
                    return lightFlags | 0x1000;
                } else if (x >= S) {
                    light.une[(x - S) + (z - S) * S + (y - S) * S * S] = v;
                    return lightFlags | 0x2000;
                } else {
                    light.un[x + (z - S) * S + (y - S) * S * S] = v;
                    return lightFlags | 0x4000;
                }
            } else {
                if (x < 0) {
                    light.uw[(x + S) + z * S + (y - S) * S * S] = v;
                    return lightFlags | 0x8000;
                } else if (x >= S) {
                    light.ue[(x - S) + z * S + (y - S) * S * S] = v;
                    return lightFlags | 0x10000;
                } else {
                    light.u[x + z * S + (y - S) * S * S] = v;
                    return lightFlags | 0x20000;
                }
            }
        } else {
            if (z < 0) {
                if (x < 0) {
                    light.sw[(x + S) + (z + S) * S + y * S * S] = v;
                    return lightFlags | 0x40000;
                } else if (x >= S) {
                    light.se[(x - S) + (z + S) * S + y * S * S] = v;
                    return lightFlags | 0x80000;
                } else {
                    light.s[x + (z + S) * S + y * S * S] = v;
                    return lightFlags | 0x100000;
                }
            } else if (z >= S) {
                if (x < 0) {
                    light.nw[(x + S) + (z - S) * S + y * S * S] = v;
                    return lightFlags | 0x200000;
                } else if (x >= S) {
                    light.ne[(x - S) + (z - S) * S + y * S * S] = v;
                    return lightFlags | 0x400000;
                } else {
                    light.n[x + (z - S) * S + y * S * S] = v;
                    return lightFlags | 0x800000;
                }
            } else {
                if (x < 0) {
                    light.w[(x + S) + z * S + y * S * S] = v;
                    return lightFlags | 0x1000000;
                } else if (x >= S) {
                    light.e[(x - S) + z * S + y * S * S] = v;
                    return lightFlags | 0x2000000;
                } else {
                    light.c[x + z * S + y * S * S] = v;
                    return lightFlags;
                }
            }
        }
    }

    public static void CheckNeighborLightUpdate(Chunk chunk, int lightFlags) {
        if ((lightFlags & 0x1) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].update = true;
        if ((lightFlags & 0x2) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].update = true;
        if ((lightFlags & 0x4) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].update = true;
        if ((lightFlags & 0x8) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].update = true;
        if ((lightFlags & 0x10) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].update = true;
        if ((lightFlags & 0x20) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].update = true;
        if ((lightFlags & 0x40) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].update = true;
        if ((lightFlags & 0x80) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].update = true;
        if ((lightFlags & 0x100) != 0)
            chunk.neighbors[Dirs.DOWN].update = true;
        if ((lightFlags & 0x200) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].update = true;
        if ((lightFlags & 0x400) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].update = true;
        if ((lightFlags & 0x800) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].update = true;
        if ((lightFlags & 0x1000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].update = true;
        if ((lightFlags & 0x2000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].update = true;
        if ((lightFlags & 0x4000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].update = true;
        if ((lightFlags & 0x8000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.WEST].update = true;
        if ((lightFlags & 0x10000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.EAST].update = true;
        if ((lightFlags & 0x20000) != 0)
            chunk.neighbors[Dirs.UP].update = true;
        if ((lightFlags & 0x40000) != 0)
            chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].update = true;
        if ((lightFlags & 0x80000) != 0)
            chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].update = true;
        if ((lightFlags & 0x100000) != 0)
            chunk.neighbors[Dirs.SOUTH].update = true;
        if ((lightFlags & 0x200000) != 0)
            chunk.neighbors[Dirs.NORTH].neighbors[Dirs.WEST].update = true;
        if ((lightFlags & 0x400000) != 0)
            chunk.neighbors[Dirs.NORTH].neighbors[Dirs.EAST].update = true;
        if ((lightFlags & 0x800000) != 0)
            chunk.neighbors[Dirs.NORTH].update = true;
        if ((lightFlags & 0x1000000) != 0)
            chunk.neighbors[Dirs.WEST].update = true;
        if ((lightFlags & 0x2000000) != 0)
            chunk.neighbors[Dirs.EAST].update = true;
    }



}
