using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace ForPatching
{
    public static partial class UnsafeUtility
    {
        // Copies sizeof(T) bytes from ptr to output
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe public static void CopyPtrToStructure<T>(void* ptr, out T output) where T : struct
        {
            // @patched at compile time
            throw new NotImplementedException("Patching this method failed");
        }

        // Copies sizeof(T) bytes from output to ptr
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe public static void CopyStructureToPtr<T>(ref T input, void* ptr) where T : struct
        {
            // @patched at compile time
            throw new NotImplementedException("Patching this method failed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe public static T ReadArrayElement<T>(void* source, int index)
        {
            // @patched at compile time
            throw new NotImplementedException("Patching this method failed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe public static T ReadArrayElementWithStride<T>(void* source, int index, int stride)
        {
            // @patched at compile time
            throw new NotImplementedException("Patching this method failed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe public static void WriteArrayElement<T>(void* destination, int index, T value)
        {
            // @patched at compile time
            throw new NotImplementedException("Patching this method failed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe public static void WriteArrayElementWithStride<T>(void* destination, int index, int stride, T value)
        {
            // @patched at compile time
            throw new NotImplementedException("Patching this method failed");
        }

        // The address of the memory where the struct resides in memory
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe public static void* AddressOf<T>(ref T output) where T : struct
        {
            // @patched at compile time
            throw new NotImplementedException("Patching this method failed");
        }

        // The size of a struct
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>() where T : struct
        {
            // @patched at compile time
            throw new NotImplementedException("Patching this method failed");
        }

        // minimum alignment of a struct
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignOf<T>() where T : struct
        {
            throw new NotImplementedException("Patching this method failed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T As<U, T>(ref U from)
        {
            throw new NotImplementedException("Patching this method failed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe public static ref T AsRef<T>(void* p) where T : struct
        {
            throw new NotImplementedException("Patching this method failed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe public static ref T ArrayElementAsRef<T>(void* ptr, int index) where T : struct
        {
            throw new NotImplementedException("Patching this method failed");
        }
    }
}
