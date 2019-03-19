using UnityEngine;
using System.IO;
using GafferNet;

// packet types
// byte put at beginning of packet indicating what type of message it is
// can use this to determine order to read back data in
public enum PacketType : byte {
    LOGIN,
    MESSAGE,
    CHAT_MESSAGE,
    STATE_UPDATE,
    SPAWN_PREFAB,
    TEST,
}

// to unify read and write functions to reduce errors
public abstract class Serializer {
    public abstract void SerializeInt(ref int i, int min, int max);

    public abstract void SerializeString(ref string s);

    public abstract void SerializeBool(ref bool b);

    public abstract void SerializeFloat(ref float f);

    public abstract void SerializeVector3(ref Vector3 v);

    public abstract void SerializeQuaternion(ref Quaternion q);

    public abstract void SerializeColor(ref Color32 c);

    // can provide less bits than usual if desired
    public abstract void SerializeBits(ref byte b, int bits);
    public abstract void SerializeBits(ref ushort b, int bits);
    public abstract void SerializeBits(ref uint b, int bits);
    public abstract void SerializeBits(ref ulong b, int bits);
}

public class PacketReader : Serializer {
    ReadStream reader = null;

    public PacketReader(ReadStream stream, byte[] packetData) {
        reader = stream;
        reader.Start(packetData);
    }

    public int ReadInt(int min = int.MinValue, int max = int.MaxValue) {
        int i;
        if (!reader.SerializeSignedInteger(out i, min, max))
            throw new SerializeException();
        return i;
    }
    public bool ReadBool() {
        uint ui;
        if (!reader.SerializeBits(out ui, 1))
            throw new SerializeException();
        return ui == 1;
    }
    public string ReadString() {
        string s;
        if (!reader.SerializeString(out s))
            throw new SerializeException();
        return s;
    }
    public float ReadFloat() {
        float f;
        if (!reader.SerializeFloat(out f))
            throw new SerializeException();
        return f;
    }
    public Vector3 ReadVector3() {
        return new Vector3(
            ReadFloat(),
            ReadFloat(),
            ReadFloat());
    }
    public Quaternion ReadQuaternion() {
        return new Quaternion(
            ReadFloat(),
            ReadFloat(),
            ReadFloat(),
            ReadFloat());
    }
    public Color32 ReadColor() {
        return new Color32(
            ReadByte(),
            ReadByte(),
            ReadByte(),
            ReadByte());
    }

    public byte ReadByte(int bits = 8) {
        byte b;
        if (!reader.SerializeBits(out b, bits)) {
            throw new SerializeException();
        }
        return b;
    }

    public ushort ReadUShort(int bits = 16) {
        ushort b;
        if (!reader.SerializeBits(out b, bits)) {
            throw new SerializeException();
        }
        return b;
    }

    public uint ReadUInt(int bits = 32) {
        uint ui;
        if (!reader.SerializeBits(out ui, bits)) {
            throw new SerializeException();
        }
        return ui;
    }

    public ulong ReadULong(int bits = 64) {
        ulong ul;
        if (!reader.SerializeBits(out ul, bits)) {
            throw new SerializeException();
        }
        return ul;
    }


    public override void SerializeInt(ref int i, int min = int.MinValue, int max = int.MaxValue) {
        i = ReadInt(min, max);
    }
    public override void SerializeString(ref string s) {
        s = ReadString();
    }
    public override void SerializeBool(ref bool b) {
        b = ReadBool();
    }
    public override void SerializeFloat(ref float f) {
        f = ReadFloat();
    }
    public override void SerializeVector3(ref Vector3 v) {
        v = ReadVector3();
    }
    public override void SerializeQuaternion(ref Quaternion q) {
        q = ReadQuaternion();
    }
    public override void SerializeColor(ref Color32 c) {
        c = ReadColor();
    }
    public override void SerializeBits(ref byte b, int bits = 8) {
        b = ReadByte(bits);
    }
    public override void SerializeBits(ref ushort b, int bits = 16) {
        b = ReadUShort(bits);
    }
    public override void SerializeBits(ref uint b, int bits = 32) {
        b = ReadUInt(bits);
    }
    public override void SerializeBits(ref ulong b, int bits = 64) {
        b = ReadULong(bits);
    }
}

public class PacketWriter : Serializer {
    WriteStream writer = null;

    public PacketWriter(PacketType type, WriteStream stream, uint[] packetBuffer) {
        writer = stream;
        writer.Start(packetBuffer);
        Write((byte)type);
    }
    public PacketWriter(WriteStream stream, uint[] packetBuffer) {
        writer = stream;
        writer.Start(packetBuffer);
    }

    public void Write(int i, int min = int.MinValue, int max = int.MaxValue) {
        writer.SerializeSignedInteger(i, min, max);
    }
    public void Write(string s) {
        writer.SerializeString(s);
    }
    public void Write(bool b) {
        uint unsigned_value = b ? 1U : 0U;
        writer.SerializeBits(unsigned_value, 1);
    }
    public void Write(float f) {
        writer.SerializeFloat(f);
    }
    public void Write(Vector3 v) {
        writer.SerializeFloat(v.x);
        writer.SerializeFloat(v.y);
        writer.SerializeFloat(v.z);
    }
    public void Write(Quaternion q) {
        writer.SerializeFloat(q.x);
        writer.SerializeFloat(q.y);
        writer.SerializeFloat(q.z);
        writer.SerializeFloat(q.w);
    }
    public void Write(Color32 c) {
        writer.SerializeBits(c.r, 8);
        writer.SerializeBits(c.g, 8);
        writer.SerializeBits(c.b, 8);
        writer.SerializeBits(c.a, 8);
    }
    public void Write(byte b, int bits = 8) {
        writer.SerializeBits(b, bits);
    }
    public void Write(ushort b, int bits = 16) {
        writer.SerializeBits(b, bits);
    }
    public void Write(uint b, int bits = 32) {
        writer.SerializeBits(b, bits);
    }
    public void Write(ulong b, int bits = 64) {
        writer.SerializeBits(b, bits);
    }

    public void WriteBytes(byte[] data, int bytes) {
        writer.SerializeBytes(data, bytes);
    }

    /// <summary>
    /// Returns packet data to be sent over network
    /// </summary>
    /// <returns></returns>
    public byte[] GetData() {
        writer.Finish();
        return writer.GetData();
    }
    /// <summary>
    /// Returns length of data
    /// </summary>
    /// <returns></returns>
    public int GetSize() {
        if (writer != null) {
            return writer.GetBytesProcessed();
        }
        return -1;
    }

    public override void SerializeInt(ref int i, int min = int.MinValue, int max = int.MaxValue) {
        Write(i, min, max);
    }
    public override void SerializeString(ref string s) {
        Write(s);
    }
    public override void SerializeBool(ref bool b) {
        Write(b);
    }
    public override void SerializeFloat(ref float f) {
        Write(f);
    }
    public override void SerializeVector3(ref Vector3 v) {
        Write(v);
    }
    public override void SerializeQuaternion(ref Quaternion q) {
        Write(q);
    }
    public override void SerializeColor(ref Color32 c) {
        Write(c);
    }
    public override void SerializeBits(ref byte b, int bits = 8) {
        Write(b, bits);
    }
    public override void SerializeBits(ref ushort b, int bits = 16) {
        Write(b, bits);
    }
    public override void SerializeBits(ref uint b, int bits = 32) {
        Write(b, bits);
    }
    public override void SerializeBits(ref ulong b, int bits = 64) {
        Write(b, bits);
    }
}
