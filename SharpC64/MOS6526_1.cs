using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    /// <summary>
    /// Emulates the 6526 Complex Interface Adapter (CIA) #1 
    /// </summary>
    public class MOS6526_1 : MOS6526
    {
        #region Public members

        public MOS6526_1(MOS6510 CPU, MOS6569 VIC)
            : base(CPU)
        {
            the_vic = VIC;
        }

        public new void Reset()
        {
            base.Reset();

	        // Clear keyboard matrix and joystick states
	        for (int i=0; i<8; i++)
		        KeyMatrix[i] = RevMatrix[i] = 0xff;

	        Joystick1 = Joystick2 = 0xff;
	        prev_lp = 0x10;
        }

        public byte ReadRegister(UInt16 adr)
        {
            switch (adr)
            {
                case 0x00:
                    {
                        byte ret = (byte)(pra | ~ddra); 
                        byte tst = (byte)((prb | ~ddrb) & Joystick1);

                        if (!((tst & 0x01) > 0)) ret &= RevMatrix[0];	// AND all active columns
                        if (!((tst & 0x02) > 0)) ret &= RevMatrix[1];
                        if (!((tst & 0x04) > 0)) ret &= RevMatrix[2];
                        if (!((tst & 0x08) > 0)) ret &= RevMatrix[3];
                        if (!((tst & 0x10) > 0)) ret &= RevMatrix[4];
                        if (!((tst & 0x20) > 0)) ret &= RevMatrix[5];
                        if (!((tst & 0x40) > 0)) ret &= RevMatrix[6];
                        if (!((tst & 0x80) > 0)) ret &= RevMatrix[7];
                        return (byte)(ret & Joystick2);
                    }

                case 0x01:
                    {
                        byte ret = (byte)(~ddrb); 
                        byte tst = (byte)((pra | ~ddra) & Joystick2);

                        if (!((tst & 0x01) > 0)) ret &= KeyMatrix[0];	// AND all active rows
                        if (!((tst & 0x02) > 0)) ret &= KeyMatrix[1];
                        if (!((tst & 0x04) > 0)) ret &= KeyMatrix[2];
                        if (!((tst & 0x08) > 0)) ret &= KeyMatrix[3];
                        if (!((tst & 0x10) > 0)) ret &= KeyMatrix[4];
                        if (!((tst & 0x20) > 0)) ret &= KeyMatrix[5];
                        if (!((tst & 0x40) > 0)) ret &= KeyMatrix[6];
                        if (!((tst & 0x80) > 0)) ret &= KeyMatrix[7];
                        return (byte)((ret | (prb & ddrb)) & Joystick1);
                    }

                case 0x02: return ddra;
                case 0x03: return ddrb;
                case 0x04: return (byte)ta;
                case 0x05: return (byte)(ta >> 8);
                case 0x06: return (byte)tb;
                case 0x07: return (byte)(tb >> 8);
                case 0x08: tod_halt = false; return tod_10ths;
                case 0x09: return tod_sec;
                case 0x0a: return tod_min;
                case 0x0b: tod_halt = true; return tod_hr;
                case 0x0c: return sdr;
                case 0x0d:
                    {
                        byte ret = icr;			// Read and clear ICR
                        icr = 0;
                        the_cpu.ClearCIAIRQ();		// Clear IRQ
                        return ret;
                    }
                case 0x0e: return cra;
                case 0x0f: return crb;
            }
            return 0;	// Can't happen
        }

        public void WriteRegister(UInt16 adr, byte abyte)
        {
            switch (adr)
            {
                case 0x0: pra = abyte; break;
                case 0x1:
                    prb = abyte;
                    check_lp();
                    break;
                case 0x2: ddra = abyte; break;
                case 0x3:
                    ddrb = abyte;
                    check_lp();
                    break;

                case 0x4: latcha = (UInt16)((latcha & 0xff00) | abyte); break;
                case 0x5:
                    latcha = (UInt16)((latcha & 0xff) | (abyte << 8));
                    if ((cra & 1) == 0)	// Reload timer if stopped
                        ta = latcha;
                    break;

                case 0x6: latchb = (UInt16)((latchb & 0xff00) | abyte); break;
                case 0x7:
                    latchb = (UInt16)((latchb & 0xff) | (abyte << 8));
                    if ((crb & 1) == 0)	// Reload timer if stopped
                        tb = latchb;
                    break;

                case 0x8:
                    if ((crb & 0x80) != 0)
                        alm_10ths = (byte)(abyte & 0x0f);
                    else
                        tod_10ths = (byte)(abyte & 0x0f);
                    break;
                case 0x9:
                    if ((crb & 0x80) != 0)
                        alm_sec = (byte)(abyte & 0x7f);
                    else
                        tod_sec = (byte)(abyte & 0x7f);
                    break;
                case 0xa:
                    if ((crb & 0x80) != 0)
                        alm_min = (byte)(abyte & 0x7f);
                    else
                        tod_min = (byte)(abyte & 0x7f);
                    break;
                case 0xb:
                    if ((crb & 0x80) != 0)
                        alm_hr = (byte)(abyte & 0x9f);
                    else
                        tod_hr = (byte)(abyte & 0x9f);
                    break;

                case 0xc:
                    sdr = abyte;
                    TriggerInterrupt(8);	// Fake SDR interrupt for programs that need it
                    break;

                case 0xd:
                    if ((abyte & 0x80) != 0)
                        int_mask |= (byte)(abyte & 0x7f);
                    else
                        int_mask &= (byte)~abyte;
                    if ((icr & int_mask & 0x1f) != 0)
                    { 
                        // Trigger IRQ if pending
                        icr |= 0x80;
                        the_cpu.TriggerCIAIRQ();
                    }
                    break;

                case 0xe:
                    has_new_cra = true;		// Delay write by 1 cycle
                    new_cra = abyte;
                    ta_cnt_phi2 = ((abyte & 0x20) == 0x00);
                    break;

                case 0xf:
                    has_new_crb = true;		// Delay write by 1 cycle
                    new_crb = abyte;
                    tb_cnt_phi2 = ((abyte & 0x60) == 0x00);
                    tb_cnt_ta = ((abyte & 0x60) == 0x40);
                    break;
            }
        }

        public override void TriggerInterrupt(int bit)
        {
            icr |= (byte)bit;
            if ((int_mask & bit) != 0)
            {
                icr |= 0x80;
                the_cpu.TriggerCIAIRQ();
            }
        }

        #endregion

        #region Public properties

        public byte[] KeyMatrix
        {
            get { return _KeyMatrix; }
            set { _KeyMatrix = value; }
        }
        public byte[] RevMatrix
        {
            get { return _RevMatrix; }
            set { _RevMatrix = value; }
        }
        public byte Joystick1
        {
            get { return _Joystick1; }
            set { _Joystick1 = value; }
        }
        public byte Joystick2
        {
            get { return _Joystick2; }
            set { _Joystick2 = value; }
        }

        #endregion

        #region Private members

        void check_lp()
        {
            if (((prb | ~ddrb) & 0x10) != prev_lp)
                the_vic.TriggerLightpen();
            prev_lp = (byte)((prb | ~ddrb) & 0x10);
        }

        #endregion

        #region Private fields

        byte[] _KeyMatrix = new byte[8];	// C64 keyboard matrix, 1 bit/key (0: key down, 1: key up)
        byte[] _RevMatrix = new byte[8];	// Reversed keyboard matrix     

        byte _Joystick1;	                // Joystick 1 AND value     
        byte _Joystick2;	                // Joystick 2 AND value

        MOS6569 the_vic;

        byte prev_lp;		            // Previous state of LP line (bit 4)
        #endregion
    }
}
