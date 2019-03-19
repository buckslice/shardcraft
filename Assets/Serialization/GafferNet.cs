
// based on this glorious source code
//https://github.com/fbsamples/oculus-networked-physics-sample/blob/master/Networked%20Physics/Assets/Scripts/Network.cs
//https://github.com/fbsamples/oculus-networked-physics-sample/blob/master/Networked%20Physics/Assets/Scripts/PacketSerializer.cs

using System;
using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;


// problem with this is you cant ref a rigidbodys parameters so kinda annoying
// also he was using c++ template witchcraft in his example
//public class StateUpdate {
//    public int id; // id for object were editing
//    public byte prefab;
//    public Vector3 position;
//    public Quaternion rotation;
//    public bool atRest;
//    public Vector3 velocity;
//    public Vector3 angularVelocity;

//    public StateUpdate() {
//    }
//}

//public class StateUpdateBatch {
//    public StateUpdate[] states;
//    public StateUpdateBatch(Packet packet) {
//        int num = packet.ReadInt();
//        states = new StateUpdate[num];
//        for (int i = 0; i < num; ++i) {
//            states[i] = new StateUpdate();
//            NetCommon.SerializeState(packet, ref states[i]);
//        }
//    }
//}

//public class NetCommon {
//    // reason this is written using ref stuff is so same function can be used for both reading and writing
//    // so you will never mess that up basically hell yea
//    public static void SerializeState(Packet p, ref StateUpdate state) {
//        p.SerializeInt(ref state.id);
//        p.SerializeByte(ref state.prefab);
//        p.SerializeVector3(ref state.position);
//        p.SerializeQuaternion(ref state.rotation);
//        p.SerializeBool(ref state.atRest);
//        if (!state.atRest) {
//            p.SerializeVector3(ref state.velocity);
//            p.SerializeVector3(ref state.angularVelocity);
//        } else {
//            state.velocity = Vector3.zero;
//            state.angularVelocity = Vector3.zero;
//        }
//    }
//}


namespace GafferNet {
    public static class Constants {
        public const int MaxStringLength = 255;

        public const int STREAM_ERROR_NONE = 0;
        public const int STREAM_ERROR_OVERFLOW = 1;
        public const int STREAM_ERROR_ALIGNMENT = 2;
        public const int STREAM_ERROR_VALUE_OUT_OF_RANGE = 3;
    };

    public static class Util {
        public static uint SignedToUnsigned(int n) {
            return (uint)((n << 1) ^ (n >> 31));
        }

        public static int UnsignedToSigned(uint n) {
            return (int)((n >> 1) ^ (-(n & 1)));
        }

        public static bool SequenceGreaterThan(ushort s1, ushort s2) {
            return ((s1 > s2) && (s1 - s2 <= 32768)) ||
                   ((s1 < s2) && (s2 - s1 > 32768));
        }

        public static bool SequenceLessThan(ushort s1, ushort s2) {
            return SequenceGreaterThan(s2, s1);
        }

        public static int BaselineDifference(ushort current, ushort baseline) {
            if (current > baseline) {
                return current - baseline;
            } else {
                return (ushort)((((uint)current) + 65536) - baseline);
            }
        }

        public static uint SwapBytes(uint value) {
            return ((value & 0x000000FF) << 24) |
                   ((value & 0x0000FF00) << 8) |
                   ((value & 0x00FF0000) >> 8) |
                   ((value & 0xFF000000) >> 24);
        }

        public static uint HostToNetwork(uint value) {
            if (BitConverter.IsLittleEndian)
                return value;
            else
                return SwapBytes(value);
        }

        public static uint NetworkToHost(uint value) {
            if (BitConverter.IsLittleEndian)
                return value;
            else
                return SwapBytes(value);
        }

        public static int PopCount(uint value) {
            value = value - ((value >> 1) & 0x55555555);
            value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
            value = ((value + (value >> 4) & 0xF0F0F0F) * 0x1010101) >> 24;
            return unchecked((int)value);
        }

        public static int Log2(uint x) {
            uint a = x | (x >> 1);
            uint b = a | (a >> 2);
            uint c = b | (b >> 4);
            uint d = c | (c >> 8);
            uint e = d | (d >> 16);
            uint f = e >> 1;
            return PopCount(f);
        }

        public static int BitsRequired(int min, int max) {
            return (min == max) ? 1 : Log2((uint)(max - min)) + 1;
        }

        public static int BitsRequired(uint min, uint max) {
            return (min == max) ? 1 : Log2(max - min) + 1;
        }
    }

    public class BitWriter {
        uint[] m_data;
        ulong m_scratch;
        int m_numBits;
        int m_numWords;
        int m_bitsWritten;
        int m_wordIndex;
        int m_scratchBits;

        public void Start(uint[] data) {
            Assert.IsTrue(data != null);
            m_data = data;
            m_numWords = data.Length / 4;
            m_numBits = m_numWords * 32;
            m_bitsWritten = 0;
            m_wordIndex = 0;
            m_scratch = 0;
            m_scratchBits = 0;
        }

        public void WriteBits(uint value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            Assert.IsTrue(m_bitsWritten + bits <= m_numBits);

            value &= (uint)((((ulong)1) << bits) - 1);

            m_scratch |= ((ulong)value) << m_scratchBits;

            m_scratchBits += bits;

            if (m_scratchBits >= 32) {
                Assert.IsTrue(m_wordIndex < m_numWords);
                m_data[m_wordIndex] = Util.HostToNetwork((uint)(m_scratch & 0xFFFFFFFF));
                m_scratch >>= 32;
                m_scratchBits -= 32;
                m_wordIndex++;
            }

            m_bitsWritten += bits;
        }

        public void WriteAlign() {
            int remainderBits = (int)(m_bitsWritten % 8);
            if (remainderBits != 0) {
                uint zero = 0;
                WriteBits(zero, 8 - remainderBits);
                Assert.IsTrue((m_bitsWritten % 8) == 0);
            }
        }

        public void WriteBytes(byte[] data, int bytes) {
            Assert.IsTrue(GetAlignBits() == 0);
            for (int i = 0; i < bytes; ++i)
                WriteBits(data[i], 8);
        }

        public void Finish() {
            if (m_scratchBits != 0) {
                Assert.IsTrue(m_wordIndex < m_numWords);
                m_data[m_wordIndex] = Util.HostToNetwork((uint)(m_scratch & 0xFFFFFFFF));
                m_scratch >>= 32;
                m_scratchBits -= 32;
                m_wordIndex++;
            }
        }

        public int GetAlignBits() {
            return (8 - (m_bitsWritten % 8)) % 8;
        }

        public int GetBitsWritten() {
            return m_bitsWritten;
        }

        public int GetBitsAvailable() {
            return m_numBits - m_bitsWritten;
        }

        public byte[] GetData() {
            int bytesWritten = GetBytesWritten();
            byte[] output = new byte[bytesWritten];
            Buffer.BlockCopy(m_data, 0, output, 0, bytesWritten);
            return output;
        }

        public int GetBytesWritten() {
            return (m_bitsWritten + 7) / 8;
        }

        public int GetTotalBytes() {
            return m_numWords * 4;
        }
    }

    public class BitReader {
        uint[] m_data;
        ulong m_scratch;
        int m_numBits;
        int m_numWords;
        int m_bitsRead;
        int m_scratchBits;
        int m_wordIndex;

        public void Start(byte[] data) {
            int bytes = data.Length;
            m_numWords = (bytes + 3) / 4;
            m_numBits = bytes * 8;
            m_bitsRead = 0;
            m_scratch = 0;
            m_scratchBits = 0;
            m_wordIndex = 0;
            m_data = new uint[m_numWords];
            Buffer.BlockCopy(data, 0, m_data, 0, bytes);
        }

        public bool WouldOverflow(int bits) {
            return m_bitsRead + bits > m_numBits;
        }

        public uint ReadBits(int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            Assert.IsTrue(m_bitsRead + bits <= m_numBits);

            m_bitsRead += bits;

            Assert.IsTrue(m_scratchBits >= 0 && m_scratchBits <= 64);

            if (m_scratchBits < bits) {
                Assert.IsTrue(m_wordIndex < m_numWords);
                m_scratch |= ((ulong)(Util.NetworkToHost(m_data[m_wordIndex]))) << m_scratchBits;
                m_scratchBits += 32;
                m_wordIndex++;
            }

            Assert.IsTrue(m_scratchBits >= bits);

            uint output = (uint)(m_scratch & ((((ulong)1) << bits) - 1));

            m_scratch >>= bits;
            m_scratchBits -= bits;

            return output;
        }

        public bool ReadAlign() {
            int remainderBits = m_bitsRead % 8;
            if (remainderBits != 0) {
                uint value = ReadBits(8 - remainderBits);
                Assert.IsTrue(m_bitsRead % 8 == 0);
                if (value != 0)
                    return false;
            }
            return true;
        }

        public void ReadBytes(byte[] data, int bytes) {
            Assert.IsTrue(GetAlignBits() == 0);
            for (int i = 0; i < bytes; ++i)
                data[i] = (byte)ReadBits(8);
        }

        public void Finish() {
            // ...
        }

        public int GetAlignBits() {
            return (8 - m_bitsRead % 8) % 8;
        }

        public int GetBitsRead() {
            return m_bitsRead;
        }

        public int GetBytesRead() {
            return m_wordIndex * 4;
        }

        public int GetBitsRemaining() {
            return m_numBits - m_bitsRead;
        }

        public int GetBytesRemaining() {
            return GetBitsRemaining() / 8;
        }
    }

    public class WriteStream {
        BitWriter m_writer = new BitWriter();
        int m_error = Constants.STREAM_ERROR_NONE;

        public void Start(uint[] buffer) {
            m_writer.Start(buffer);
        }

        public void SerializeSignedInteger(int value, int min, int max) {
            Assert.IsTrue(min < max);
            Assert.IsTrue(value >= min);
            Assert.IsTrue(value <= max);
            int bits = Util.BitsRequired(min, max);
            uint unsigned_value = (uint)(value - min);
            m_writer.WriteBits(unsigned_value, bits);
        }

        public void SerializeUnsignedInteger(uint value, uint min, uint max) {
            Assert.IsTrue(min < max);
            Assert.IsTrue(value >= min);
            Assert.IsTrue(value <= max);
            int bits = Util.BitsRequired(min, max);
            uint unsigned_value = value - min;
            m_writer.WriteBits(unsigned_value, bits);
        }

        public void SerializeBits(byte value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 8);
            Assert.IsTrue(bits == 8 || (value < (1 << bits)));
            m_writer.WriteBits(value, bits);
        }

        public void SerializeBits(ushort value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 16);
            Assert.IsTrue(bits == 16 || (value < (1 << bits)));
            m_writer.WriteBits(value, bits);
        }

        public void SerializeBits(uint value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            Assert.IsTrue(bits == 32 || (value < (1 << bits)));
            m_writer.WriteBits(value, bits);
        }

        public void SerializeBits(ulong value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 64);
            Assert.IsTrue(bits == 64 || (value < (1UL << bits)));

            uint loword = (uint)value;
            uint hiword = (uint)(value >> 32);

            if (bits <= 32) {
                m_writer.WriteBits(loword, bits);
            } else {
                m_writer.WriteBits(loword, 32);
                m_writer.WriteBits(hiword, bits - 32);
            }
        }

        public void SerializeBytes(byte[] data, int bytes) {
            Assert.IsTrue(data != null);
            Assert.IsTrue(bytes >= 0);
            SerializeAlign();
            m_writer.WriteBytes(data, bytes);
        }

        public void SerializeString(string s) {
            SerializeAlign();
            int stringLength = (int)s.Length;
            Assert.IsTrue(stringLength <= GafferNet.Constants.MaxStringLength);
            m_writer.WriteBits((byte)stringLength, Util.BitsRequired(0, GafferNet.Constants.MaxStringLength));
            for (int i = 0; i < stringLength; ++i) {
                char charValue = s[i];
                m_writer.WriteBits(charValue, 16);
            }
        }

        public void SerializeFloat(float f) {
            byte[] byteArray = BitConverter.GetBytes(f);
            for (int i = 0; i < 4; ++i)
                m_writer.WriteBits(byteArray[i], 8);
        }

        public void SerializeAlign() {
            m_writer.WriteAlign();
        }

        public void Finish() {
            m_writer.Finish();
        }

        public int GetAlignBits() {
            return m_writer.GetAlignBits();
        }

        public byte[] GetData() {
            return m_writer.GetData();
        }

        public int GetBytesProcessed() {
            return m_writer.GetBytesWritten();
        }

        public int GetBitsProcessed() {
            return m_writer.GetBitsWritten();
        }

        public int GetError() {
            return m_error;
        }
    }

    public class ReadStream {
        BitReader m_reader = new BitReader();
        int m_bitsRead = 0;
        int m_error = Constants.STREAM_ERROR_NONE;
        byte[] m_floatBytes = new byte[4];

        public void Start(byte[] data) {
            m_reader.Start(data);
        }

        public bool SerializeSignedInteger(out int value, int min, int max) {
            Assert.IsTrue(min < max);
            int bits = Util.BitsRequired(min, max);
            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                value = 0;
                return false;
            }
            uint unsigned_value = m_reader.ReadBits(bits);
            value = (int)(unsigned_value + min);
            m_bitsRead += bits;
            return true;
        }

        public bool SerializeUnsignedInteger(out uint value, uint min, uint max) {
            Assert.IsTrue(min < max);
            int bits = Util.BitsRequired(min, max);
            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                value = 0;
                return false;
            }
            uint unsigned_value = m_reader.ReadBits(bits);
            value = unsigned_value + min;
            m_bitsRead += bits;
            return true;
        }

        public bool SerializeBits(out byte value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 8);
            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                value = 0;
                return false;
            }
            byte read_value = (byte)m_reader.ReadBits(bits);
            value = read_value;
            m_bitsRead += bits;
            return true;
        }

        public bool SerializeBits(out ushort value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 16);
            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                value = 0;
                return false;
            }
            ushort read_value = (ushort)m_reader.ReadBits(bits);
            value = read_value;
            m_bitsRead += bits;
            return true;
        }

        public bool SerializeBits(out uint value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                value = 0;
                return false;
            }
            uint read_value = m_reader.ReadBits(bits);
            value = read_value;
            m_bitsRead += bits;
            return true;
        }

        public bool SerializeBits(out ulong value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 64);

            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                value = 0;
                return false;
            }

            if (bits <= 32) {
                uint loword = m_reader.ReadBits(bits);
                value = (ulong)loword;
            } else {
                uint loword = m_reader.ReadBits(32);
                uint hiword = m_reader.ReadBits(bits - 32);
                value = ((ulong)loword) | (((ulong)hiword) << 32);
            }

            return true;
        }

        public bool SerializeBytes(byte[] data, int bytes) {
            if (!SerializeAlign())
                return false;
            if (m_reader.WouldOverflow(bytes * 8)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                return false;
            }
            m_reader.ReadBytes(data, bytes);
            m_bitsRead += bytes * 8;
            return true;
        }

        public bool SerializeString(out string s) {
            if (!SerializeAlign()) {
                s = null;
                return false;
            }

            int stringLength;
            if (!SerializeSignedInteger(out stringLength, 0, GafferNet.Constants.MaxStringLength)) {
                s = null;
                return false;
            }

            if (m_reader.WouldOverflow((int)(stringLength * 16))) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                s = null;
                return false;
            }

            char[] stringData = new char[GafferNet.Constants.MaxStringLength];

            for (int i = 0; i < stringLength; ++i) {
                stringData[i] = (char)m_reader.ReadBits(16);
            }

            s = new string(stringData, 0, stringLength);

            return true;
        }

        public bool SerializeFloat(out float f) {
            if (m_reader.WouldOverflow(32)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                f = 0.0f;
                return false;
            }

            for (int i = 0; i < 4; ++i)
                m_floatBytes[i] = (byte)m_reader.ReadBits(8);

            f = BitConverter.ToSingle(m_floatBytes, 0);

            return true;
        }

        public bool SerializeAlign() {
            int alignBits = m_reader.GetAlignBits();
            if (m_reader.WouldOverflow(alignBits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                return false;
            }
            if (!m_reader.ReadAlign()) {
                m_error = Constants.STREAM_ERROR_ALIGNMENT;
                return false;
            }
            m_bitsRead += alignBits;
            return true;
        }

        public void Finish() {
            m_reader.Finish();
        }

        public int GetAlignBits() {
            return m_reader.GetAlignBits();
        }

        public int GetBitsProcessed() {
            return m_bitsRead;
        }

        public int GetBytesProcessed() {
            return (m_bitsRead + 7) / 8;
        }

        public int GetError() {
            return m_error;
        }
    }

    public class SerializeException : Exception {
        public SerializeException() { }
    };

}