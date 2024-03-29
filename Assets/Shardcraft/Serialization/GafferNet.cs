﻿
// based on this glorious source code
// adapted and simplified code to my liking
//https://github.com/fbsamples/oculus-networked-physics-sample/blob/master/Networked%20Physics/Assets/Scripts/Network.cs
//https://github.com/fbsamples/oculus-networked-physics-sample/blob/master/Networked%20Physics/Assets/Scripts/PacketSerializer.cs

using System;
using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;


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

        // little endian is most common these days so lets work in that
        public static uint EnsureLittleEndian(uint value) {
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
            m_numWords = data.Length;
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
                m_data[m_wordIndex] = Util.EnsureLittleEndian((uint)(m_scratch & 0xFFFFFFFF));
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
                m_data[m_wordIndex] = Util.EnsureLittleEndian((uint)(m_scratch & 0xFFFFFFFF));
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

        public int GetData(byte[] dstBuffer) {
            int bytesWritten = GetBytesWritten();
            Assert.IsTrue(dstBuffer.Length >= bytesWritten);
            Buffer.BlockCopy(m_data, 0, dstBuffer, 0, bytesWritten);
            return bytesWritten;
        }

        public int GetData(byte[] dstBuffer, int dstOffset, int bytes) { // bytes not really required but just for error checking
            int bytesWritten = GetBytesWritten();
            Assert.IsTrue(bytes == bytesWritten);
            Assert.IsTrue(dstBuffer.Length - dstOffset >= bytesWritten);
            Buffer.BlockCopy(m_data, 0, dstBuffer, dstOffset, bytesWritten);
            return bytesWritten;
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
            m_numWords = (bytes + 3) / 4; // so if just 1 extra byte it will become whole word
            m_numBits = bytes * 8;
            m_bitsRead = 0;
            m_scratch = 0;
            m_scratchBits = 0;
            m_wordIndex = 0;
            m_data = new uint[m_numWords];
            Buffer.BlockCopy(data, 0, m_data, 0, bytes);
        }

        // TEST THIS OUT, provide a buffer too on reading so dont need to instantiate new array
        // make sure you can use the uint buffer at this time. mightve been written to...

        public void Start(byte[] data, int srcOffset, uint[] buffer, int bytes) {
            m_numWords = (bytes + 3) / 4; // so if just 1 extra byte it will become whole word
            m_numBits = bytes * 8;
            m_bitsRead = 0;
            m_scratch = 0;
            m_scratchBits = 0;
            m_wordIndex = 0;
            m_data = buffer;
            Buffer.BlockCopy(data, srcOffset, buffer, 0, bytes);
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
                m_scratch |= ((ulong)(Util.EnsureLittleEndian(m_data[m_wordIndex]))) << m_scratchBits;
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

        //public void Finish() {
        //}

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

    public class SerializeException : Exception {
        public SerializeException() {
        }
    };

    public class WriteStream {
        BitWriter m_writer = new BitWriter();
        int m_error = Constants.STREAM_ERROR_NONE;

        public void Start(uint[] buffer) {
            m_writer.Start(buffer);
        }

        public void Write(int value, int min = int.MinValue, int max = int.MaxValue) {
            Assert.IsTrue(min < max);
            Assert.IsTrue(value >= min);
            Assert.IsTrue(value <= max);
            int bits = Util.BitsRequired(min, max);
            uint unsigned_value = (uint)(value - min);
            m_writer.WriteBits(unsigned_value, bits);
        }

        public void WriteUInt(uint value, uint min = uint.MinValue, uint max = uint.MaxValue) {
            Assert.IsTrue(min < max);
            Assert.IsTrue(value >= min);
            Assert.IsTrue(value <= max);
            int bits = Util.BitsRequired(min, max);
            uint unsigned_value = value - min;
            m_writer.WriteBits(unsigned_value, bits);
        }

        public void Write(byte value, int bits = 8) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 8);
            Assert.IsTrue(bits == 8 || (value < (1 << bits)));
            m_writer.WriteBits(value, bits);
        }

        public void Write(ushort value, int bits = 16) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 16);
            Assert.IsTrue(bits == 16 || (value < (1 << bits)));
            m_writer.WriteBits(value, bits);
        }

        public void Write(uint value, int bits = 32) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            Assert.IsTrue(bits == 32 || (value < (1 << bits)));
            m_writer.WriteBits(value, bits);
        }

        public void Write(ulong value, int bits = 64) {
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

        public void WriteBytes(byte[] data, int bytes) {
            Assert.IsTrue(data != null);
            Assert.IsTrue(bytes >= 0);
            SerializeAlign();
            m_writer.WriteBytes(data, bytes);
        }

        public void Write(string s) {
            SerializeAlign();
            int stringLength = (int)s.Length;
            Assert.IsTrue(stringLength <= GafferNet.Constants.MaxStringLength);
            m_writer.WriteBits((byte)stringLength, Util.BitsRequired(0, GafferNet.Constants.MaxStringLength));
            for (int i = 0; i < stringLength; ++i) {
                char charValue = s[i];
                m_writer.WriteBits(charValue, 16);
            }
        }

        public void Write(bool b) {
            uint unsigned_value = b ? 1U : 0U;
            Write(unsigned_value, 1);
        }

        public void Write(float f) {
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
        public int GetData(byte[] dstBuffer) {
            return m_writer.GetData(dstBuffer);
        }
        public int GetData(byte[] dstBuffer, int dstOffset, int bytes) {
            return m_writer.GetData(dstBuffer, dstOffset, bytes);
        }

        public int GetBytesWritten() {
            return m_writer.GetBytesWritten();
        }

        public int GetBitsWritten() {
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

        public void Start(byte[] data, int srcOffset, uint[] buffer, int bytes) {
            m_reader.Start(data, srcOffset, buffer, bytes);
        }

        public int ReadInt(int min = int.MinValue, int max = int.MaxValue) {
            Assert.IsTrue(min < max);
            int bits = Util.BitsRequired(min, max);
            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                throw new SerializeException();
            }
            uint unsigned_value = m_reader.ReadBits(bits);
            m_bitsRead += bits;
            return (int)(unsigned_value + min);
        }

        public byte ReadByte(int bits = 8) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 8);
            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                throw new SerializeException();
            }
            byte read_value = (byte)m_reader.ReadBits(bits);
            m_bitsRead += bits;
            return read_value;
        }

        public ushort ReadUShort(int bits = 16) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 16);
            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                throw new SerializeException();
            }
            ushort read_value = (ushort)m_reader.ReadBits(bits);
            m_bitsRead += bits;
            return read_value;
        }

        public uint ReadUInt(int bits = 32) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                throw new SerializeException();
            }
            uint read_value = m_reader.ReadBits(bits);
            m_bitsRead += bits;
            return read_value;
        }

        public ulong ReadULong(int bits = 64) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 64);

            if (m_reader.WouldOverflow(bits)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                throw new SerializeException();
            }

            if (bits <= 32) {
                uint loword = m_reader.ReadBits(bits);
                return (ulong)loword;
            } else {
                uint loword = m_reader.ReadBits(32);
                uint hiword = m_reader.ReadBits(bits - 32);
                return ((ulong)loword) | (((ulong)hiword) << 32);
            }
        }

        //public bool ReadBytes(byte[] data, int bytes) {
        //    if (!SerializeAlign())
        //        return false;
        //    if (m_reader.WouldOverflow(bytes * 8)) {
        //        m_error = Constants.STREAM_ERROR_OVERFLOW;
        //        return false;
        //    }
        //    m_reader.ReadBytes(data, bytes);
        //    m_bitsRead += bytes * 8;
        //    return true;
        //}

        public string ReadString() {
            if (!SerializeAlign()) {
                throw new SerializeException();
            }

            int stringLength = ReadInt(0, GafferNet.Constants.MaxStringLength);

            if (m_reader.WouldOverflow((int)(stringLength * 16))) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                throw new SerializeException();
            }

            char[] stringData = new char[GafferNet.Constants.MaxStringLength];

            for (int i = 0; i < stringLength; ++i) {
                stringData[i] = (char)m_reader.ReadBits(16);
            }

            return new string(stringData, 0, stringLength);
        }

        public float ReadFloat() {
            if (m_reader.WouldOverflow(32)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                throw new SerializeException();
            }

            for (int i = 0; i < 4; ++i)
                m_floatBytes[i] = (byte)m_reader.ReadBits(8);

            return BitConverter.ToSingle(m_floatBytes, 0);
        }

        public bool ReadBool() {
            if (m_reader.WouldOverflow(1)) {
                m_error = Constants.STREAM_ERROR_OVERFLOW;
                throw new SerializeException();
            }
            uint read_value = m_reader.ReadBits(1);
            m_bitsRead += 1;
            return read_value == 1U;
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

        //public void Finish() {
        //    m_reader.Finish();
        //}

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
}