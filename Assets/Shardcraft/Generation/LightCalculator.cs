
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Assertions;

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
    public byte sun;    // ---vvvvv
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
        Assert.IsTrue(r >= 0 && r <= MAX_LIGHT && g >= 0 && g <= MAX_LIGHT && b >= 0 && g <= MAX_LIGHT);
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
            Light opLight = light.Get(opx, opy, opz);
            opLight.torch = SetIsLight(opLight.torch, op.val > 0);
            light.Set(opx, opy, opz, opLight);

            // loop over each channel of light maps
            for (int cIndex = 0; cIndex < 3; cIndex++) {

                if (GetChannel(op.val, cIndex) == 0) { // remove light from this channel
                    // add current light to removal queue then set to zero
                    Light curLight = light.Get(opx, opy, opz);
                    lrbfs.Enqueue(new LightRemovalNode { index = startIndex, light = (byte)GetChannel(curLight.torch, cIndex) });
                    curLight.torch = SetChannel(curLight.torch, cIndex, 0);
                    light.Set(opx, opy, opz, curLight);

                    while (lrbfs.Count > 0) {
                        LightRemovalNode node = lrbfs.Dequeue();

                        // extract coords from index
                        int x = node.index % W - S;
                        int y = node.index / (W * W) - S;
                        int z = (node.index % (W * W)) / W - S;

                        byte oneLess = (byte)(node.light - 1); // each time reduce light by one

                        //ushort westLight = light.Get(x - 1, y, z).torch;
                        Light westLight = light.Get(x - 1, y, z);
                        byte westChannel = (byte)GetChannel(westLight.torch, cIndex);
                        if (westChannel != 0) {
                            int index = x - 1 + S + (z + S) * W + (y + S) * W * W;
                            if (westChannel < node.light) {
                                if (!GetIsLight(westLight.torch)) {
                                    westLight.torch = SetChannel(westLight.torch, cIndex, 0);
                                    light.Set(x - 1, y, z, westLight);
                                } else { // if this node is a light, dont override value, but still add a removal node as if you did, then add to repropagate to fill it back in
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        Light downLight = light.Get(x, y - 1, z);
                        byte downChannel = (byte)GetChannel(downLight.torch, cIndex);
                        if (downChannel != 0) {
                            int index = x + S + (z + S) * W + (y - 1 + S) * W * W;
                            if (downChannel < node.light) {
                                if (!GetIsLight(downLight.torch)) {
                                    downLight.torch = SetChannel(downLight.torch, cIndex, 0);
                                    light.Set(x, y - 1, z, downLight);
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        Light southLight = light.Get(x, y, z - 1);
                        byte southChannel = (byte)GetChannel(southLight.torch, cIndex);
                        if (southChannel != 0) {
                            int index = x + S + (z - 1 + S) * W + (y + S) * W * W;
                            if (southChannel < node.light) {
                                if (!GetIsLight(southLight.torch)) {
                                    southLight.torch = SetChannel(southLight.torch, cIndex, 0);
                                    light.Set(x, y, z - 1, southLight);
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        Light eastLight = light.Get(x + 1, y, z);
                        byte eastChannel = (byte)GetChannel(eastLight.torch, cIndex);
                        if (eastChannel != 0) {
                            int index = x + 1 + S + (z + S) * W + (y + S) * W * W;
                            if (eastChannel < node.light) {
                                if (!GetIsLight(eastLight.torch)) {
                                    eastLight.torch = SetChannel(eastLight.torch, cIndex, 0);
                                    light.Set(x + 1, y, z, eastLight);
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        Light upLight = light.Get(x, y + 1, z);
                        byte upChannel = (byte)GetChannel(upLight.torch, cIndex);
                        if (upChannel != 0) {
                            int index = x + S + (z + S) * W + (y + 1 + S) * W * W;
                            if (upChannel < node.light) {
                                if (!GetIsLight(upLight.torch)) {
                                    upLight.torch = SetChannel(upLight.torch, cIndex, 0);
                                    light.Set(x, y + 1, z, upLight);
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        Light northLight = light.Get(x, y, z + 1);
                        byte northChannel = (byte)GetChannel(northLight.torch, cIndex);
                        if (northChannel != 0) {
                            int index = x + S + (z + 1 + S) * W + (y + S) * W * W;
                            if (northChannel < node.light) {
                                if (!GetIsLight(northLight.torch)) {
                                    northLight.torch = SetChannel(northLight.torch, cIndex, 0);
                                    light.Set(x, y, z + 1, northLight);
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

                    Light curLight = light.Get(opx, opy, opz);

                    // set the new light value here
                    Light newLight = curLight;
                    int opChannel = GetChannel(op.val, cIndex);
                    newLight.torch = SetChannel(newLight.torch, cIndex, opChannel);
                    light.Set(opx, opy, opz, newLight);

                    // if the new ops channel value is same or less than current channel, dont need to progagate
                    if (opChannel <= GetChannel(curLight.torch, cIndex)) {
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
                    byte westLR = blockData[blocks.Get(x - 1, y, z).type].lightReduction;
                    if (westLR < mChan) {
                        Light nLight = light.Get(x - 1, y, z);
                        if (GetChannel(nLight.torch, cIndex) + 2 + westLR <= mChan) {
                            nLight.torch = SetChannel(nLight.torch, cIndex, mChan - 1 - westLR);
                            light.Set(x - 1, y, z, nLight);
                            lbfs.Enqueue(x - 1 + S + (z + S) * W + (y + S) * W * W);
                        }
                    }
                    byte downLR = blockData[blocks.Get(x, y - 1, z).type].lightReduction;
                    if (downLR < mChan) {
                        Light nLight = light.Get(x, y - 1, z);
                        if (GetChannel(nLight.torch, cIndex) + 2 + downLR <= mChan) {
                            nLight.torch = SetChannel(nLight.torch, cIndex, mChan - 1 - downLR);
                            light.Set(x, y - 1, z, nLight);
                            lbfs.Enqueue(x + S + (z + S) * W + (y - 1 + S) * W * W);
                        }
                    }
                    byte southLR = blockData[blocks.Get(x, y, z - 1).type].lightReduction;
                    if (southLR < mChan) {
                        Light nLight = light.Get(x, y, z - 1);
                        if (GetChannel(nLight.torch, cIndex) + 2 + southLR <= mChan) {
                            nLight.torch = SetChannel(nLight.torch, cIndex, mChan - 1 - southLR);
                            light.Set(x, y, z - 1, nLight);
                            lbfs.Enqueue(x + S + (z - 1 + S) * W + (y + S) * W * W);
                        }
                    }
                    byte eastLR = blockData[blocks.Get(x + 1, y, z).type].lightReduction;
                    if (eastLR < mChan) {
                        Light nLight = light.Get(x + 1, y, z);
                        if (GetChannel(nLight.torch, cIndex) + 2 + eastLR <= mChan) {
                            nLight.torch = SetChannel(nLight.torch, cIndex, mChan - 1 - eastLR);
                            light.Set(x + 1, y, z, nLight);
                            lbfs.Enqueue(x + 1 + S + (z + S) * W + (y + S) * W * W);
                        }
                    }
                    byte upLR = blockData[blocks.Get(x, y + 1, z).type].lightReduction;
                    if (upLR < mChan) {
                        Light nLight = light.Get(x, y + 1, z);
                        if (GetChannel(nLight.torch, cIndex) + 2 + upLR <= mChan) {
                            nLight.torch = SetChannel(nLight.torch, cIndex, mChan - 1 - upLR);
                            light.Set(x, y + 1, z, nLight);
                            lbfs.Enqueue(x + S + (z + S) * W + (y + 1 + S) * W * W);
                        }
                    }
                    byte northLR = blockData[blocks.Get(x, y, z + 1).type].lightReduction;
                    if (northLR < mChan) {
                        Light nLight = light.Get(x, y, z + 1);
                        if (GetChannel(nLight.torch, cIndex) + 2 + northLR <= mChan) {
                            nLight.torch = SetChannel(nLight.torch, cIndex, mChan - 1 - northLR);
                            light.Set(x, y, z + 1, nLight);
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
        //if ((lightFlags & 0x100) != 0)
        //    chunk.neighbors[Dirs.DOWN].lightUpdate = true;
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
        //if ((lightFlags & 0x20000) != 0)
        //    chunk.neighbors[Dirs.UP].lightUpdate = true;
        if ((lightFlags & 0x40000) != 0)
            chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x80000) != 0)
            chunk.neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].lightUpdate = true;
        //if ((lightFlags & 0x100000) != 0)
        //    chunk.neighbors[Dirs.SOUTH].lightUpdate = true;
        if ((lightFlags & 0x200000) != 0)
            chunk.neighbors[Dirs.NORTH].neighbors[Dirs.WEST].lightUpdate = true;
        if ((lightFlags & 0x400000) != 0)
            chunk.neighbors[Dirs.NORTH].neighbors[Dirs.EAST].lightUpdate = true;
        //if ((lightFlags & 0x800000) != 0)
        //    chunk.neighbors[Dirs.NORTH].lightUpdate = true;
        //if ((lightFlags & 0x1000000) != 0)
        //    chunk.neighbors[Dirs.WEST].lightUpdate = true;
        //if ((lightFlags & 0x2000000) != 0)
        //    chunk.neighbors[Dirs.EAST].lightUpdate = true;

        // edge case where light in center updated but neighbor has no air so doesnt get light update but face is exposed to center light
        // might as well just always set direct neighbors to need a light update cuz pretty good chance they will anyways and this fixes it
        for (int i = 0; i < 6; ++i) {
            chunk.neighbors[i].lightUpdate = true;
        }
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
