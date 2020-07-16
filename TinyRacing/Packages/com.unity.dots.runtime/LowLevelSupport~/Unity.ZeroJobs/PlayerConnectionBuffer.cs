//-----------------------------------------------------------------------------------------------------------
// Player connection buffer - memory usage patterns
//-----------------------------------------------------------------------------------------------------------
// GENERAL:
// If large messages are sent or received, we don't typically expect a repeat frame-by-frame. This
// could include live-link data (receive), screenshots (send), or other one shot binary large objects.
// For this reason we always free memory associated with buffers larger than the reserve size.
//
// RECEIVE:
// - Wait for exactly message header data size
// - Wait for exactly message byte size data size (information provided by header)
// - Call any callbacks that will use this
// - Reset and do again
//
// If many messages are received, we don't typically expect a repeat frame-by-frame,
// so after we've processed we free the memory used to store it. 
// This is contrary to usage patterns in player connection buffer send use case.
//
// SEND:
// - Fill buffer chunk by chunk from arbitrary outside sources possibly on another thread
// - Swap buffer with a free buffer (allocating if necessary) between frames if not mid-message
// - Send the buffers that were ready
// - Flush what successfully sent and add to free list for next swap
//
// If many messages are sent, we typically expect a repeate frame-by-frame (such as profiler data),
// so cache newly allocated buffers for re-use. 
// This is contrary to usage patterns in player connection buffer receive use case.

#if ENABLE_PLAYERCONNECTION

using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

namespace Unity.Development.PlayerConnection
{
    // NOT THREAD SAFE - not designed for sharing
    // BURSTABLE
    //
    // This is a general purpose buffer API which allocates in chunks. Expanding avoids moving memory around by maintaining
    // a linked list of memory buffer chunks. Use-cases with specific semantics (namely receiving versus sending data in
    // the player connection based on usage patterns) is denoted with specific method names. Separating these methods into
    // individual classes/structs provides no perceivable organizational benefit and complicates reasoning about code
    // especially when ensuring these branch offs of functionality maintain burst and il2cpp compatiblilty.
    [StructLayout(LayoutKind.Sequential)]
    internal struct MessageStream
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MessageStreamBuffer
        {
            public IntPtr Buffer { get; private set; }
            public unsafe MessageStreamBuffer* Next { get; set; }
            public int Size { get; set; }
            public int Capacity { get; private set; }

            public void Alloc(int bytes)
            {
                Free();

                unsafe
                {
                    Buffer = (IntPtr)UnsafeUtility.Malloc(bytes, 0, Unity.Collections.Allocator.Persistent);
                    Next = null;
                }

                Capacity = bytes;
                Size = 0;
            }

            public void Free()
            {
                if (Buffer == IntPtr.Zero)
                    return;

                unsafe
                {
                    UnsafeUtility.Free((void*)Buffer, Unity.Collections.Allocator.Persistent);
                    Next = null;
                }

                Buffer = IntPtr.Zero;
                Capacity = 0;
                Size = 0;
            }
        }

        public unsafe MessageStreamBuffer* BufferWrite { get; set; }  // always tail node (may also be head)
        public unsafe MessageStreamBuffer* BufferRead { get; set; }  // always head node
        public unsafe MessageStream* next;  // @@todo remove w/native queue
        public int TotalBytes { get; set; }
        public readonly int reserve;

        public unsafe MessageStream(int reserveSize)
        {
            reserve = reserveSize;
            BufferRead = (MessageStreamBuffer*)UnsafeUtility.Malloc(sizeof(MessageStreamBuffer), 0, Collections.Allocator.Persistent);
            *BufferRead = new MessageStreamBuffer();
            BufferRead->Alloc(reserve);
            BufferWrite = BufferRead;
            TotalBytes = 0;
            next = null;
        }

        public unsafe void Free()
        {
            FreeRange(BufferRead, null);
            BufferRead = null;
        }

        public unsafe void Allocate(int bytes)
        {
            if (BufferWrite->Size + bytes > BufferWrite->Capacity)
            {
                BufferWrite->Next = (MessageStreamBuffer*)UnsafeUtility.Malloc(sizeof(MessageStreamBuffer), 0, Collections.Allocator.Persistent);
                *BufferWrite->Next = new MessageStreamBuffer();
                BufferWrite = BufferWrite->Next;
                BufferWrite->Alloc(bytes < reserve ? reserve : bytes);
            }
        }

        public unsafe void UpdateSize(int bytesUsed)
        {
            BufferWrite->Size += bytesUsed;
            TotalBytes += bytesUsed;
        }

        // Deallocates buffers in the list including beginNode and excluding endNode (null to free all)
        public unsafe void FreeRange(MessageStreamBuffer* beginNode, MessageStreamBuffer* endNode)
        {
            MessageStreamBuffer* node = beginNode;
            while (node != endNode)
            {
                var next = node->Next;
                TotalBytes -= node->Size;
                node->Free();
                node = next;
            }

            beginNode = null;
            if (endNode == null)
                BufferWrite = BufferRead;
        }

        // Deallocate buffers in the list until a desired node, and if there is a buffer which is partially finished
        // reduce it to the remaining data only.
        public unsafe void RecycleRange(MessageStreamBuffer* recycleUntil, int recycleUntilPlusOffset)
        {
            // @@todo - flushing the send buffer list should actually zero out the buffer nodes, but not free them since
            // typical send buffer usage patterns are repeat each frame (profiling for instance)
            if (recycleUntil != BufferRead)
            {
                // Free the buffer nodes after the head
                FreeRange(BufferRead->Next, recycleUntil);
                // then flag the head buffer empty (size = 0) so that it is always available
                // to try to avoid defragmentation
                TotalBytes -= BufferRead->Size;
                BufferRead->Size = 0;
                BufferRead->Next = recycleUntil;
            }

            if (recycleUntilPlusOffset > 0)
            {
                if (recycleUntil == null)
                    throw new ArgumentException("Flushing buffer past end");
                if (recycleUntilPlusOffset > recycleUntil->Size)
                    throw new ArgumentOutOfRangeException("Flushing buffer with offset past end");

                unsafe
                {
                    UnsafeUtility.MemCpy((void*)recycleUntil->Buffer, (void*)(recycleUntil->Buffer + recycleUntilPlusOffset), recycleUntil->Size - recycleUntilPlusOffset);
                }
                recycleUntil->Size -= recycleUntilPlusOffset;
                TotalBytes -= recycleUntilPlusOffset;

                if (recycleUntil->Size == 0)
                {
                    MessageStream.MessageStreamBuffer* newNext = recycleUntil->Next;
                    FreeRange(BufferRead->Next, newNext);
                    BufferRead->Next = newNext;
                }
            }
        }

        // Used for send memory usage patterns
        public void RecycleAll()
        {
            // @@todo - recycling the send buffer list should actually zero out the buffer nodes, but not free them since
            //          typical send buffer usage patterns are repeat each frame (profiling for instance)
            RecycleAndFreeExtra();
        }

        // Used for receiving memory usage patterns
        public unsafe void RecycleAndFreeExtra()
        {
            FreeRange(BufferRead->Next, null);
            BufferRead->Size = 0;
            BufferRead->Next = null;
            TotalBytes = 0;
        }

        // Used for receiving over PlayerConnection for public API compatibility
        public byte[] ToByteArray(int offsetBegin, int offsetEnd)
        {
            if (offsetEnd > TotalBytes || offsetEnd < offsetBegin)
                throw new ArgumentOutOfRangeException("Bad offsetEnd in Player Connection data size");

            int bytesLeft = offsetEnd - offsetBegin;
            byte[] data = new byte[bytesLeft];

            unsafe
            {
                fixed (byte* m = data)
                {
                    MessageStream.MessageStreamBuffer* bufferReadNode = BufferRead;
                    int readOffset = offsetBegin;
                    int writeOffset = 0;

                    while (bytesLeft > 0)
                    {
                        while (readOffset >= bufferReadNode->Size)
                        {
                            readOffset -= bufferReadNode->Size;
                            bufferReadNode = bufferReadNode->Next;
                        }

                        int copyBytes = bufferReadNode->Size - readOffset;
                        if (bytesLeft < copyBytes)
                            copyBytes = bytesLeft;

                        UnsafeUtility.MemCpy(m + writeOffset, (void*)(bufferReadNode->Buffer + readOffset), copyBytes);

                        readOffset += copyBytes;
                        writeOffset += copyBytes;
                        bytesLeft -= copyBytes;
                    }
                }
            }

            return data;
        }
    }

    // NOT THREAD SAFE
    //   - except lockless submission to main thread
    // BURSTABLE
    //
    // This is used in any context where we want to accumulate data to send over Player Connection to Unity Editor.
    // It builds on the functionality of "Buffer" and specializes the behaviour to specifically send player
    // connection formatted messages.
    // Examples:
    //   Logging (one per thread for multithreaded in-job logging)
    //   Profiler (one per worker thread, one main thread, one non-thread "session" buffer)
    //   Unit Testing (create them as we need them)
    //   Live Link
    //   etc.
    //
    // Multithreaded synchronization is only achieved by TrySubmitBuffer() and is also burstable in case
    // of delayed synchronization due to contention (i.e. trying to submit but a job exists beyond end-of-frame)
    [StructLayout(LayoutKind.Sequential)]
    internal struct MessageStreamBuilder
    {
        public unsafe MessageStream* bufferList;
        private unsafe MessageStream* bufferListSwap;
        private unsafe int* deferredSizePtr;
        private int deferredSizeStart;
        private bool resubmitBuffer;

        public unsafe bool HasDataToSend => bufferList->TotalBytes > 0;
        public unsafe int DataToSendBytes => bufferList->TotalBytes;
        public unsafe int DeferredSize => (deferredSizePtr == null) ? 0 : bufferList->TotalBytes - deferredSizeStart;
        public bool IsExternal { get; private set; }

        // Note - DON'T USE DIRECTLY
        // Please either construct directly:
        //   bufferSend = BufferSendManager.Create()
        // or if already allocated (such as in a StaticShared<>) construct indirectly with
        //   BufferSendManager.Init(&bufferSend)
        internal unsafe MessageStreamBuilder(MessageStream* buffer, bool isExternal)
        {
            bufferList = buffer;
            bufferListSwap = bufferList;
            deferredSizePtr = null;
            deferredSizeStart = 0;
            resubmitBuffer = false;
            IsExternal = isExternal;
        }

        public unsafe void WriteRaw(byte* data, int dataBytes)
        {
            if (dataBytes == 0)
                return;
            if (data == null)
                throw new ArgumentNullException("src is null in SendRaw");
            bufferList->Allocate(dataBytes);
            UnsafeUtility.MemCpy((void*)(bufferList->BufferWrite->Buffer + bufferList->BufferWrite->Size), data, dataBytes);
            bufferList->UpdateSize(dataBytes);
        }

        public unsafe void WriteData<T>(T data) where T : unmanaged
        {
            WriteRaw((byte*)&data, sizeof(T));
        }

        public unsafe void MessageBegin(UnityGuid messageId)
        {
            if (deferredSizePtr != null)
                throw new InvalidOperationException("Can't defer player connection message header - previous one not patched");

            // @@todo lockfree op - bufferListLocked should have been null, so an atomic set? here should be great
            bufferListSwap = null;

            WriteData(EditorMessageIds.kMagicNumber);
            WriteData(messageId);
            deferredSizePtr = (int*)(bufferList->BufferWrite->Buffer + bufferList->BufferWrite->Size);
            WriteData<int>(0);
            // In case the buffer was exactly full, the 32 bit size may have ended up in a new memory buffer
            // which didn't exist prior to reserving it
            if (bufferList->BufferWrite->Size == 4)
                deferredSizePtr = (int*)(bufferList->BufferWrite->Buffer);
            deferredSizeStart = bufferList->TotalBytes;
        }

        public unsafe void MessageEnd()
        {
            if (deferredSizePtr == null)
                throw new InvalidOperationException("Can't patch player connection message header - nothing to patch");

            while ((bufferList->TotalBytes & 3) != 0)
                WriteData((byte)0);

            *deferredSizePtr = bufferList->TotalBytes - deferredSizeStart;
            deferredSizeStart = 0;
            deferredSizePtr = null;

            // @@todo lockfree op - bufferList should have been null, so an atomic exchange here should be great
            bufferListSwap = bufferList;

            if (resubmitBuffer)
            {
                TrySubmitBuffer();
                // This should always succeed...
            }
        }

        public unsafe void WriteMessage(UnityGuid messageId, byte* d, int dataBytes)
        {
            MessageBegin(messageId);
            WriteRaw(d, dataBytes);
            MessageEnd();
        }

        public unsafe void TrySubmitBuffer()
        {
            if (bufferList->TotalBytes == 0)
                return;

            MessageStream* freeBuffer = MessageStreamManager.SubmitGetFreeBuffer();
            MessageStream* sendBuffer = bufferList;

            // @@todo NOO CAS loop part for compare not null and exchange
            //        instead compare if bufferList == bufferListLocked and if so set bufferlist
            bool swapped = false;
            if (bufferList == bufferListSwap)
            {
                bufferList = freeBuffer;
                swapped = true;
            }

            if (swapped)
            {
                MessageStreamManager.SubmitSendBuffer(sendBuffer);
                resubmitBuffer = false;
            }
            else
            {
                MessageStreamManager.SubmitReturnFreeBuffer(freeBuffer);
                resubmitBuffer = true;
            }
        }
    }

    // THREAD SAFE
    // BURSTABLE
    //
    // Owns all BufferSends. Externally created BufferSends (ex. SharedStatic) should be registered.
    // Handles multithreaded synchronization, holding buffers for sending over player connection on main thread,
    // and maintaining/allocating replacement buffers for uninterrupted data flow while the previous set are
    // waiting on the async tcp sends to full process the data.
    [StructLayout(LayoutKind.Sequential)]
    internal struct MessageStreamManager
    {
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct StreamCache
        {
            [StructLayout(LayoutKind.Sequential)]
            internal unsafe struct BufferQueue
            {
                public MessageStream* head;
                public MessageStream* tail;

                public void Push(MessageStream* buffer)
                {
                    if (Empty())
                    {
                        head = buffer;
                        tail = buffer;
                    }
                    else
                    {
                        tail->next = buffer;
                        tail = buffer;
                    }
                    tail->next = null;
                }

                public MessageStream* Pop()
                {
                    MessageStream* node = head;

                    if (!Empty())
                    {
                        head = head->next;
                        if (head == null)
                            tail = null;
                    }

                    return node;
                }

                public bool Empty()
                {
                    return head == null;
                }
            }

            // @@todo these queues should be mpmc queues (sendqueue might be able to be mpsc)
            // @@todo reorganize freequeue so Buffers have only one BufferNode in list and other
            //        buffernodes can be acquired from another queue ... this way we are
            //        1) Recycling BufferNode's (where the actual allocations exist)
            //        2) Only swapping higher level Buffer's in and out for lock free submission to playerconnection to send

            public int allSendsCount;

            public BufferQueue sendQueue;
            public BufferQueue freeQueue;

            public MessageStreamBuilder** allSends;
            public const int kAllSendsMax = 1024;
        }

        public static readonly SharedStatic<StreamCache> bufferQueue = 
            SharedStatic<StreamCache>.GetOrCreate<MessageStreamManager, StreamCache>();
        public unsafe static bool HasDataToSend => bufferQueue.Data.sendQueue.head != null;
        private const int kInitialCapacity = 8192;

        //---------------------------------------------------------------------------------------------------
        // Lifetime
        //---------------------------------------------------------------------------------------------------
        public unsafe static void Initialize()
        {
            bufferQueue.Data.allSends = (MessageStreamBuilder**)UnsafeUtility.Malloc(StreamCache.kAllSendsMax * IntPtr.Size, 0, Collections.Allocator.Persistent);
        }

        public unsafe static void Shutdown()
        {
            // allSends
            for (int i = 0; i < bufferQueue.Data.allSendsCount; i++)
            {
                bufferQueue.Data.allSends[i]->bufferList->Free();
                if (!bufferQueue.Data.allSends[i]->IsExternal)
                    UnsafeUtility.Free(bufferQueue.Data.allSends[i], Collections.Allocator.Persistent);
            }
            bufferQueue.Data.allSendsCount = 0;

            UnsafeUtility.Free(bufferQueue.Data.allSends, Collections.Allocator.Persistent);
            bufferQueue.Data.allSends = null;

            // sendQueue
            while (!bufferQueue.Data.sendQueue.Empty())
            {
                MessageStream* buffer = bufferQueue.Data.sendQueue.Pop();
                buffer->Free();
            }
            bufferQueue.Data.sendQueue = new StreamCache.BufferQueue();

            // freeQueue
            while (!bufferQueue.Data.freeQueue.Empty())
            {
                MessageStream* buffer = bufferQueue.Data.freeQueue.Pop();
                buffer->Free();
            }
            bufferQueue.Data.freeQueue = new StreamCache.BufferQueue();
        }

        public unsafe static MessageStreamBuilder* CreateBufferSend()
        {
            MessageStreamBuilder* buffer = (MessageStreamBuilder*)UnsafeUtility.Malloc(sizeof(MessageStreamBuilder), 0, Collections.Allocator.Persistent);
            RegisterBufferSend(buffer, false);
            return buffer;
        }

        public unsafe static void DestroyBufferSend(MessageStreamBuilder *buffer)
        {
            if (!buffer->IsExternal)
                UnsafeUtility.Free(buffer, Collections.Allocator.Persistent);

            bufferQueue.Data.allSendsCount--;

            for (int i = 0; i < bufferQueue.Data.allSendsCount; i++)
            {
                if (bufferQueue.Data.allSends[i] == buffer)
                {
                    bufferQueue.Data.allSends[i] = bufferQueue.Data.allSends[bufferQueue.Data.allSendsCount];
                    break;
                }
            }
        }

        public unsafe static void RegisterExternalBufferSend(MessageStreamBuilder* buffer)
        {
            RegisterBufferSend(buffer, true);
        }

        private unsafe static void RegisterBufferSend(MessageStreamBuilder* buffer, bool isExternal)
        {
            //@@todo atomic inc?
            int index = bufferQueue.Data.allSendsCount++;

            bufferQueue.Data.allSends[index] = buffer;
            *buffer = new MessageStreamBuilder(SubmitGetFreeBuffer(), isExternal);
        }


        //---------------------------------------------------------------------------------------------------
        // Send Queue
        //---------------------------------------------------------------------------------------------------
        public static void TrySubmitAll()
        {
            int count = bufferQueue.Data.allSendsCount;
            for (int i = 0; i < count; i++)
            {
                unsafe
                {
                    bufferQueue.Data.allSends[i]->TrySubmitBuffer();
                }
            }
        }

        public unsafe static MessageStream* SubmitGetFreeBuffer()
        {
            if (!bufferQueue.Data.freeQueue.Empty())
            {
                return bufferQueue.Data.freeQueue.Pop();
            }
            
            MessageStream* buffer = (MessageStream*)UnsafeUtility.Malloc(sizeof(MessageStream), 0, Collections.Allocator.Persistent);
            *buffer = new MessageStream(kInitialCapacity);
            return buffer;
        }

        public unsafe static void SubmitReturnFreeBuffer(MessageStream* buffer)
        {
            bufferQueue.Data.freeQueue.Push(buffer);
        }

        public unsafe static void SubmitSendBuffer(MessageStream* buffer)
        {
            bufferQueue.Data.sendQueue.Push(buffer);
        }

        public unsafe static void RecycleBuffer(MessageStream *buffer)
        {
            buffer->RecycleAll();
            bufferQueue.Data.freeQueue.Push(buffer);
        }

        public unsafe static void RecycleAll()
        {
            // @@todo also check allSends?

            // Instead of queuing for send, we just erase what's there
            while (!bufferQueue.Data.sendQueue.Empty())
            {
                MessageStream* buffer = bufferQueue.Data.sendQueue.Pop();
                RecycleBuffer(buffer);
            }
        }
    }
}

#endif
