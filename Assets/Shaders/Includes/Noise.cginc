

// PRNG function
float nrand(float2 uv, float salt) {
    uv += float2(salt, 0);
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}
// 3D version
float rand(float3 p) {
    return frac(sin(dot(p.xyz, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
}

//https://www.ronja-tutorials.com/2018/09/02/white-noise.html
//
float rand1dTo1d(float value, float mutator = 0.546) {
    return frac(sin(value + mutator) * 143758.5453);
}
float3 rand1dTo3d(float value) {
    return float3(rand1dTo1d(value, 3.9812), rand1dTo1d(value, 7.1536), rand1dTo1d(value, 5.7241));
}
float rand3dTo1d(float3 value, float3 dotDir = float3(12.9898, 78.233, 37.719)) {
    //make value smaller to avoid artefacts
    float3 smallValue = sin(value);
    //get scalar value from 3d vector
    float random = dot(smallValue, dotDir);
    //make value more random by making it bigger and then taking the factional part
    random = frac(sin(random) * 143758.5453);
    return random;
}

// other numbers you can try??
//return frac(sin(dot(n, float3(95.43583, 93.323197, 94.993431))) * 65536.32);


//https://github.com/keijiro/NoiseShader/blob/master/Assets/HLSL/SimplexNoise3D.hlsl

float3 mod289(float3 x) {
    return x - floor(x / 289.0) * 289.0;
}

float4 mod289(float4 x) {
    return x - floor(x / 289.0) * 289.0;
}

float4 permute(float4 x) {
    return mod289((x * 34.0 + 1.0) * x);
}

float4 taylorInvSqrt(float4 r) {
    return 1.79284291400159 - r * 0.85373472095314;
}

float snoise(float3 v) {
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);

    // First corner
    float3 i = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);

    // Other corners
    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    // x1 = x0 - i1  + 1.0 * C.xxx;
    // x2 = x0 - i2  + 2.0 * C.xxx;
    // x3 = x0 - 1.0 + 3.0 * C.xxx;
    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - 0.5;

    // Permutations
    i = mod289(i); // Avoid truncation effects in permutation
    float4 p =
        permute(permute(permute(i.z + float4(0.0, i1.z, i2.z, 1.0))
            + i.y + float4(0.0, i1.y, i2.y, 1.0))
            + i.x + float4(0.0, i1.x, i2.x, 1.0));

    // Gradients: 7x7 points over a square, mapped onto an octahedron.
    // The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
    float4 j = p - 49.0 * floor(p / 49.0);  // mod(p,7*7)

    float4 x_ = floor(j / 7.0);
    float4 y_ = floor(j - 7.0 * x_);  // mod(j,N)

    float4 x = (x_ * 2.0 + 0.5) / 7.0 - 1.0;
    float4 y = (y_ * 2.0 + 0.5) / 7.0 - 1.0;

    float4 h = 1.0 - abs(x) - abs(y);

    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    //float4 s0 = float4(lessThan(b0, 0.0)) * 2.0 - 1.0;
    //float4 s1 = float4(lessThan(b1, 0.0)) * 2.0 - 1.0;
    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(h, 0.0);

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    float3 g0 = float3(a0.xy, h.x);
    float3 g1 = float3(a0.zw, h.y);
    float3 g2 = float3(a1.xy, h.z);
    float3 g3 = float3(a1.zw, h.w);

    // Normalise gradients
    float4 norm = taylorInvSqrt(float4(dot(g0, g0), dot(g1, g1), dot(g2, g2), dot(g3, g3)));
    g0 *= norm.x;
    g1 *= norm.y;
    g2 *= norm.z;
    g3 *= norm.w;

    // Mix final noise value
    float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    m = m * m;
    m = m * m;

    float4 px = float4(dot(x0, g0), dot(x1, g1), dot(x2, g2), dot(x3, g3));
    return 42.0 * dot(m, px);
}

float4 snoise_grad(float3 v) {
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);

    // First corner
    float3 i = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);

    // Other corners
    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    // x1 = x0 - i1  + 1.0 * C.xxx;
    // x2 = x0 - i2  + 2.0 * C.xxx;
    // x3 = x0 - 1.0 + 3.0 * C.xxx;
    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - 0.5;

    // Permutations
    i = mod289(i); // Avoid truncation effects in permutation
    float4 p =
        permute(permute(permute(i.z + float4(0.0, i1.z, i2.z, 1.0))
            + i.y + float4(0.0, i1.y, i2.y, 1.0))
            + i.x + float4(0.0, i1.x, i2.x, 1.0));

    // Gradients: 7x7 points over a square, mapped onto an octahedron.
    // The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
    float4 j = p - 49.0 * floor(p / 49.0);  // mod(p,7*7)

    float4 x_ = floor(j / 7.0);
    float4 y_ = floor(j - 7.0 * x_);  // mod(j,N)

    float4 x = (x_ * 2.0 + 0.5) / 7.0 - 1.0;
    float4 y = (y_ * 2.0 + 0.5) / 7.0 - 1.0;

    float4 h = 1.0 - abs(x) - abs(y);

    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    //float4 s0 = float4(lessThan(b0, 0.0)) * 2.0 - 1.0;
    //float4 s1 = float4(lessThan(b1, 0.0)) * 2.0 - 1.0;
    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(h, 0.0);

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    float3 g0 = float3(a0.xy, h.x);
    float3 g1 = float3(a0.zw, h.y);
    float3 g2 = float3(a1.xy, h.z);
    float3 g3 = float3(a1.zw, h.w);

    // Normalise gradients
    float4 norm = taylorInvSqrt(float4(dot(g0, g0), dot(g1, g1), dot(g2, g2), dot(g3, g3)));
    g0 *= norm.x;
    g1 *= norm.y;
    g2 *= norm.z;
    g3 *= norm.w;

    // Compute noise and gradient at P
    float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    float4 m2 = m * m;
    float4 m3 = m2 * m;
    float4 m4 = m2 * m2;
    float3 grad =
        -6.0 * m3.x * x0 * dot(x0, g0) + m4.x * g0 +
        -6.0 * m3.y * x1 * dot(x1, g1) + m4.y * g1 +
        -6.0 * m3.z * x2 * dot(x2, g2) + m4.z * g2 +
        -6.0 * m3.w * x3 * dot(x3, g3) + m4.w * g3;
    float4 px = float4(dot(x0, g0), dot(x1, g1), dot(x2, g2), dot(x3, g3));
    return 42.0 * float4(grad, dot(m4, px));
}

//	FAST32_hash
//	A very fast hashing function.  Requires 32bit support.
//	http://briansharpe.wordpress.com/2011/11/15/a-fast-and-simple-32bit-floating-point-hash-function/
//
//	The hash formula takes the form....
//	hash = mod( coord.x * coord.x * coord.y * coord.y, SOMELARGEFLOAT ) / SOMELARGEFLOAT
//	We truncate and offset the domain to the most interesting part of the noise.
//	SOMELARGEFLOAT should be in the range of 400.0->1000.0 and needs to be hand picked.  Only some give good results.
//	3D Noise is achieved by offsetting the SOMELARGEFLOAT value by the Z coordinate
//
float4 FAST32_hash_3D_Cell(float3 gridcell)	//	generates 4 different random numbers for the single given cell point
{
    //    gridcell is assumed to be an integer coordinate

    //	TODO: 	these constants need tweaked to find the best possible noise.
    //			probably requires some kind of brute force computational searching or something....
    const float2 OFFSET = float2(50.0, 161.0);
    const float DOMAIN = 69.0;
    const float4 SOMELARGEFLOATS = float4(635.298681, 682.357502, 668.926525, 588.255119);
    const float4 ZINC = float4(48.500388, 65.294118, 63.934599, 63.279683);

    //	truncate the domain
    gridcell.xyz = gridcell - floor(gridcell * (1.0 / DOMAIN)) * DOMAIN;
    gridcell.xy += OFFSET.xy;
    gridcell.xy *= gridcell.xy;
    return frac((gridcell.x * gridcell.y) * (1.0 / (SOMELARGEFLOATS + gridcell.zzzz * ZINC)));
}
static const int MinVal = -1;
static const int MaxVal = 1;


float worleyNoise(float3 xyz, float cellType, float distanceFunction) {
    int xi = int(floor(xyz.x));
    int yi = int(floor(xyz.y));
    int zi = int(floor(xyz.z));

    float xf = xyz.x - float(xi);
    float yf = xyz.y - float(yi);
    float zf = xyz.z - float(zi);

    float dist1 = 9999999.0;
    float dist2 = 9999999.0;
    float dist3 = 9999999.0;
    float dist4 = 9999999.0;
    float3 cell;

    for (int z = MinVal; z <= MaxVal; z++) {
        for (int y = MinVal; y <= MaxVal; y++) {
            for (int x = MinVal; x <= MaxVal; x++) {
                cell = FAST32_hash_3D_Cell(float3(xi + x, yi + y, zi + z)).xyz;
                float3 c = cell;
                cell.x += (float(x) - xf);
                cell.y += (float(y) - yf);
                cell.z += (float(z) - zf);
                float dist = 0.0;
                if (distanceFunction <= 1) { // seems to be strictly worse than below
                    dist = sqrt(dot(cell, cell));
                } else if (distanceFunction > 1 && distanceFunction <= 2) { // true euclidian
                    dist = dot(cell, cell);
                } else if (distanceFunction > 2 && distanceFunction <= 3) { // manhattan
                    dist = abs(cell.x) + abs(cell.y) + abs(cell.z);
                    dist *= dist;
                } else if (distanceFunction > 3 && distanceFunction <= 4) { // chebyshev
                    dist = max(abs(cell.x), max(abs(cell.y), abs(cell.z)));
                    dist *= dist;
                } else if (distanceFunction > 4 && distanceFunction <= 5) {
                    dist = dot(cell, cell) + cell.x*cell.y + cell.x*cell.z + cell.y*cell.z;
                } else if (distanceFunction > 5 && distanceFunction <= 6) {
                    dist = pow(abs(cell.x*cell.x*cell.x*cell.x + cell.y*cell.y*cell.y*cell.y + cell.z*cell.z*cell.z*cell.z), 0.25);
                } else if (distanceFunction > 6 && distanceFunction <= 7) {
                    dist = sqrt(abs(cell.x)) + sqrt(abs(cell.y)) + sqrt(abs(cell.z));
                    dist *= dist;
                }
                if (dist < dist1) {
                    dist4 = dist3;
                    dist3 = dist2;
                    dist2 = dist1;
                    dist1 = dist;
                } else if (dist < dist2) {
                    dist4 = dist3;
                    dist3 = dist2;
                    dist2 = dist;
                } else if (dist < dist3) {
                    dist4 = dist3;
                    dist3 = dist;
                } else if (dist < dist4) {
                    dist4 = dist;
                }
            }
        }
    }
    if (cellType <= 1) { // F1
        return dist1;	//	scale return value from 0.0->1.333333 to 0.0->1.0  	(2/3)^2 * 3  == (12/9) == 1.333333
    } else if (cellType > 1 && cellType <= 2) {	// F2
        return dist2;
    } else if (cellType > 2 && cellType <= 3) {	// F3
        return dist3;
    } else if (cellType > 3 && cellType <= 4) {	// F4
        return dist4;
    } else if (cellType > 4 && cellType <= 5) {	// F2 - F1 
        return dist2 - dist1;
    } else if (cellType > 5 && cellType <= 6) {	// F3 - F2 
        return dist3 - dist2;
    } else if (cellType > 6 && cellType <= 7) {	// F1 + F2/2
        return dist1 + dist2 / 2.0;
    } else if (cellType > 7 && cellType <= 8) {	// F1 * F2
        return dist1 * dist2;
    } else if (cellType > 8 && cellType <= 9) {	// Crackle
        return max(1.0, 10 * (dist2 - dist1));
    } else {
        return dist1;
    }
}


float worleyCell(float3 xyz, float cellType, float distanceFunction) {
    int xi = int(floor(xyz.x));
    int yi = int(floor(xyz.y));
    int zi = int(floor(xyz.z));

    float xf = xyz.x - float(xi);
    float yf = xyz.y - float(yi);
    float zf = xyz.z - float(zi);

    float dist1 = 9999999.0;
    float3 cell;
    float3 closestCell;

    for (int z = MinVal; z <= MaxVal; z++) {
        for (int y = MinVal; y <= MaxVal; y++) {
            for (int x = MinVal; x <= MaxVal; x++) {
                cell = FAST32_hash_3D_Cell(float3(xi + x, yi + y, zi + z)).xyz;
                float3 c = cell;
                cell.x += (float(x) - xf);
                cell.y += (float(y) - yf);
                cell.z += (float(z) - zf);
                float dist = 0.0;
                if (distanceFunction <= 1) { // seems to be strictly worse than below
                    dist = sqrt(dot(cell, cell));
                } else if (distanceFunction > 1 && distanceFunction <= 2) { // true euclidian
                    dist = dot(cell, cell);
                } else if (distanceFunction > 2 && distanceFunction <= 3) { // manhattan
                    dist = abs(cell.x) + abs(cell.y) + abs(cell.z);
                    dist *= dist;
                } else if (distanceFunction > 3 && distanceFunction <= 4) { // chebyshev
                    dist = max(abs(cell.x), max(abs(cell.y), abs(cell.z)));
                    dist *= dist;
                } else if (distanceFunction > 4 && distanceFunction <= 5) { // this one looks skewed..
                    dist = dot(cell, cell) + cell.x*cell.y + cell.x*cell.z + cell.y*cell.z;
                } else if (distanceFunction > 5 && distanceFunction <= 6) {
                    dist = pow(abs(cell.x*cell.x*cell.x*cell.x + cell.y*cell.y*cell.y*cell.y + cell.z*cell.z*cell.z*cell.z), 0.25);
                } else if (distanceFunction > 6 && distanceFunction <= 7) {
                    dist = sqrt(abs(cell.x)) + sqrt(abs(cell.y)) + sqrt(abs(cell.z));
                    dist *= dist;
                }
                if (dist < dist1) {
                    dist1 = dist;
                    closestCell = c;
                }
            }
        }
    }
    return rand3dTo1d(closestCell);

}

// (-1.0, 1.0)
float fbm(float3 x, int octaves, float frequency, float amplitude, float persistence, float lacunarity) {
    float sum = 0.0;
    for (int i = 0; i < octaves; i++) {
        sum += snoise(x * frequency) * amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }
    return sum;
}

// todo: add smooth ridged from noise.cs
// (-1.0, 1.0)
float ridged(float3 x, int octaves, float frequency, float amplitude, float persistence, float lacunarity) {
    float sum = 0.0;
    for (int i = 0; i < octaves; i++) {
        float n = snoise(x * frequency);
        n = 1.0 - abs(n);
        sum += n * amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }
    return (sum - 1.1)*1.25;
}

// not sure if necesarry, just inverted ridged pretty much
// (-1.0, 1.0)
float billowed(float3 x, int octaves, float frequency, float amplitude, float persistence, float lacunarity) {
    float sum = 0.0;
    for (int i = 0; i < octaves; i++) {
        sum += abs(snoise(x * frequency)) * amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }
    return sum;
}

// (0.0, 1.0)
float worley(float3 p, float octaves, float frequency, float amplitude, float persistence, float lacunarity, float cellType, float distanceFunction) {
    float sum = 0;
    for (int i = 0; i < octaves; i++) {
        sum += worleyNoise(p * frequency, cellType, distanceFunction) * amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }
    return sum;  // 1.0 - sum looks cool
}

// kinda trash
//float swiss(float3 p, int octaves, float frequency, float amplitude, float persistence, float lacunarity, float warp, float ridgeOffset) {
//    float sum = 0.0;
//    float3 dsum = float3(0.0, 0.0, 0.0);
//    for (int i = 0; i < octaves; i++) {
//        float4 n = 0.5 * (0 + (ridgeOffset - abs(snoise_grad((p + warp * dsum)*frequency))));
//        sum += amplitude * n.x;
//        dsum += amplitude * n.yzw * -n.x;
//        frequency *= lacunarity;
//        amplitude *= persistence * saturate(sum);
//    }
//    return sum;
//}




//https://github.com/Erkaman/glsl-worley/blob/master/worley3D.glsl

// glsl style mod
#define mod(x, y) (x - y * floor(x / y)) 

// Permutation polynomial: (34x^2 + x) mod 289
float3 permute(float3 x) {
    return mod((34.0 * x + 1.0) * x, 289.0);
}

float3 dist(float3 x, float3 y, float3 z, bool manhattanDistance) {
    return manhattanDistance ? abs(x) + abs(y) + abs(z) : (x * x + y * y + z * z);
}

// faster worley probably but only 2 closest distances
// looks pretty good though, jitter around 1.16 looks good
float2 worley(float3 P, float jitter, bool manhattanDistance) {
    float K = 0.142857142857; // 1/7
    float Ko = 0.428571428571; // 1/2-K/2
    float K2 = 0.020408163265306; // 1/(7*7)
    float Kz = 0.166666666667; // 1/6
    float Kzo = 0.416666666667; // 1/2-1/6*2

    float3 Pi = mod(floor(P), 289.0);
    float3 Pf = frac(P) - 0.5;

    float3 Pfx = Pf.x + float3(1.0, 0.0, -1.0);
    float3 Pfy = Pf.y + float3(1.0, 0.0, -1.0);
    float3 Pfz = Pf.z + float3(1.0, 0.0, -1.0);

    float3 p = permute(Pi.x + float3(-1.0, 0.0, 1.0));
    float3 p1 = permute(p + Pi.y - 1.0);
    float3 p2 = permute(p + Pi.y);
    float3 p3 = permute(p + Pi.y + 1.0);

    float3 p11 = permute(p1 + Pi.z - 1.0);
    float3 p12 = permute(p1 + Pi.z);
    float3 p13 = permute(p1 + Pi.z + 1.0);

    float3 p21 = permute(p2 + Pi.z - 1.0);
    float3 p22 = permute(p2 + Pi.z);
    float3 p23 = permute(p2 + Pi.z + 1.0);

    float3 p31 = permute(p3 + Pi.z - 1.0);
    float3 p32 = permute(p3 + Pi.z);
    float3 p33 = permute(p3 + Pi.z + 1.0);

    float3 ox11 = frac(p11*K) - Ko;
    float3 oy11 = mod(floor(p11*K), 7.0)*K - Ko;
    float3 oz11 = floor(p11*K2)*Kz - Kzo; // p11 < 289 guaranteed

    float3 ox12 = frac(p12*K) - Ko;
    float3 oy12 = mod(floor(p12*K), 7.0)*K - Ko;
    float3 oz12 = floor(p12*K2)*Kz - Kzo;

    float3 ox13 = frac(p13*K) - Ko;
    float3 oy13 = mod(floor(p13*K), 7.0)*K - Ko;
    float3 oz13 = floor(p13*K2)*Kz - Kzo;

    float3 ox21 = frac(p21*K) - Ko;
    float3 oy21 = mod(floor(p21*K), 7.0)*K - Ko;
    float3 oz21 = floor(p21*K2)*Kz - Kzo;

    float3 ox22 = frac(p22*K) - Ko;
    float3 oy22 = mod(floor(p22*K), 7.0)*K - Ko;
    float3 oz22 = floor(p22*K2)*Kz - Kzo;

    float3 ox23 = frac(p23*K) - Ko;
    float3 oy23 = mod(floor(p23*K), 7.0)*K - Ko;
    float3 oz23 = floor(p23*K2)*Kz - Kzo;

    float3 ox31 = frac(p31*K) - Ko;
    float3 oy31 = mod(floor(p31*K), 7.0)*K - Ko;
    float3 oz31 = floor(p31*K2)*Kz - Kzo;

    float3 ox32 = frac(p32*K) - Ko;
    float3 oy32 = mod(floor(p32*K), 7.0)*K - Ko;
    float3 oz32 = floor(p32*K2)*Kz - Kzo;

    float3 ox33 = frac(p33*K) - Ko;
    float3 oy33 = mod(floor(p33*K), 7.0)*K - Ko;
    float3 oz33 = floor(p33*K2)*Kz - Kzo;

    float3 dx11 = Pfx + jitter * ox11;
    float3 dy11 = Pfy.x + jitter * oy11;
    float3 dz11 = Pfz.x + jitter * oz11;

    float3 dx12 = Pfx + jitter * ox12;
    float3 dy12 = Pfy.x + jitter * oy12;
    float3 dz12 = Pfz.y + jitter * oz12;

    float3 dx13 = Pfx + jitter * ox13;
    float3 dy13 = Pfy.x + jitter * oy13;
    float3 dz13 = Pfz.z + jitter * oz13;

    float3 dx21 = Pfx + jitter * ox21;
    float3 dy21 = Pfy.y + jitter * oy21;
    float3 dz21 = Pfz.x + jitter * oz21;

    float3 dx22 = Pfx + jitter * ox22;
    float3 dy22 = Pfy.y + jitter * oy22;
    float3 dz22 = Pfz.y + jitter * oz22;

    float3 dx23 = Pfx + jitter * ox23;
    float3 dy23 = Pfy.y + jitter * oy23;
    float3 dz23 = Pfz.z + jitter * oz23;

    float3 dx31 = Pfx + jitter * ox31;
    float3 dy31 = Pfy.z + jitter * oy31;
    float3 dz31 = Pfz.x + jitter * oz31;

    float3 dx32 = Pfx + jitter * ox32;
    float3 dy32 = Pfy.z + jitter * oy32;
    float3 dz32 = Pfz.y + jitter * oz32;

    float3 dx33 = Pfx + jitter * ox33;
    float3 dy33 = Pfy.z + jitter * oy33;
    float3 dz33 = Pfz.z + jitter * oz33;

    float3 d11 = dist(dx11, dy11, dz11, manhattanDistance);
    float3 d12 = dist(dx12, dy12, dz12, manhattanDistance);
    float3 d13 = dist(dx13, dy13, dz13, manhattanDistance);
    float3 d21 = dist(dx21, dy21, dz21, manhattanDistance);
    float3 d22 = dist(dx22, dy22, dz22, manhattanDistance);
    float3 d23 = dist(dx23, dy23, dz23, manhattanDistance);
    float3 d31 = dist(dx31, dy31, dz31, manhattanDistance);
    float3 d32 = dist(dx32, dy32, dz32, manhattanDistance);
    float3 d33 = dist(dx33, dy33, dz33, manhattanDistance);

    float3 d1a = min(d11, d12);
    d12 = max(d11, d12);
    d11 = min(d1a, d13); // Smallest now not in d12 or d13
    d13 = max(d1a, d13);
    d12 = min(d12, d13); // 2nd smallest now not in d13
    float3 d2a = min(d21, d22);
    d22 = max(d21, d22);
    d21 = min(d2a, d23); // Smallest now not in d22 or d23
    d23 = max(d2a, d23);
    d22 = min(d22, d23); // 2nd smallest now not in d23
    float3 d3a = min(d31, d32);
    d32 = max(d31, d32);
    d31 = min(d3a, d33); // Smallest now not in d32 or d33
    d33 = max(d3a, d33);
    d32 = min(d32, d33); // 2nd smallest now not in d33
    float3 da = min(d11, d21);
    d21 = max(d11, d21);
    d11 = min(da, d31); // Smallest now in d11
    d31 = max(da, d31); // 2nd smallest now not in d31
    d11.xy = (d11.x < d11.y) ? d11.xy : d11.yx;
    d11.xz = (d11.x < d11.z) ? d11.xz : d11.zx; // d11.x now smallest
    d12 = min(d12, d21); // 2nd smallest now not in d21
    d12 = min(d12, d22); // nor in d22
    d12 = min(d12, d31); // nor in d31
    d12 = min(d12, d32); // nor in d32
    d11.yz = min(d11.yz, d12.xy); // nor in d12.yz
    d11.y = min(d11.y, d12.z); // Only two more to go
    d11.y = min(d11.y, d11.z); // Done! (Phew!)
    return sqrt(d11.xy); // F1, F2

}
