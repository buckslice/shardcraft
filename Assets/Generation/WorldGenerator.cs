﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

public static class WorldGenerator {


    public static void Generate(Vector3 chunkPos, NativeArray<Block> blocks) {
        // make this single loop instead and calculate x,y,z from index i
        for (int z = 0; z < Chunk.SIZE; ++z) {
            for (int y = 0; y < Chunk.SIZE; ++y) {
                for (int x = 0; x < Chunk.SIZE; ++x) {
                    Vector3 wp = new Vector3(x, y, z) + chunkPos;
                    float n = 0.0f;

                    // experiment with catlike coding noise some more
                    //NoiseSample samp = Noise.Sum(Noise.Simplex3D, wp, 0.015f, 5, 2.0f, 0.5f);
                    //float n = samp.value * 3.0f;

                    // TODO: convert shapes.cginc into c# equiv, and or get gen going on multiple thread (try job system!!!)
                    n -= Vector3.Dot(wp, Vector3.up) * 0.05f;

                    n += Noise.Fractal(wp, 5, 0.01f);

                    if (n > 0.3f) {
                        blocks[x + y * Chunk.SIZE + z * Chunk.SIZE * Chunk.SIZE] = Blocks.STONE;
                    } else if (n > 0.15f) {
                        blocks[x + y * Chunk.SIZE + z * Chunk.SIZE * Chunk.SIZE] = Blocks.GRASS;

                        // trying to make grass not spawn on cliff edge...
                        //if (Mathf.Abs(samp.derivative.normalized.y) < 0.4f) {
                        //    chunk.SetBlock(x, y, z, new BlockGrass());
                        //} else {
                        //    chunk.SetBlock(x, y, z, new Block());
                        //}
                    } else {
                        blocks[x + y * Chunk.SIZE + z * Chunk.SIZE * Chunk.SIZE] = Blocks.AIR;
                    }

                }
            }
        }
    }

}
