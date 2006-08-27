using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    /// <summary>
    /// Emulates the 6526 Complex Interface Adapter (CIA) #2
    /// </summary>
    public class MOS6526_2 : MOS6526
    {
        #region Public members

        public MOS6526_2(MOS6510 CPU, MOS6569 VIC, MOS6502_1541 CPU1541)
            : base(CPU)
        {
            the_vic = VIC;
            the_cpu_1541 = CPU1541;
        }

        public new void Reset()
        {
            base.Reset();

            // VA14/15 = 0
            the_vic.ChangedVA(0);

            // IEC
            IECLines = 0xd0;
        }

        public byte ReadRegister(UInt16 adr)
        {
            switch (adr)
            {
                case 0x00:
                    return (byte)((pra | ~ddra) & 0x3f | IECLines & the_cpu_1541.IECLines);
                case 0x01: return (byte)(prb | ~ddrb);
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
                        byte ret = icr; // Read and clear ICR
                        icr = 0;
                        the_cpu.ClearNMI();
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
                case 0x0:
                    {
                        pra = abyte;
                        the_vic.ChangedVA((UInt16)(~(pra | ~ddra) & 3));
                        byte old_lines = IECLines;
                        IECLines = (byte)((~abyte << 2) & 0x80	// DATA
                            | (~abyte << 2) & 0x40		// CLK
                            | (~abyte << 1) & 0x10);		// ATN
                        
                        if (((IECLines ^ old_lines) & 0x10) > 0)
                        {	// ATN changed
                            the_cpu_1541.NewATNState();
                            if ((old_lines & 0x10) > 0)				// ATN 1->0
                                the_cpu_1541.IECInterrupt();
                        }
                        break;
                    }
                case 0x1: prb = abyte; break;

                case 0x2:
                    ddra = abyte;
                    the_vic.ChangedVA((UInt16)(~(pra | ~ddra) & 3));
                    break;
                case 0x3: ddrb = abyte; break;

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
                        // Trigger NMI if pending
                        icr |= 0x80;
                        the_cpu.TriggerNMI();
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
                the_cpu.TriggerNMI();
            }
        }

        #endregion

        #region Public properties

        public byte IECLines
        {
            get { return _IECLines; }
            set { _IECLines = value; }
        }

        #endregion

        #region Private fields

        byte _IECLines;

        MOS6569 the_vic;
        MOS6502_1541 the_cpu_1541;

        #endregion
    }
}
