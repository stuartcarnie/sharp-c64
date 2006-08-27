using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace SharpC64
{
    public partial class MOS6502_1541
    {
        #region Private constants
        const bool BALow = false;       // used for the EmulateCycle.cs file to ignore this condition
        #endregion

        #region public methods

        public MOS6502_1541(C64 c64, Job1541 job, C64Display disp, byte[] Ram, byte[] Rom)
        {
            the_c64 = c64;
            the_job = job;
            the_display = disp;
            ram = Ram;
            rom = Rom;

            a = x = y = 0;
            sp = 0xff;
            n_flag = z_flag = 0;
            v_flag = d_flag = c_flag = false;
            i_flag = true;

            via1_t1c = via1_t1l = via1_t2c = via1_t2l = 0;
            via1_sr = 0;
            via2_t1c = via2_t1l = via2_t2c = via2_t2l = 0;
            via2_sr = 0;

            first_irq_cycle = 0;
            Idle = false;
        }

        public void Reset()
        {
            // IEC lines and VIA registers
            IECLines = 0xc0;

            via1_pra = via1_ddra = via1_prb = via1_ddrb = 0;
            via1_acr = via1_pcr = 0;
            via1_ifr = via1_ier = 0;
            via2_pra = via2_ddra = via2_prb = via2_ddrb = 0;
            via2_acr = via2_pcr = 0;
            via2_ifr = via2_ier = 0;

            // Clear all interrupt lines
            interrupt.ANY = 0;

            // Read reset vector
            pc = read_word(0xfffc);
            state = 0;

            // Wake up 1541
            Idle = false;
        }

        public void AsyncReset()
        {
            interrupt.RESET = ByteBool.True;
            Idle = false;
        }

        public byte ExtReadByte(UInt16 adr)
        {
            return read_byte(adr);
        }

        public void ExtWriteByte(UInt16 adr, byte abyte)
        {
            write_byte(adr, abyte);
        }

        public void CountVIATimers(int cycles)
        {
            UInt32 tmp;

            via1_t1c = (UInt16)(tmp = (UInt32)(via1_t1c - cycles));
            if (tmp > 0xffff)
            {
                if ((via1_acr & 0x40) != 0)	// Reload from latch in free-run mode
                    via1_t1c = via1_t1l;
                via1_ifr |= 0x40;
            }

            if ((via1_acr & 0x20) == 0)
            {
                // Only count in one-shot mode
                via1_t2c = (UInt16)(tmp = (UInt32)(via1_t2c - cycles));
                if (tmp > 0xffff)
                    via1_ifr |= 0x20;
            }

            via2_t1c = (UInt16)(tmp = (UInt32)(via2_t1c - cycles));
            if (tmp > 0xffff)
            {
                if ((via2_acr & 0x40) != 0)	// Reload from latch in free-run mode
                    via2_t1c = via2_t1l;
                via2_ifr |= 0x40;
                if ((via2_ier & 0x40) != 0)
                    TriggerJobIRQ();
            }

            if ((via2_acr & 0x20) == 0)
            {	// Only count in one-shot mode
                via2_t2c = (UInt16)(tmp = (UInt32)(via2_t2c - cycles));
                if (tmp > 0xffff)
                    via2_ifr |= 0x20;
            }
        }

        public void NewATNState()
        {
            byte abyte = (byte)(~via1_prb & via1_ddrb);
            IECLines = (byte)((abyte << 6) & ((~abyte ^ TheCIA2.IECLines) << 3) & 0x80	    // DATA (incl. ATN acknowledge)
                | (abyte << 3) & 0x40);											            // CLK
        }

        public void IECInterrupt()
        {
            ram[0x7c] = 1;

            // Wake up 1541
            Idle = false;
        }

        public void TriggerJobIRQ()
        {
            if ((interrupt.VIA2IRQ == ByteBool.False))
                first_irq_cycle = the_c64.CycleCounter;
            interrupt.VIA2IRQ = ByteBool.True;
            Idle = false;
        }

        public bool InterruptEnabled
        {
            get { return !i_flag; }
        }

        #endregion

        #region Public propertie

        public MOS6526_2 TheCIA2
        {
            get { return _TheCIA2; }
            set { _TheCIA2 = value; }
        }

        public byte IECLines
        {
            get { return _IECLines; }
            set { _IECLines = value; }
        }

        public bool Idle
        {
            get { return _Idle; }
            set { _Idle = value; }
        }

        public MOS6502State State
        {
            get
            {
                MOS6502State s = new MOS6502State();

                s.a = a;
                s.x = x;
                s.y = y;

                s.p = (byte)(0x20 | (n_flag & 0x80));
                if (v_flag) s.p |= 0x40;
                if (d_flag) s.p |= 0x08;
                if (i_flag) s.p |= 0x04;
                if (z_flag == 0) s.p |= 0x02;
                if (c_flag) s.p |= 0x01;

                s.pc = pc;
                s.sp = (UInt16)(sp | 0x0100);

                s.intr = interrupt;
                s.idle = Idle;

                s.via1_pra = via1_pra; s.via1_ddra = via1_ddra;
                s.via1_prb = via1_prb; s.via1_ddrb = via1_ddrb;
                s.via1_t1c = via1_t1c; s.via1_t1l = via1_t1l;
                s.via1_t2c = via1_t2c; s.via1_t2l = via1_t2l;
                s.via1_sr = via1_sr;
                s.via1_acr = via1_acr; s.via1_pcr = via1_pcr;
                s.via1_ifr = via1_ifr; s.via1_ier = via1_ier;

                s.via2_pra = via2_pra; s.via2_ddra = via2_ddra;
                s.via2_prb = via2_prb; s.via2_ddrb = via2_ddrb;
                s.via2_t1c = via2_t1c; s.via2_t1l = via2_t1l;
                s.via2_t2c = via2_t2c; s.via2_t2l = via2_t2l;
                s.via2_sr = via2_sr;
                s.via2_acr = via2_acr; s.via2_pcr = via2_pcr;
                s.via2_ifr = via2_ifr; s.via2_ier = via2_ier;

                return s;
            }

            set
            {
                a = value.a;
                x = value.x;
                y = value.y;

                n_flag = value.p;
                v_flag = (value.p & 0x40) != 0;
                d_flag = (value.p & 0x08) != 0;
                i_flag = (value.p & 0x04) != 0;
                z_flag = (byte)((value.p & 0x02) ^ 0x02);
                c_flag = (value.p & 0x01) != 0;

                pc = value.pc;
                sp = (byte)(value.sp & 0xff);

                interrupt = value.intr;
                Idle = value.idle;

                via1_pra = value.via1_pra; via1_ddra = value.via1_ddra;
                via1_prb = value.via1_prb; via1_ddrb = value.via1_ddrb;
                via1_t1c = value.via1_t1c; via1_t1l = value.via1_t1l;
                via1_t2c = value.via1_t2c; via1_t2l = value.via1_t2l;
                via1_sr = value.via1_sr;
                via1_acr = value.via1_acr; via1_pcr = value.via1_pcr;
                via1_ifr = value.via1_ifr; via1_ier = value.via1_ier;

                via2_pra = value.via2_pra; via2_ddra = value.via2_ddra;
                via2_prb = value.via2_prb; via2_ddrb = value.via2_ddrb;
                via2_t1c = value.via2_t1c; via2_t1l = value.via2_t1l;
                via2_t2c = value.via2_t2c; via2_t2l = value.via2_t2l;
                via2_sr = value.via2_sr;
                via2_acr = value.via2_acr; via2_pcr = value.via2_pcr;
                via2_ifr = value.via2_ifr; via2_ier = value.via2_ier;
            }
        }

        public C64 TheC64
        {
            get { return the_c64; }
            set { the_c64 = value; }
        }

        #endregion public properties

        #region private methods

        byte read_byte(UInt16 adr)
        {
            if (adr >= 0xc000)
                return rom[adr & 0x3fff];
            else if (adr < 0x1000)
                return ram[adr & 0x07ff];
            else
                return read_byte_io(adr);
        }

        byte read_byte_io(UInt16 adr)
        {
            if ((adr & 0xfc00) == 0x1800)	// VIA 1
                switch (adr & 0xf)
                {
                    case 0:
                        return (byte)((via1_prb & 0x1a
                            | ((IECLines & TheCIA2.IECLines) >> 7)			// DATA
                            | ((IECLines & TheCIA2.IECLines) >> 4) & 0x04	// CLK
                            | (TheCIA2.IECLines << 3) & 0x80) ^ 0x85);		// ATN
                    case 1:
                    case 15:
                        return 0xff;	// Keep 1541C ROMs happy (track 0 sensor)
                    case 2:
                        return via1_ddrb;
                    case 3:
                        return via1_ddra;
                    case 4:
                        via1_ifr &= 0xbf;
                        return (byte)via1_t1c;
                    case 5:
                        return (byte)(via1_t1c >> 8);
                    case 6:
                        return (byte)via1_t1l;
                    case 7:
                        return (byte)(via1_t1l >> 8);
                    case 8:
                        via1_ifr &= 0xdf;
                        return (byte)(via1_t2c);
                    case 9:
                        return (byte)(via1_t2c >> 8);
                    case 10:
                        return via1_sr;
                    case 11:
                        return via1_acr;
                    case 12:
                        return via1_pcr;
                    case 13:
                        return (byte)(via1_ifr | ((via1_ifr & via1_ier) != 0 ? 0x80 : 0));
                    case 14:
                        return (byte)(via1_ier | 0x80);
                    default:	// Can't happen
                        return 0;
                }

            else if ((adr & 0xfc00) == 0x1c00)	// VIA 2
                switch (adr & 0xf)
                {
                    case 0:
                        if (the_job.SyncFound())
                            return (byte)(via2_prb & 0x7f | the_job.WPState());
                        else
                            return (byte)(via2_prb | 0x80 | the_job.WPState());
                    case 1:
                    case 15:
                        return the_job.ReadGCRByte();
                    case 2:
                        return via2_ddrb;
                    case 3:
                        return via2_ddra;
                    case 4:
                        via2_ifr &= 0xbf;
                        interrupt.VIA2IRQ = ByteBool.False;	// Clear job IRQ
                        return (byte)(via2_t1c);
                    case 5:
                        return (byte)(via2_t1c >> 8);
                    case 6:
                        return (byte)(via2_t1l);
                    case 7:
                        return (byte)(via2_t1l >> 8);
                    case 8:
                        via2_ifr &= 0xdf;
                        return (byte)(via2_t2c);
                    case 9:
                        return (byte)(via2_t2c >> 8);
                    case 10:
                        return via2_sr;
                    case 11:
                        return via2_acr;
                    case 12:
                        return via2_pcr;
                    case 13:
                        return (byte)(via2_ifr | ((via2_ifr & via2_ier) != 0 ? 0x80 : 0));
                    case 14:
                        return (byte)(via2_ier | 0x80);
                    default:	// Can't happen
                        return 0;
                }

            else
                return (byte)(adr >> 8);
        }

        UInt16 read_word(UInt16 adr)
        {
            return (UInt16)(read_byte(adr) | (read_byte((UInt16)(adr + 1)) << 8));
        }

        void write_byte(UInt16 adr, byte abyte)
        {
            if (adr < 0x1000)
                ram[adr & 0x7ff] = abyte;
            else
                write_byte_io(adr, abyte);
        }

        void write_byte_io(UInt16 adr, byte abyte)
        {
            if ((adr & 0xfc00) == 0x1800)		// VIA 1
                switch (adr & 0xf)
                {
                    case 0:
                        via1_prb = abyte;
                        abyte = (byte)(~via1_prb & via1_ddrb);
                        IECLines = (byte)((abyte << 6) & ((~abyte ^ TheCIA2.IECLines) << 3) & 0x80
                            | (abyte << 3) & 0x40);
                        break;
                    case 1:
                    case 15:
                        via1_pra = abyte;
                        break;
                    case 2:
                        via1_ddrb = abyte;
                        abyte &= (byte)~via1_prb;
                        IECLines = (byte)((abyte << 6) & ((~abyte ^ TheCIA2.IECLines) << 3) & 0x80
                            | (abyte << 3) & 0x40);
                        break;
                    case 3:
                        via1_ddra = abyte;
                        break;
                    case 4:
                    case 6:
                        via1_t1l = (UInt16)(via1_t1l & 0xff00 | abyte);
                        break;
                    case 5:
                        via1_t1l = (UInt16)(via1_t1l & 0xff | (abyte << 8));
                        via1_ifr &= 0xbf;
                        via1_t1c = via1_t1l;
                        break;
                    case 7:
                        via1_t1l = (UInt16)(via1_t1l & 0xff | (abyte << 8));
                        break;
                    case 8:
                        via1_t2l = (UInt16)(via1_t2l & 0xff00 | abyte);
                        break;
                    case 9:
                        via1_t2l = (UInt16)(via1_t2l & 0xff | (abyte << 8));
                        via1_ifr &= 0xdf;
                        via1_t2c = via1_t2l;
                        break;
                    case 10:
                        via1_sr = abyte;
                        break;
                    case 11:
                        via1_acr = abyte;
                        break;
                    case 12:
                        via1_pcr = abyte;
                        break;
                    case 13:
                        via1_ifr &= (byte)~abyte;
                        break;
                    case 14:
                        if ((abyte & 0x80) != 0)
                            via1_ier |= (byte)(abyte & 0x7f);
                        else
                            via1_ier &= (byte)~abyte;
                        break;
                }

            else if ((adr & 0xfc00) == 0x1c00)
                switch (adr & 0xf)
                {
                    case 0:
                        if (((via2_prb ^ abyte) & 8) != 0)	// Bit 3: Drive LED
                            the_display.UpdateLEDs((abyte & 8) != 0 ? DriveLEDState.DRVLED_ON : 0, 0, 0, 0);
                        if (((via2_prb ^ abyte) & 3) != 0)	// Bits 0/1: Stepper motor
                            if ((via2_prb & 3) == ((abyte + 1) & 3))
                                the_job.MoveHeadOut();
                            else if ((via2_prb & 3) == ((abyte - 1) & 3))
                                the_job.MoveHeadIn();
                        via2_prb = (byte)(abyte & 0xef);
                        break;
                    case 1:
                    case 15:
                        via2_pra = abyte;
                        break;
                    case 2:
                        via2_ddrb = abyte;
                        break;
                    case 3:
                        via2_ddra = abyte;
                        break;
                    case 4:
                    case 6:
                        via2_t1l = (UInt16)(via2_t1l & 0xff00 | abyte);
                        break;
                    case 5:
                        via2_t1l = (UInt16)(via2_t1l & 0xff | (abyte << 8));
                        via2_ifr &= 0xbf;
                        via2_t1c = via2_t1l;
                        break;
                    case 7:
                        via2_t1l = (UInt16)(via2_t1l & 0xff | (abyte << 8));
                        break;
                    case 8:
                        via2_t2l = (UInt16)(via2_t2l & 0xff00 | abyte);
                        break;
                    case 9:
                        via2_t2l = (UInt16)(via2_t2l & 0xff | (abyte << 8));
                        via2_ifr &= 0xdf;
                        via2_t2c = via2_t2l;
                        break;
                    case 10:
                        via2_sr = abyte;
                        break;
                    case 11:
                        via2_acr = abyte;
                        break;
                    case 12:
                        via2_pcr = abyte;
                        break;
                    case 13:
                        via2_ifr &= (byte)~abyte;
                        break;
                    case 14:
                        if ((abyte & 0x80) != 0)
                            via2_ier |= (byte)(abyte & 0x7f);
                        else
                            via2_ier &= (byte)~abyte;
                        break;
                }
        }


        byte read_zp(UInt16 adr)
        {
            return ram[adr];
        }

        UInt16 read_zp_word(UInt16 adr)
        {
            return (UInt16)(ram[adr & 0xff] | (ram[(adr + 1) & 0xff] << 8));
        }

        void write_zp(UInt16 adr, byte abyte)
        {
            ram[adr] = abyte;
        }


        void jump(UInt16 adr)
        {
            pc = adr;
        }

        void illegal_op(byte op, UInt16 at)
        {
            throw new IllegalOpcodeException(String.Format("1541: Illegal opcode ${0:X4} at ${1:X2}", op, at));

            //if (ShowRequester(illop_msg, "Reset 1541", "Reset C64"))
            //    the_c64->Reset();
            //Reset();
        }

        void illegal_jump(UInt16 at, UInt16 to)
        {
            throw new IllegalOpcodeException(String.Format("1541: Jump to I/O space at ${0:X4} to ${1:X2}", at, to));

            //if (ShowRequester(illop_msg, "Reset 1541", "Reset C64"))
            //    the_c64->Reset();
            //Reset();
        }


        void do_adc(byte abyte)
        {
            if (!d_flag)
            {
                UInt16 tmp;

                // Binary mode
                tmp = (UInt16)(a + abyte + (c_flag ? 1 : 0));
                c_flag = tmp > 0xff;
                v_flag = !(((a ^ abyte) & 0x80) != 0) && ((a ^ tmp) & 0x80) != 0;
                z_flag = n_flag = a = (byte)tmp;
            }
            else
            {
                UInt16 al, ah;

                // Decimal mode
                al = (UInt16)((a & 0x0f) + (abyte & 0x0f) + (c_flag ? 1 : 0));		// Calculate lower nybble
                if (al > 9) al += 6;									// BCD fixup for lower nybble

                ah = (UInt16)((a >> 4) + (abyte >> 4));							// Calculate upper nybble
                if (al > 0x0f) ah++;

                z_flag = (byte)(a + abyte + (c_flag ? 1 : 0));					// Set flags
                n_flag = (byte)(ah << 4);	// Only highest bit used
                v_flag = (((ah << 4) ^ a) & 0x80) != 0 && !(((a ^ abyte) & 0x80) != 0);

                if (ah > 9) ah += 6;									// BCD fixup for upper nybble
                c_flag = ah > 0x0f;										// Set carry flag
                a = (byte)((ah << 4) | (al & 0x0f));							// Compose result
            }
        }

        void do_sbc(byte abyte)
        {
            UInt16 tmp = (UInt16)(a - abyte - (c_flag ? 0 : 1));

            if (!d_flag)
            {

                // Binary mode
                c_flag = tmp < 0x100;
                v_flag = ((a ^ tmp) & 0x80) > 0 && ((a ^ abyte) & 0x80) > 0;
                z_flag = n_flag = (byte)tmp;
                a = (byte)tmp;
            }
            else
            {
                UInt16 al, ah;

                // Decimal mode
                al = (UInt16)((a & 0x0f) - (abyte & 0x0f) - (c_flag ? 0 : 1));	// Calculate lower nybble
                ah = (UInt16)((a >> 4) - (abyte >> 4));							// Calculate upper nybble
                if ((al & 0x10) != 0)
                {
                    al -= 6;											        // BCD fixup for lower nybble
                    ah--;
                }
                if ((ah & 0x10) != 0) ah -= 6;									// BCD fixup for upper nybble

                c_flag = tmp < 0x100;									        // Set flags
                v_flag = ((a ^ tmp) & 0x80) != 0 && ((a ^ abyte) & 0x80) != 0;
                z_flag = n_flag = (byte)tmp;

                a = (byte)((ah << 4) | (al & 0x0f));							// Compose result
            }
        }


        #endregion

        #region Private field

        MOS6526_2 _TheCIA2;		// Pointer to C64 CIA 2

        byte _IECLines;
        bool _Idle;

        byte[] ram;				// Pointer to main RAM
        byte[] rom;				// Pointer to ROM
        C64 the_c64;			// Pointer to C64 object

        C64Display the_display; // Pointer to C64 display object
        Job1541 the_job;		// Pointer to 1541 job object

        InterruptState6502 interrupt;

        // CPU Flags
        byte n_flag;                        // sign
        byte z_flag;                        // zero
        bool v_flag;                        // overflow
        bool c_flag;                        // carry
        bool d_flag;                        // decimal
        bool b_flag;                        // break
        bool i_flag;                        // interrupt disable

        // CPU Registers
        byte a, x, y, sp;
        UInt16 pc;                          // program counter

        UInt32 first_irq_cycle;

        byte state, op;		// Current state and opcode
        UInt16 ar, ar2;		// Address registers
        byte rdbuf;			// Data buffer for RMW instructions

        byte via1_pra;		// PRA of VIA 1
        byte via1_ddra;	    // DDRA of VIA 1
        byte via1_prb;		// PRB of VIA 1
        byte via1_ddrb;	    // DDRB of VIA 1
        UInt16 via1_t1c;	// T1 Counter of VIA 1
        UInt16 via1_t1l;	// T1 Latch of VIA 1
        UInt16 via1_t2c;	// T2 Counter of VIA 1
        UInt16 via1_t2l;	// T2 Latch of VIA 1
        byte via1_sr;		// SR of VIA 1
        byte via1_acr;		// ACR of VIA 1
        byte via1_pcr;		// PCR of VIA 1
        byte via1_ifr;		// IFR of VIA 1
        byte via1_ier;		// IER of VIA 1

        byte via2_pra;		// PRA of VIA 2
        byte via2_ddra;	    // DDRA of VIA 2
        byte via2_prb;		// PRB of VIA 2
        byte via2_ddrb;	    // DDRB of VIA 2
        UInt16 via2_t1c;	// T1 Counter of VIA 2
        UInt16 via2_t1l;	// T1 Latch of VIA 2
        UInt16 via2_t2c;	// T2 Counter of VIA 2
        UInt16 via2_t2l;	// T2 Latch of VIA 2
        byte via2_sr;		// SR of VIA 2
        byte via2_acr;		// ACR of VIA 2
        byte via2_pcr;		// PCR of VIA 2
        byte via2_ifr;		// IFR of VIA 2
        byte via2_ier;		// IER of VIA 2

        #endregion private field

    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InterruptState6502
    {
        [FieldOffset(0)]
        public ByteBool VIA1IRQ;

        [FieldOffset(1)]
        public ByteBool VIA2IRQ;

        [FieldOffset(2)]
        public ByteBool IECIRQ;

        [FieldOffset(3)]
        public ByteBool RESET;

        [FieldOffset(0)]
        public UInt32 ANY;
    }
}
