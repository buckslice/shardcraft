﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

public struct LightOp {
    public Vector3i pos;
    public int val;
}

public static class LightCalculator {

    //public struct LightNode {
    //    short index; // x y z coordinate!
    //}

    public static void ProcessLights(MeshJob job, NativeQueue<LightOp> ops, NativeQueue<int> lbfs) {
        const int ww = 96; // because processing 3x3x3 block of 32x32x32 chunks
        while (ops.Count > 0) {
            LightOp op = ops.Dequeue();

            if (op.val > 0) {
                Debug.Assert(lbfs.Count == 0);

                int startIndex = (op.pos.x + 32) + (op.pos.z + 32) * ww + (op.pos.y + 32) * ww * ww;
                lbfs.Enqueue(startIndex);

                while (lbfs.Count > 0) {
                    int index = lbfs.Dequeue();

                    // extract coords from index
                    int x = index % ww - 32;
                    int y = index / (ww * ww) - 32;
                    int z = (index % (ww * ww)) / ww - 32;

                    // get light level at this node
                    byte lightLevel = job.GetLight(x, y, z);

                    // check each neighbor if its air (should later be any transparent block)
                    // if neighbor light level is 2 or more levels less than this node, set them to this light -1 and add to queue
                    if (job.GetBlock(x - 1, y, z) == Blocks.AIR && job.GetLight(x - 1, y, z) + 2 <= lightLevel) {
                        job.SetLight(x - 1, y, z, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x - 1 + 32 + (z + 32) * ww + (y + 32) * ww * ww);
                    }
                    if (job.GetBlock(x, y - 1, z) == Blocks.AIR && job.GetLight(x, y - 1, z) + 2 <= lightLevel) {
                        job.SetLight(x, y - 1, z, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x + 32 + (z + 32) * ww + (y - 1 + 32) * ww * ww);
                    }
                    if (job.GetBlock(x, y, z - 1) == Blocks.AIR && job.GetLight(x, y, z - 1) + 2 <= lightLevel) {
                        job.SetLight(x, y, z - 1, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x + 32 + (z - 1 + 32) * ww + (y + 32) * ww * ww);
                    }
                    if (job.GetBlock(x + 1, y, z) == Blocks.AIR && job.GetLight(x + 1, y, z) + 2 <= lightLevel) {
                        job.SetLight(x + 1, y, z, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x + 1 + 32 + (z + 32) * ww + (y + 32) * ww * ww);
                    }
                    if (job.GetBlock(x, y + 1, z) == Blocks.AIR && job.GetLight(x, y + 1, z) + 2 <= lightLevel) {
                        job.SetLight(x, y + 1, z, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x + 32 + (z + 32) * ww + (y + 1 + 32) * ww * ww);
                    }
                    if (job.GetBlock(x, y, z + 1) == Blocks.AIR && job.GetLight(x, y, z + 1) + 2 <= lightLevel) {
                        job.SetLight(x, y, z + 1, (byte)(lightLevel - 1));
                        lbfs.Enqueue(x + 32 + (z + 1 + 32) * ww + (y + 32) * ww * ww);
                    }


                }

            } else { // light removal

            }

        }




    }



}
