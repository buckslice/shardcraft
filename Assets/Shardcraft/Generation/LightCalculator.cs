﻿
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;


// optimization notes:
// tried compressing LightRemovalNode into one int but didnt really improve performance so left out cuz made everything uglier
// tried making SetLight exit early for center chunk but was ~8ms worse performance
// tried compressing everything in main BFSs into for loops for 6 neighbors but was a bit slower, like ~68ms avg to ~75ms
// also rewrote the GetRed,GetGreen,and setter etc functions into single functions to see if using these instead of Funcs were faster
// but it was pretty much the same performancewise, i couldnt tell, so i kept Funcs as its clearer id say


public struct LightOp {
    public int index;
    public ushort val;
}

// need 5 bits for rgb each since max dist is 32
// and 5 bits for sun... so 20 bits basically
// so doing rgb in a short and then just a byte for sun
// could maybe just do a byte for each, maybe thad be faster
public struct Light {
    public ushort torch; // u rrrrr ggggg bbbbb
    //public byte sun;
}

public struct LightRemovalNode {
    public int index; // compressed x,y,z coordinate
    public byte light;
}

public static class LightCalculator {

    public static int GetRed(int torch) {
        return (torch >> 10) & 0b11111; // hex 0x1F
    }
    public static int GetGreen(int torch) {
        return (torch >> 5) & 0b11111;
    }
    public static int GetBlue(int torch) {
        return torch & 0b11111;
    }
    public static ushort SetRed(int torch, int val) {
        return (ushort)((torch & 0b00000_11111_11111) | (val << 10)); // 1023, 31775, 32736
    }
    public static ushort SetGreen(int torch, int val) {
        return (ushort)((torch & 0b11111_00000_11111) | (val << 5));
    }
    public static ushort SetBlue(int torch, int val) {
        return (ushort)((torch & 0b11111_11111_00000) | (val));
    }

    // keepings these here for later, note the channels are backwards, so iterates in B G R order
    // channel 0 = B, 1 = G, 2 = R
    public static int GetChan(int torch, int chan) {
        return (torch >> (chan * 5)) & 0b11111;
    }
    public static ushort SetChan(int torch, int chan, int val) {
        return (ushort)((torch & ~(0b11111 << (chan * 5))) | (val << (chan * 5)));
    }

    public static ushort GetColor(int r, int g, int b) {
        Debug.Assert(r >= 0 && r <= MAX_LIGHT && g >= 0 && g <= MAX_LIGHT && b >= 0 && g <= MAX_LIGHT);
        return (ushort)((r << 10) | (g << 5) | b);
    }

    public const byte MAX_LIGHT = 31;
    const int S = Chunk.SIZE;
    const int W = S + S + S; // because processing 3x3x3 block of 32x32x32 chunks

    public static int ProcessLightOps(ref NativeArray3x3<Light> light, ref NativeArray3x3<Block> blocks, NativeQueue<LightOp> ops, NativeQueue<int> lbfs, NativeQueue<LightRemovalNode> lrbfs) {

        int lightFlags = 0;

        System.Func<int, int> GetChannel;
        System.Func<int, int, ushort> SetChannel;

        while (ops.Count > 0) {
            LightOp op = ops.Dequeue();

            // linearized starting index of this operation
            // ranging from -32 -> -1 , 0 -> 31 , 32 -> 63 , so add 32 to build index from 0-95
            int opx = op.index % S;
            int opy = op.index / (S * S);
            int opz = (op.index % (S * S)) / S;

            int startIndex = (opx + S) + (opz + S) * W + (opy + S) * W * W;

            // loop over each channel of light maps
            for (int cIndex = 0; cIndex < 3; cIndex++) {
                if (cIndex == 0) {
                    GetChannel = GetRed;
                    SetChannel = SetRed;
                } else if (cIndex == 1) {
                    GetChannel = GetGreen;
                    SetChannel = SetGreen;
                } else {
                    GetChannel = GetBlue;
                    SetChannel = SetBlue;
                }

                if (GetChannel(op.val) == 0) { // remove light from this channel
                    // get current light value before overriding
                    ushort curLight = light.Get(opx, opy, opz).torch;
                    lrbfs.Enqueue(new LightRemovalNode { index = startIndex, light = (byte)GetChannel(curLight) });
                    lightFlags = SetLight(ref light, lightFlags, opx, opy, opz, new Light { torch = SetChannel(curLight, 0) });

                    while (lrbfs.Count > 0) {
                        LightRemovalNode node = lrbfs.Dequeue();

                        // extract coords from index
                        int x = node.index % W - S;
                        int y = node.index / (W * W) - S;
                        int z = (node.index % (W * W)) / W - S;

                        ushort westLight = light.Get(x - 1, y, z).torch;
                        byte westChannel = (byte)GetChannel(westLight);
                        if (westChannel != 0 && westChannel < node.light) {
                            lightFlags = SetLight(ref light, lightFlags, x - 1, y, z, new Light { torch = SetChannel(westLight, 0) });
                            lrbfs.Enqueue(new LightRemovalNode { index = x - 1 + S + (z + S) * W + (y + S) * W * W, light = westChannel });
                        } else if (westChannel >= node.light) { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(x - 1 + S + (z + S) * W + (y + S) * W * W);
                        }
                        ushort downLight = light.Get(x, y - 1, z).torch;
                        byte downChannel = (byte)GetChannel(downLight);
                        if (downChannel != 0 && downChannel < node.light) {
                            lightFlags = SetLight(ref light, lightFlags, x, y - 1, z, new Light { torch = SetChannel(downLight, 0) });
                            lrbfs.Enqueue(new LightRemovalNode { index = x + S + (z + S) * W + (y - 1 + S) * W * W, light = downChannel });
                        } else if (downChannel >= node.light) { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(x + S + (z + S) * W + (y - 1 + S) * W * W);
                        }

                        ushort southLight = light.Get(x, y, z - 1).torch;
                        byte southChannel = (byte)GetChannel(southLight);
                        if (southChannel != 0 && southChannel < node.light) {
                            lightFlags = SetLight(ref light, lightFlags, x, y, z - 1, new Light { torch = SetChannel(southLight, 0) });
                            lrbfs.Enqueue(new LightRemovalNode { index = x + S + (z - 1 + S) * W + (y + S) * W * W, light = southChannel });
                        } else if (southChannel >= node.light) { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(x + S + (z - 1 + S) * W + (y + S) * W * W);
                        }

                        ushort eastLight = light.Get(x + 1, y, z).torch;
                        byte eastChannel = (byte)GetChannel(eastLight);
                        if (eastChannel != 0 && eastChannel < node.light) {
                            lightFlags = SetLight(ref light, lightFlags, x + 1, y, z, new Light { torch = SetChannel(eastLight, 0) });
                            lrbfs.Enqueue(new LightRemovalNode { index = x + 1 + S + (z + S) * W + (y + S) * W * W, light = eastChannel });
                        } else if (eastChannel >= node.light) { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(x + 1 + S + (z + S) * W + (y + S) * W * W);
                        }

                        ushort upLight = light.Get(x, y + 1, z).torch;
                        byte upChannel = (byte)GetChannel(upLight);
                        if (upChannel != 0 && upChannel < node.light) {
                            lightFlags = SetLight(ref light, lightFlags, x, y + 1, z, new Light { torch = SetChannel(upLight, 0) });
                            lrbfs.Enqueue(new LightRemovalNode { index = x + S + (z + S) * W + (y + 1 + S) * W * W, light = upChannel });
                        } else if (upChannel >= node.light) { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(x + S + (z + S) * W + (y + 1 + S) * W * W);
                        }

                        ushort northLight = light.Get(x, y, z + 1).torch;
                        byte northChannel = (byte)GetChannel(northLight);
                        if (northChannel != 0 && northChannel < node.light) {
                            lightFlags = SetLight(ref light, lightFlags, x, y, z + 1, new Light { torch = SetChannel(northLight, 0) });
                            lrbfs.Enqueue(new LightRemovalNode { index = x + S + (z + 1 + S) * W + (y + S) * W * W, light = northChannel });
                        } else if (northChannel >= node.light) { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(x + S + (z + 1 + S) * W + (y + S) * W * W);
                        }
                    }

                } else { // propagate light from this channel

                    ushort curLight = light.Get(opx, opy, opz).torch;
                     
                    lightFlags = SetLight(ref light, lightFlags, opx, opy, opz, new Light { torch = SetChannel(curLight, GetChannel(op.val)) });

                    lbfs.Enqueue(startIndex);

                }

                // propagate (either way)
                while (lbfs.Count > 0) {
                    int index = lbfs.Dequeue();

                    // extract coords from index
                    int x = index % W - S;
                    int y = index / (W * W) - S;
                    int z = (index % (W * W)) / W - S;

                    // get light level at this node
                    int mChan = GetChannel(light.Get(x, y, z).torch);

                    // check each neighbor if its air (should later be any transparent block)
                    // if neighbor light level is 2 or more levels less than this node, set them to this light-1 and add to queue
                    if (blocks.Get(x - 1, y, z) == Blocks.AIR) {
                        ushort nLight = light.Get(x - 1, y, z).torch;
                        if (GetChannel(nLight) + 2 <= mChan) {
                            lightFlags = SetLight(ref light, lightFlags, x - 1, y, z, new Light { torch = SetChannel(nLight, mChan - 1) });
                            lbfs.Enqueue(x - 1 + S + (z + S) * W + (y + S) * W * W);
                        }
                    }
                    if (blocks.Get(x, y - 1, z) == Blocks.AIR) {
                        ushort nLight = light.Get(x, y - 1, z).torch;
                        if (GetChannel(nLight) + 2 <= mChan) {
                            lightFlags = SetLight(ref light, lightFlags, x, y - 1, z, new Light { torch = SetChannel(nLight, mChan - 1) });
                            lbfs.Enqueue(x + S + (z + S) * W + (y - 1 + S) * W * W);
                        }
                    }
                    if (blocks.Get(x, y, z - 1) == Blocks.AIR) {
                        ushort nLight = light.Get(x, y, z - 1).torch;
                        if (GetChannel(nLight) + 2 <= mChan) {
                            lightFlags = SetLight(ref light, lightFlags, x, y, z - 1, new Light { torch = SetChannel(nLight, mChan - 1) });
                            lbfs.Enqueue(x + S + (z - 1 + S) * W + (y + S) * W * W);
                        }
                    }
                    if (blocks.Get(x + 1, y, z) == Blocks.AIR) {
                        ushort nLight = light.Get(x + 1, y, z).torch;
                        if (GetChannel(nLight) + 2 <= mChan) {
                            lightFlags = SetLight(ref light, lightFlags, x + 1, y, z, new Light { torch = SetChannel(nLight, mChan - 1) });
                            lbfs.Enqueue(x + 1 + S + (z + S) * W + (y + S) * W * W);
                        }
                    }
                    if (blocks.Get(x, y + 1, z) == Blocks.AIR) {
                        ushort nLight = light.Get(x, y + 1, z).torch;
                        if (GetChannel(nLight) + 2 <= mChan) {
                            lightFlags = SetLight(ref light, lightFlags, x, y + 1, z, new Light { torch = SetChannel(nLight, mChan - 1) });
                            lbfs.Enqueue(x + S + (z + S) * W + (y + 1 + S) * W * W);
                        }
                    }
                    if (blocks.Get(x, y, z + 1) == Blocks.AIR) {
                        ushort nLight = light.Get(x, y, z + 1).torch;
                        if (GetChannel(nLight) + 2 <= mChan) {
                            lightFlags = SetLight(ref light, lightFlags, x, y, z + 1, new Light { torch = SetChannel(nLight, mChan - 1) });
                            lbfs.Enqueue(x + S + (z + 1 + S) * W + (y + S) * W * W);
                        }
                    }


                }
            }

        }

        return lightFlags;
    }

    // queue up initial light updates for any light emitting block in loaded chunk
    // could prob work this into generation and load routines more efficiently but whatever for now
    public static void CalcInitialLightOps(NativeArray<Block> blocks, NativeQueue<LightOp> lightOps) {
        for (int i = 0; i < blocks.Length; ++i) {
            ushort light = blocks[i].GetType().GetLight();
            if (light > 0) { // new light update
                lightOps.Enqueue(new LightOp { index = i, val = light });
            }
        }

    }
    static int SetLight(ref NativeArray3x3<Light> light, int lightFlags, int x, int y, int z, Light v) {
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
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x2) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].lightUpdate = true;
        if ((lightFlags & 0x4) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].lightUpdate = true;
        if ((lightFlags & 0x8) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x10) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].lightUpdate = true;
        if ((lightFlags & 0x20) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].lightUpdate = true;
        if ((lightFlags & 0x40) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x80) != 0)
            chunk.neighbors[Dirs.DOWN].neighbors[Dirs.EAST].lightUpdate = true;
        if ((lightFlags & 0x100) != 0)
            chunk.neighbors[Dirs.DOWN].lightUpdate = true;
        if ((lightFlags & 0x200) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x400) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].lightUpdate = true;
        if ((lightFlags & 0x800) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.SOUTH].lightUpdate = true;
        if ((lightFlags & 0x1000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x2000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].lightUpdate = true;
        if ((lightFlags & 0x4000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.NORTH].lightUpdate = true;
        if ((lightFlags & 0x8000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x10000) != 0)
            chunk.neighbors[Dirs.UP].neighbors[Dirs.EAST].lightUpdate = true;
        if ((lightFlags & 0x20000) != 0)
            chunk.neighbors[Dirs.UP].lightUpdate = true;
        if ((lightFlags & 0x40000) != 0)
            chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x80000) != 0)
            chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].lightUpdate = true;
        if ((lightFlags & 0x100000) != 0)
            chunk.neighbors[Dirs.SOUTH].lightUpdate = true;
        if ((lightFlags & 0x200000) != 0)
            chunk.neighbors[Dirs.NORTH].neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x400000) != 0)
            chunk.neighbors[Dirs.NORTH].neighbors[Dirs.EAST].lightUpdate = true;
        if ((lightFlags & 0x800000) != 0)
            chunk.neighbors[Dirs.NORTH].lightUpdate = true;
        if ((lightFlags & 0x1000000) != 0)
            chunk.neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x2000000) != 0)
            chunk.neighbors[Dirs.EAST].lightUpdate = true;
    }


    public static void LightUpdate(ref NativeArray3x3<Light> lights, NativeList<Face> faces, NativeList<Color32> colors) {
        for (int i = 0; i < faces.Length; ++i) {

            int pos = faces[i].pos;
            int x = pos % Chunk.SIZE;
            int y = pos / (Chunk.SIZE * Chunk.SIZE);
            int z = (pos % (Chunk.SIZE * Chunk.SIZE)) / Chunk.SIZE;

            switch (faces[i].dir) {
                case Dir.west:
                    colors.Add(GetColorFromLight(lights.Get(x - 1, y, z)));
                    continue;
                case Dir.down:
                    colors.Add(GetColorFromLight(lights.Get(x, y - 1, z)));
                    continue;
                case Dir.south:
                    colors.Add(GetColorFromLight(lights.Get(x, y, z - 1)));
                    continue;
                case Dir.east:
                    colors.Add(GetColorFromLight(lights.Get(x + 1, y, z)));
                    continue;
                case Dir.up:
                    colors.Add(GetColorFromLight(lights.Get(x, y + 1, z)));
                    continue;
                case Dir.north:
                    colors.Add(GetColorFromLight(lights.Get(x, y, z + 1)));
                    continue;

            }

        }
    }

    public static Color32 GetColorFromLight(Light light) {
        float red = (float)GetRed(light.torch) / MAX_LIGHT;
        float green = (float)GetGreen(light.torch) / MAX_LIGHT;
        float blue = (float)GetBlue(light.torch) / MAX_LIGHT;
        return new Color(red, green, blue, 1.0f);
    }


}
