using UnityEngine;
using System.Collections;

// 3D cube array class which is actually just a 1D array using index calculations
public class Array3<T> where T : struct {

    public int size;
    public int sizeCubed;

    private T[] data;

    public Array3(int size) {
        this.size = size;
        sizeCubed = size * size * size;
        data = new T[sizeCubed];
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

    public T[] GetData() {
        return data;
    }

}

