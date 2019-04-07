
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
    public ushort val; //should change this to Light type once sunlight is added
}

// need 5 bits for rgb each since max dist is 32
// and 5 bits for sun... so 20 bits basically
// so doing rgb in a short and then just a byte for sun
// could maybe just do a byte for each, maybe thad be faster
// greatest bit s is bool for if light is a source block or not
// so when removing, added source blocks to propagation list, to allow different strength lighting
public struct Light {
    public ushort torch; // s rrrrr ggggg bbbbb
    //public byte sun;
}

public struct LightRemovalNode {
    public int index; // compressed x,y,z coordinate
    public byte light;
}

public static class LightCalculator {

    public static bool GetIsLight(int torch) {
        return ((torch >> 15) & 1) == 1;
    }
    public static ushort SetIsLight(int torch, bool isLight) {
        return (ushort)((torch & 0b0_11111_11111_11111) | (isLight ? 0x8000 : 0));
    }

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
        return (ushort)((torch & 0b1_00000_11111_11111) | (val << 10)); // 1023, 31775, 32736
    }
    public static ushort SetGreen(int torch, int val) {
        return (ushort)((torch & 0b1_11111_00000_11111) | (val << 5));
    }
    public static ushort SetBlue(int torch, int val) {
        return (ushort)((torch & 0b1_11111_11111_00000) | (val));
    }

    // keepings these here for later, note the channels are backwards, so iterates in B G R order
    // channel 0 = B, 1 = G, 2 = R
    public static int GetChannel(int torch, int chan) {
        return (torch >> (chan * 5)) & 0b11111;
    }
    public static ushort SetChannel(int torch, int chan, int val) {
        return (ushort)((torch & ~(0b11111 << (chan * 5))) | (val << (chan * 5)));
    }

    public static ushort GetColor(int r, int g, int b) {
        Debug.Assert(r >= 0 && r <= MAX_LIGHT && g >= 0 && g <= MAX_LIGHT && b >= 0 && g <= MAX_LIGHT);
        return (ushort)((r << 10) | (g << 5) | b);
    }

    public const byte MAX_LIGHT = 31;
    const int S = Chunk.SIZE;
    const int W = S + S + S; // because processing 3x3x3 block of 32x32x32 chunks

    public static void ProcessLightOps(ref NativeArray3x3<Light> light, ref NativeArray3x3<Block> blocks, NativeArray<BlockData> blockData, NativeQueue<LightOp> ops, NativeQueue<int> lbfs, NativeQueue<LightRemovalNode> lrbfs) {

        light.flags = 0;

        while (ops.Count > 0) {
            LightOp op = ops.Dequeue();

            // linearized starting index of this operation
            // ranging from -32 -> -1 , 0 -> 31 , 32 -> 63 , so add 32 to build index from 0-95
            int opx = op.index % S;
            int opy = op.index / (S * S);
            int opz = (op.index % (S * S)) / S;

            int startIndex = (opx + S) + (opz + S) * W + (opy + S) * W * W;

            // set the target light at block to have correct 'IsLight' flag
            // lights are repropagated after removals, this allows support for lesser lights to be mixed in with the rest
            ushort opLight = light.Get(opx, opy, opz).torch;
            opLight = SetIsLight(opLight, op.val > 0);
            light.Set(opx, opy, opz, new Light { torch = opLight });

            // loop over each channel of light maps
            for (int cIndex = 0; cIndex < 3; cIndex++) {

                if (GetChannel(op.val, cIndex) == 0) { // remove light from this channel
                    // get current light value before overriding
                    ushort curLight = light.Get(opx, opy, opz).torch;
                    lrbfs.Enqueue(new LightRemovalNode { index = startIndex, light = (byte)GetChannel(curLight, cIndex) });
                    light.Set(opx, opy, opz, new Light { torch = SetChannel(curLight, cIndex, 0) });

                    while (lrbfs.Count > 0) {
                        LightRemovalNode node = lrbfs.Dequeue();

                        // extract coords from index
                        int x = node.index % W - S;
                        int y = node.index / (W * W) - S;
                        int z = (node.index % (W * W)) / W - S;

                        byte oneLess = (byte)(node.light - 1); // each time reduce light by one

                        ushort westLight = light.Get(x - 1, y, z).torch;
                        byte westChannel = (byte)GetChannel(westLight, cIndex);
                        if (westChannel != 0) {
                            int index = x - 1 + S + (z + S) * W + (y + S) * W * W;
                            if (westChannel < node.light) {
                                if (!GetIsLight(westLight)) {
                                    light.Set(x - 1, y, z, new Light { torch = SetChannel(westLight, cIndex, 0) });
                                } else { // if this node is a light, dont override value, but still add a removal node as if you did, then add to repropagate to fill it back in
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        ushort downLight = light.Get(x, y - 1, z).torch;
                        byte downChannel = (byte)GetChannel(downLight, cIndex);
                        if (downChannel != 0) {
                            int index = x + S + (z + S) * W + (y - 1 + S) * W * W;
                            if (downChannel < node.light) {
                                if (!GetIsLight(downLight)) {
                                    light.Set(x, y - 1, z, new Light { torch = SetChannel(downLight, cIndex, 0) });
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        ushort southLight = light.Get(x, y, z - 1).torch;
                        byte southChannel = (byte)GetChannel(southLight, cIndex);
                        if (southChannel != 0) {
                            int index = x + S + (z - 1 + S) * W + (y + S) * W * W;
                            if (southChannel < node.light) {
                                if (!GetIsLight(southLight)) {
                                    light.Set(x, y, z - 1, new Light { torch = SetChannel(southLight, cIndex, 0) });
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        ushort eastLight = light.Get(x + 1, y, z).torch;
                        byte eastChannel = (byte)GetChannel(eastLight, cIndex);
                        if (eastChannel != 0) {
                            int index = x + 1 + S + (z + S) * W + (y + S) * W * W;
                            if (eastChannel < node.light) {
                                if (!GetIsLight(eastLight)) {
                                    light.Set(x + 1, y, z, new Light { torch = SetChannel(eastLight, cIndex, 0) });
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        ushort upLight = light.Get(x, y + 1, z).torch;
                        byte upChannel = (byte)GetChannel(upLight, cIndex);
                        if (upChannel != 0) {
                            int index = x + S + (z + S) * W + (y + 1 + S) * W * W;
                            if (upChannel < node.light) {
                                if (!GetIsLight(upLight)) {
                                    light.Set(x, y + 1, z, new Light { torch = SetChannel(upLight, cIndex, 0) });
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        ushort northLight = light.Get(x, y, z + 1).torch;
                        byte northChannel = (byte)GetChannel(northLight, cIndex);
                        if (northChannel != 0) {
                            int index = x + S + (z + 1 + S) * W + (y + S) * W * W;
                            if (northChannel < node.light) {
                                if (!GetIsLight(northLight)) {
                                    light.Set(x, y, z + 1, new Light { torch = SetChannel(northLight, cIndex, 0) });
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }


                    }

                } else { // propagate light from this channel
                    ushort curLight = light.Get(opx, opy, opz).torch;

                    light.Set(opx, opy, opz, new Light { torch = SetChannel(curLight, cIndex, GetChannel(op.val, cIndex)) });

                    // if the ops channel is same or less than current channel, dont need to progagate
                    if (GetChannel(op.val, cIndex) <= GetChannel(curLight, cIndex)) {
                        continue;
                    }

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
                    int mChan = GetChannel(light.Get(x, y, z).torch, cIndex);

                    // check each neighbor if its air (should be any non solid except torches)
                    // if neighbor light level is 2 or more levels less than this node, set them to this light-1 and add to queue
                    // also add like 1 for each additional light reduction value
                    BlockData westBD = blockData[blocks.Get(x - 1, y, z).type];
                    if (westBD.lightReduction < mChan) {
                        ushort nLight = light.Get(x - 1, y, z).torch;
                        if (GetChannel(nLight, cIndex) + 2 + westBD.lightReduction <= mChan) {
                            light.Set(x - 1, y, z, new Light { torch = SetChannel(nLight, cIndex, mChan - 1 - westBD.lightReduction) });
                            lbfs.Enqueue(x - 1 + S + (z + S) * W + (y + S) * W * W);
                        }
                    }
                    BlockData downBD = blockData[blocks.Get(x, y - 1, z).type];
                    if (downBD.lightReduction < mChan) {
                        ushort nLight = light.Get(x, y - 1, z).torch;
                        if (GetChannel(nLight, cIndex) + 2 + downBD.lightReduction <= mChan) {
                            light.Set(x, y - 1, z, new Light { torch = SetChannel(nLight, cIndex, mChan - 1 - downBD.lightReduction) });
                            lbfs.Enqueue(x + S + (z + S) * W + (y - 1 + S) * W * W);
                        }
                    }
                    BlockData southBD = blockData[blocks.Get(x, y, z - 1).type];
                    if (southBD.lightReduction < mChan) {
                        ushort nLight = light.Get(x, y, z - 1).torch;
                        if (GetChannel(nLight, cIndex) + 2 + southBD.lightReduction <= mChan) {
                            light.Set(x, y, z - 1, new Light { torch = SetChannel(nLight, cIndex, mChan - 1 - southBD.lightReduction) });
                            lbfs.Enqueue(x + S + (z - 1 + S) * W + (y + S) * W * W);
                        }
                    }
                    BlockData eastBD = blockData[blocks.Get(x + 1, y, z).type];
                    if (eastBD.lightReduction < mChan) {
                        ushort nLight = light.Get(x + 1, y, z).torch;
                        if (GetChannel(nLight, cIndex) + 2 + eastBD.lightReduction <= mChan) {
                            light.Set(x + 1, y, z, new Light { torch = SetChannel(nLight, cIndex, mChan - 1 - eastBD.lightReduction) });
                            lbfs.Enqueue(x + 1 + S + (z + S) * W + (y + S) * W * W);
                        }
                    }
                    BlockData upBD = blockData[blocks.Get(x, y + 1, z).type];
                    if (upBD.lightReduction < mChan) {
                        ushort nLight = light.Get(x, y + 1, z).torch;
                        if (GetChannel(nLight, cIndex) + 2 + upBD.lightReduction <= mChan) {
                            light.Set(x, y + 1, z, new Light { torch = SetChannel(nLight, cIndex, mChan - 1 - upBD.lightReduction) });
                            lbfs.Enqueue(x + S + (z + S) * W + (y + 1 + S) * W * W);
                        }
                    }
                    BlockData northBD = blockData[blocks.Get(x, y, z + 1).type];
                    if (northBD.lightReduction < mChan) {
                        ushort nLight = light.Get(x, y, z + 1).torch;
                        if (GetChannel(nLight, cIndex) + 2 + northBD.lightReduction <= mChan) {
                            light.Set(x, y, z + 1, new Light { torch = SetChannel(nLight, cIndex, mChan - 1 - northBD.lightReduction) });
                            lbfs.Enqueue(x + S + (z + 1 + S) * W + (y + S) * W * W);
                        }
                    }


                }
            }

        }

    }

    // queue up initial light updates for any light emitting block in loaded chunk
    // could prob work this into generation and load routines more efficiently but whatever for now
    public static void CalcInitialLightOps(NativeArray<Block> blocks, NativeArray<BlockData> blockData, NativeQueue<LightOp> lightOps) {
        for (int i = 0; i < blocks.Length; ++i) {
            ushort light = blockData[blocks[i].type].light;
            if (light > 0) { // new light update
                lightOps.Enqueue(new LightOp { index = i, val = light });
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
                default:
                    colors.Add(GetColorFromLight(lights.Get(x, y, z)));
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
