using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;

public static class WorldGenerator {

    // using built in Unity.Mathematics noise so can be burst compiled
    public static float Fractal(Vector3 v, int octaves, float frequency, float persistence = 0.5f, float lacunarity = 2.0f) {
        float total = 0.0f;
        float amplitude = 1.0f;

        for (int i = 0; i < octaves; ++i) {
            float n = noise.snoise((v * frequency));

            //float n = noise.cellular(v * frequency).y;

            //float2 n2 = noise.cellular(v * frequency);
            //float n = n2.y - n2.x;

            total += n * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return total;
    }


    public static void Generate(Vector3 chunkWorldPos, NativeArray<Block> blocks) {
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
                    n -= Vector3.Dot(wp, Vector3.up) * 0.03f;

                    //n += Fractal(wp, 5, 0.01f);
                    n += Fractal(wp, 5, 0.01f);

                    Block b = Blocks.AIR;
                    if (n > 0.3f) {
                        b = Blocks.STONE;
                    } else if (n > 0.15f) {
                        b = Blocks.GRASS;

                        // trying to make grass not spawn on cliff edge...
                        //if (Mathf.Abs(samp.derivative.normalized.y) < 0.4f) {
                        //    chunk.SetBlock(x, y, z, new BlockGrass());
                        //} else {
                        //    chunk.SetBlock(x, y, z, new Block());
                        //}
                    }

                    blocks[x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE] = b;

                }
            }
        }
    }


}
