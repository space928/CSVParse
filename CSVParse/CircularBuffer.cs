using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CSVParse;

/*
 *   A simple single-producer, multi-consumer thread-safe circular buffer.
 *   
 * The MIT License (MIT)
 * 
 * Copyright (c) 2024 Thomas Mathieson
 *  
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 */

public class CircularBuffer<T> : IProducerConsumerCollection<T>, IReadOnlyCollection<T>
{
    private readonly T[] items;
    private readonly uint capacityMask;
    /// <summary>
    /// Points to the next item to be dequeued.
    /// </summary>
    private volatile uint start;
    /// <summary>
    /// Points to the next slot where an item will be enqueued.
    /// </summary>
    private volatile uint end;
    /// <summary>
    /// Stores the number of items in the buffer. This is only incremented/decremented 
    /// after an item has finished being added/removed.
    /// </summary>
    private volatile int count;

    public CircularBuffer() : this(16)
    {
    }

    public CircularBuffer(int capacity)
    {
        // At the moment the capacity must be a power of two such that start/end pointer wrapping works correctly.
        uint cap = BitOperations.RoundUpToPowerOf2((uint)capacity);
        capacityMask = cap - 1;
        items = new T[cap];
    }

    public int Count => count;
    public int Capacity => items.Length;
    public bool IsReadOnly => false;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public delegate void ReplaceFunc(ref T destination, uint index);

    /// <summary>
    /// Attempts to add an item to this queue, replacing the previosuly discarded value in the backing array.
    /// </summary>
    /// <param name="replace">The function to call to update the item in the array</param>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool TryAddReplace(ReplaceFunc replace)
    {
        uint oldEnd;
        uint newEnd;
        // Keep trying to add an item at the end pointer until it succeeds.
        do
        {
            oldEnd = end;
            // If the buffer is full fail
            if (count > 0 && ((oldEnd ^ start) & capacityMask) == 0)
                return false;

            uint ind = oldEnd & capacityMask;
            replace(ref items[ind], ind); // Only valid if this function is not reentrant
            newEnd = unchecked(oldEnd + 1);
        } while (Interlocked.CompareExchange(ref end, newEnd, oldEnd) != oldEnd);

        Interlocked.Increment(ref count);

        return true;
    }

    public bool TryAdd(T item)
    {
        uint oldEnd;
        uint newEnd;
        // Keep trying to add an item at the end pointer until it succeeds.
        do
        {
            oldEnd = end;
            // If the buffer is full fail
            if (count > 0 && ((oldEnd ^ start) & capacityMask) == 0)
                return false;

            items[oldEnd & capacityMask] = item; // Only valid if this function is not reentrant
            newEnd = unchecked(oldEnd + 1);
        } while (Interlocked.CompareExchange(ref end, newEnd, oldEnd) != oldEnd);

        Interlocked.Increment(ref count);
        //if (count > capacityMask + 1)
        //    Debugger.Break();
        return true;
    }

    /// <summary>
    /// Adds an item to the queue, blocking with a <see cref="SpinWait"/> until the 
    /// queue has the capacity to add the item.
    /// </summary>
    /// <param name="item">The item to add to the queue.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        SpinWait spinner = default;
        while (!TryAdd(item))
            spinner.SpinOnce();
            //Thread.Yield();
    }

    /// <inheritdoc cref="Add(T)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(T item)
    {
        Add(item);
    }

    /// <inheritdoc cref="TryTake(out T)"/>
    public bool TryDequeue(out T item)
    {
        uint oldStart;
        uint newStart;
        // Keep reading the last item in the queue until we successfully update the pointer.
        do
        {
            oldStart = start;
            // If the buffer is empty fail
            if (count <= 0 || end == oldStart)
            {
                item = default!;
                return false;
            }
            item = items[oldStart & capacityMask];
            newStart = unchecked(oldStart + 1);
        } while (Interlocked.CompareExchange(ref start, newStart, oldStart) != oldStart);

        Interlocked.Decrement(ref count);

        return true;
    }

    /// <summary>
    /// Removes the next item in the queue and returns it. 
    /// This method blocks with a <see cref="SpinWait"/> until 
    /// the queue has the capacity to add the item.
    /// </summary>
    /// <returns>The dequeued item.</returns>
    public T Dequeue()
    {
        SpinWait spinner = default;
        T ret;
        while (!TryDequeue(out ret))
            spinner.SpinOnce();
        //if (!TryDequeue(out var ret))
        //    throw new InvalidOperationException("Queue is empty!");

        return ret;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryTake([MaybeNullWhen(false)] out T item)
    {
        return TryDequeue(out item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        while (TryTake(out var item) && arrayIndex < array.Length)
        {
            array[arrayIndex++] = item;
        }
    }

    public void CopyTo(Array array, int index)
    {
        while (TryTake(out var item) && index < array.Length)
        {
            array.SetValue(item, index++);
        }
    }

    public T[] ToArray()
    {
        var dst = new T[count];
        CopyTo(dst, 0);
        return dst;
    }

    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    public IEnumerator<T> GetEnumerator() => new Enumerator(this);

    private struct Enumerator : IEnumerator<T>
    {
        private readonly CircularBuffer<T> queue;
        private T current;

        public Enumerator(CircularBuffer<T> queue)
        {
            this.queue = queue;
            this.current = default!;
        }

        public readonly T Current => current;

        readonly object IEnumerator.Current => Current!;

        public bool MoveNext()
        {
            return queue.TryTake(out current!);
        }

        public void Reset()
        {
            throw new InvalidOperationException();
        }

        public readonly void Dispose() { }
    }
}
