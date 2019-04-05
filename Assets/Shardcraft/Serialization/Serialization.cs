#if UNITY_EDITOR
//#define _DEBUG
#endif

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
    static uint[] uintBuffer = new uint[32768]; // uint buffer for writing and reading
    static byte[] byteBuffer = new byte[131072]; // equal sized buffer for copying data from writeStream
    static byte[] fourBytes = new byte[4]; // small buffer for single operations
    static uint[] oneUint = new uint[1];   // small buffer for single operations
    static byte[] sectorBuffer = new byte[SECTOR_SIZE]; // buffer to help with shifting region operations
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
        Vector3i rc = WorldUtils.GetRegionCoord(chunk.cp);
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

    // sets new chunk variables and collects load failed chunks to be generated
    public static int CheckNewLoaded(Queue<Chunk> loadFails) {
        int loaded = 0;
        lock (chunksLoaded) {
            loaded = chunksLoaded.Count;
            while (chunksLoaded.Count > 0) {
                Chunk chunk = chunksLoaded.Dequeue();
                chunk.SetLoaded();
                chunk.update = true;
            }
        }

        lock (chunkLoadFailures) {
            while (chunkLoadFailures.Count > 0) {
                loadFails.Enqueue(chunkLoadFailures.Dequeue());
            }
        }

        return loaded;
    }

    public static void SaveChunk(Chunk chunk, bool autoSet = true) {
        if (!chunk.needToUpdateSave) {
            lock (chunksSaved) {
                chunksSaved.Enqueue(chunk);
            }
            return;
        }
        Vector3i rc = WorldUtils.GetRegionCoord(chunk.cp);
        lock (chunksToSave) {
            if (chunksToSave.TryGetValue(rc, out Queue<Chunk> q)) {
                q.Enqueue(chunk);
            } else {
                q = new Queue<Chunk>();
                q.Enqueue(chunk);
                chunksToSave[rc] = q;
            }
        }
        if (autoSet) {
            newWork.Set();
        }
    }

    public static void FreeSavedChunks(Pool<Chunk> pool) {
        lock (chunksSaved) {
            while (chunksSaved.Count > 0) {
                pool.Return(chunksSaved.Dequeue());
            }
        }
    }

    public static void SetNewWork() {
        newWork.Set();
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
        FileStream stream = null;
        try {
            stream = File.Open(regionFile, FileMode.Open); // will open or create if not there
            int c = stream.Read(table, 0, TABLE_SIZE);
            Debug.Assert(c == TABLE_SIZE);
            while (chunks.Count > 0) {
                Chunk chunk = chunks.Dequeue();

                int sectorOffset;
                byte sectorCount;
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
                stream.Read(fourBytes, 0, 4);
                reader.Start(fourBytes, 0, oneUint, 4);
                int bytes = (int)reader.ReadUInt();

                // read from stream into byte buffer
                stream.Read(byteBuffer, 0, bytes);

                DecodeChunk(byteBuffer, bytes, chunk);

                lock (chunksLoaded) {
                    chunksLoaded.Enqueue(chunk);
                }

            }
        } catch (Exception e) {
            Debug.Log(e.Message);
        } finally {
            if (stream != null) {
                stream.Close();
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
        FileStream stream = null;
        try {
            stream = File.Open(regionFile, FileMode.OpenOrCreate); // will open or create if not there

            if (alreadyExisted) { // load lookup table if file was there already
                int c = stream.Read(table, 0, TABLE_SIZE);
                Debug.Assert(c == TABLE_SIZE);
            } else {
                Array.Clear(table, 0, TABLE_SIZE);   // make sure its 0s
                stream.Write(table, 0, TABLE_SIZE);  // write table at beginning of new file (will get updated at end as well)
            }

            while (chunks.Count > 0) {
                Chunk chunk = chunks.Dequeue();

                // get encoded chunk bytes
                int bytes = EncodeChunk(byteBuffer, chunk);

                int requiredSectors = (bytes + 4) / SECTOR_SIZE + 1; // +4 for length uint
                Debug.Assert(requiredSectors < 256); // max for 1 byte sector count

                // get position in lookup table
                int sectorOffset; // starting point of this chunk in terms of number of sectors from front
                byte sectorCount; // how many sectors this chunk takes up
                GetTableEntry(chunk.cp, out sectorOffset, out sectorCount);

                // if no entry in table, add one to the end
                if (sectorOffset == 0 && sectorCount == 0) {
                    sectorOffset = (int)((stream.Length - TABLE_SIZE) / SECTOR_SIZE);
                    sectorCount = (byte)requiredSectors;

                    SetTableEntry(chunk.cp, sectorOffset, sectorCount); // append new table entry
                } else if (requiredSectors != sectorCount) {
                    Debug.Assert(requiredSectors > 0 && sectorCount > 0);

                    SetTableEntry(chunk.cp, sectorOffset, (byte)requiredSectors); // update table entry

                    int endOfSectorPos = TABLE_SIZE + (sectorOffset + sectorCount) * SECTOR_SIZE;
                    ShiftRegionData(stream, requiredSectors - sectorCount, endOfSectorPos);
                }

                writer.Start(uintBuffer);
                writer.Write((uint)bytes);
                writer.Finish();
                writer.GetData(fourBytes);

                // add padding to end of byteBuffer
                int pad = SECTOR_SIZE * requiredSectors - bytes - 4;
                Debug.Assert(pad >= 0 && pad <= SECTOR_SIZE);
                Array.Clear(byteBuffer, bytes, pad);

                // seek to correct spot in file
                stream.Seek(TABLE_SIZE + sectorOffset * SECTOR_SIZE, SeekOrigin.Begin);

                long streamPos = stream.Position;
                // write data length followed by padded chunk data
                stream.Write(fourBytes, 0, 4);
                stream.Write(byteBuffer, 0, bytes + pad);
                Debug.Assert(stream.Position - streamPos == SECTOR_SIZE * requiredSectors);

                lock (chunksSaved) {
                    chunksSaved.Enqueue(chunk);
                }
            }

            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(table, 0, TABLE_SIZE); // write updated table back and close stream

            Debug.Assert((stream.Length - TABLE_SIZE) % SECTOR_SIZE == 0);

        } catch (Exception e) {
            Debug.Log(e.Message);
        } finally {
            if (stream != null) {
                stream.Close();
            }
        }

#if _DEBUG
        Debug.Log("saved in " + watch.ElapsedMilliseconds + " ms");
#endif
    }

    // moves all data after position back or forward a certain number of sectors
    // this is an expensive but rare operation when chunks need more storage space
    static void ShiftRegionData(FileStream stream, int sectorShift, int position) {
#if _DEBUG
        Debug.Log("shifting region " + (sectorShift > 0 ? "forward" : "backward"));
#endif
        Debug.Assert(sectorShift != 0);

        // go thru table and shift offsets occuring after position 
        for (int i = 0; i < TABLE_SIZE; i += 4) {
            reader.Start(table, i, oneUint, 4);
            int sectorOffset = (int)reader.ReadUInt(24);
            byte sectorCount = reader.ReadByte(8);

            if (TABLE_SIZE + sectorOffset * SECTOR_SIZE >= position) {
                sectorOffset += sectorShift;
                writer.Start(uintBuffer);
                writer.Write((uint)sectorOffset, 24);
                writer.Write(sectorCount, 8);
                writer.Finish();
                writer.GetData(table, i, 4); // write back to table
            }
        }

        // shift data after position in sector sized chunks
        long len = stream.Length;
        if (sectorShift > 0) { // expanding, moving chunks forward, so start from back
            for (long i = len - SECTOR_SIZE; i >= position; i -= SECTOR_SIZE) {
                stream.Seek(i, SeekOrigin.Begin);
                int read = stream.Read(sectorBuffer, 0, SECTOR_SIZE);
                Debug.Assert(read == SECTOR_SIZE);
                stream.Seek(i + sectorShift * SECTOR_SIZE, SeekOrigin.Begin);
                stream.Write(sectorBuffer, 0, SECTOR_SIZE);
            }
        } else { // shrinking, moving chunks backward, so start from front
            for (long i = position; i < len; i += SECTOR_SIZE) {
                stream.Seek(i, SeekOrigin.Begin);
                int read = stream.Read(sectorBuffer, 0, SECTOR_SIZE);
                Debug.Assert(read == SECTOR_SIZE);
                stream.Seek(i + sectorShift * SECTOR_SIZE, SeekOrigin.Begin);
                stream.Write(sectorBuffer, 0, SECTOR_SIZE);
            }
            // when shrinking need to manually shrink file afterwards, expanding will do this automatically
            stream.SetLength(len + SECTOR_SIZE * sectorShift);
        }

    }

    // returns lookup byte position to sector table
    public static int GetTablePos(Vector3i cp) {
        return 4 * (Mth.Mod16(cp.x) + Mth.Mod16(cp.z) * 16 + Mth.Mod16(cp.y) * 256);
    }

    static void SetTableEntry(Vector3i cp, int sectorOffset, byte sectorCount) {
        int lookupPos = GetTablePos(cp);

        writer.Start(uintBuffer);
        writer.Write((uint)sectorOffset, 24);
        writer.Write(sectorCount, 8);
        writer.Finish();
        writer.GetData(table, lookupPos, 4);

    }

    static void GetTableEntry(Vector3i cp, out int sectorOffset, out byte sectorCount) {
        int lookupPos = GetTablePos(cp);

        reader.Start(table, lookupPos, oneUint, 4);
        sectorOffset = (int)reader.ReadUInt(24);
        sectorCount = reader.ReadByte(8);
    }

    // todo: try not making new array and just write from position and length of write buffer
    static int EncodeChunk(byte[] buffer, Chunk chunk) {
        var blocks = chunk.blocks;

        // run length encoding, a byte for type followed by byte for runcount
        // rewrote project to access data in xzy order, because i assume there will be more horizontal than vertical structures in the world gen (and thus more runs)
        // WWWWWBWWWBWWWW  - example
        // W5B1W3B1W4   - type followed by run count (up to length 256)
        // WW5BWW3BWW4  - alternate way where double type indicates a run
        // runs cost one byte extra but singles cost one byte less? not sure if worth

        // todo: investigate whether using ushort is better for runs and types eventually maybe

        writer.Start(uintBuffer);

        writer.Write(chunk.builtStructures);

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
    static void DecodeChunk(byte[] buffer, int bytes, Chunk chunk) {
        var blocks = chunk.blocks;
        var lights = chunk.lights; // just resetting this here

        reader.Start(buffer, 0, uintBuffer, bytes);

        chunk.builtStructures = reader.ReadBool();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var bh = AtomicSafetyHandle.Create();
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref blocks, bh);
        var lh = AtomicSafetyHandle.Create();
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref lights, bh);
#endif

        int i = 0;
        while (i < blocks.Length) {
            byte type = reader.ReadByte();
            byte run = reader.ReadByte();
            while (run-- > 0) {
                blocks[i++] = new Block { type = type };
            }
        }

        // clear light array
        for (i = 0; i < lights.Length; ++i) {
            lights[i] = new Light { torch = 0 };
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(bh);
        AtomicSafetyHandle.Release(lh);
#endif
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

        FlyCam player = Camera.main.transform.GetComponent<FlyCam>();
        if (player == null) {
            return;
        }

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

        FlyCam player = Camera.main.transform.GetComponent<FlyCam>();
        if (player == null) {
            return;
        }

        IFormatter formatter = new BinaryFormatter();
        FileStream stream = new FileStream(saveFile, FileMode.Open);

        player.transform.position = new Vector3((float)formatter.Deserialize(stream), (float)formatter.Deserialize(stream), (float)formatter.Deserialize(stream));
        player.yaw = (float)formatter.Deserialize(stream);
        player.pitch = (float)formatter.Deserialize(stream);

        stream.Close();
    }

}