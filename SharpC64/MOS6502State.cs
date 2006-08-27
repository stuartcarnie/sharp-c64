using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class MOS6502State
    {
        public byte a, x, y;
        public byte p;			// Processor flags
        public UInt16 pc, sp;

        public InterruptState6502 intr;		// Interrupt state
        public bool instruction_complete;
        public bool idle;

        public byte via1_pra;		// VIA 1
        public byte via1_ddra;
        public byte via1_prb;
        public byte via1_ddrb;
        public UInt16 via1_t1c;
        public UInt16 via1_t1l;
        public UInt16 via1_t2c;
        public UInt16 via1_t2l;
        public byte via1_sr;
        public byte via1_acr;
        public byte via1_pcr;
        public byte via1_ifr;
        public byte via1_ier;

        public byte via2_pra;		// VIA 2
        public byte via2_ddra;
        public byte via2_prb;
        public byte via2_ddrb;
        public UInt16 via2_t1c;
        public UInt16 via2_t1l;
        public UInt16 via2_t2c;
        public UInt16 via2_t2l;
        public byte via2_sr;
        public byte via2_acr;
        public byte via2_pcr;
        public byte via2_ifr;
        public byte via2_ier;
    }
}
