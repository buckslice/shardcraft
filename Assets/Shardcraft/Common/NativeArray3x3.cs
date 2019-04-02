using Unity.Collections;

// used to store native arrays for local 3x3 block of neighbors
public struct NativeArray3x3<T> where T : struct {

    public NativeArray<T> c; // center
    // 26 neighbors in a 3x3 cube around center
    public NativeArray<T> w;
    public NativeArray<T> d;
    public NativeArray<T> s;
    public NativeArray<T> e;
    public NativeArray<T> u;
    public NativeArray<T> n;
    public NativeArray<T> uw;
    public NativeArray<T> us;
    public NativeArray<T> ue;
    public NativeArray<T> un;
    public NativeArray<T> dw;
    public NativeArray<T> ds;
    public NativeArray<T> de;
    public NativeArray<T> dn;
    public NativeArray<T> sw;
    public NativeArray<T> se;
    public NativeArray<T> nw;
    public NativeArray<T> ne;
    public NativeArray<T> usw;
    public NativeArray<T> use;
    public NativeArray<T> unw;
    public NativeArray<T> une;
    public NativeArray<T> dsw;
    public NativeArray<T> dse;
    public NativeArray<T> dnw;
    public NativeArray<T> dne;

    const int S = Chunk.SIZE;

    public T Get(int x, int y, int z) {
        if (y < 0) {
            if (z < 0) {
                if (x < 0) {
                    return dsw[(x + S) + (z + S) * S + (y + S) * S * S];
                } else if (x >= S) {
                    return dse[(x - S) + (z + S) * S + (y + S) * S * S];
                } else {
                    return ds[x + (z + S) * S + (y + S) * S * S];
                }
            } else if (z >= S) {
                if (x < 0) {
                    return dnw[(x + S) + (z - S) * S + (y + S) * S * S];
                } else if (x >= S) {
                    return dne[(x - S) + (z - S) * S + (y + S) * S * S];
                } else {
                    return dn[x + (z - S) * S + (y + S) * S * S];
                }
            } else {
                if (x < 0) {
                    return dw[(x + S) + z * S + (y + S) * S * S];
                } else if (x >= S) {
                    return de[(x - S) + z * S + (y + S) * S * S];
                } else {
                    return d[x + z * S + (y + S) * S * S];
                }
            }
        } else if (y >= S) {
            if (z < 0) {
                if (x < 0) {
                    return usw[(x + S) + (z + S) * S + (y - S) * S * S];
                } else if (x >= S) {
                    return use[(x - S) + (z + S) * S + (y - S) * S * S];
                } else {
                    return us[x + (z + S) * S + (y - S) * S * S];
                }
            } else if (z >= S) {
                if (x < 0) {
                    return unw[(x + S) + (z - S) * S + (y - S) * S * S];
                } else if (x >= S) {
                    return une[(x - S) + (z - S) * S + (y - S) * S * S];
                } else {
                    return un[x + (z - S) * S + (y - S) * S * S];
                }
            } else {
                if (x < 0) {
                    return uw[(x + S) + z * S + (y - S) * S * S];
                } else if (x >= S) {
                    return ue[(x - S) + z * S + (y - S) * S * S];
                } else {
                    return u[x + z * S + (y - S) * S * S];
                }
            }
        } else {
            if (z < 0) {
                if (x < 0) {
                    return sw[(x + S) + (z + S) * S + y * S * S];
                } else if (x >= S) {
                    return se[(x - S) + (z + S) * S + y * S * S];
                } else {
                    return s[x + (z + S) * S + y * S * S];
                }
            } else if (z >= S) {
                if (x < 0) {
                    return nw[(x + S) + (z - S) * S + y * S * S];
                } else if (x >= S) {
                    return ne[(x - S) + (z - S) * S + y * S * S];
                } else {
                    return n[x + (z - S) * S + y * S * S];
                }
            } else {
                if (x < 0) {
                    return w[(x + S) + z * S + y * S * S];
                } else if (x >= S) {
                    return e[(x - S) + z * S + y * S * S];
                } else {
                    return c[x + z * S + y * S * S];
                }
            }
        }
    }
}


// used to store native arrays for center plus 6 side neighbors
public struct NativeArrayC6<T> where T : struct {

    public NativeArray<T> c; // center
    // 26 neighbors in a 3x3 cube around center
    public NativeArray<T> w;
    public NativeArray<T> d;
    public NativeArray<T> s;
    public NativeArray<T> e;
    public NativeArray<T> u;
    public NativeArray<T> n;

    const int S = Chunk.SIZE;

    public T Get(int x, int y, int z) {
        if (x < 0) {
            return w[x + S + z * S + y * S * S];
        } else if (x >= S) {
            return e[x - S + z * S + y * S * S];
        } else if (y < 0) {
            return d[x + z * S + (y + S) * S * S];
        } else if (y >= S) {
            return u[x + z * S + (y - S) * S * S];
        } else if (z < 0) {
            return s[x + (z + S) * S + y * S * S];
        } else if (z >= S) {
            return s[x + (z - S) * S + y * S * S];
        } else {
            return c[x + z * S + y * S * S];
        }

        //if (x >= 0 && x < S && y >= 0 && y < S && z >= 0 && z < S) {
        // should try reordering these if chains to favor case where you are in center block
        // compare average job completion time in profiler instead of fps / time to complete
        //}

    }

}