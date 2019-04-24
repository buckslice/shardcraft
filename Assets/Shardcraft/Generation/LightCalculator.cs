
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
// but jk i changed that now because cant use funcs with burst compiler

public struct TorchLightOp {
    public int index;
    public ushort val; // torch light value of block placed
}

public struct SunLightOp {
    public int index;
    public byte val; // sunlight value
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

    // max light level for torches and sunlight, 5 BITS
    public const byte MAX_LIGHT = 31;
    const int S = Chunk.SIZE;
    const int W = S + S + S; // because processing 3x3x3 block of 32x32x32 chunks

    public static void InitializeLights(NativeArray<Light> lights, Vector3 chunkBlockPos) {
        // actually doing this differently i think because this will gunk up arrays and they will need to be cleared a lot
        //byte predictedSunValue = 0;
        //if (chunkBlockPos.y >= 0) {
        //    predictedSunValue = MAX_LIGHT;
        //}

        // init light array
        for (int i = 0; i < lights.Length; ++i) {
            lights[i] = new Light { torch = 0, sun = 0 };
        }
    }

    public static void ProcessTorchLightOps(ref NativeArray3x3<Light> lights, ref NativeArray3x3<Block> blocks, NativeArray<BlockData> blockData, NativeQueue<TorchLightOp> ops, NativeQueue<int> lbfs, NativeQueue<LightRemovalNode> lrbfs) {

        lights.flags = 0;

        while (ops.Count > 0) {
            TorchLightOp op = ops.Dequeue();

            // linearized starting index of this operation
            // ranging from -32 -> -1 , 0 -> 31 , 32 -> 63 , so add 32 to build index from 0-95
            int opx = op.index % S;
            int opy = op.index / (S * S);
            int opz = (op.index % (S * S)) / S;

            int startIndex = (opx + S) + (opz + S) * W + (opy + S) * W * W;

            // set the target light at block to have correct 'IsLight' flag
            // lights are repropagated after removals, this allows support for lesser lights to be mixed in with the rest
            Light opLight = lights.Get(opx, opy, opz);
            opLight.torch = SetIsLight(opLight.torch, op.val > 0);
            lights.Set(opx, opy, opz, opLight);

            // loop over each channel of light maps
            for (int cIndex = 0; cIndex < 3; cIndex++) {

                if (GetChannel(op.val, cIndex) == 0) { // remove light from this channel
                    // add current light to removal queue then set to zero
                    Light curLight = lights.Get(opx, opy, opz);
                    lrbfs.Enqueue(new LightRemovalNode { index = startIndex, light = (byte)GetChannel(curLight.torch, cIndex) });
                    curLight.torch = SetChannel(curLight.torch, cIndex, 0);
                    lights.Set(opx, opy, opz, curLight);

                    while (lrbfs.Count > 0) {
                        LightRemovalNode node = lrbfs.Dequeue();

                        // extract coords from index
                        int x = node.index % W - S;
                        int y = node.index / (W * W) - S;
                        int z = (node.index % (W * W)) / W - S;

                        byte oneLess = (byte)(node.light - 1); // each time reduce light by one

                        //ushort westLight = light.Get(x - 1, y, z).torch;
                        Light westLight = lights.Get(x - 1, y, z);
                        byte westChannel = (byte)GetChannel(westLight.torch, cIndex);
                        if (westChannel != 0) {
                            int index = x - 1 + S + (z + S) * W + (y + S) * W * W;
                            if (westChannel < node.light) {
                                if (!GetIsLight(westLight.torch)) {
                                    westLight.torch = SetChannel(westLight.torch, cIndex, 0);
                                    lights.Set(x - 1, y, z, westLight);
                                } else { // if this node is a light, dont override value, but still add a removal node as if you did, then add to repropagate to fill it back in
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        Light downLight = lights.Get(x, y - 1, z);
                        byte downChannel = (byte)GetChannel(downLight.torch, cIndex);
                        if (downChannel != 0) {
                            int index = x + S + (z + S) * W + (y - 1 + S) * W * W;
                            if (downChannel < node.light) {
                                if (!GetIsLight(downLight.torch)) {
                                    downLight.torch = SetChannel(downLight.torch, cIndex, 0);
                                    lights.Set(x, y - 1, z, downLight);
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        Light southLight = lights.Get(x, y, z - 1);
                        byte southChannel = (byte)GetChannel(southLight.torch, cIndex);
                        if (southChannel != 0) {
                            int index = x + S + (z - 1 + S) * W + (y + S) * W * W;
                            if (southChannel < node.light) {
                                if (!GetIsLight(southLight.torch)) {
                                    southLight.torch = SetChannel(southLight.torch, cIndex, 0);
                                    lights.Set(x, y, z - 1, southLight);
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        Light eastLight = lights.Get(x + 1, y, z);
                        byte eastChannel = (byte)GetChannel(eastLight.torch, cIndex);
                        if (eastChannel != 0) {
                            int index = x + 1 + S + (z + S) * W + (y + S) * W * W;
                            if (eastChannel < node.light) {
                                if (!GetIsLight(eastLight.torch)) {
                                    eastLight.torch = SetChannel(eastLight.torch, cIndex, 0);
                                    lights.Set(x + 1, y, z, eastLight);
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        Light upLight = lights.Get(x, y + 1, z);
                        byte upChannel = (byte)GetChannel(upLight.torch, cIndex);
                        if (upChannel != 0) {
                            int index = x + S + (z + S) * W + (y + 1 + S) * W * W;
                            if (upChannel < node.light) {
                                if (!GetIsLight(upLight.torch)) {
                                    upLight.torch = SetChannel(upLight.torch, cIndex, 0);
                                    lights.Set(x, y + 1, z, upLight);
                                } else {
                                    lbfs.Enqueue(index);
                                }
                                lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                            } else { // add to propagate queue so can fill gaps left behind by removal
                                lbfs.Enqueue(index);
                            }
                        }

                        Light northLight = lights.Get(x, y, z + 1);
                        byte northChannel = (byte)GetChannel(northLight.torch, cIndex);
                        if (northChannel != 0) {
                            int index = x + S + (z + 1 + S) * W + (y + S) * W * W;
                            if (northChannel < node.light) {
                                if (!GetIsLight(northLight.torch)) {
                                    northLight.torch = SetChannel(northLight.torch, cIndex, 0);
                                    lights.Set(x, y, z + 1, northLight);
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

                    Light curLight = lights.Get(opx, opy, opz);

                    // set the new light value here
                    Light newLight = curLight;
                    int opChannel = GetChannel(op.val, cIndex);
                    newLight.torch = SetChannel(newLight.torch, cIndex, opChannel);
                    lights.Set(opx, opy, opz, newLight);

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
                    int mChan = GetChannel(lights.Get(x, y, z).torch, cIndex);

                    //if(mChan == 0) { // can happen frequently when batching together light removals
                    //    continue;
                    //}

                    // check each neighbor blocks light reduction value
                    // if neighbor light level is 2 or more levels less than this node, set them to this light-1 and add to queue
                    // also add additional light reduction value
                    byte LR = blockData[blocks.Get(x - 1, y, z).type].lightReduction;
                    if (LR < mChan) { // WEST
                        Light light = lights.Get(x - 1, y, z);
                        if (GetChannel(light.torch, cIndex) + 2 + LR <= mChan) {
                            light.torch = SetChannel(light.torch, cIndex, mChan - 1 - LR);
                            lights.Set(x - 1, y, z, light);
                            lbfs.Enqueue(x - 1 + S + (z + S) * W + (y + S) * W * W);
                        }
                    }
                    LR = blockData[blocks.Get(x, y - 1, z).type].lightReduction;
                    if (LR < mChan) { // DOWN
                        Light light = lights.Get(x, y - 1, z);
                        if (GetChannel(light.torch, cIndex) + 2 + LR <= mChan) {
                            light.torch = SetChannel(light.torch, cIndex, mChan - 1 - LR);
                            lights.Set(x, y - 1, z, light);
                            lbfs.Enqueue(x + S + (z + S) * W + (y - 1 + S) * W * W);
                        }
                    }
                    LR = blockData[blocks.Get(x, y, z - 1).type].lightReduction;
                    if (LR < mChan) { // SOUTH
                        Light light = lights.Get(x, y, z - 1);
                        if (GetChannel(light.torch, cIndex) + 2 + LR <= mChan) {
                            light.torch = SetChannel(light.torch, cIndex, mChan - 1 - LR);
                            lights.Set(x, y, z - 1, light);
                            lbfs.Enqueue(x + S + (z - 1 + S) * W + (y + S) * W * W);
                        }
                    }
                    LR = blockData[blocks.Get(x + 1, y, z).type].lightReduction;
                    if (LR < mChan) { // EAST
                        Light light = lights.Get(x + 1, y, z);
                        if (GetChannel(light.torch, cIndex) + 2 + LR <= mChan) {
                            light.torch = SetChannel(light.torch, cIndex, mChan - 1 - LR);
                            lights.Set(x + 1, y, z, light);
                            lbfs.Enqueue(x + 1 + S + (z + S) * W + (y + S) * W * W);
                        }
                    }
                    LR = blockData[blocks.Get(x, y + 1, z).type].lightReduction;
                    if (LR < mChan) { // UP
                        Light light = lights.Get(x, y + 1, z);
                        if (GetChannel(light.torch, cIndex) + 2 + LR <= mChan) {
                            light.torch = SetChannel(light.torch, cIndex, mChan - 1 - LR);
                            lights.Set(x, y + 1, z, light);
                            lbfs.Enqueue(x + S + (z + S) * W + (y + 1 + S) * W * W);
                        }
                    }
                    LR = blockData[blocks.Get(x, y, z + 1).type].lightReduction;
                    if (LR < mChan) { // NORTH
                        Light light = lights.Get(x, y, z + 1);
                        if (GetChannel(light.torch, cIndex) + 2 + LR <= mChan) {
                            light.torch = SetChannel(light.torch, cIndex, mChan - 1 - LR);
                            lights.Set(x, y, z + 1, light);
                            lbfs.Enqueue(x + S + (z + 1 + S) * W + (y + S) * W * W);
                        }
                    }


                }
            }

        }

    }

    // similar to torch light except the light level does not decrease when propagating down
    public static void ProcessSunLightOps(ref NativeArray3x3<Light> lights, ref NativeArray3x3<Block> blocks, NativeArray<BlockData> blockData, NativeQueue<SunLightOp> ops, NativeQueue<int> lbfs, NativeQueue<LightRemovalNode> lrbfs) {
        //then have a propagate and removal function thats basically the same as torch but with simple downward propagation changes

        //trigger light updates on all touched neighbors except down neighbor trigger a sunlight update if you reach the bottom
        //of his blocks with sunlight propagating downward
        // in this case need to add all infinished nodes in bfs to bottom chunk and tell him to do this function
        // add them as light sunlightOps i guess?

        //actually this is the SAME! as the initial propagation function so just run that again? oooo checking the bottom slice
        //is all contiguous in memory too actually prob pretty fast. dont need to worry about saving and sending nodes. MYESSS
        //wait you do need nodes tho because how do you do removal then??

        lights.flags = 0;

        while (ops.Count > 0) {
            SunLightOp op = ops.Dequeue();

            // linearized starting index of this operation
            // ranging from -32 -> -1 , 0 -> 31 , 32 -> 63 , so add 32 to build index from 0-95
            int opx = op.index % S;
            int opy = op.index / (S * S);
            int opz = (op.index % (S * S)) / S;

            int startIndex = (opx + S) + (opz + S) * W + (opy + S) * W * W;

            if (op.val == 0) { // remove sunlight
                // add current light to removal queue then set to zero
                Light curLight = lights.Get(opx, opy, opz);
                lrbfs.Enqueue(new LightRemovalNode { index = startIndex, light = curLight.sun });
                curLight.sun = 0;
                lights.Set(opx, opy, opz, curLight);

                while (lrbfs.Count > 0) {
                    LightRemovalNode node = lrbfs.Dequeue();

                    // extract coords from index
                    int x = node.index % W - S;
                    int y = node.index / (W * W) - S;
                    int z = (node.index % (W * W)) / W - S;

                    byte oneLess = (byte)(node.light - 1); // each time reduce light by one

                    Light light = lights.Get(x - 1, y, z);
                    if (light.sun != 0) { // WEST
                        int index = x - 1 + S + (z + S) * W + (y + S) * W * W;
                        if (light.sun < node.light) {
                            light.sun = 0;
                            lights.Set(x - 1, y, z, light);
                            lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                        } else { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(index);
                        }
                    }

                    light = lights.Get(x, y - 1, z);
                    if (light.sun != 0) { // DOWN
                        int index = x + S + (z + S) * W + (y - 1 + S) * W * W;
                        if (light.sun < node.light || node.light == MAX_LIGHT) {
                            light.sun = 0;
                            lights.Set(x, y - 1, z, light);
                            byte lv = node.light == MAX_LIGHT ? MAX_LIGHT : oneLess;
                            lrbfs.Enqueue(new LightRemovalNode { index = index, light = lv });
                        } else { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(index);
                        }
                    }

                    light = lights.Get(x, y, z - 1);
                    if (light.sun != 0) { // SOUTH
                        int index = x + S + (z - 1 + S) * W + (y + S) * W * W;
                        if (light.sun < node.light) {
                            light.sun = 0;
                            lights.Set(x, y, z - 1, light);
                            lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                        } else { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(index);
                        }
                    }

                    light = lights.Get(x + 1, y, z);
                    if (light.sun != 0) { // EAST
                        int index = x + 1 + S + (z + S) * W + (y + S) * W * W;
                        if (light.sun < node.light) {
                            light.sun = 0;
                            lights.Set(x + 1, y, z, light);
                            lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                        } else { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(index);
                        }
                    }

                    light = lights.Get(x, y + 1, z);
                    if (light.sun != 0) { // UP
                        int index = x + S + (z + S) * W + (y + 1 + S) * W * W;
                        if (light.sun < node.light) {
                            light.sun = 0;
                            lights.Set(x, y + 1, z, light);
                            lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                        } else { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(index);
                        }
                    }

                    light = lights.Get(x, y, z + 1);
                    if (light.sun != 0) { // NORTH
                        int index = x + S + (z + 1 + S) * W + (y + S) * W * W;
                        if (light.sun < node.light) {
                            light.sun = 0;
                            lights.Set(x, y, z + 1, light);
                            lrbfs.Enqueue(new LightRemovalNode { index = index, light = oneLess });
                        } else { // add to propagate queue so can fill gaps left behind by removal
                            lbfs.Enqueue(index);
                        }
                    }


                }

            } else { // propagate light from this channel

                Light curLight = lights.Get(opx, opy, opz);

                // set the new light value here
                Light newLight = curLight;
                newLight.sun = (byte)op.val;

                lights.Set(opx, opy, opz, newLight);

                // if the new sun value is same or less than current dont need to progagate
                // this might not happen with sunlight but left check anyways
                if (op.val <= curLight.sun) {
                    continue;
                }

                lbfs.Enqueue(startIndex);

            }

        }
    }

    public static void PropagateSunlight(ref NativeArray3x3<Light> lights, ref NativeArray3x3<Block> blocks, NativeArray<BlockData> blockData, NativeQueue<int> lbfs, NativeQueue<int> uflbfs) {
        // propagate (either way)
        while (lbfs.Count > 0) {
            int index = lbfs.Dequeue();

            // extract coords from index
            int x = index % W - S;
            int y = index / (W * W) - S;
            int z = (index % (W * W)) / W - S;

            // get light level at this node
            int sunLight = lights.Get(x, y, z).sun;

            // check each neighbor blocks light reduction value
            // if neighbor light level is 2 or more levels less than this node, set them to this light-1 and add to queue
            // also add additional light reduction value
            byte LR = blockData[blocks.Get(x - 1, y, z).type].lightReduction;
            if (LR < sunLight) { // WEST
                Light light = lights.Get(x - 1, y, z);
                if (light.sun + 2 + LR <= sunLight) {
                    light.sun = (byte)(sunLight - 1 - LR);
                    lights.Set(x - 1, y, z, light);
                    lbfs.Enqueue(x - 1 + S + (z + S) * W + (y + S) * W * W);
                }
            }
            LR = blockData[blocks.Get(x, y - 1, z).type].lightReduction;
            if (LR < sunLight) { // DOWN
                Light light = lights.Get(x, y - 1, z);
                if (light.sun + 2 + LR <= sunLight) {
                    if (sunLight == MAX_LIGHT) { // if at maxlight dont reduce by 1 each time
                        light.sun = (byte)(sunLight - LR);
                    } else {
                        light.sun = (byte)(sunLight - 1 - LR);
                    }
                    lights.Set(x, y - 1, z, light);
                    if (y <= -31) {
                        uflbfs.Enqueue(x + S + (z + S) * W + (S + 32) * W * W); // add to unfinished queue and shift index to be proper for downdown chunk
                    } else {
                        lbfs.Enqueue(x + S + (z + S) * W + (y - 1 + S) * W * W);
                    }
                }
            }
            LR = blockData[blocks.Get(x, y, z - 1).type].lightReduction;
            if (LR < sunLight) { // SOUTH
                Light light = lights.Get(x, y, z - 1);
                if (light.sun + 2 + LR <= sunLight) {
                    light.sun = (byte)(sunLight - 1 - LR);
                    lights.Set(x, y, z - 1, light);
                    lbfs.Enqueue(x + S + (z - 1 + S) * W + (y + S) * W * W);
                }
            }
            LR = blockData[blocks.Get(x + 1, y, z).type].lightReduction;
            if (LR < sunLight) { // EAST
                Light light = lights.Get(x + 1, y, z);
                if (light.sun + 2 + LR <= sunLight) {
                    light.sun = (byte)(sunLight - 1 - LR);
                    lights.Set(x + 1, y, z, light);
                    lbfs.Enqueue(x + 1 + S + (z + S) * W + (y + S) * W * W);
                }
            }
            LR = blockData[blocks.Get(x, y + 1, z).type].lightReduction;
            if (LR < sunLight) { // UP
                Light light = lights.Get(x, y + 1, z);
                if (light.sun + 2 + LR <= sunLight) {
                    light.sun = (byte)(sunLight - 1 - LR);
                    lights.Set(x, y + 1, z, light);
                    lbfs.Enqueue(x + S + (z + S) * W + (y + 1 + S) * W * W);
                }
            }
            LR = blockData[blocks.Get(x, y, z + 1).type].lightReduction;
            if (LR < sunLight) { // NORTH
                Light light = lights.Get(x, y, z + 1);
                if (light.sun + 2 + LR <= sunLight) {
                    light.sun = (byte)(sunLight - 1 - LR);
                    lights.Set(x, y, z + 1, light);
                    lbfs.Enqueue(x + S + (z + 1 + S) * W + (y + S) * W * W);
                }
            }
        }
    }



    public static void CalcInitialSunLight(NativeArray<Block> blocks, NativeArray<BlockData> blockData, NativeArray<Light> clight, NativeArray<Light> topLight, NativeQueue<int> lbfs, bool upRendered, Vector3 chunkWorldPos) {
        if (upRendered) { // if up neighbor has been sunlight processed before
            for (int z = 0; z < S; ++z) {
                for (int x = 0; x < S; ++x) {
                    // check bottom xz slice of up neighbors sunlight
                    Light light = topLight[x + z * S];
                    if (light.sun == 0) {
                        continue;
                    }
                    // if its greater than zero then add a sunlight node at the top of this chunk with correct propagation rules
                    int i = x + z * S + (S - 1) * S * S;
                    byte lr = blockData[blocks[i].type].lightReduction;
                    if (lr < light.sun) {
                        Light mlight = clight[i];
                        mlight.sun = (light.sun == MAX_LIGHT ? MAX_LIGHT : (byte)(light.sun - 1 - lr));
                        clight[i] = mlight;
                        lbfs.Enqueue((x + S) + (z + S) * W + (S - 1 + S) * W * W);
                    }
                }
            }
        } else {
            // guess if top neighbor is above ground
            bool aboveGround = chunkWorldPos.y + Chunk.SIZE / Chunk.BPU >= 0;

            if (!aboveGround) {
                return; // no predicted sunlight here so return
            }

            // add a max sunlight node for each non opaque block at top of this chunk
            for (int z = 0; z < S; ++z) {
                for (int x = 0; x < S; ++x) {
                    int i = x + z * S + (S - 1) * S * S;
                    byte lr = blockData[blocks[i].type].lightReduction;
                    if (lr < MAX_LIGHT) {
                        Light mlight = clight[i];
                        mlight.sun = (byte)(MAX_LIGHT - lr);
                        clight[i] = mlight;
                        lbfs.Enqueue((x + S) + (z + S) * W + (S - 1 + S) * W * W);
                    }
                }
            }

        }
    }

    // queue up initial light updates for any light emitting block in loaded chunk
    // could prob work this into generation and load routines more efficiently but whatever for now
    public static void CalcInitialLightOps(NativeArray<Block> blocks, NativeArray<BlockData> blockData, NativeQueue<TorchLightOp> lightOps) {
        for (int i = 0; i < blocks.Length; ++i) {
            ushort light = blockData[blocks[i].type].light;
            if (light > 0) { // new light update
                lightOps.Enqueue(new TorchLightOp { index = i, val = light });
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

            //Light curLight = lights.Get(x, y, z);
            //if (GetIsLight(curLight.torch)) {
            //    colors.Add(GetColorFromLight(curLight));
            //} else {
            switch (faces[i].dir) {
                case Dir.west:
                    colors.Add(GetColorFromLight(lights.Get(x - 1, y, z)));
                    break;
                case Dir.down:
                    colors.Add(GetColorFromLight(lights.Get(x, y - 1, z)));
                    break;
                case Dir.south:
                    colors.Add(GetColorFromLight(lights.Get(x, y, z - 1)));
                    break;
                case Dir.east:
                    colors.Add(GetColorFromLight(lights.Get(x + 1, y, z)));
                    break;
                case Dir.up:
                    colors.Add(GetColorFromLight(lights.Get(x, y + 1, z)));
                    break;
                case Dir.north:
                    colors.Add(GetColorFromLight(lights.Get(x, y, z + 1)));
                    break;
                default:
                    colors.Add(GetColorFromLight(lights.Get(x, y, z)));
                    break;

            }
            //}

            // add the last color 3 more times (4 colors per face)
            Color32 lastColor = colors[colors.Length - 1];
            colors.Add(lastColor);
            colors.Add(lastColor);
            colors.Add(lastColor);

        }
    }

    public static Color32 GetColorFromLight(Light light) {
        float red = (float)GetRed(light.torch) / MAX_LIGHT;
        float green = (float)GetGreen(light.torch) / MAX_LIGHT;
        float blue = (float)GetBlue(light.torch) / MAX_LIGHT;
        float sun = (float)light.sun / MAX_LIGHT;
        return new Color(red, green, blue, sun);
    }


}
