using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class T64Drive : Drive
    {
        public T64Drive(IEC iec, string filepath)
            : base(iec)
        {
        }

        public override byte Open(int channel, byte[] filename)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override byte Close(int channel)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override byte Read(int channel, ref byte abyte)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override byte Write(int channel, byte abyte, bool eoi)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void Reset()
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
