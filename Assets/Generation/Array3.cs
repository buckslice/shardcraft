using UnityEngine;
using System.Collections;

// 3D cube array class which is actually just a 1D array using index calculations
public class Array3<T> where T : struct {

    public int size;

    public T[] data;

    public Array3(int size) {
        this.size = size;
        data = new T[size * size * size];
    }

    public Array3(T[] data, int size) {
        this.data = data;
        this.size = size;
        Debug.Assert(size * size * size == data.Length);
    }

    public T this[int x, int y, int z] {
        get {
            return data[x + y * size + z * size * size];
        }
        set {
            data[x + y * size + z * size * size] = value;
        }
    }

    // make sure to use sizeCubed
    public T this[int i] {
        get {
            return data[i];
        }
        set {
            data[i] = value;
        }
    }

    public T this[Vector3i v] {
        get {
            return this[v.x, v.y, v.z];
        }
        set {
            this[v.x, v.y, v.z] = value;
        }
    }

}

