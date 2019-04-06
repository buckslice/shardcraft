﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;

public static class WorldGenerator {

    public static void Generate(Vector3 chunkWorldPos, NativeArray<Block> blocks, NativeArray<Light> lights) {
        // make this single loop instead and calculate x,y,z from index i
        for (int y = 0; y < Chunk.SIZE; ++y) {
            for (int z = 0; z < Chunk.SIZE; ++z) {
                for (int x = 0; x < Chunk.SIZE; ++x) {
                    Vector3 wp = new Vector3(x, y, z) / Chunk.BPU + chunkWorldPos + new Vector3(55.0f, 12.4f, 87.5f);
                    float n = 0.0f;

                    // experiment with catlike coding noise some more
                    //NoiseSample samp = Noise.Sum(Noise.Simplex3D, wp, 0.015f, 5, 2.0f, 0.5f);
                    //float n = samp.value * 3.0f;

                    // TODO: convert shapes.cginc into c# equiv, and or get gen going on multiple thread (try job system!!!)
                    // this is adding a flat ground plane density at low strength, so as you go lower will slowly become solid
                    n -= Vector3.Dot(wp, Vector3.up) * 0.03f;

                    //n += Fractal(wp, 5, 0.01f);
                    float4 ng = NoiseUM.FractalGrad(wp, 5, 0.01f);
                    n += ng.x;

                    Block b = Blocks.AIR;

                    if (n > 0.25f) {
                        b = Blocks.STONE;
                    } else if (n > 0.15f) {
                        b = Blocks.STONE;

                        if (Vector3.Dot(Vector3.up, new Vector3(ng.y, ng.z, ng.w).normalized) < .7f) {
                            b = Blocks.GRASS;
                        }

                        // trying to make grass not spawn on cliff edge...
                        //if (Mathf.Abs(samp.derivative.normalized.y) < 0.4f) {
                        //    chunk.SetBlock(x, y, z, new BlockGrass());
                        //} else {
                        //    chunk.SetBlock(x, y, z, new Block());
                        //}
                    }

                    if (b == Blocks.STONE) {

                        float coal = NoiseUM.Billow(wp + new Vector3(1000, 722, 255), 2, 0.04f, 1, 0.6f);

                        if (coal > 0.0f) {
                            b = Blocks.COAL;
                        }

                    }

                    // for testing singular blocks
                    //if (b != Blocks.COAL) {
                    //    b = Blocks.AIR;
                    //}

                    blocks[x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE] = b;

                }
            }
        }

        // clear light array
        for (int i = 0; i < lights.Length; ++i) {
            lights[i] = new Light { torch = 0 };
        }
    }


}
