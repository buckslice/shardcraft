#define _DEBUG

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Collections.Concurrent;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class Serialization {

    public static string saveFolderName = "saves";

    // public methods add to these dictionaries
    static Dictionary<Vector3i, Queue<Chunk>> chunksToLoad = new Dictionary<Vector3i, Queue<Chunk>>();
    static Dictionary<Vector3i, Queue<Chunk>> chunksToSave = new Dictionary<Vector3i, Queue<Chunk>>();
    // these ones are used internally while thread works
    static Dictionary<Vector3i, Queue<Chunk>> _chunksToLoad = new Dictionary<Vector3i, Queue<Chunk>>();
    static Dictionary<Vector3i, Queue<Chunk>> _chunksToSave = new Dictionary<Vector3i, Queue<Chunk>>();

    static Queue<Chunk> chunksLoaded = new Queue<Chunk>();
    static Queue<Chunk> chunkLoadFailures = new Queue<Chunk>();
    static Queue<Chunk> chunksSaved = new Queue<Chunk>();


    // explaining table size: 3 bytes for sector offset, 2 bytes would hit up to 65K 
    // so in case where each chunk hits 2^3=65K / 4K chunks per region, so around 12 sectors each
    // which is probably rare but maybe possible, so 3 bumps up to 16 mill which is def enough room
    // then 1 byte for sector count, so up to 256 sectors each chunk can have
    const int TABLE_SIZE = 16384; // 4 bytes per chunk * 16*16*16 chunks per region
    const int SECTOR_SIZE = 4096; // in bytes
    static uint[] writeBuffer = new uint[32768]; // buffer for writing to writeStream
    static byte[] byteBuffer = new byte[65536]; // buffer for copying data from writeStream
    static byte[] uintBytes = new byte[4]; // buffer for copying data from writeStream
    static byte[] padBuffer = new byte[SECTOR_SIZE]; // buffer to help write padding at end of file
    static byte[] table = new byte[TABLE_SIZE]; // todo: cache these tables for each chunk so u dont need to read them everytime

    static GafferNet.WriteStream writer = new GafferNet.WriteStream();
    static GafferNet.ReadStream reader = new GafferNet.ReadStream();

    public static Thread thread;
    static EventWaitHandle newWork = new EventWaitHandle(false, EventResetMode.AutoReset);
    static readonly object killLock = new object();
    static bool kill = false;

    static System.Diagnostics.Stopwatch killWatch = new System.Diagnostics.Stopwatch();
    static System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();


    public static void LoadChunk(Chunk chunk) {
        Vector3i rc = GetRegionCoord(chunk.cp);
        lock (chunksToLoad) {
            if (chunksToLoad.TryGetValue(rc, out Queue<Chunk> q)) {
                q.Enqueue(chunk);
            } else {
                q = new Queue<Chunk>();
                q.Enqueue(chunk);
                chunksToLoad[rc] = q;
            }
        }
        newWork.Set();
    }

    public static void SaveChunk(Chunk chunk, bool manualSet = false) {
        if (!chunk.needToUpdateSave) {
            lock (chunksSaved) {
                chunksSaved.Enqueue(chunk);
            }
            return;
        }
        Vector3i rc = GetRegionCoord(chunk.cp);
        lock (chunksToSave) {
            if (chunksToSave.TryGetValue(rc, out Queue<Chunk> q)) {
                q.Enqueue(chunk);
            } else {
                q = new Queue<Chunk>();
                q.Enqueue(chunk);
                chunksToSave[rc] = q;
            }
        }
        if (!manualSet) {
            newWork.Set();
        }
    }

    public static void SetNewWork() {
        newWork.Set();
    }

    // sets new chunk variables and collects load failed chunks to be generated
    public static int CheckNewLoaded(Queue<Chunk> loadFails) {
        int loaded = 0;
        lock (chunksLoaded) {
            loaded = chunksLoaded.Count;
            while (chunksLoaded.Count > 0) {
                Chunk c = chunksLoaded.Dequeue();
                c.loaded = true;
                c.update = true;
            }
        }

        lock (chunkLoadFailures) {
            while (chunkLoadFailures.Count > 0) {
                loadFails.Enqueue(chunkLoadFailures.Dequeue());
            }
        }

        return loaded;
    }

    public static void FreeSavedChunks(Pool<Chunk> pool) {
        lock (chunksSaved) {
            while (chunksSaved.Count > 0) {
                pool.Return(chunksSaved.Dequeue());
            }
        }
    }

    public static void KillThread() {
        lock (killLock) {
            kill = true;
            killWatch.Start();
        }
        newWork.Set();
        Debug.Log("IO thread shutting down...");
    }

    public static void StartThread() {
        thread = new Thread(SerializationThread);
        thread.Start();
    }


    static void SerializationThread() {

        while (true) {

            bool lastRun = false;
            lock (killLock) {
                if (kill) {
                    lastRun = true;
                }
            }

            if (!lastRun) { // if last run just blast through to double check
#if _DEBUG
                Debug.Log("waiting for data");
#endif
                // wait for new work signal on main thread
                newWork.WaitOne();
#if _DEBUG
                Debug.Log("ok going");
#endif
            } else {
#if _DEBUG
                Debug.Log("last check");
#endif

            }

            // swap references with internal lists
            Dictionary<Vector3i, Queue<Chunk>> tmp;
            lock (chunksToLoad) {
                tmp = chunksToLoad;
                chunksToLoad = _chunksToLoad;
                _chunksToLoad = tmp;
            }
            lock (chunksToSave) {
                tmp = chunksToSave;
                chunksToSave = _chunksToSave;
                _chunksToSave = tmp;
            }

            // loop over each pair of region and list of chunks in that region
            foreach (var entry in _chunksToLoad) {
                LoadChunksFromRegion(entry.Key, entry.Value);
            }

            foreach (var entry in _chunksToSave) {
                SaveChunksFromRegion(entry.Key, entry.Value);
            }

            if (lastRun) {
                // verify that lists are all empty before quiting
                lock (chunksToSave) {
                    foreach (var entry in chunksToSave) {
                        Debug.Assert(entry.Value.Count == 0);
                    }
                }
                lock (chunksToLoad) {
                    foreach (var entry in chunksToLoad) {
                        Debug.Assert(entry.Value.Count == 0);
                    }
                }

                Debug.Log("IO thread shut down " + killWatch.ElapsedMilliseconds + " ms post kill");
                return;
            }

        }
    }

    static void LoadChunksFromRegion(Vector3i regionCoord, Queue<Chunk> chunks) {
        if (chunks.Count <= 0) {
            return;
        }

        string regionFile = RegionFileName(regionCoord);
        if (!File.Exists(regionFile)) { // if no region file then all these chunks need to be generated fresh
            lock (chunkLoadFailures) {
                while (chunks.Count > 0) {
                    chunkLoadFailures.Enqueue(chunks.Dequeue());
                }
            }
            return;
        }

#if _DEBUG
        Debug.Log("loading " + chunks.Count + " chunks from region " + regionCoord);
        watch.Restart();
#endif

        FileStream stream = File.Open(regionFile, FileMode.Open); // will open or create if not there
        int c = stream.Read(table, 0, TABLE_SIZE);
        Debug.Assert(c == TABLE_SIZE);
        while (chunks.Count > 0) {
            Chunk chunk = chunks.Dequeue();

            uint sectorOffset;
            uint sectorCount;
            GetTableEntry(chunk.cp, out sectorOffset, out sectorCount);

            if (sectorOffset == 0 && sectorCount == 0) { // no entry in table
                lock (chunkLoadFailures) {
                    chunkLoadFailures.Enqueue(chunk);
                }
                continue;
            }

            // seek to start of sector
            stream.Seek(TABLE_SIZE + sectorOffset * SECTOR_SIZE, SeekOrigin.Begin);

            // read count to get length of chunk data
            stream.Read(uintBytes, 0, 4);
            reader.Start(uintBytes, 0, 4);
            int bytes = (int)reader.ReadUInt();

            // read from stream into byte buffer
            stream.Read(byteBuffer, 0, bytes);

            DecodeBlocks(byteBuffer, bytes, chunk.blocks);

            lock (chunksLoaded) {
                chunksLoaded.Enqueue(chunk);
            }

        }

#if _DEBUG
        Debug.Log("loaded in " + watch.ElapsedMilliseconds + " ms");
#endif
    }

    static void SaveChunksFromRegion(Vector3i regionCoord, Queue<Chunk> chunks) {
        if (chunks.Count <= 0) {
            return;
        }
#if _DEBUG
        Debug.Log("saving " + chunks.Count + " chunks from region " + regionCoord);
        watch.Restart();
#endif

        // check if region file exists
        string regionFile = RegionFileName(regionCoord);

        bool alreadyExisted = File.Exists(regionFile);
        FileStream stream = File.Open(regionFile, FileMode.OpenOrCreate); // will open or create if not there
        if (alreadyExisted) { // load lookup table if file was there already
            int c = stream.Read(table, 0, TABLE_SIZE);
            Debug.Assert(c == TABLE_SIZE);
        } else {
            Array.Clear(table, 0, 0);   // make sure its 0s
            stream.Write(table, 0, TABLE_SIZE); // write empty table to file
        }

        while (chunks.Count > 0) {
            Chunk chunk = chunks.Dequeue();

            // get encoded chunk bytes
            int count = EncodeBlocks(byteBuffer, chunk.blocks);
            if (count > SECTOR_SIZE - 4) {
                Debug.Log(count);
                Debug.Log(chunk.cp);
            }

            Debug.Assert(count <= SECTOR_SIZE - 4); // will deal with multiple sectors after getting basics working

            writer.Start(writeBuffer);
            writer.Write((uint)count);
            writer.Finish();
            writer.GetData(uintBytes);

            // get position in lookup table (region chunk size is hardcoded as 16)
            uint sectorOffset;
            uint sectorCount;
            GetTableEntry(chunk.cp, out sectorOffset, out sectorCount);

            int requiredSectors = 1; //need to expand eventually but not sure if should contract if less sectors needed
            // detect if we need to update the table entry
            if ((sectorOffset == 0 && sectorCount == 0) || sectorCount != requiredSectors) {
                sectorOffset = (uint)((stream.Length - TABLE_SIZE) / SECTOR_SIZE);
                sectorCount = (uint)requiredSectors;

                SetTableEntry(chunk.cp, sectorOffset, sectorCount); // update table entry
            }

            // now write
            stream.Seek(TABLE_SIZE + sectorOffset * SECTOR_SIZE, SeekOrigin.Begin);

            long streamPos = stream.Position;

            // write data length followed by chunk data and then padding
            stream.Write(uintBytes, 0, 4);
            stream.Write(byteBuffer, 0, count);
            stream.Write(padBuffer, 0, SECTOR_SIZE - count - 4); // may be faster to add padding to end of bytes instead..?

            Debug.Assert(stream.Position - streamPos == SECTOR_SIZE);

            lock (chunksSaved) {
                chunksSaved.Enqueue(chunk);
            }
        }

        stream.Close();

#if _DEBUG
        Debug.Log("saved in " + watch.ElapsedMilliseconds + " ms");
#endif
    }

    // returns lookup byte position to sector table
    public static int GetLookUpPos(Vector3i cp) {
        return 4 * (Mod16(cp.x) + Mod16(cp.z) * 16 + Mod16(cp.y) * 256);
    }

    public static int Mod16(int c) {  // true mod implementation
        int r = c % 16;
        return r < 0 ? r + 16 : r;
    }

    static void SetTableEntry(Vector3i cp, uint sectorOffset, uint sectorCount) {
        int lookupPos = GetLookUpPos(cp);

        writer.Start(writeBuffer);
        writer.Write(sectorOffset, 24);
        writer.Write(sectorCount, 8);
        writer.Finish();
        writer.GetData(uintBytes);

        Buffer.BlockCopy(uintBytes, 0, table, lookupPos, 4);

    }

    static void GetTableEntry(Vector3i cp, out uint sectorOffset, out uint sectorCount) {
        int lookupPos = GetLookUpPos(cp);

        reader.Start(table, lookupPos, 4);
        sectorOffset = reader.ReadUInt(24);
        sectorCount = reader.ReadUInt(8);
    }

    // todo: try not making new array and just write from position and length of write buffer
    static int EncodeBlocks(byte[] buffer, NativeArray<Block> blocks) {
        // run length encoding, a byte for type followed by byte for runcount
        // rewrote project to access data in xzy order, because i assume there will be more horizontal than vertical structures in the world gen (and thus more runs)
        // WWWWWBWWWBWWWW  - example
        // W5B1W3B1W4   - type followed by run count (up to length 256)
        // WW5BWW3BWW4  - alternate way where double type indicates a run
        // runs cost one byte extra but singles cost one byte less? not sure if worth

        // todo: investigate whether using ushort is better for runs and types eventually maybe

        writer.Start(writeBuffer);

        // circumvent the safety check here because it gets mad for some reason when trying to save
        // even though i am quite sure it is safe. Saving is only called when a chunk is being destroyed
        // and only if it was either already generated new or loaded and then modified
        // in both cases the generation job is finished editing the blocks
        // i think it doesnt trust the fact that its in a separate thread
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var handle = AtomicSafetyHandle.Create();
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref blocks, handle);
#endif

        int i = 0;
        while (i < blocks.Length) {
            byte type = blocks[i].type;
            byte run = 1;
            while (++i < blocks.Length && blocks[i].type == type && run < byte.MaxValue) { run++; }
            writer.Write(type);
            writer.Write(run);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(handle);
#endif

        writer.Finish();
        return writer.GetData(buffer);
    }

    // decode given byte array into nativearray for chunk
    static void DecodeBlocks(byte[] buffer, int bytes, NativeArray<Block> blocks) {
        reader.Start(buffer, 0, bytes);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var handle = AtomicSafetyHandle.Create();
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref blocks, handle);
#endif

        int i = 0;
        while (i < blocks.Length) {
            byte type = reader.ReadByte();
            byte run = reader.ReadByte();
            while (run-- > 0) {
                blocks[i++] = new Block { type = type };
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(handle);
#endif
    }

    //static void _SaveChunk(Chunk chunk) {

    //    var bytes = EncodeBlocks(chunk.blocks);

    //    File.WriteAllBytes(SaveFileName(chunk), bytes);

    //    lock (chunksSaved) {
    //        chunksSaved.Enqueue(chunk);
    //    }
    //}

    //static void _LoadChunk(Chunk chunk) {
    //    string saveFile = SaveFileName(chunk);
    //    Debug.Assert(File.Exists(saveFile));

    //    byte[] bytes = File.ReadAllBytes(saveFile);
    //    DecodeBlocks(bytes, chunk.blocks);

    //    lock (chunksLoaded) {
    //        chunksLoaded.Enqueue(chunk);
    //    }
    //}

    // regions are 16x16x16 chunks
    public static Vector3i GetRegionCoord(Vector3i cp) {

        //return chunkPos / 16; // doesnt deal with negatives as smoothly as bit shifting
        return new Vector3i(cp.x >> 4, cp.y >> 4, cp.z >> 4);

    }


    static string SaveLocation(string worldName) {
        string saveLocation = saveFolderName + "/" + worldName + "/";

        if (!Directory.Exists(saveLocation)) {
            Directory.CreateDirectory(saveLocation);
        }

        return saveLocation;
    }

    //public static string SaveFileName(Chunk chunk) {
    //    return SaveLocation(World.worldName) + string.Format("{0},{1},{2}.scs", chunk.cp.x, chunk.cp.y, chunk.cp.z);
    //}

    // get region name given region coord
    static string RegionFileName(Vector3i rc) {
        return SaveLocation(World.worldName) + string.Format("{0},{1},{2}.screg", rc.x, rc.y, rc.z);
    }



    public static void SavePlayer() {
        string saveFile = saveFolderName + "/" + "player.bin";

        CamModify player = Camera.main.transform.GetComponent<CamModify>();

        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream(saveFile, FileMode.Create, FileAccess.Write, FileShare.None);
        formatter.Serialize(stream, player.transform.position.x);
        formatter.Serialize(stream, player.transform.position.y);
        formatter.Serialize(stream, player.transform.position.z);
        formatter.Serialize(stream, player.yaw);
        formatter.Serialize(stream, player.pitch);

        stream.Close();
    }

    public static void LoadPlayer() {
        string saveFile = saveFolderName + "/" + "player.bin";

        if (!File.Exists(saveFile)) {
            return;
        }

        CamModify player = Camera.main.transform.GetComponent<CamModify>();

        IFormatter formatter = new BinaryFormatter();
        FileStream stream = new FileStream(saveFile, FileMode.Open);

        player.transform.position = new Vector3((float)formatter.Deserialize(stream), (float)formatter.Deserialize(stream), (float)formatter.Deserialize(stream));
        player.yaw = (float)formatter.Deserialize(stream);
        player.pitch = (float)formatter.Deserialize(stream);

        stream.Close();
    }

}