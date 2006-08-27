using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace SGC
{
    class HiResTimer : Stopwatch
    {
        static readonly double MicrosecondsPerTick;

        static HiResTimer()
        {
            MicrosecondsPerTick = Frequency / (1000.0f * 1000.0f);
        }

        public long ElapsedMicroseconds
        {
            get
            {
                return (long)(ElapsedTicks / MicrosecondsPerTick);
            }
        }
        
        public static new HiResTimer StartNew()
        {
            HiResTimer ht = new HiResTimer();
            ht.Start();
            return ht;
        }
    }
}
