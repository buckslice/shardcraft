using System;
using UnityEngine;

[Serializable]
public struct Vector3i : IEquatable<Vector3i> {
    public int x;
    public int y;
    public int z;

    public Vector3i(int x, int y, int z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public void Add(Vector3i v) {
        x += v.x;
        y += v.y;
        z += v.z;
    }

    public void Sub(Vector3i v) {
        x -= v.x;
        y -= v.y;
        z -= v.z;
    }

    public void Mul(int scalar) {
        x *= scalar;
        y *= scalar;
        z *= scalar;
    }

    public void Div(int scalar) {
        x /= scalar;
        y /= scalar;
        z /= scalar;
    }

    public int this[int i] {
        get {
            if (i == 0)
                return x;
            if (i == 1)
                return y;
            if (i == 2)
                return z;

            throw new ArgumentOutOfRangeException(string.Format("There is no value at {0} index.", i));
        }
        set {
            if (i == 0)
                x = value;
            if (i == 1)
                y = value;
            if (i == 2)
                z = value;

            throw new ArgumentOutOfRangeException(string.Format("There is no value at {0} index.", i));
        }
    }

    public override string ToString() {
        return string.Format("({0}, {1}, {2})", x, y, z);
    }

    public Vector3 ToVector3() {
        return new Vector3(x, y, z);
    }

    // Default values
    public static Vector3i one {
        get { return new Vector3i(1, 1, 1); }
    }
    public static Vector3i zero {
        get { return new Vector3i(0, 0, 0); }
    }
    public static Vector3i right {
        get { return new Vector3i(1, 0, 0); }
    }
    public static Vector3i up {
        get { return new Vector3i(0, 1, 0); }
    }
    public static Vector3i forward {
        get { return new Vector3i(0, 0, 1); }
    }
    public static Vector3i left {
        get { return new Vector3i(-1, 0, 0); }
    }
    public static Vector3i down {
        get { return new Vector3i(0, -1, 0); }
    }
    public static Vector3i back {
        get { return new Vector3i(0, 0, -1); }
    }


    public static explicit operator Vector3i(Vector3 vf) {
        Vector3i v;
        v.x = (int)vf.x;
        v.y = (int)vf.y;
        v.z = (int)vf.z;
        return v;
    }
    //public static implicit operator Vector3(Vector3i source) {
    //    return new Vector3(source.x, source.y, source.z);
    //}

    public static Vector3i operator +(Vector3i v0, Vector3i v1) {
        v0.x += v1.x;
        v0.y += v1.y;
        v0.z += v1.z;
        return v0;
    }

    public static Vector3i operator -(Vector3i v0, Vector3i v1) {
        v0.x -= v1.x;
        v0.y -= v1.y;
        v0.z -= v1.z;
        return v0;
    }
    public static Vector3i operator /(Vector3i v0, Vector3i v1) {
        v0.x /= v1.x;
        v0.y /= v1.y;
        v0.z /= v1.z;
        return v0;
    }

    public static Vector3i operator *(Vector3i v0, Vector3i v1) {
        v0.x *= v1.x;
        v0.y *= v1.y;
        v0.z *= v1.z;
        return v0;
    }

    public static Vector3i operator *(Vector3i v, int s) {
        v.x *= s;
        v.y *= s;
        v.z *= s;
        return v;
    }
    public static Vector3i operator *(int s, Vector3i v) {
        return v * s;
    }

    public static Vector3i operator /(Vector3i v, int s) {
        v.x /= s;
        v.y /= s;
        v.z /= s;
        return v;
    }

    public static bool operator <(Vector3i a, Vector3i b) {
        return a.x < b.x && a.y < b.y && a.z < b.z;
    }

    public static bool operator >(Vector3i a, Vector3i b) {
        return a.x > b.x && a.y > b.y && a.z > b.z;
    }

    public static bool operator <=(Vector3i a, Vector3i b) {
        return a.x <= b.x && a.y <= b.y && a.z <= b.z;
    }

    public static bool operator >=(Vector3i a, Vector3i b) {
        return a.x >= b.x && a.y >= b.y && a.z >= b.z;
    }

    public static bool operator ==(Vector3i lhs, Vector3i rhs) {
        return lhs.x == rhs.x &&
               lhs.y == rhs.y &&
               lhs.z == rhs.z;
    }

    public static bool operator !=(Vector3i lhs, Vector3i rhs) {
        return !(lhs == rhs);
    }

    public bool Equals(Vector3i other) {
        return this == other;
    }

    public override bool Equals(object other) {
        if (!(other is Vector3i)) {
            return false;
        }
        return this == (Vector3i)other;
    }

    public override int GetHashCode() {
        unchecked {
            int result = y;
            result = (result * 397) ^ z;
            result = (result * 397) ^ x;
            return result;
        }
    }

    //public override int GetHashCode() {
    //    var yHash = y.GetHashCode();
    //    var zHash = z.GetHashCode();
    //    return x.GetHashCode() ^ (yHash << 4) ^ (yHash >> 28) ^ (zHash >> 4) ^ (zHash << 28);
    //}

    //public override int GetHashCode() {
    //    return this.x.GetHashCode() ^ this.y.GetHashCode() << 2 ^ this.z.GetHashCode() >> 2;
    //}
}