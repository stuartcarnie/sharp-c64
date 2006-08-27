using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class MOS6526State
    {
        public byte pra;
        public byte ddra;
        public byte prb;
        public byte ddrb;
        public byte ta_lo;
        public byte ta_hi;
        public byte tb_lo;
        public byte tb_hi;
        public byte tod_10ths;
        public byte tod_sec;
        public byte tod_min;
        public byte tod_hr;
        public byte sdr;
        public byte int_data;		// Pending interrupts
        public byte cra;
        public byte crb;

        // Additional registers
        public UInt16 latcha;		// Timer latches
        public UInt16 latchb;
        public byte alm_10ths;	    // Alarm time
        public byte alm_sec;
        public byte alm_min;
        public byte alm_hr;
        public byte int_mask;		// Enabled interrupts
    }
}
