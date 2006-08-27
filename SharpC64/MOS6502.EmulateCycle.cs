// ************************************************************
// ***                      WARNING                         *** 
// *** This file is a copy of the MOS6510.EmulateCycle.cs   ***
// *** file, with the following pragma and #define          ***
// *** included, to generate the appropriate 1541 emulation ***
// ***                                                      ***
// ************************************************************ 
#define IS_CPU_1541
#pragma warning disable 0162

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
#if !IS_CPU_1541
    public partial class MOS6510
#else
    public partial class MOS6502_1541
#endif
    {
        public byte StatusByte
        {
            get
            {
                byte data = (byte)(0x20 | (n_flag & 0x80));
                if (v_flag) data |= 0x40;
                if (d_flag) data |= 0x08;
                if (i_flag) data |= 0x04;
                if (z_flag == 0) data |= 0x02;
                if (c_flag) data |= 0x01;

                return data;
            }
        }

        public string StatusString
        {
            get
            {
                byte status = StatusByte;

                bool carry = ((status & 0x01) != 0);
                bool zero = ((status & 0x02) != 0);
                bool disableInterupt = ((status & 0x04) != 0);
                bool decimal_Renamed = ((status & 0x08) != 0);
                bool brk = ((status & 0x10) != 0);
                bool overflow = ((status & 0x40) != 0);
                bool sign = ((status & 0x80) != 0);

                StringBuilder str = new StringBuilder();

                str.Append(carry ? "C" : "-");
                str.Append(zero ? "Z" : "-");
                str.Append(disableInterupt ? "I" : "-");
                str.Append(decimal_Renamed ? "D" : "-");
                str.Append(brk ? "B" : "-");
                str.Append(overflow ? "O" : "-");
                str.Append(sign ? "S" : "-");

                return str.ToString();
            }
        }

#if DEBUG_INSTRUCTIONS
        byte lastState = 0xff;
        uint startlogCycle = 0;
        bool log = false;
        UInt16 startLog = 0xEA0E;
        bool logging = false;

        UInt16 breakPoint = 0xEA87;
#endif

#if TIMERS
        public uint ins_counter = 0;
#endif
        public void EmulateCycle()
        {

#if TIMERS
            if (state == 0)
            {
                ins_counter++;
            }
#endif

#if DEBUG_INSTRUCTIONS
            if (logging)
            {
                if (!log && pc == startLog)
                {
                    log = true;
                    startlogCycle = TheC64.CycleCounter - 1;
                }

                if (log && lastState == 0 && TheC64.CycleCounter > startlogCycle)
                {
                    Console.WriteLine("{0:X4} {1:X2}    acc:{2:X2} x:{3:X2} y:{4:X2} St:{5}", pc - 1, op, a, x, y, StatusString);
                    lastState = 0xff;
                    if (TheC64.CycleCounter - startlogCycle > 1000000)
                        System.Environment.Exit(0);
                }
            }
#endif

            byte data, tmp;

            // Any pending interrupts in state 0 (opcode fetch)?
            if (state == 0 && interrupt.ANY != 0)
            {
#if !IS_CPU_1541
                if (interrupt.RESET == ByteBool.True)
                    Reset();
                else if (interrupt.NMI == ByteBool.True && (TheC64.CycleCounter - first_nmi_cycle >= 2))
                {
                    interrupt.NMI = ByteBool.False;	// Simulate an edge-triggered input
                    state = 0x0010;
                }
                else if ((interrupt.VICIRQ == ByteBool.True || interrupt.CIAIRQ == ByteBool.True) && (TheC64.CycleCounter - first_irq_cycle >= 2) && !i_flag)
                    state = 0x0008;
#else
                if (interrupt.RESET == ByteBool.True)
                    Reset();
                else if ((interrupt.VIA1IRQ == ByteBool.True || interrupt.VIA2IRQ == ByteBool.True || interrupt.IECIRQ == ByteBool.True) && (TheC64.CycleCounter - first_irq_cycle >= 2) && !i_flag)
                    state = 0x0008;
#endif
            }

            switch ((CPUState)state)
            {


                // Opcode fetch (cycle 0)
                case 0:
                    if (BALow)
                        return;
                    op = read_byte(pc++);
                    state = (byte)CPUCommon.ModeTab[op];
#if DEBUG_INSTRUCTIONS
                    lastState = 0;
#endif
                    break;

                // IRQ
                case (CPUState)0x08:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = 0x0009;
                    break;

                case (CPUState)0x09:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = 0x000a;
                    break;

                case (CPUState)0x0a:
                    write_byte((UInt16)(sp-- | 0x100), (byte)(pc >> 8));
                    state = 0x000b;
                    break;

                case (CPUState)0x0b:
                    write_byte((UInt16)(sp-- | 0x100), (byte)pc);
                    state = 0x000c;
                    break;

                case (CPUState)0x0c:
                    data = (byte)(0x20 | (n_flag & 0x80));
                    if (v_flag) data |= 0x40;
                    if (d_flag) data |= 0x08;
                    if (i_flag) data |= 0x04;
                    if (z_flag == 0) data |= 0x02;
                    if (c_flag) data |= 0x01;
                    write_byte((UInt16)(sp-- | 0x100), data);
                    i_flag = true;
                    state = 0x000d;
                    break;

                case (CPUState)0x0d:
                    if (BALow)
                        return;
                    pc = read_byte(0xfffe);
                    state = 0x000e;
                    break;

                case (CPUState)0x0e:
                    if (BALow)
                        return;
                    data = read_byte(0xffff);
                    pc |= (UInt16)(data << 8);
                    state = 0; break; // Last macro;


                // NMI
                case (CPUState)0x10:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = 0x0011;
                    break;

                case (CPUState)0x11:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = 0x0012;
                    break;

                case (CPUState)0x12:
                    write_byte((UInt16)(sp-- | 0x100), (byte)(pc >> 8));
                    state = 0x0013;
                    break;

                case (CPUState)0x13:
                    write_byte((UInt16)(sp-- | 0x100), (byte)pc);
                    state = 0x0014;
                    break;

                case (CPUState)0x14:
                    data = (byte)(0x20 | (n_flag & 0x80));
                    if (v_flag) data |= 0x40;
                    if (d_flag) data |= 0x08;
                    if (i_flag) data |= 0x04;
                    if (z_flag == 0) data |= 0x02;
                    if (c_flag) data |= 0x01;
                    write_byte((UInt16)(sp-- | 0x100), data);
                    i_flag = true;
                    state = 0x0015;
                    break;

                case (CPUState)0x15:
                    if (BALow)
                        return;
                    pc = read_byte(0xfffa);
                    state = 0x0016;
                    break;

                case (CPUState)0x16:
                    if (BALow)
                        return;
                    data = read_byte(0xfffb);
                    pc |= (UInt16)(data << 8);
                    state = 0; break; // Last macro;


                // Addressing modes: Fetch effective address, no extra cycles (-> ar)
                case CPUState.A_ZERO:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUCommon.OpTab[op]; break;

                case CPUState.A_ZEROX:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.A_ZEROX1;
                    break;

                case CPUState.A_ZEROX1:
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar = (UInt16)((ar + x) & 0xff);
                    state = (byte)CPUCommon.OpTab[op]; break;

                case CPUState.A_ZEROY:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.A_ZEROY1;
                    break;

                case CPUState.A_ZEROY1:
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar = (UInt16)((ar + y) & 0xff);
                    state = (byte)CPUCommon.OpTab[op]; break;

                case CPUState.A_ABS:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.A_ABS1;
                    break;
                case CPUState.A_ABS1:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    ar = (UInt16)(ar | (data << 8));
                    state = (byte)CPUCommon.OpTab[op]; break;

                case CPUState.A_ABSX:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.A_ABSX1;
                    break;
                case CPUState.A_ABSX1:
                    if (BALow)
                        return;
                    ar2 = read_byte(pc++);	// Note: Some undocumented opcodes rely on the value of ar2
                    if (ar + x < 0x100)
                        state = (byte)CPUState.A_ABSX2;
                    else
                        state = (byte)CPUState.A_ABSX3;
                    ar = (UInt16)((ar + x) & 0xff | (ar2 << 8));
                    break;
                case CPUState.A_ABSX2:	// No page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    state = (byte)CPUCommon.OpTab[op]; break;
                case CPUState.A_ABSX3:	// Page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar += 0x100;
                    state = (byte)CPUCommon.OpTab[op]; break;

                case CPUState.A_ABSY:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.A_ABSY1;
                    break;
                case CPUState.A_ABSY1:
                    if (BALow)
                        return;
                    ar2 = read_byte(pc++);	// Note: Some undocumented opcodes rely on the value of ar2
                    if (ar + y < 0x100)
                        state = (byte)CPUState.A_ABSY2;
                    else
                        state = (byte)CPUState.A_ABSY3;
                    ar = (UInt16)((ar + y) & 0xff | (ar2 << 8));
                    break;
                case CPUState.A_ABSY2:	// No page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    state = (byte)CPUCommon.OpTab[op]; break;
                case CPUState.A_ABSY3:	// Page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar += 0x100;
                    state = (byte)CPUCommon.OpTab[op]; break;

                case CPUState.A_INDX:
                    if (BALow)
                        return;
                    ar2 = read_byte(pc++);
                    state = (byte)CPUState.A_INDX1;
                    break;
                case CPUState.A_INDX1:
                    if (BALow)
                        return;
                    read_byte(ar2);
                    ar2 = (UInt16)((ar2 + x) & 0xff);
                    state = (byte)CPUState.A_INDX2;
                    break;
                case CPUState.A_INDX2:
                    if (BALow)
                        return;
                    ar = read_byte(ar2);
                    state = (byte)CPUState.A_INDX3;
                    break;
                case CPUState.A_INDX3:
                    if (BALow)
                        return;
                    data = read_byte((UInt16)((ar2 + 1) & 0xff));
                    ar = (UInt16)(ar | (data << 8));
                    state = (byte)CPUCommon.OpTab[op]; break;

                case CPUState.A_INDY:
                    if (BALow)
                        return;
                    ar2 = read_byte(pc++);
                    state = (byte)CPUState.A_INDY1;
                    break;
                case CPUState.A_INDY1:
                    if (BALow)
                        return;
                    ar = read_byte(ar2);
                    state = (byte)CPUState.A_INDY2;
                    break;
                case CPUState.A_INDY2:
                    if (BALow)
                        return;
                    ar2 = read_byte((UInt16)((ar2 + 1) & 0xff));	// Note: Some undocumented opcodes rely on the value of ar2
                    if (ar + y < 0x100)
                        state = (byte)CPUState.A_INDY3;
                    else
                        state = (byte)CPUState.A_INDY4;
                    ar = (UInt16)((ar + y) & 0xff | (ar2 << 8));
                    break;
                case CPUState.A_INDY3:	// No page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    state = (byte)CPUCommon.OpTab[op]; break;
                case CPUState.A_INDY4:	// Page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar += 0x100;
                    state = (byte)CPUCommon.OpTab[op]; break;


                // Addressing modes: Fetch effective address, extra cycle on page crossing (-> ar)
                case CPUState.AE_ABSX:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.AE_ABSX1;
                    break;
                case CPUState.AE_ABSX1:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (ar + x < 0x100)
                    {
                        ar = (UInt16)((ar + x) & 0xff | (data << 8));
                        state = (byte)CPUCommon.OpTab[op]; break;
                    }
                    else
                    {
                        ar = (UInt16)((ar + x) & 0xff | (data << 8));
                        state = (byte)CPUState.AE_ABSX2;
                    }
                    break;
                case CPUState.AE_ABSX2:	// Page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar += 0x100;
                    state = (byte)CPUCommon.OpTab[op]; break;

                case CPUState.AE_ABSY:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.AE_ABSY1;
                    break;
                case CPUState.AE_ABSY1:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (ar + y < 0x100)
                    {
                        ar = (UInt16)((ar + y) & 0xff | (data << 8));
                        state = (byte)CPUCommon.OpTab[op]; break;
                    }
                    else
                    {
                        ar = (UInt16)((ar + y) & 0xff | (data << 8));
                        state = (byte)CPUState.AE_ABSY2;
                    }
                    break;
                case CPUState.AE_ABSY2:	// Page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar += 0x100;
                    state = (byte)CPUCommon.OpTab[op]; break;

                case CPUState.AE_INDY:
                    if (BALow)
                        return;
                    ar2 = read_byte(pc++);
                    state = (byte)CPUState.AE_INDY1;
                    break;
                case CPUState.AE_INDY1:
                    if (BALow)
                        return;
                    ar = read_byte(ar2);
                    state = (byte)CPUState.AE_INDY2;
                    break;
                case CPUState.AE_INDY2:
                    if (BALow)
                        return;
                    data = read_byte((UInt16)((ar2 + 1) & 0xff));
                    if (ar + y < 0x100)
                    {
                        ar = (UInt16)((ar + y) & 0xff | (data << 8));
                        state = (byte)CPUCommon.OpTab[op]; break;
                    }
                    else
                    {
                        ar = (UInt16)((ar + y) & 0xff | (data << 8));
                        state = (byte)CPUState.AE_INDY3;
                    }
                    break;

                case CPUState.AE_INDY3:	// Page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar += 0x100;
                    state = (byte)CPUCommon.OpTab[op]; break;


                // Addressing modes: Read operand, write it back, no extra cycles (-> ar, rdbuf)
                case CPUState.M_ZERO:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.RMW_DO_IT; break;

                case CPUState.M_ZEROX:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.M_ZEROX1;
                    break;
                case CPUState.M_ZEROX1:
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar = (UInt16)((ar + x) & 0xff);
                    state = (byte)CPUState.RMW_DO_IT; break;

                case CPUState.M_ZEROY:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.M_ZEROY1;
                    break;
                case CPUState.M_ZEROY1:
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar = (UInt16)((ar + y) & 0xff);
                    state = (byte)CPUState.RMW_DO_IT; break;

                case CPUState.M_ABS:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.M_ABS1;
                    break;
                case CPUState.M_ABS1:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    ar = (UInt16)(ar | (data << 8));
                    state = (byte)CPUState.RMW_DO_IT; break;

                case CPUState.M_ABSX:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.M_ABSX1;
                    break;
                case CPUState.M_ABSX1:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (ar + x < 0x100)
                        state = (byte)CPUState.M_ABSX2;
                    else
                        state = (byte)CPUState.M_ABSX3;
                    ar = (UInt16)((ar + x) & 0xff | (data << 8));
                    break;
                case CPUState.M_ABSX2:	// No page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    state = (byte)CPUState.RMW_DO_IT; break;
                case CPUState.M_ABSX3:	// Page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar += 0x100;
                    state = (byte)CPUState.RMW_DO_IT; break;

                case CPUState.M_ABSY:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.M_ABSY1;
                    break;
                case CPUState.M_ABSY1:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (ar + y < 0x100)
                        state = (byte)CPUState.M_ABSY2;
                    else
                        state = (byte)CPUState.M_ABSY3;
                    ar = (UInt16)((ar + y) & 0xff | (data << 8));
                    break;
                case CPUState.M_ABSY2:	// No page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    state = (byte)CPUState.RMW_DO_IT; break;
                case CPUState.M_ABSY3:	// Page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar += 0x100;
                    state = (byte)CPUState.RMW_DO_IT; break;

                case CPUState.M_INDX:
                    if (BALow)
                        return;
                    ar2 = read_byte(pc++);
                    state = (byte)CPUState.M_INDX1;
                    break;
                case CPUState.M_INDX1:
                    if (BALow)
                        return;
                    read_byte(ar2);
                    ar2 = (UInt16)((ar2 + x) & 0xff);
                    state = (byte)CPUState.M_INDX2;
                    break;
                case CPUState.M_INDX2:
                    if (BALow)
                        return;
                    ar = read_byte(ar2);
                    state = (byte)CPUState.M_INDX3;
                    break;
                case CPUState.M_INDX3:
                    if (BALow)
                        return;
                    data = read_byte((UInt16)((ar2 + 1) & 0xff));
                    ar = (UInt16)(ar | (data << 8));
                    state = (byte)CPUState.RMW_DO_IT; break;

                case CPUState.M_INDY:
                    if (BALow)
                        return;
                    ar2 = read_byte(pc++);
                    state = (byte)CPUState.M_INDY1;
                    break;
                case CPUState.M_INDY1:
                    if (BALow)
                        return;
                    ar = read_byte(ar2);
                    state = (byte)CPUState.M_INDY2;
                    break;
                case CPUState.M_INDY2:
                    if (BALow)
                        return;
                    data = read_byte((UInt16)((ar2 + 1) & 0xff));
                    if (ar + y < 0x100)
                        state = (byte)CPUState.M_INDY3;
                    else
                        state = (byte)CPUState.M_INDY4;
                    ar = (UInt16)((ar + y) & 0xff | (data << 8));
                    break;
                case CPUState.M_INDY3:	// No page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    state = (byte)CPUState.RMW_DO_IT; break;
                case CPUState.M_INDY4:	// Page crossed
                    if (BALow)
                        return;
                    read_byte(ar);
                    ar += 0x100;
                    state = (byte)CPUState.RMW_DO_IT; break;

                case CPUState.RMW_DO_IT:
                    if (BALow)
                        return;
                    rdbuf = read_byte(ar);
                    state = (byte)CPUState.RMW_DO_IT1;
                    break;
                case CPUState.RMW_DO_IT1:
                    write_byte(ar, rdbuf);
                    state = (byte)CPUCommon.OpTab[op]; break;


                // Load group
                case CPUState.O_LDA:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (a = data);
                    state = 0; break; // Last macro;
                case CPUState.O_LDA_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (a = data);
                    state = 0; break; // Last macro;

                case CPUState.O_LDX:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (x = data);
                    state = 0; break; // Last macro;
                case CPUState.O_LDX_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (x = data);
                    state = 0; break; // Last macro;

                case CPUState.O_LDY:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (y = data);
                    state = 0; break; // Last macro;
                case CPUState.O_LDY_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (y = data);
                    state = 0; break; // Last macro;


                // Store group
                case CPUState.O_STA:
                    write_byte(ar, a);
                    state = 0; break; // Last macro;

                case CPUState.O_STX:
                    write_byte(ar, x);
                    state = 0; break; // Last macro;

                case CPUState.O_STY:
                    write_byte(ar, y);
                    state = 0; break; // Last macro;


                // Transfer group
                case CPUState.O_TAX:
                    if (BALow)
                        return;
                    read_byte(pc);
                    z_flag = n_flag = (x = a);
                    state = 0; break; // Last macro;

                case CPUState.O_TXA:
                    if (BALow)
                        return;
                    read_byte(pc);
                    z_flag = n_flag = (a = x);
                    state = 0; break; // Last macro;

                case CPUState.O_TAY:
                    if (BALow)
                        return;
                    read_byte(pc);
                    z_flag = n_flag = (y = a);
                    state = 0; break; // Last macro;

                case CPUState.O_TYA:
                    if (BALow)
                        return;
                    read_byte(pc);
                    z_flag = n_flag = (a = y);
                    state = 0; break; // Last macro;

                case CPUState.O_TSX:
                    if (BALow)
                        return;
                    read_byte(pc);
                    z_flag = n_flag = (x = sp);
                    state = 0; break; // Last macro;

                case CPUState.O_TXS:
                    if (BALow)
                        return;
                    read_byte(pc);
                    sp = x;
                    state = 0; break; // Last macro;


                // Arithmetic group
                case CPUState.O_ADC:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    do_adc(data);
                    state = 0; break; // Last macro;
                case CPUState.O_ADC_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    do_adc(data);
                    state = 0; break; // Last macro;

                case CPUState.O_SBC:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    do_sbc(data);
                    state = 0; break; // Last macro;
                case CPUState.O_SBC_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    do_sbc(data);
                    state = 0; break; // Last macro;


                // Increment/decrement group
                case CPUState.O_INX:
                    if (BALow)
                        return;
                    read_byte(pc);
                    z_flag = n_flag = (++x);
                    state = 0; break; // Last macro;

                case CPUState.O_DEX:
                    if (BALow)
                        return;
                    read_byte(pc);
                    z_flag = n_flag = (--x);
                    state = 0; break; // Last macro;

                case CPUState.O_INY:
                    if (BALow)
                        return;
                    read_byte(pc);
                    z_flag = n_flag = (++y);
                    state = 0; break; // Last macro;

                case CPUState.O_DEY:
                    if (BALow)
                        return;
                    read_byte(pc);
                    z_flag = n_flag = (--y);
                    state = 0; break; // Last macro;

                case CPUState.O_INC:
                    z_flag = n_flag = (byte)(rdbuf + 1);
                    write_byte(ar, (byte)(rdbuf + 1));
                    state = 0; break; // Last macro;

                case CPUState.O_DEC:
                    z_flag = n_flag = (byte)(rdbuf - 1);
                    write_byte(ar, (byte)(rdbuf - 1));
                    state = 0; break; // Last macro;


                // Logic group
                case CPUState.O_AND:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (a &= data);
                    state = 0; break; // Last macro;
                case CPUState.O_AND_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (a &= data);
                    state = 0; break; // Last macro;

                case CPUState.O_ORA:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (a |= data);
                    state = 0; break; // Last macro;
                case CPUState.O_ORA_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (a |= data);
                    state = 0; break; // Last macro;

                case CPUState.O_EOR:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (a ^= data);
                    state = 0; break; // Last macro;
                case CPUState.O_EOR_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (a ^= data);
                    state = 0; break; // Last macro;

                // Compare group
                case CPUState.O_CMP:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (byte)(ar = (UInt16)(a - data));
                    c_flag = ar < 0x100;
                    state = 0; break; // Last macro;
                case CPUState.O_CMP_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (byte)(ar = (UInt16)(a - data));
                    c_flag = ar < 0x100;
                    state = 0; break; // Last macro;

                case CPUState.O_CPX:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (byte)(ar = (UInt16)(x - data));
                    c_flag = ar < 0x100;
                    state = 0; break; // Last macro;
                case CPUState.O_CPX_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (byte)(ar = (UInt16)(x - data));
                    c_flag = ar < 0x100;
                    state = 0; break; // Last macro;

                case CPUState.O_CPY:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (byte)(ar = (UInt16)(y - data));
                    c_flag = ar < 0x100;
                    state = 0; break; // Last macro;
                case CPUState.O_CPY_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (byte)(ar = (UInt16)(y - data));
                    c_flag = ar < 0x100;
                    state = 0; break; // Last macro;


                // Bit-test group
                case CPUState.O_BIT:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = (byte)(a & data);
                    n_flag = data;
                    v_flag = (data & 0x40) != 0;
                    state = 0; break; // Last macro;


                // Shift/rotate group
                case CPUState.O_ASL:
                    c_flag = (rdbuf & 0x80) != 0;
                    z_flag = n_flag = (byte)(rdbuf << 1);
                    write_byte(ar, (byte)(rdbuf << 1));
                    state = 0; break; // Last macro;
                case CPUState.O_ASL_A:
                    if (BALow)
                        return;
                    read_byte(pc);
                    c_flag = (a & 0x80) != 0;
                    z_flag = n_flag = (byte)(a <<= 1);
                    state = 0; break; // Last macro;

                case CPUState.O_LSR:
                    c_flag = (rdbuf & 0x01) != 0;
                    z_flag = n_flag = (byte)(rdbuf >> 1);
                    write_byte(ar, (byte)(rdbuf >> 1));
                    state = 0; break; // Last macro;
                case CPUState.O_LSR_A:
                    if (BALow)
                        return;
                    read_byte(pc);
                    c_flag = (a & 0x01) != 0;
                    z_flag = n_flag = (byte)(a >>= 1);
                    state = 0; break; // Last macro;

                case CPUState.O_ROL:
                    z_flag = n_flag = (byte)(c_flag ? (rdbuf << 1) | 0x01 : rdbuf << 1);
                    write_byte(ar, (byte)(c_flag ? (rdbuf << 1) | 0x01 : rdbuf << 1));
                    c_flag = (rdbuf & 0x80) != 0;
                    state = 0; break; // Last macro;
                case CPUState.O_ROL_A:
                    if (BALow)
                        return;
                    read_byte(pc);
                    data = (byte)(a & 0x80);
                    z_flag = n_flag = (byte)(a = (byte)(c_flag ? (a << 1) | 0x01 : a << 1));
                    c_flag = data != 0;
                    state = 0; break; // Last macro;

                case CPUState.O_ROR:
                    z_flag = n_flag = (byte)(c_flag ? (rdbuf >> 1) | 0x80 : rdbuf >> 1);
                    write_byte(ar, (byte)(c_flag ? (rdbuf >> 1) | 0x80 : rdbuf >> 1));
                    c_flag = (rdbuf & 0x01) != 0;
                    state = 0; break; // Last macro;
                case CPUState.O_ROR_A:
                    if (BALow)
                        return;
                    read_byte(pc);
                    data = (byte)(a & 0x01);
                    z_flag = n_flag = (byte)(a = (byte)(c_flag ? (a >> 1) | 0x80 : a >> 1));
                    c_flag = data != 0;
                    state = 0; break; // Last macro;


                // Stack group
                case CPUState.O_PHA:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = (byte)CPUState.O_PHA1;
                    break;
                case CPUState.O_PHA1:
                    write_byte((UInt16)(sp-- | 0x100), a);
                    state = 0; break; // Last macro;

                case CPUState.O_PLA:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = (byte)CPUState.O_PLA1;
                    break;
                case CPUState.O_PLA1:
                    if (BALow)
                        return;
                    read_byte((UInt16)(sp++ | 0x100));
                    state = (byte)CPUState.O_PLA2;
                    break;
                case CPUState.O_PLA2:
                    if (BALow)
                        return;
                    data = read_byte((UInt16)(sp | 0x100));
                    z_flag = n_flag = (byte)(a = data);
                    state = 0; break; // Last macro;

                case CPUState.O_PHP:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = (byte)CPUState.O_PHP1;
                    break;
                case CPUState.O_PHP1:
                    data = (byte)(0x20 | (n_flag & 0x80));
                    if (v_flag) data |= 0x40;
                    if (true) data |= 0x10;
                    if (d_flag) data |= 0x08;
                    if (i_flag) data |= 0x04;
                    if (z_flag == 0) data |= 0x02;
                    if (c_flag) data |= 0x01;
                    write_byte((UInt16)(sp-- | 0x100), data);
                    state = 0; break; // Last macro;

                case CPUState.O_PLP:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = (byte)CPUState.O_PLP1;
                    break;
                case CPUState.O_PLP1:
                    if (BALow)
                        return;
                    read_byte((UInt16)(sp++ | 0x100));
                    state = (byte)CPUState.O_PLP2;
                    break;
                case CPUState.O_PLP2:
                    if (BALow)
                        return;
                    data = read_byte((UInt16)(sp | 0x100));
                    n_flag = data;
                    v_flag = (data & 0x40) != 0;
                    d_flag = (data & 0x08) != 0;
                    i_flag = (data & 0x04) != 0;
                    z_flag = (byte)((data & 0x02) ^ 0x02);
                    c_flag = (data & 0x01) != 0;
                    state = 0; break; // Last macro;


                // Jump/branch group
                case CPUState.O_JMP:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.O_JMP1;
                    break;
                case CPUState.O_JMP1:
                    if (BALow)
                        return;
                    data = read_byte(pc);
                    pc = (UInt16)((data << 8) | ar);
                    state = 0; break; // Last macro;

                case CPUState.O_JMP_I:
                    if (BALow)
                        return;
                    pc = read_byte(ar);
                    state = (byte)CPUState.O_JMP_I1;
                    break;
                case CPUState.O_JMP_I1:
                    if (BALow)
                        return;
                    data = read_byte((UInt16)((ar + 1) & 0xff | ar & 0xff00));
                    pc |= (UInt16)(data << 8);
                    state = 0; break; // Last macro;

                case CPUState.O_JSR:
                    if (BALow)
                        return;
                    ar = read_byte(pc++);
                    state = (byte)CPUState.O_JSR1;
                    break;
                case CPUState.O_JSR1:
                    if (BALow)
                        return;
                    read_byte((UInt16)(sp | 0x100));
                    state = (byte)CPUState.O_JSR2;
                    break;
                case CPUState.O_JSR2:
                    write_byte((UInt16)(sp-- | 0x100), (byte)(pc >> 8));
                    state = (byte)CPUState.O_JSR3;
                    break;
                case CPUState.O_JSR3:
                    write_byte((UInt16)(sp-- | 0x100), (byte)pc);
                    state = (byte)CPUState.O_JSR4;
                    break;
                case CPUState.O_JSR4:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    pc = (UInt16)(ar | (data << 8));
                    state = 0; break; // Last macro;

                case CPUState.O_RTS:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = (byte)CPUState.O_RTS1;
                    break;
                case CPUState.O_RTS1:
                    if (BALow)
                        return;
                    read_byte((UInt16)(sp++ | 0x100));
                    state = (byte)CPUState.O_RTS2;
                    break;
                case CPUState.O_RTS2:
                    if (BALow)
                        return;
                    pc = read_byte((UInt16)(sp++ | 0x100));
                    state = (byte)CPUState.O_RTS3;
                    break;
                case CPUState.O_RTS3:
                    if (BALow)
                        return;
                    data = read_byte((UInt16)(sp | 0x100));
                    pc |= (UInt16)(data << 8);
                    state = (byte)CPUState.O_RTS4;
                    break;
                case CPUState.O_RTS4:
                    if (BALow)
                        return;
                    read_byte(pc++);
                    state = 0; break; // Last macro;

                case CPUState.O_RTI:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = (byte)CPUState.O_RTI1;
                    break;
                case CPUState.O_RTI1:
                    if (BALow)
                        return;
                    read_byte((UInt16)(sp++ | 0x100));
                    state = (byte)CPUState.O_RTI2;
                    break;
                case CPUState.O_RTI2:
                    if (BALow)
                        return;
                    data = read_byte((UInt16)(sp | 0x100));
                    n_flag = data;
                    v_flag = (data & 0x40) != 0;
                    d_flag = (data & 0x08) != 0;
                    i_flag = (data & 0x04) != 0;
                    z_flag = (byte)((data & 0x02) ^ 0x02);
                    c_flag = (data & 0x01) != 0;
                    sp++;
                    state = (byte)CPUState.O_RTI3;
                    break;
                case CPUState.O_RTI3:
                    if (BALow)
                        return;
                    pc = read_byte((UInt16)(sp++ | 0x100));
                    state = (byte)CPUState.O_RTI4;
                    break;
                case CPUState.O_RTI4:
                    if (BALow)
                        return;
                    data = read_byte((UInt16)(sp | 0x100));
                    pc |= (UInt16)(data << 8);
                    state = 0; break; // Last macro;

                case CPUState.O_BRK:
                    if (BALow)
                        return;
                    read_byte(pc++);
                    state = (byte)CPUState.O_BRK1;
                    break;
                case CPUState.O_BRK1:
                    write_byte((UInt16)(sp-- | 0x100), (byte)(pc >> 8));
                    state = (byte)CPUState.O_BRK2;
                    break;
                case CPUState.O_BRK2:
                    write_byte((UInt16)(sp-- | 0x100), (byte)pc);
                    state = (byte)CPUState.O_BRK3;
                    break;
                case CPUState.O_BRK3:
                    data = (byte)(0x20 | (n_flag & 0x80));
                    if (v_flag) data |= 0x40;
                    if (true) data |= 0x10;
                    if (d_flag) data |= 0x08;
                    if (i_flag) data |= 0x04;
                    if (z_flag == 0) data |= 0x02;
                    if (c_flag) data |= 0x01;
                    write_byte((UInt16)(sp-- | 0x100), data);
                    i_flag = true;
#if !IS_CPU_1541
                    if (interrupt.NMI == ByteBool.True)
                    {			
                        // BRK interrupted by NMI?
                        interrupt.NMI = ByteBool.False;	// Simulate an edge-triggered input
                        state = 0x0015;						// Jump to NMI sequence
                        break;
                    }
#endif
                    state = (byte)CPUState.O_BRK4;
                    break;

                case CPUState.O_BRK4:
#if !IS_CPU_1541
                    first_nmi_cycle++;		// Delay NMI
#endif
                    if (BALow)
                        return;
                    pc = read_byte(0xfffe);
                    state = (byte)CPUState.O_BRK5;
                    break;

                case CPUState.O_BRK5:
                    if (BALow)
                        return;
                    data = read_byte(0xffff);
                    pc |= (UInt16)(data << 8);
                    state = 0; break; // Last macro;

                case CPUState.O_BCS:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (c_flag)
                    {
                        ar = (UInt16)(pc + (sbyte)data);
                        if ((ar >> 8) != (pc >> 8))
                        {
                            if ((data & 0x80) != 0)
                                state = (byte)CPUState.O_BRANCH_BP;
                            else
                                state = (byte)CPUState.O_BRANCH_FP;
                        }
                        else
                            state = (byte)CPUState.O_BRANCH_NP;
                    }
                    else
                        state = 0;
                    break;

                case CPUState.O_BCC:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (!c_flag)
                    {
                        ar = (UInt16)(pc + (sbyte)data);
                        if ((ar >> 8) != (pc >> 8))
                        {
                            if ((data & 0x80) != 0)
                                state = (byte)CPUState.O_BRANCH_BP;
                            else
                                state = (byte)CPUState.O_BRANCH_FP;
                        }
                        else
                            state = (byte)CPUState.O_BRANCH_NP;
                    }
                    else
                        state = 0;
                    break;

                case CPUState.O_BEQ:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (z_flag == 0)
                    {
                        ar = (UInt16)(pc + (sbyte)data);
                        if ((ar >> 8) != (pc >> 8))
                        {
                            if ((data & 0x80) != 0)
                                state = (byte)CPUState.O_BRANCH_BP;
                            else
                                state = (byte)CPUState.O_BRANCH_FP;
                        }
                        else
                            state = (byte)CPUState.O_BRANCH_NP;
                    }
                    else
                        state = 0;
                    break;

                case CPUState.O_BNE:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (z_flag != 0)
                    {
                        ar = (UInt16)(pc + (sbyte)data);
                        if ((ar >> 8) != (pc >> 8))
                        {
                            if ((data & 0x80) != 0)
                                state = (byte)CPUState.O_BRANCH_BP;
                            else
                                state = (byte)CPUState.O_BRANCH_FP;
                        }
                        else
                            state = (byte)CPUState.O_BRANCH_NP;
                    }
                    else
                        state = 0;
                    break;

                case CPUState.O_BVS:
#if !IS_CPU_1541
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (v_flag)
                    {
                        ar = (UInt16)(pc + (sbyte)data);
                        if ((ar >> 8) != (pc >> 8))
                        {
                            if ((data & 0x80) != 0)
                                state = (byte)CPUState.O_BRANCH_BP;
                            else
                                state = (byte)CPUState.O_BRANCH_FP;
                        }
                        else
                            state = (byte)CPUState.O_BRANCH_NP;
                    }
                    else
                        state = 0;
                    break;
#else
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (((via2_pcr & 0x0e) == 0x0e ? true : v_flag))
                    {
                        ar = (UInt16)(pc + (sbyte)data);
                        if ((ar >> 8) != (pc >> 8))
                        {
                            if ((data & 0x80) != 0)
                                state = (byte)CPUState.O_BRANCH_BP;
                            else
                                state = (byte)CPUState.O_BRANCH_FP;
                        }
                        else
                            state = (byte)CPUState.O_BRANCH_NP;
                    }
                    else
                        state = 0;
                    break;	// GCR byte ready flag
#endif

                case CPUState.O_BVC:
#if !IS_CPU_1541
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if (!v_flag)
                    {
                        ar = (UInt16)(pc + (sbyte)data);
                        if ((ar >> 8) != (pc >> 8))
                        {
                            if ((data & 0x80) != 0)
                                state = (byte)CPUState.O_BRANCH_BP;
                            else
                                state = (byte)CPUState.O_BRANCH_FP;
                        }
                        else
                            state = (byte)CPUState.O_BRANCH_NP;
                    }
                    else
                        state = 0;
                    break;
#else
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if ((!((via2_pcr & 0x0e) == 0x0e) ? false : v_flag))
                    {
                        ar = (UInt16)(pc + (sbyte)data);
                        if ((ar >> 8) != (pc >> 8))
                        {
                            if ((data & 0x80) != 0)
                                state = (byte)CPUState.O_BRANCH_BP;
                            else
                                state = (byte)CPUState.O_BRANCH_FP;
                        }
                        else
                            state = (byte)CPUState.O_BRANCH_NP;
                    }
                    else
                        state = 0;
                    break;	// GCR byte ready flag
#endif

                case CPUState.O_BMI:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if ((n_flag & 0x80) != 0)
                    {
                        ar = (UInt16)(pc + (sbyte)data);
                        if ((ar >> 8) != (pc >> 8))
                        {
                            if ((data & 0x80) != 0)
                                state = (byte)CPUState.O_BRANCH_BP;
                            else
                                state = (byte)CPUState.O_BRANCH_FP;
                        }
                        else
                            state = (byte)CPUState.O_BRANCH_NP;
                    }
                    else
                        state = 0;
                    break;

                case CPUState.O_BPL:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    if ((n_flag & 0x80) == 0)
                    {
                        ar = (UInt16)(pc + (sbyte)data);
                        if ((ar >> 8) != (pc >> 8))
                        {
                            if ((data & 0x80) != 0)
                                state = (byte)CPUState.O_BRANCH_BP;
                            else
                                state = (byte)CPUState.O_BRANCH_FP;
                        }
                        else
                            state = (byte)CPUState.O_BRANCH_NP;
                    }
                    else
                        state = 0;
                    break;

                case CPUState.O_BRANCH_NP:	// No page crossed
                    first_irq_cycle++;	// Delay IRQ
#if !IS_CPU_1541
                    first_nmi_cycle++;	// Delay NMI
#endif
                    if (BALow)
                        return;
                    read_byte(pc);
                    pc = ar;
                    state = 0; break; // Last macro;
                case CPUState.O_BRANCH_BP:	// Page crossed, branch backwards
                    if (BALow)
                        return;
                    read_byte(pc);
                    pc = ar;
                    state = (byte)CPUState.O_BRANCH_BP1;
                    break;
                case CPUState.O_BRANCH_BP1:
                    if (BALow)
                        return;
                    read_byte((UInt16)(pc + 0x100));
                    state = 0; break; // Last macro;
                case CPUState.O_BRANCH_FP:	// Page crossed, branch forwards
                    if (BALow)
                        return;
                    read_byte(pc);
                    pc = ar;
                    state = (byte)CPUState.O_BRANCH_FP1;
                    break;
                case CPUState.O_BRANCH_FP1:
                    if (BALow)
                        return;
                    read_byte((UInt16)(pc - 0x100));
                    state = 0; break; // Last macro;


                // Flag group
                case CPUState.O_SEC:
                    if (BALow)
                        return;
                    read_byte(pc);
                    c_flag = true;
                    state = 0; break; // Last macro;

                case CPUState.O_CLC:
                    if (BALow)
                        return;
                    read_byte(pc);
                    c_flag = false;
                    state = 0; break; // Last macro;

                case CPUState.O_SED:
                    if (BALow)
                        return;
                    read_byte(pc);
                    d_flag = true;
                    state = 0; break; // Last macro;

                case CPUState.O_CLD:
                    if (BALow)
                        return;
                    read_byte(pc);
                    d_flag = false;
                    state = 0; break; // Last macro;

                case CPUState.O_SEI:
                    if (BALow)
                        return;
                    read_byte(pc);
                    i_flag = true;
                    state = 0; break; // Last macro;

                case CPUState.O_CLI:
                    if (BALow)
                        return;
                    read_byte(pc);
                    i_flag = false;
                    state = 0; break; // Last macro;

                case CPUState.O_CLV:
                    if (BALow)
                        return;
                    read_byte(pc);
                    v_flag = false;
                    state = 0; break; // Last macro;


                // NOP group
                case CPUState.O_NOP:
                    if (BALow)
                        return;
                    read_byte(pc);
                    state = 0; break; // Last macro;


                /*
                 * Undocumented opcodes start here
                 */

                // NOP group
                case CPUState.O_NOP_I:
                    if (BALow)
                        return;
                    read_byte(pc++);
                    state = 0; break; // Last macro;

                case CPUState.O_NOP_A:
                    if (BALow)
                        return;
                    read_byte(ar);
                    state = 0; break; // Last macro;


                // Load A/X group
                case CPUState.O_LAX:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (a = x = data);
                    state = 0; break; // Last macro;


                // Store A/X group
                case CPUState.O_SAX:
                    write_byte(ar, (byte)(a & x));
                    state = 0; break; // Last macro;


                // ASL/ORA group
                case CPUState.O_SLO:
                    c_flag = (rdbuf & 0x80) != 0;
                    rdbuf <<= 1;
                    write_byte(ar, rdbuf);
                    z_flag = n_flag = (byte)(a |= rdbuf);
                    state = 0; break; // Last macro;


                // ROL/AND group
                case CPUState.O_RLA:
                    tmp = (byte)(rdbuf & 0x80);
                    rdbuf = (byte)(c_flag ? (rdbuf << 1) | 0x01 : rdbuf << 1);
                    c_flag = tmp != 0;
                    write_byte(ar, rdbuf);
                    z_flag = n_flag = (byte)(a &= rdbuf);
                    state = 0; break; // Last macro;


                // LSR/EOR group
                case CPUState.O_SRE:
                    c_flag = (rdbuf & 0x01) != 0;
                    rdbuf >>= 1;
                    write_byte(ar, rdbuf);
                    z_flag = n_flag = (byte)(a ^= rdbuf);
                    state = 0; break; // Last macro;


                // ROR/ADC group
                case CPUState.O_RRA:
                    tmp = (byte)(rdbuf & 0x01);
                    rdbuf = (byte)(c_flag ? (rdbuf >> 1) | 0x80 : rdbuf >> 1);
                    c_flag = tmp != 0;
                    write_byte(ar, rdbuf);
                    do_adc(rdbuf);
                    state = 0; break; // Last macro;


                // DEC/CMP group
                case CPUState.O_DCP:
                    write_byte(ar, --rdbuf);
                    z_flag = n_flag = (byte)(ar = (UInt16)(a - rdbuf));
                    c_flag = ar < 0x100;
                    state = 0; break; // Last macro;


                // INC/SBC group
                case CPUState.O_ISB:
                    write_byte(ar, ++rdbuf);
                    do_sbc(rdbuf);
                    state = 0; break; // Last macro;


                // Complex functions
                case CPUState.O_ANC_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (byte)(a &= data);
                    c_flag = (n_flag & 0x80) != 0;
                    state = 0; break; // Last macro;

                case CPUState.O_ASR_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    a &= data;
                    c_flag = (a & 0x01) != 0;
                    z_flag = n_flag = (byte)(a >>= 1);
                    state = 0; break; // Last macro;

                case CPUState.O_ARR_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    data &= a;
                    a = (byte)(c_flag ? (data >> 1) | 0x80 : data >> 1);
                    if (!d_flag)
                    {
                        z_flag = n_flag = a;
                        c_flag = (a & 0x40) != 0;
                        v_flag = ((a & 0x40) ^ ((a & 0x20) << 1)) != 0;
                    }
                    else
                    {
                        n_flag = (byte)(c_flag ? 0x80 : 0);
                        z_flag = a;
                        v_flag = ((data ^ a) & 0x40) != 0;
                        if ((data & 0x0f) + (data & 0x01) > 5)
                            a = (byte)(a & 0xf0 | (a + 6) & 0x0f);
                        if (c_flag = ((data + (data & 0x10)) & 0x1f0) > 0x50)
                            a += 0x60;
                    }
                    state = 0; break; // Last macro;

                case CPUState.O_ANE_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (a = (byte)((a | 0xee) & x & data));
                    state = 0; break; // Last macro;

                case CPUState.O_LXA_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (a = x = (byte)((a | 0xee) & data));
                    state = 0; break; // Last macro;

                case CPUState.O_SBX_I:
                    if (BALow)
                        return;
                    data = read_byte(pc++);
                    z_flag = n_flag = (x = (byte)(ar = (UInt16)((x & a) - data)));
                    c_flag = ar < 0x100;
                    state = 0; break; // Last macro;

                case CPUState.O_LAS:
                    if (BALow)
                        return;
                    data = read_byte(ar);
                    z_flag = n_flag = (a = x = sp = (byte)(data & sp));
                    state = 0; break; // Last macro;

                case CPUState.O_SHS:		// ar2 contains the high byte of the operand address
                    write_byte(ar, (byte)((ar2 + 1) & (sp = (byte)(a & x))));
                    state = 0; break; // Last macro;

                case CPUState.O_SHY:		// ar2 contains the high byte of the operand address
                    write_byte(ar, (byte)(y & (ar2 + 1)));
                    state = 0; break; // Last macro;

                case CPUState.O_SHX:		// ar2 contains the high byte of the operand address
                    write_byte(ar, (byte)(x & (ar2 + 1)));
                    state = 0; break; // Last macro;

                case CPUState.O_SHA:		// ar2 contains the high byte of the operand address
                    write_byte(ar, (byte)(a & x & (ar2 + 1)));
                    state = 0; break; // Last macro;

                // end include "CPU_emulcycle.i"

#if !IS_CPU_1541
                // Extension opcode
                case CPUState.O_EXT:
                    if (pc < 0xe000)
                    {
                        illegal_op(0xf2, (UInt16)(pc - 1));
                        break;
                    }
                    switch (read_byte(pc++))
                    {
                        case 0x00:
                            ram[0x90] |= TheIEC.Out(ram[0x95], (ram[0xa3] & 0x80) != 0);
                            c_flag = false;
                            pc = 0xedac;
                            state = 0; break; // Last macro;
                        case 0x01:
                            ram[0x90] |= TheIEC.OutATN(ram[0x95]);
                            c_flag = false;
                            pc = 0xedac;
                            state = 0; break; // Last macro;
                        case 0x02:
                            ram[0x90] |= TheIEC.OutSec(ram[0x95]);
                            c_flag = false;
                            pc = 0xedac;
                            state = 0; break; // Last macro;
                        case 0x03:
                            ram[0x90] |= TheIEC.In(ref a);
                            z_flag = n_flag = a;
                            c_flag = false;
                            pc = 0xedac;
                            state = 0; break; // Last macro;
                        case 0x04:
                            TheIEC.SetATN();
                            pc = 0xedfb;
                            state = 0; break; // Last macro;
                        case 0x05:
                            TheIEC.RelATN();
                            pc = 0xedac;
                            state = 0; break; // Last macro;
                        case 0x06:
                            TheIEC.Turnaround();
                            pc = 0xedac;
                            state = 0; break; // Last macro;
                        case 0x07:
                            TheIEC.Release();
                            pc = 0xedac;
                            state = 0; break; // Last macro;
                        default:
                            illegal_op(0xf2, (UInt16)(pc - 1));
                            break;
                    }
                    break;

#else
                // Extension opcode
                case CPUState.O_EXT:
                    if (pc < 0xc000)
                    {
                        illegal_op(0xf2, (UInt16)(pc - 1));
                        break;
                    }
                    switch (read_byte(pc++))
                    {
                        case 0x00:	// Go to sleep in DOS idle loop if error flag is clear and no command received
                            Idle = (ram[0x26c] | ram[0x7c]) == 0;
                            pc = 0xebff;
                            state = 0; break;
                        case 0x01:	// Write sector
                            the_job.WriteSector();
                            pc = 0xf5dc;
                            state = 0; break;
                        case 0x02:	// Format track
                            the_job.FormatTrack();
                            pc = 0xfd8b;
                            state = 0; break;
                        default:
                            illegal_op(0xf2, (UInt16)(pc - 1));
                            break;
                    }
                    break;
#endif

                default:
                    illegal_op(op, (UInt16)(pc - 1));
                    break;
            }
        }
    }
}
