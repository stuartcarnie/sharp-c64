using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class MOS6510State
    {
        public byte a, x, y;
        public byte p;			            // Processor flags
        public byte ddr, pr;		        // Port
        public UInt16 pc; 
        public byte sp;
        public InterruptState6510 intr;		// Interrupt state
        public bool nmi_state;
        public byte dfff_byte;
        public bool instruction_complete;
    }
}
