using UnityEngine;
using Unity.Mathematics;

// noise functions for unity mathematics librarys
public static class NoiseUM {

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

    public static float4 FractalGrad(Vector3 v, int octaves, float frequency, float persistence = 0.5f, float lacunarity = 2.0f) {
        float total = 0.0f;
        float3 gradTotal = new float3();
        float amplitude = 1.0f;

        for (int i = 0; i < octaves; ++i) {
            float n = noise.snoise((v * frequency), out float3 grad);
            //float n = noise.cellular(v * frequency).y;

            //float2 n2 = noise.cellular(v * frequency);
            //float n = n2.y - n2.x;

            total += n * amplitude;
            gradTotal += grad * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return new float4(total, gradTotal.x, gradTotal.y, gradTotal.z);
    }
}
