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

// would be cool to try to compress the index to be 2^24 instead so whole struct fits into a word
public struct LightRemovalNode {
    //public int index; // compressed x,y,z coordinate
    //public byte light;
    public uint indexAndLight; // index in first 24 bits, light in last 8
}

public static class LightCalculator {

    public const byte MAX_LIGHT = 16;
    const int ww = 96; // because processing 3x3x3 block of 32x32x32 chunks

    // assumes ops are provided in descending order of value
    public static int ProcessLightOps(ref NativeArray3x3<byte> light, ref NativeArray3x3<Block> blocks, NativeQueue<LightOp> ops, NativeQueue<int> lbfs, NativeQueue<LightRemovalNode> lrbfs) {

        int lightFlags = 0;

        while (ops.Count > 0) {
            LightOp op = ops.Dequeue();

            // linearized starting index of this operation
            // ranging from -32 -> -1 , 0 -> 31 , 32 -> 63 , so add 32 to build index from 0-95
            uint startIndex = (uint)((op.x + 32) + (op.z + 32) * ww + (op.y + 32) * ww * ww);

            if (op.val == 0) {  // remove light
                uint lit = light.Get(op.x, op.y, op.z);
                startIndex += lit << 24;

                // get previous value before overriding
                lrbfs.Enqueue(new LightRemovalNode { indexAndLight = startIndex });
                lightFlags = SetLight(ref light, lightFlags, op.x, op.y, op.z, 0);

                while (lrbfs.Count > 0) {
                    LightRemovalNode node = lrbfs.Dequeue();

                    byte nodeLight = (byte)(node.indexAndLight >> 24);
                    uint nodeIndex = node.indexAndLight & 0x111111; // keep only first 24 bits

                    // extract coords from index
                    int x = (int)(nodeIndex % ww - 32);
                    int y = (int)(nodeIndex / (ww * ww) - 32);
                    int z = (int)((nodeIndex % (ww * ww)) / ww - 32);

                    byte westLight = light.Get(x - 1, y, z);
                    if (westLight != 0 && westLight < nodeLight) {
                        lightFlags = SetLight(ref light, lightFlags, x - 1, y, z, 0);
                        lrbfs.Enqueue(new LightRemovalNode { indexAndLight = (uint)(x - 1 + 32 + (z + 32) * ww + (y + 32) * ww * ww + (uint)westLight << 24) });
                    } else if (westLight >= nodeLight) { // add to propagate queue so can fill gaps left behind by removal
                        lbfs.Enqueue(x - 1 + 32 + (z + 32) * ww + (y + 32) * ww * ww);
                    }
                    byte downLight = light.Get(x, y - 1, z);
                    if (downLight != 0 && downLight < nodeLight) {
                        lightFlags = SetLight(ref light, lightFlags, x, y - 1, z, 0);
                        lrbfs.Enqueue(new LightRemovalNode { indexAndLight = (uint)(x + 32 + (z + 32) * ww + (y - 1 + 32) * ww * ww + (uint)downLight << 24) });
                    } else if (downLight >= nodeLight) { // add to propagate queue so can fill gaps left behind by removal
                        lbfs.Enqueue(x + 32 + (z + 32) * ww + (y - 1 + 32) * ww * ww);
                    }
                    byte southLight = light.Get(x, y, z - 1);
                    if (southLight != 0 && southLight < nodeLight) {
                        lightFlags = SetLight(ref light, lightFlags, x, y, z - 1, 0);
                        lrbfs.Enqueue(new LightRemovalNode { indexAndLight = (uint)(x + 32 + (z - 1 + 32) * ww + (y + 32) * ww * ww + (uint)southLight << 24) });
                    } else if (southLight >= nodeLight) { // add to propagate queue so can fill gaps left behind by removal
                        lbfs.Enqueue(x + 32 + (z - 1 + 32) * ww + (y + 32) * ww * ww);
                    }
                    byte eastLight = light.Get(x + 1, y, z);
                    if (eastLight != 0 && eastLight < nodeLight) {
                        lightFlags = SetLight(ref light, lightFlags, x + 1, y, z, 0);
                        lrbfs.Enqueue(new LightRemovalNode { indexAndLight = (uint)(x + 1 + 32 + (z + 32) * ww + (y + 32) * ww * ww + (uint)eastLight << 24) });
                    } else if (eastLight >= nodeLight) { // add to propagate queue so can fill gaps left behind by removal
                        lbfs.Enqueue(x + 1 + 32 + (z + 32) * ww + (y + 32) * ww * ww);
                    }
                    byte upLight = light.Get(x, y + 1, z);
                    if (upLight != 0 && upLight < nodeLight) {
                        lightFlags = SetLight(ref light, lightFlags, x, y + 1, z, 0);
                        lrbfs.Enqueue(new LightRemovalNode { indexAndLight = (uint)(x + 32 + (z + 32) * ww + (y + 1 + 32) * ww * ww + (uint)upLight << 24) });
                    } else if (upLight >= nodeLight) { // add to propagate queue so can fill gaps left behind by removal
                        lbfs.Enqueue(x + 32 + (z + 32) * ww + (y + 1 + 32) * ww * ww);
                    }
                    byte northLight = light.Get(x, y, z + 1);
                    if (northLight != 0 && northLight < nodeLight) {
                        lightFlags = SetLight(ref light, lightFlags, x, y, z + 1, 0);
                        lrbfs.Enqueue(new LightRemovalNode { indexAndLight = (uint)(x + 32 + (z + 1 + 32) * ww + (y + 32) * ww * ww + (uint)northLight << 24) });
                    } else if (northLight >= nodeLight) { // add to propagate queue so can fill gaps left behind by removal
                        lbfs.Enqueue(x + 32 + (z + 1 + 32) * ww + (y + 32) * ww * ww);
                    }
                }

            } else { // propagate light

                lightFlags = SetLight(ref light, lightFlags, op.x, op.y, op.z, (byte)op.val);

                lbfs.Enqueue((int)startIndex);

            }

            // propagate (either way)
            while (lbfs.Count > 0) {
                int index = lbfs.Dequeue();

                // extract coords from index
                int x = index % ww - 32;
                int y = index / (ww * ww) - 32;
                int z = (index % (ww * ww)) / ww - 32;

                // get light level at this node
                byte lightLevel = light.Get(x, y, z);

                // check each neighbor if its air (should later be any transparent block)
                // if neighbor light level is 2 or more levels less than this node, set them to this light-1 and add to queue
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

        }

        //// logic for light refills (when placing transparent, simpler operation necessary than removal i think)
        //// kinda gunks up logic tho. like when replacing a torch need to do more checks that removal would just handle
        //while (ops.Count > 0) {
        //    LightOp op = ops.Dequeue();
        //    Debug.Assert(op.val < 0);

        //    byte w = light.Get(op.x - 1, op.y, op.z);
        //    byte d = light.Get(op.x, op.y - 1, op.z);
        //    byte s = light.Get(op.x, op.y, op.z - 1);
        //    byte e = light.Get(op.x + 1, op.y, op.z);
        //    byte u = light.Get(op.x, op.y + 1, op.z);
        //    byte n = light.Get(op.x, op.y, op.z + 1);

        //    int max = Mathf.Max(w, d, s, e, u, n); // assume light of nearest neighbor
        //    byte lightLevel = (byte)(max > 0 ? max - 1 : 0);
        //    lightFlags = SetLight(ref light, lightFlags, op.x, op.y, op.z, lightLevel);
        //    if(lightLevel > 0) {
        //        int startIndex = (op.x + 32) + (op.z + 32) * ww + (op.y + 32) * ww * ww;
        //        lbfs.Enqueue(startIndex);
        //    }
        //}
        //// one last propagate for light fills
        //lightFlags = Propagate(ref light, ref blocks, lbfs, lightFlags);

        return lightFlags;
    }

    // queue up initial light updates for any light emitting block in loaded chunk
    // could prob work this into generation and load routines more efficiently but whatever for now
    public static void CalcInitialLight(NativeArray<Block> blocks, NativeQueue<LightOp> lightOps) {
        for (int i = 0; i < blocks.Length; ++i) {
            int light = blocks[i].GetType().GetLight();
            if (light > 0) { // new light update
                int x = i % Chunk.SIZE;
                int y = i / (Chunk.SIZE * Chunk.SIZE);
                int z = (i % (Chunk.SIZE * Chunk.SIZE)) / Chunk.SIZE;
                lightOps.Enqueue(new LightOp { x = x, y = y, z = z, val = light });
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
