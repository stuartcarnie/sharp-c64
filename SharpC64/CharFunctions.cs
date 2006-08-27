using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace SharpC64
{
    public static class CharFunctions
    {
        unsafe public static byte* strchr(BytePtr src, byte c)
        {
            for (int i = 0; i < src.Length; i++)
                if (src[i] == c)
                    return src.Pointer + i;

            return null;
        }

        unsafe public static byte* strchr(BytePtr src, Char c)
        {
            return strchr(src, (byte)c);
        }

        unsafe public static byte* strchr(byte* src, byte c)
        {
            for (; *src != 0x00; src++ )
                if (*src == c) return src;

            return null;
        }

        unsafe public static byte* strchr(byte* src, Char c)
        {
            return strchr(src, (byte)c);
        }

        public static void strncpy(BytePtr dest, BytePtr src, long start)
        {
            long i = start, j = 0;
            for (; i < dest.Length && src[i] != 0x00; i++, j++)
                dest[j] = src[i];

            dest[j] = 0x00;
        }

        public static void strncpy(BytePtr dest, string src)
        {
            int i = 0;
            for (; i < dest.Length && i < src.Length; i++)
                dest[i] = (byte)src[i];

            dest[i] = 0x00;
        }

        unsafe public static void strncpy(BytePtr dest, BytePtr src, byte* start)
        {
            strncpy(dest, src, start - src.Pointer);
        }

    }

    public class BytePtr : IDisposable
    {
        byte[] _array;
        GCHandle _array_handle;
        unsafe byte* _array_ptr;

        #region public members

        public BytePtr(byte[] array)
        {
            _array = array;
            _array_handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
            unsafe
            {
                _array_ptr = (byte*)_array_handle.AddrOfPinnedObject();
            }
        }

        public BytePtr(int size)
            : this(new byte[size])
        { }

        public override string ToString()
        {
            string str = Encoding.ASCII.GetString(_array);
            return str.Substring(0, str.IndexOf('\0'));
        }

        #endregion

        #region public properties

        unsafe public byte* Pointer
        {
            get { return _array_ptr; }
        }

        public byte this[int index]
        {
            get { unsafe { return _array_ptr[index]; } }
            set { unsafe { _array_ptr[index] = value; } }
        }

        public byte this[long index]
        {
            get { unsafe { return _array_ptr[index]; } }
            set { unsafe { _array_ptr[index] = value; } }
        }

        public int Length
        {
            get { return _array.Length; }
        }

        #endregion public properties

        #region casting operators

        public unsafe static implicit operator byte*(BytePtr c)
        {
            return c._array_ptr;
        }

        public static implicit operator byte[](BytePtr c)
        {
            return c._array;
        }

        public static implicit operator Array(BytePtr c)
        {
            return c._array;
        }

        public static implicit operator BytePtr(String str)
        {
            BytePtr b = new BytePtr(Encoding.ASCII.GetBytes(str));
            return b;
        }

        #endregion casting operators

        #region private members

        private void ReleaseHandle()
        {
            if (_array_handle.IsAllocated)
                _array_handle.Free();
            _array = null;
            unsafe { _array_ptr = null; }
        }

        #endregion private members

        #region IDisposable Members

        bool disposed = false;

        ~BytePtr()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed && _array_handle != null)
            {
                ReleaseHandle();
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
