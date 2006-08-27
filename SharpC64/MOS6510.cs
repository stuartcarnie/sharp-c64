using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

/*
 *  CPUC64_SC.cpp - Single-cycle 6510 (C64) emulation
 *
 *  Frodo (C) 1994-1997,2002 Christian Bauer
 *

 *
 * Notes:
 * ------
 *
 * Opcode execution:
 *  - All opcodes are resolved into single clock cycles. There is one
 *    switch case for each cycle.
 *  - The "state" variable specifies the routine to be executed in the
 *    next cycle. Its upper 8 bits contain the current opcode, its lower
 *    8 bits contain the cycle number (0..7) within the opcode.
 *  - Opcodes are fetched in cycle 0 (state = 0)
 *  - The states 0x0010..0x0027 are used for interrupts
 *  - There is exactly one memory access in each clock cycle
 *
 * Memory configurations:
 *
 * $01  $a000-$bfff  $d000-$dfff  $e000-$ffff
 * -----------------------------------------------
 *  0       RAM          RAM          RAM
 *  1       RAM       Char ROM        RAM
 *  2       RAM       Char ROM    Kernal ROM
 *  3    Basic ROM    Char ROM    Kernal ROM
 *  4       RAM          RAM          RAM
 *  5       RAM          I/O          RAM
 *  6       RAM          I/O      Kernal ROM
 *  7    Basic ROM       I/O      Kernal ROM
 *
 *  - All memory accesses are done with the read_byte() and
 *    write_byte() functions which also do the memory address
 *    decoding.
 *  - If a write occurs to addresses 0 or 1, new_config is
 *    called to check whether the memory configuration has
 *    changed
 *  - The possible interrupt sources are:
 *      INT_VICIRQ: I flag is checked, jump to ($fffe)
 *      INT_CIAIRQ: I flag is checked, jump to ($fffe)
 *      INT_NMI: Jump to ($fffa)
 *      INT_RESET: Jump to ($fffc)
 *  - The z_flag variable has the inverse meaning of the
 *    6510 Z flag
 *  - Only the highest bit of the n_flag variable is used
 *  - The $f2 opcode that would normally crash the 6510 is
 *    used to implement emulator-specific functions, mainly
 *    those for the IEC routines
 *
 * Incompatibilities:
 * ------------------
 *
 *  - If BA is low and AEC is high, read accesses should occur
 */

namespace SharpC64
{
    /// <summary>
    /// Emulates the MOS 6510 Processor
    /// </summary>
    public partial class MOS6510
    {
        public MOS6510(C64 c64, byte[] ram, byte[] basic_rom, byte[] kernel_rom, byte[] char_rom, byte[] color_ram)
        {
            TheC64 = c64;
            this.ram = ram;
            this.basic_rom = basic_rom;
            this.kernel_rom = kernel_rom;
            this.char_rom = char_rom;
            this.color_ram = color_ram;

            a = x = y = 0;
            sp = 0xff;

            n_flag = z_flag = 0;
            v_flag = c_flag = d_flag = b_flag = i_flag = false;

            dfff_byte = 0x55;
            BALow = false;
            first_irq_cycle = first_nmi_cycle = 0;
#if DEBUG_INSTRUCTIONS
            debugLogger = new DebugLog(this, false);
#endif
        }

        #region Public Members

        public void Reset()
        {
            // Delete 'CBM80' if present
            if (ram[0x8004] == 0xc3 && ram[0x8005] == 0xc2 && ram[0x8006] == 0xcd
             && ram[0x8007] == 0x38 && ram[0x8008] == 0x30)
                ram[0x8004] = 0;

            // Initialize extra 6510 registers and memory configuration
            ddr = pr = 0;
            new_config();

            // Clear all interrupt lines
            interrupt.ANY = 0;
            nmi_state = false;

            // Read reset vector
            pc = read_word(0xfffc);
            state = 0;
        }

        /// <summary>
        /// Reset CPU asynchronously
        /// </summary>
        public void AsyncReset()
        {
            interrupt.RESET = ByteBool.True;
        }

        /// <summary>
        /// Raise NMI asynchronously (NMI pulse)
        /// </summary>
        public void AsyncNMI()
        {
            if (!nmi_state)
                interrupt.NMI = ByteBool.True;
        }

        public MOS6510State State
        {
            get
            {
                MOS6510State s = new MOS6510State();
                s.a = a;
                s.x = x;
                s.y = y;

                s.p = (byte)(0x20 | (n_flag & 0x80));
                if (v_flag) s.p |= 0x40;
                if (d_flag) s.p |= 0x08;
                if (i_flag) s.p |= 0x04;
                if (z_flag == 0) s.p |= 0x02;
                if (c_flag) s.p |= 0x01;

                s.ddr = ddr;
                s.pr = pr;

                s.pc = pc;
                s.sp = sp;

                s.intr = interrupt;

                s.nmi_state = nmi_state;
                s.dfff_byte = dfff_byte;
                s.instruction_complete = (state == 0);

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

                ddr = value.ddr;
                pr = value.pr;
                //new_config();

                pc = value.pc;
                sp = value.sp;

                interrupt = value.intr;
                nmi_state = value.nmi_state;
                dfff_byte = value.dfff_byte;
                if (value.instruction_complete)
                    state = 0;
            }
        }

        public byte ExtReadByte(UInt16 adr)
        {
            // Save old memory configuration
            bool bi = basic_in, ki = kernal_in, ci = char_in, ii = io_in;

            // Set new configuration
            basic_in = (ExtConfig & 3) != 0;
            kernal_in = (ExtConfig & 2) != 0;
            char_in = ((ExtConfig & 3) != 0) && (~(ExtConfig & 4) != 0);
            io_in = (ExtConfig & 3) != 0 && (ExtConfig & 4) != 0;

            // Read byte
            byte abyte = read_byte(adr);

            // Restore old configuration
            basic_in = bi; kernal_in = ki; char_in = ci; io_in = ii;

            return abyte;
        }

        public void ExtWriteByte(UInt16 adr, byte abyte)
        {
            // Save old memory configuration
            bool bi = basic_in, ki = kernal_in, ci = char_in, ii = io_in;

            // Set new configuration
            basic_in = (ExtConfig & 3) != 0;
            kernal_in = (ExtConfig & 2) != 0;
            char_in = (ExtConfig & 3) != 0 && ~(ExtConfig & 4) != 0;
            io_in = (ExtConfig & 3) != 0 && (ExtConfig & 4) != 0;

            // Write byte
            write_byte(adr, abyte);

            // Restore old configuration
            basic_in = bi; kernal_in = ki; char_in = ci; io_in = ii;
        }

        public byte REUReadByte(UInt16 adr)
        {
            return read_byte(adr);
        }

        public void REUWriteByte(UInt16 adr, byte abyte)
        {
            write_byte(adr, abyte);
        }

        #region IRQ Triggers

        public void TriggerVICIRQ()
        {
            if (!(interrupt.VICIRQ == ByteBool.True || interrupt.CIAIRQ == ByteBool.True))
                first_irq_cycle = TheC64.CycleCounter;

            interrupt.VICIRQ = ByteBool.True;
        }

        public void ClearVICIRQ()
        {
            interrupt.VICIRQ = ByteBool.False;
        }

        public void TriggerCIAIRQ()
        {
            if (!(interrupt.VICIRQ == ByteBool.True || interrupt.CIAIRQ == ByteBool.True))
                first_irq_cycle = TheC64.CycleCounter;

            interrupt.CIAIRQ = ByteBool.True;
        }

        public void ClearCIAIRQ()
        {
            interrupt.CIAIRQ = ByteBool.False;
        }

        public void TriggerNMI()
        {
            if (!nmi_state)
            {
                nmi_state = true;
                interrupt.NMI = ByteBool.True;
                first_nmi_cycle = TheC64.CycleCounter;
            }
        }

        public void ClearNMI()
        {
            interrupt.NMI = ByteBool.False;
        }

        #endregion IRQ Triggers

        #endregion Public Members

        #region Public Properties

        public bool BALow
        {
            get { return _BALow; }
            set { _BALow = value; }
        }

        public MOS6569 TheVIC
        {
            get { return _TheVIC; }
            set { _TheVIC = value; }
        }

        public MOS6581 TheSID
        {
            get { return _TheSID; }
            set { _TheSID = value; }
        }

        public MOS6526_1 TheCIA1
        {
            get { return _TheCIA1; }
            set { _TheCIA1 = value; }
        }

        public MOS6526_2 TheCIA2
        {
            get { return _TheCIA2; }
            set { _TheCIA2 = value; }
        }

        public REU TheREU
        {
            get { return _TheREU; }
            set { _TheREU = value; }
        }

        public IEC TheIEC
        {
            get { return _TheIEC; }
            set { _TheIEC = value; }
        }

        public int ExtConfig
        {
            get { return _extConfig; }
            set { _extConfig = value; }
        }

        #endregion Public Properties

        #region Private members

        internal byte read_byte(UInt16 adr)
        {
            if (adr < 0xa000)
            {
                if (adr >= 2)
                    return ram[adr];
                else if (adr == 0)
                    return ddr;
                else
                    return (byte)((ddr & pr) | (~ddr & 0x17));
            }
            else
                return read_byte_io(adr);
        }

        byte read_byte_io(UInt16 adr)
        {
            switch (adr >> 12)
            {
                case 0xa:
                case 0xb:
                    if (basic_in)
                        return basic_rom[adr & 0x1fff];
                    else
                        return ram[adr];

                case 0xc:
                    return ram[adr];

                case 0xd:
                    if (io_in)
                    {
                        switch ((adr >> 8) & 0x0f)
                        {
                            case 0x0:	// VIC
                            case 0x1:
                            case 0x2:
                            case 0x3:
                                return TheVIC.ReadRegister((UInt16)(adr & 0x3f));
                            case 0x4:	// SID
                            case 0x5:
                            case 0x6:
                            case 0x7:
                                return TheSID.ReadRegister((UInt16)(adr & 0x1f));
                            case 0x8:	// Color RAM
                            case 0x9:
                            case 0xa:
                            case 0xb:
                                return (byte)(color_ram[adr & 0x03ff] & 0x0f | TheVIC.LastVICByte & 0xf0);
                            case 0xc:	// CIA 1
                                return TheCIA1.ReadRegister((UInt16)(adr & 0x0f));
                            case 0xd:	// CIA 2
                                return TheCIA2.ReadRegister((UInt16)(adr & 0x0f));
                            case 0xe:	// REU/Open I/O
                            case 0xf:
                                if ((adr & 0xfff0) == 0xdf00)
                                    return TheREU.ReadRegister((UInt16)(adr & 0x0f));
                                else if (adr < 0xdfa0)
                                    return TheVIC.LastVICByte;
                                else
                                    return read_emulator_id((UInt16)(adr & 0x7f));
                        }
                    }
                    else if (char_in)
                    {
                        return char_rom[adr & 0x0fff];
                    }

                    return ram[adr];
                case 0xe:
                case 0xf:
                    if (kernal_in)
                        return kernel_rom[adr & 0x1fff];
                    else
                        return ram[adr];
                default:	// Can't happen
                    return 0;
            }
        }

        UInt16 read_word(UInt16 adr)
        {
            return (UInt16)(read_byte(adr) | (read_byte((UInt16)(adr + 1)) << 8));
        }

        void write_byte(UInt16 adr, byte abyte)
        {
            if (adr < 0xd000)
            {
                if (adr >= 2)
                    ram[adr] = abyte;
                else if (adr == 0)
                {
                    ddr = abyte;
                    ram[0] = TheVIC.LastVICByte;
                    new_config();
                }
                else
                {
                    pr = abyte;
                    ram[1] = TheVIC.LastVICByte;
                    new_config();
                }
            }
            else
                write_byte_io(adr, abyte);
        }

        void write_byte_io(UInt16 adr, byte abyte)
        {
            if (adr >= 0xe000)
            {
                ram[adr] = abyte;
                if (adr == 0xff00)
                    TheREU.FF00Trigger();
            }
            else if (io_in)
                switch ((adr >> 8) & 0x0f)
                {
                    case 0x0:	// VIC
                    case 0x1:
                    case 0x2:
                    case 0x3:
                        TheVIC.WriteRegister((UInt16)(adr & 0x3f), abyte);
                        return;
                    case 0x4:	// SID
                    case 0x5:
                    case 0x6:
                    case 0x7:
                        TheSID.WriteRegister((UInt16)(adr & 0x1f), abyte);
                        return;
                    case 0x8:	// Color RAM
                    case 0x9:
                    case 0xa:
                    case 0xb:
                        color_ram[adr & 0x03ff] = (byte)(abyte & 0x0f);
                        return;
                    case 0xc:	// CIA 1
                        TheCIA1.WriteRegister((UInt16)(adr & 0x0f), abyte);
                        return;
                    case 0xd:	// CIA 2
                        TheCIA2.WriteRegister((UInt16)(adr & 0x0f), abyte);
                        return;
                    case 0xe:	// REU/Open I/O
                    case 0xf:
                        if ((adr & 0xfff0) == 0xdf00)
                            TheREU.WriteRegister((UInt16)(adr & 0x0f), abyte);
                        return;
                }
            else
                ram[adr] = abyte;
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

            // Check if memory configuration may have changed.
            if (adr < 2)
                new_config();
        }

        void new_config()
        {
            byte port = (byte)(~ddr | pr);

            basic_in = (port & 3) == 3;
            kernal_in = (port & 2) != 0;
            char_in = ((port & 3) != 0) && !((port & 4) != 0);
            io_in = ((port & 3) != 0) && ((port & 4) != 0);
        }

        void jump(UInt16 adr)
        {
            pc = adr;
        }

        void illegal_op(byte op, UInt16 at)
        {
            throw new IllegalOpcodeException(String.Format("Illegal opcode 0x{0:X2} at 0x{1:X4}.", op, at));

            // Catch this exception and do this:
            // the_c64->Reset();
            // Reset();
        }

        void illegal_jump(UInt16 at, UInt16 to)
        {
            // "Jump to I/O space at %04x to %04x.", at, to
            throw new NotImplementedException();
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

        readonly char[] frodo_id = "FRODO\r(C) 1994-1997 CHRISTIAN BAUER".ToCharArray();
        byte FRODO_REVISION = 0x00;
        byte FRODO_VERSION = 0x04;

        byte read_emulator_id(UInt16 adr)
        {
            switch (adr)
            {
                case 0x7c:	// $dffc: revision
                    return (byte)(FRODO_REVISION << 4);
                case 0x7d:	// $dffd: version
                    return FRODO_VERSION;
                case 0x7e:	// $dffe returns 'F' (Frodo ID)
                    return (byte)'F';
                case 0x7f:	// $dfff alternates between $55 and $aa
                    dfff_byte = (byte)~dfff_byte;
                    return dfff_byte;
                default:
                    return adr - 0x20 >= frodo_id.Length ? (byte)0x00 : (byte)frodo_id[adr - 0x20];
            }
        }

        #endregion

        #region Private Fields

        int _extConfig;	                    // Memory configuration for ExtRead/WriteByte (0..7)

        bool _BALow;

        C64 TheC64;

        MOS6569 _TheVIC;
        MOS6581 _TheSID;
        MOS6526_1 _TheCIA1;
        MOS6526_2 _TheCIA2;
        REU _TheREU;
        IEC _TheIEC;

        byte[] ram, basic_rom, kernel_rom, char_rom, color_ram;

        InterruptState6510 interrupt;
        bool nmi_state;                     // state of NMI line

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

        // cycle codes
        UInt32 first_irq_cycle,
            first_nmi_cycle;

        byte state, op;		                // Current state and opcode
        UInt16 ar, ar2;			            // Address registers
        byte rdbuf;			                // Data buffer for RMW instructions
        byte ddr, pr;			            // Processor port

        bool basic_in, kernal_in, char_in, io_in;
        byte dfff_byte;

        #endregion
    }

    public enum InterruptType
    {
        None,
        VICIRQ,
        CIAIRQ,
        NMI,
        Reset
    }

    public enum ByteBool : byte
    {
        False = 0x00,
        True = 0x01
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InterruptState6510
    {
        [FieldOffset(0)]
        public ByteBool VICIRQ;

        [FieldOffset(1)]
        public ByteBool CIAIRQ;

        [FieldOffset(2)]
        public ByteBool NMI;

        [FieldOffset(3)]
        public ByteBool RESET;

        [FieldOffset(0)]
        public UInt32 ANY;
    }

    public class MOS6510Exception : Exception
    {
        public MOS6510Exception(string Message) : base(Message) { }
    }

    public class IllegalOpcodeException : MOS6510Exception
    {
        public IllegalOpcodeException(string Message)
            : base(Message)
        {
        }
    }

    public class IllegalJumpException : MOS6510Exception
    {
        public IllegalJumpException(string Message)
            : base(Message)
        {
        }
    }
}
