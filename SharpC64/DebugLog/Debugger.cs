using System;
using System.Collections.Generic;

namespace SharpC64
{
    internal interface ICPU
    {
        byte ReadByte(int address);
    }

    internal class MOS6510Wrapper : ICPU
    {
        MOS6510 _cpu;

        public MOS6510Wrapper(MOS6510 cpu)
        {
            _cpu = cpu;
        }

        #region ICPU Members

        public byte ReadByte(int address)
        {
            return _cpu.read_byte((UInt16)address);
        }

        #endregion
    }

    public class DebugLog
    {
        ICPU _cpu;

        public DebugLog(MOS6510 cpu, bool enabled)
            :this()
        {
            Enabled = enabled;
            _cpu = (ICPU)(new MOS6510Wrapper(cpu));
        }

        private DebugLog()
        {
            _lastInterrupt = new Stack<InterruptType>();
        }

        public void PushLastInterrupt(InterruptType interruptType)
        {
            _lastInterrupt.Push(interruptType);
        }

        public void PopLastInterrupt()
        {
            _lastInterrupt.Pop();
        }

        public void DisAssemble(UInt16 pc, byte acc, byte x, byte y, byte sp, byte status)
        {
            if (Enabled)
            {
                int startPC = pc;
                System.Text.StringBuilder str = new System.Text.StringBuilder();
                if (debugIntructions)
                {
                    str.AppendFormat("{0:X4}{1}", pc, new String(' ', 6 - str.Length));

                    switch ((MOS6510Ops)_cpu.ReadByte(pc))
                    {

                        case MOS6510Ops.AND_I:
                            str.Append("AND");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.AND:
                            str.Append("AND");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.AND_Z:
                            str.Append("AND");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.ORA_I:
                            str.Append("ORA");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.ORA:
                            str.Append("ORA");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.ORA_Z:
                            str.Append("ORA");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.ASL_ZX:
                            str.Append("ASL");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            str.Append(", X");
                            pc += 2;
                            break;


                        case MOS6510Ops.ASL_Z:
                            str.Append("ASL");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.ASL:
                            str.Append("ASL");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.ASL_ACC:
                            str.Append("ASL A");
                            pc += 1;
                            break;


                        case MOS6510Ops.BIT_Z:
                            str.Append("BIT");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.BIT:
                            str.Append("BIT");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.DEC:
                            str.Append("DEC");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;



                        case MOS6510Ops.DEC_Z:
                            str.Append("DEC");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.LDA:
                            str.Append("LDA");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.LDA_X:
                            str.Append("LDA");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            str.Append(", X");
                            pc += 3;
                            break;


                        case MOS6510Ops.LDA_Y:
                            str.Append("LDA");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            str.Append(", Y");
                            pc += 3;
                            break;


                        case MOS6510Ops.LDA_I:
                            str.Append("LDA");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.LDA_Z:
                            str.Append("LDA");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.LDA_INDY:
                            str.Append("LDA (");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            str.Append("),Y");
                            pc += 2;
                            break;


                        case MOS6510Ops.LDA_ZX:
                            str.Append("LDA");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            str.Append(", X");
                            pc += 2;
                            break;


                        case MOS6510Ops.LSR_ZX:
                            str.Append("LSR");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            str.Append(", X");
                            pc += 2;
                            break;


                        case MOS6510Ops.JSR:
                            str.Append("JSR");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.RTS:
                            str.Append("RTS");
                            pc += 1;
                            break;


                        case MOS6510Ops.RTI:
                            str.Append("RTI");
                            pc += 1;
                            break;


                        case MOS6510Ops.SEC:
                            str.Append("SEC");
                            pc += 1;
                            break;


                        case MOS6510Ops.PHP:
                            str.Append("PHP");
                            pc += 1;
                            break;


                        case MOS6510Ops.PHA:
                            str.Append("PHA");
                            pc += 1;
                            break;


                        case MOS6510Ops.PLA:
                            str.Append("PLA");
                            pc += 1;
                            break;


                        case MOS6510Ops.BPL:
                            str.Append("BPL");
                            str.Append(branch(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.BMI:
                            str.Append("BMI");
                            str.Append(branch(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.BVS:
                            str.Append("BVS");
                            str.Append(branch(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.BVC:
                            str.Append("BVC");
                            str.Append(branch(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.BEQ:
                            str.Append("BEQ");
                            str.Append(branch(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.BNE:
                            str.Append("BNE");
                            str.Append(branch(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.BCC:
                            str.Append("BCC");
                            str.Append(branch(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.BCS:
                            str.Append("BCS");
                            str.Append(branch(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.DEX:
                            str.Append("DEX");
                            pc += 1;
                            break;


                        case MOS6510Ops.DEY:
                            str.Append("DEY");
                            pc += 1;
                            break;


                        case MOS6510Ops.INX:
                            str.Append("INX");
                            pc += 1;
                            break;


                        case MOS6510Ops.INC_Z:
                            str.Append("INC");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.INY:
                            str.Append("INY");
                            pc += 1;
                            break;


                        case MOS6510Ops.JMP:
                            str.Append("JMP");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.JMP_IND:
                            str.Append("JMP");
                            str.Append(indirect(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.CLC:
                            str.Append("CLC");
                            pc += 1;
                            break;


                        case MOS6510Ops.CLI:
                            str.Append("CLI");
                            pc += 1;
                            break;

                        case MOS6510Ops.SEI:
                            str.Append("SEI");
                            pc += 1;
                            break;


                        case MOS6510Ops.LDX:
                            str.Append("LDX");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.LDX_I:
                            str.Append("LDX");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.LDX_Z:
                            str.Append("LDX");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.LDX_ZY:
                            str.Append("LDX");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            str.Append(", Y");
                            pc += 2;
                            break;


                        case MOS6510Ops.LDY:
                            str.Append("LDY");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.LDY_Z:
                            str.Append("LDY");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.LDY_ZX:
                            str.Append("LDY");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            str.Append(", X");
                            pc += 2;
                            break;


                        case MOS6510Ops.LDY_I:
                            str.Append("LDY");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.ROL_ACC:
                            str.Append("ROL A");
                            pc += 1;
                            break;


                        case MOS6510Ops.ROL_Z:
                            str.Append("ROL");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.STA:
                            str.Append("STA");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.STA_X:
                            str.Append("STA");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            str.Append(", X");
                            pc += 3;
                            break;


                        case MOS6510Ops.STA_Y:
                            str.Append("STA");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            str.Append(", Y");
                            pc += 3;
                            break;


                        case MOS6510Ops.STA_INDY:
                            str.Append("STA (");
                            str.Append(indirect_zero(pc + 1, acc, x, y, status));
                            str.Append("), Y");
                            pc += 3;
                            break;


                        case MOS6510Ops.STA_Z:
                            str.Append("STA");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.STA_ZX:
                            str.Append("STA");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            str.Append(", X");
                            pc += 3;
                            break;


                        case MOS6510Ops.STX:
                            str.Append("STX");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.STY:
                            str.Append("STY");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.STX_Z:
                            str.Append("STX");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.STY_Z:
                            str.Append("STY");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.CPY_Z:
                            str.Append("CPY_Z");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.CPY:
                            str.Append("CPY");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.CMP_Z:
                            str.Append("CMP_Z");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.CMP_I:
                            str.Append("CMP");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.CMP_Y:
                            str.Append("CMP ");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            str.Append(",Y");
                            pc += 3;
                            break;


                        case MOS6510Ops.CMP_X:
                            str.Append("CMP ");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            str.Append(",X");
                            pc += 3;
                            break;


                        case MOS6510Ops.CMP_INDY:
                            str.Append("CMP (");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            str.Append("),Y");
                            pc += 2;
                            break;


                        case MOS6510Ops.CMP:
                            str.Append("CMP");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.CPX_Z:
                            str.Append("CPX_Z");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.CPX:
                            str.Append("CPX");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.CPX_I:
                            str.Append("CPX");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.CPY_I:
                            str.Append("CPY");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.ROR_ZX:
                            str.Append("ROR");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            str.Append(", X");
                            pc += 2;
                            break;

                        case MOS6510Ops.ROR_Z:
                            str.Append("ROR");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.ROR_ACC:
                            str.Append("ROR A");
                            pc += 1;
                            break;


                        case MOS6510Ops.SBC_Z:
                            str.Append("SBC");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.SBC:
                            str.Append("SBC");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.SBC_INDY:
                            str.Append("SBC");
                            str.Append(indirect_y(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.SBC_I:
                            str.Append("SBC");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.ADC_I:
                            str.Append("ADC");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.ADC_Y:
                            str.Append("ADC");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            str.Append(", Y");
                            pc += 2;
                            break;


                        case MOS6510Ops.ADC_Z:
                            str.Append("ADC");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.EOR_I:
                            str.Append("EOR");
                            str.Append(immediate(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.EOR_Z:
                            str.Append("EOR");
                            str.Append(zero(pc + 1, acc, x, y, status));
                            pc += 2;
                            break;


                        case MOS6510Ops.EOR:
                            str.Append("EOR");
                            str.Append(absolute(pc + 1, acc, x, y, status));
                            pc += 3;
                            break;


                        case MOS6510Ops.LSR_ACC:
                            str.Append("LSR A");
                            pc += 1;
                            break;

                        case MOS6510Ops.TAX:
                        case MOS6510Ops.TAY:
                        case MOS6510Ops.TSX:
                        case MOS6510Ops.TXA:
                        case MOS6510Ops.TXS:
                        case MOS6510Ops.TYA:
                        case MOS6510Ops.CLD:
                        case MOS6510Ops.NOP:
                            MOS6510Ops op = (MOS6510Ops)_cpu.ReadByte(pc);
                            str.Append(op.ToString());
                            pc++;
                            break;


                        default:
                            str.Append("??? ");
                            str.Append(Convert.ToString(_cpu.ReadByte(pc), 16));
                            pc++;
                            break;

                    }
                }

                System.Text.StringBuilder bytes = new System.Text.StringBuilder();
                for (int i = startPC; i < pc; i++)
                {
                    bytes.AppendFormat("{0:X2} ", _cpu.ReadByte(i));
                }

                bytes.Append(new String(' ', 9 - bytes.Length));

                if (InterruptLevel > 0)
                {
                    if (_lastInterrupt.Peek() == InterruptType.NMI)
                        bytes.Append("[NMI] " + InterruptLevel + " ");
                    else
                        bytes.Append("[IRQ] " + InterruptLevel + " ");
                }
                else
                {
                    bytes.Append("        ");
                }
                if (str.Length > 6)
                    str.Insert(6, bytes.ToString());

                if (str.Length < 48)
                    str.Append(new String(' ', 48 - str.Length));

                if (debugIntructions)
                {
                    str.AppendFormat("Acc:{0:X2}", acc);
                    str.AppendFormat(" X:{0:X2}", x);
                    str.AppendFormat(" Y:{0:X2}", y);
                    str.AppendFormat(" SP:{0:X2}", sp);
                    str.Append(" St:");

                    bool carry = ((status & 0x01) != 0);
                    bool zero = ((status & 0x02) != 0);
                    bool disableInterupt = ((status & 0x04) != 0);
                    bool decimal_Renamed = ((status & 0x08) != 0);
                    bool brk = ((status & 0x10) != 0);
                    bool overflow = ((status & 0x40) != 0);
                    bool sign = ((status & 0x80) != 0);
                    str.Append(carry ? "C" : "-");
                    str.Append(zero ? "Z" : "-");
                    str.Append(disableInterupt ? "I" : "-");
                    str.Append(decimal_Renamed ? "D" : "-");
                    str.Append(brk ? "B" : "-");
                    str.Append(overflow ? "O" : "-");
                    str.Append(sign ? "S" : "-");
                }

                // Print out system routine names
                switch (startPC & 0xFFFF)
                {

                    case 0xa000:
                        str.Append("  % -	Restart Vectors				WORD");
                        break;


                    case 0xa00c:
                        str.Append("  % stmdsp	BASIC Command Vectors			WORD");
                        break;


                    case 0xa052:
                        str.Append("  % fundsp	BASIC Function Vectors			WORD");
                        break;


                    case 0xa080:
                        str.Append("  % optab	BASIC Operator Vectors			WORD");
                        break;


                    case 0xa09e:
                        str.Append("  % reslst	BASIC Command Keyword Table		DATA");
                        break;


                    case 0xa129:
                        str.Append("  % msclst	BASIC Misc. Keyword Table		DATA");
                        break;


                    case 0xa140:
                        str.Append("  % oplist	BASIC Operator Keyword Table		DATA");
                        break;


                    case 0xa14d:
                        str.Append("  % funlst	BASIC Function Keyword Table		DATA");
                        break;


                    case 0xa19e:
                        str.Append("  % errtab	Error Message Table			DATA");
                        break;


                    case 0xa328:
                        str.Append("  % errptr	Error Message Pointers			WORD");
                        break;


                    case 0xa364:
                        str.Append("  % okk	Misc. Messages				TEXT");
                        break;


                    case 0xa38a:
                        str.Append("  % fndfor	Find FOR/GOSUB Entry on Stack");
                        break;


                    case 0xa3b8:
                        str.Append("  % bltu	Open Space in Memory");
                        break;


                    case 0xa3fb:
                        str.Append("  % getstk	Check Stack Depth");
                        break;


                    case 0xa408:
                        str.Append("  % reason	Check Memory Overlap / Array area overflow check");
                        break;


                    case 0xa435:
                        str.Append("  % omerr	Output ?OUT OF MEMORY Error");
                        break;


                    case 0xa437:
                        str.Append("  % error	Error Routine");
                        break;


                    case 0xa469:
                        str.Append("  % errfin	Break Entry");
                        break;


                    case 0xa474:
                        str.Append("  % ready	Restart BASIC");
                        break;


                    case 0xa480:
                        str.Append("  % main	Input & Identify BASIC Line");
                        break;


                    case 0xa49c:
                        str.Append("  % main1	Get Line Number & Tokenise Text");
                        break;


                    case 0xa4a2:
                        str.Append("  % inslin	Insert BASIC Text");
                        break;


                    case 0xa533:
                        str.Append("  % linkprg	Rechain Lines");
                        break;


                    case 0xa560:
                        str.Append("  % inlin	Input Line Into Buffer");
                        break;


                    case 0xa579:
                        str.Append("  % crunch	Tokenise Input Buffer");
                        break;


                    case 0xa613:
                        str.Append("  % fndlin	Search for Line Number");
                        break;


                    case 0xa642:
                        str.Append("  % scrtch	Perform [new]");
                        break;


                    case 0xa65e:
                        str.Append("  % clear	Perform [clr]");
                        break;


                    case 0xa68e:
                        str.Append("  % stxpt	Reset TXTPTR");
                        break;


                    case 0xa69c:
                        str.Append("  % list	Perform [list]");
                        break;


                    case 0xa717:
                        str.Append("  % qplop	Handle LIST Character");
                        break;


                    case 0xa742:
                        str.Append("  % for	Perform [for]");
                        break;


                    case 0xa7ae:
                        str.Append("  % newstt	BASIC Warm Start");
                        break;


                    case 0xa7c4:
                        str.Append("  % ckeol	Check End of Program");
                        break;


                    case 0xa7e1:
                        str.Append("  % gone	Prepare to execute statement");
                        break;


                    case 0xa7ed:
                        str.Append("  % gone3	Perform BASIC Keyword,Execute command in A");
                        break;


                    case 0xa81d:
                        str.Append("  % restor	Perform [restore]");
                        break;


                    case 0xa82c:
                        str.Append("  % stop	Perform [stop], [end], break");
                        break;


                    case 0xa857:
                        str.Append("  % cont	Perform [cont]");
                        break;


                    case 0xa871:
                        str.Append("  % run	Perform [run]");
                        break;


                    case 0xa883:
                        str.Append("  % gosub	Perform [gosub]");
                        break;


                    case 0xa8a0:
                        str.Append("  % goto	Perform [goto]");
                        break;


                    case 0xa8d2:
                        str.Append("  % return	Perform [return]");
                        break;


                    case 0xa8f8:
                        str.Append("  % data	Perform [data]");
                        break;


                    case 0xa906:
                        str.Append("  % datan	Search for Next Statement / Line");
                        break;


                    case 0xa928:
                        str.Append("  % if	Perform [if]");
                        break;


                    case 0xa93b:
                        str.Append("  % rem	Perform [rem]");
                        break;


                    case 0xa94b:
                        str.Append("  % ongoto	Perform [on]");
                        break;


                    case 0xa96b:
                        str.Append("  % linget	Fetch linnum From BASIC");
                        break;


                    case 0xa9a5:
                        str.Append("  % let	Perform [let]");
                        break;


                    case 0xa9c4:
                        str.Append("  % putint	Assign Integer");
                        break;


                    case 0xa9d6:
                        str.Append("  % ptflpt	Assign Floating Point");
                        break;


                    case 0xa9d9:
                        str.Append("  % putstr	Assign String");
                        break;


                    case 0xa9e3:
                        str.Append("  % puttim	Assign TI$");
                        break;


                    case 0xaa2c:
                        str.Append("  % getspt	Add Digit to FAC#1");
                        break;


                    case 0xaa80:
                        str.Append("  % printn	Perform [print]#");
                        break;


                    case 0xaa86:
                        str.Append("  % cmd	Perform [cmd]");
                        break;


                    case 0xaa9a:
                        str.Append("  % strdon	Print String From Memory");
                        break;


                    case 0xaaa0:
                        str.Append("  % print	Perform [print]");
                        break;


                    case 0xaab8:
                        str.Append("  % varop	Output Variable");
                        break;


                    case 0xaad7:
                        str.Append("  % crdo	Output CR/LF");
                        break;


                    case 0xaae8:
                        str.Append("  % comprt	Handle comma, TAB(, SPC(");
                        break;


                    case 0xab1e:
                        str.Append("  % strout	Output String");
                        break;


                    case 0xab3b:
                        str.Append("  % outspc	Output Format Character");
                        break;


                    case 0xab4d:
                        str.Append("  % doagin	Handle Bad Data");
                        break;


                    case 0xab7b:
                        str.Append("  % get	Perform [get]");
                        break;


                    case 0xaba5:
                        str.Append("  % inputn	Perform [input#]");
                        break;


                    case 0xabbf:
                        str.Append("  % input	Perform [input]");
                        break;


                    case 0xabea:
                        str.Append("  % bufful	Read Input Buffer");
                        break;


                    case 0xabf9:
                        str.Append("  % qinlin	Do Input Prompt");
                        break;


                    case 0xac06:
                        str.Append("  % read	Perform [read]");
                        break;


                    case 0xac35:
                        str.Append("  % rdget	General Purpose Read Routine");
                        break;


                    case 0xacfc:
                        str.Append("  % exint	Input Error Messages			TEXT");
                        break;


                    case 0xad1e:
                        str.Append("  % next	Perform [next]");
                        break;


                    case 0xad61:
                        str.Append("  % donext	Check Valid Loop");
                        break;


                    case 0xad8a:
                        str.Append("  % frmnum	Confirm Result");
                        break;


                    case 0xad9e:
                        str.Append("  % frmevl	Evaluate Expression in Text");
                        break;


                    case 0xae83:
                        str.Append("  % eval	Evaluate Single Term");
                        break;


                    case 0xaea8:
                        str.Append("  % pival	Constant - pi				DATA");
                        break;


                    case 0xaead:
                        str.Append("  % qdot	Continue Expression");
                        break;


                    case 0xaef1:
                        str.Append("  % parchk	Expression in Brackets");
                        break;


                    case 0xaef7:
                        str.Append("  % chkcls	Confirm Character");
                        break;

                    //				case 0xaef7:
                    //				str.append("  % -	-test ')'-");
                    //				break;


                    case 0xaefa:
                        str.Append("  % -	-test '('-");
                        break;


                    case 0xaefd:
                        str.Append("  % -	-test comma-");
                        break;


                    case 0xaf08:
                        str.Append("  % synerr	Output ?SYNTAX Error");
                        break;


                    case 0xaf0d:
                        str.Append("  % domin	Set up NOT Function");
                        break;


                    case 0xaf14:
                        str.Append("  % rsvvar	Identify Reserved Variable");
                        break;


                    case 0xaf28:
                        str.Append("  % isvar	Search for Variable");
                        break;


                    case 0xaf48:
                        str.Append("  % tisasc	Convert TI to ASCII String");
                        break;


                    case 0xafa7:
                        str.Append("  % isfun	Identify Function Type");
                        break;


                    case 0xafb1:
                        str.Append("  % strfun	Evaluate String Function");
                        break;


                    case 0xafd1:
                        str.Append("  % numfun	Evaluate Numeric Function");
                        break;


                    case 0xafe6:
                        str.Append("  % orop	Perform [or], [and]");
                        break;


                    case 0xb016:
                        str.Append("  % dorel	Perform <, =, >");
                        break;


                    case 0xb01b:
                        str.Append("  % numrel	Numeric Comparison");
                        break;


                    case 0xb02e:
                        str.Append("  % strrel	String Comparison");
                        break;


                    case 0xb07e:
                        str.Append("  % dim	Perform [dim]");
                        break;


                    case 0xb08b:
                        str.Append("  % ptrget	Identify Variable");
                        break;


                    case 0xb0e7:
                        str.Append("  % ordvar	Locate Ordinary Variable");
                        break;


                    case 0xb11d:
                        str.Append("  % notfns	Create New Variable");
                        break;


                    case 0xb128:
                        str.Append("  % notevl	Create Variable");
                        break;


                    case 0xb194:
                        str.Append("  % aryget	Allocate Array Pointer Space");
                        break;


                    case 0xb1a5:
                        str.Append("  % n32768	Constant 32768 in Flpt			DATA");
                        break;


                    case 0xb1aa:
                        str.Append("  % facinx	FAC#1 to Integer in (AC/YR)");
                        break;


                    case 0xb1b2:
                        str.Append("  % intidx	Evaluate Text for Integer (convert)");
                        break;


                    case 0xb1bf:
                        str.Append("  % ayint	Float (FAC#1) to Positive Integer (convert)");
                        break;


                    case 0xb1d1:
                        str.Append("  % isary	Get Array Parameters");
                        break;


                    case 0xb218:
                        str.Append("  % fndary	Find Array");
                        break;


                    case 0xb245:
                        str.Append("  % bserr	?BAD SUBSCRIPT/?ILLEGAL QUANTITY");
                        break;


                    case 0xb261:
                        str.Append("  % notfdd	Create Array");
                        break;


                    case 0xb30e:
                        str.Append("  % inlpn2	Locate Element in Array");
                        break;


                    case 0xb34c:
                        str.Append("  % umult	Number of Bytes in Subscript");
                        break;


                    case 0xb37d:
                        str.Append("  % fre	Perform [fre]");
                        break;


                    case 0xb391:
                        str.Append("  % givayf	Convert Integer in (AC/YR) to Flpt");
                        break;


                    case 0xb39e:
                        str.Append("  % pos	Perform [pos]");
                        break;


                    case 0xb3a6:
                        str.Append("  % errdir	Confirm Program Mode");
                        break;


                    case 0xb3e1:
                        str.Append("  % getfnm	Check Syntax of FN");
                        break;


                    case 0xb3f4:
                        str.Append("  % fndoer	Perform [fn]");
                        break;


                    case 0xb465:
                        str.Append("  % strd	Perform [str$]");
                        break;


                    case 0xb487:
                        str.Append("  % strlit	Set Up String");
                        break;


                    case 0xb4d5:
                        str.Append("  % putnw1	Save String Descriptor");
                        break;


                    case 0xb4f4:
                        str.Append("  % getspa	Allocate Space for String");
                        break;


                    case 0xb526:
                        str.Append("  % garbag	Garbage Collection");
                        break;


                    case 0xb5bd:
                        str.Append("  % dvars	Search for Next String");
                        break;


                    case 0xb606:
                        str.Append("  % grbpas	Collect a String");
                        break;


                    case 0xb63d:
                        str.Append("  % cat	Concatenate Two Strings");
                        break;


                    case 0xb67a:
                        str.Append("  % movins	Store String in High RAM");
                        break;


                    case 0xb6a3:
                        str.Append("  % frestr	Perform String Housekeeping");
                        break;


                    case 0xb6db:
                        str.Append("  % frefac	Clean Descriptor Stack");
                        break;


                    case 0xb6ec:
                        str.Append("  % chrd	Perform [chr$]");
                        break;


                    case 0xb700:
                        str.Append("  % leftd	Perform [left$]");
                        break;


                    case 0xb72c:
                        str.Append("  % rightd	Perform [right$]");
                        break;


                    case 0xb737:
                        str.Append("  % midd	Perform [mid$]");
                        break;


                    case 0xb761:
                        str.Append("  % pream	Pull sTring Parameters");
                        break;


                    case 0xb77c:
                        str.Append("  % len	Perform [len]");
                        break;


                    case 0xb782:
                        str.Append("  % len1	Exit String Mode");
                        break;


                    case 0xb78b:
                        str.Append("  % asc	Perform [asc]");
                        break;


                    case 0xb79b:
                        str.Append("  % gtbytc	Evaluate Text to 1 Byte in XR");
                        break;


                    case 0xb7ad:
                        str.Append("  % val	Perform [val]");
                        break;


                    case 0xb7b5:
                        str.Append("  % strval	Convert ASCII String to Flpt");
                        break;


                    case 0xb7eb:
                        str.Append("  % getnum	Get parameters for POKE/WAIT");
                        break;


                    case 0xb7f7:
                        str.Append("  % getadr	Convert FAC#1 to Integer in LINNUM");
                        break;


                    case 0xb80d:
                        str.Append("  % peek	Perform [peek]");
                        break;


                    case 0xb824:
                        str.Append("  % poke	Perform [poke]");
                        break;


                    case 0xb82d:
                        str.Append("  % wait	Perform [wait]");
                        break;


                    case 0xb849:
                        str.Append("  % faddh	Add 0.5 to FAC#1");
                        break;


                    case 0xb850:
                        str.Append("  % fsub	Perform Subtraction");
                        break;


                    case 0xb862:
                        str.Append("  % fadd5	Normalise Addition");
                        break;


                    case 0xb867:
                        str.Append("  % fadd	Perform Addition");
                        break;


                    case 0xb947:
                        str.Append("  % negfac	2's Complement FAC#1");
                        break;


                    case 0xb97e:
                        str.Append("  % overr	Output ?OVERFLOW Error");
                        break;


                    case 0xb983:
                        str.Append("  % mulshf	Multiply by Zero Byte");
                        break;


                    case 0xb9bc:
                        str.Append("  % fone	Table of Flpt Constants			DATA");
                        break;


                    case 0xb9ea:
                        str.Append("  % log	Perform [log]");
                        break;


                    case 0xba28:
                        str.Append("  % fmult	Perform Multiply");
                        break;


                    case 0xba59:
                        str.Append("  % mulply	Multiply by a Byte");
                        break;


                    case 0xba8c:
                        str.Append("  % conupk	Load FAC#2 From Memory");
                        break;


                    case 0xbab7:
                        str.Append("  % muldiv	Test Both Accumulators");
                        break;


                    case 0xbad4:
                        str.Append("  % mldvex	Overflow / Underflow");
                        break;


                    case 0xbae2:
                        str.Append("  % mul10	Multiply FAC#1 by 10");
                        break;


                    case 0xbaf9:
                        str.Append("  % tenc	Constant 10 in Flpt			DATA");
                        break;


                    case 0xbafe:
                        str.Append("  % div10	Divide FAC#1 by 10");
                        break;


                    case 0xbb07:
                        str.Append("  % fdiv	Divide FAC#2 by Flpt at (AC/YR)");
                        break;


                    case 0xbb0f:
                        str.Append("  % fdivt	Divide FAC#2 by FAC#1");
                        break;


                    case 0xbba2:
                        str.Append("  % movfm	Load FAC#1 From Memory");
                        break;


                    case 0xbbc7:
                        str.Append("  % mov2f	Store FAC#1 in Memory");
                        break;


                    case 0xbbfc:
                        str.Append("  % movfa	Copy FAC#2 into FAC#1");
                        break;


                    case 0xbc0c:
                        str.Append("  % movaf	Copy FAC#1 into FAC#2");
                        break;


                    case 0xbc1b:
                        str.Append("  % round	Round FAC#1");
                        break;


                    case 0xbc2b:
                        str.Append("  % sign	Check Sign of FAC#1");
                        break;


                    case 0xbc39:
                        str.Append("  % sgn	Perform [sgn]");
                        break;


                    case 0xbc58:
                        str.Append("  % abs	Perform [abs]");
                        break;


                    case 0xbc5b:
                        str.Append("  % fcomp	Compare FAC#1 With Memory");
                        break;


                    case 0xbc9b:
                        str.Append("  % qint	Convert FAC#1 to Integer");
                        break;


                    case 0xbccc:
                        str.Append("  % int	Perform [int]");
                        break;


                    case 0xbcf3:
                        str.Append("  % fin	Convert ASCII String to a Float in FAC#1");
                        break;


                    case 0xbdb3:
                        str.Append("  % n0999	String Conversion Constants		DATA");
                        break;


                    case 0xbdc2:
                        str.Append("  % inprt	Output 'IN' and Line Number");
                        break;


                    case 0xbddd:
                        str.Append("  % fout	Convert FAC#1 to ASCII String");
                        break;


                    case 0xbe68:
                        str.Append("  % foutim	Convert TI to String");
                        break;


                    case 0xbf11:
                        str.Append("  % fhalf	Table of Constants			DATA");
                        break;


                    case 0xbf71:
                        str.Append("  % sqr	Perform [sqr]");
                        break;


                    case 0xbf7b:
                        str.Append("  % fpwrt	Perform power ($)");
                        break;


                    case 0xbfb4:
                        str.Append("  % negop	Negate FAC#1");
                        break;


                    case 0xbfbf:
                        str.Append("  % logeb2	Table of Constants			DATA");
                        break;


                    case 0xbfed:
                        str.Append("  % exp	Perform [exp]");
                        break;


                    case 0xe000:
                        str.Append("  % (exp continues)	EXP continued From BASIC ROM");
                        break;


                    case 0xe043:
                        str.Append("  % polyx	Series Evaluation");
                        break;


                    case 0xe08d:
                        str.Append("  % rmulc	Constants for RND			DATA");
                        break;


                    case 0xe097:
                        str.Append("  % rnd	Perform [rnd]");
                        break;


                    case 0xe0f9:
                        str.Append("  % bioerr	Handle I/O Error in BASIC");
                        break;


                    case 0xe10c:
                        str.Append("  % bchout	Output Character");
                        break;


                    case 0xe112:
                        str.Append("  % bchin	Input Character");
                        break;


                    case 0xe118:
                        str.Append("  % bckout	Set Up For Output");
                        break;


                    case 0xe11e:
                        str.Append("  % bckin	Set Up For Input");
                        break;


                    case 0xe124:
                        str.Append("  % bgetin	Get One Character");
                        break;


                    case 0xe12a:
                        str.Append("  % sys	Perform [sys]");
                        break;


                    case 0xe156:
                        str.Append("  % savet	Perform [save]");
                        break;


                    case 0xe165:
                        str.Append("  % verfyt	Perform [verify / load]");
                        break;


                    case 0xe1be:
                        str.Append("  % opent	Perform [open]");
                        break;


                    case 0xe1c7:
                        str.Append("  % closet	Perform [close]");
                        break;


                    case 0xe1d4:
                        str.Append("  % slpara	Get Parameters For LOAD/SAVE");
                        break;


                    case 0xe200:
                        str.Append("  % combyt	Get Next One Byte Parameter");
                        break;


                    case 0xe206:
                        str.Append("  % deflt	Check Default Parameters");
                        break;


                    case 0xe20e:
                        str.Append("  % cmmerr	Check For Comma");
                        break;


                    case 0xe219:
                        str.Append("  % ocpara	Get Parameters For OPEN/CLOSE");
                        break;


                    case 0xe264:
                        str.Append("  % cos	Perform [cos]");
                        break;


                    case 0xe26b:
                        str.Append("  % sin	Perform [sin]");
                        break;


                    case 0xe2b4:
                        str.Append("  % tan	Perform [tan]");
                        break;


                    case 0xe2e0:
                        str.Append("  % pi2	Table of Trig Constants			DATA");
                        break;


                    case 0xe30e:
                        str.Append("  % atn	Perform [atn]");
                        break;


                    case 0xe33e:
                        str.Append("  % atncon	Table of ATN Constants			DATA");
                        break;


                    case 0xe37b:
                        str.Append("  % bassft	BASIC Warm Start [RUNSTOP-RESTORE]");
                        break;


                    case 0xe394:
                        str.Append("  % init	BASIC Cold Start");
                        break;


                    case 0xe3a2:
                        str.Append("  % initat	CHRGET For Zero-page");
                        break;


                    case 0xe3ba:
                        str.Append("  % rndsed	RND Seed For zero-page			DATA");
                        break;


                    case 0xe3bf:
                        str.Append("  % initcz	Initialize BASIC RAM");
                        break;


                    case 0xe422:
                        str.Append("  % initms	Output Power-Up Message");
                        break;


                    case 0xe447:
                        str.Append("  % bvtrs	Table of BASIC Vectors (for 0300)	WORD");
                        break;


                    case 0xe453:
                        str.Append("  % initv	Initialize Vectors");
                        break;


                    case 0xe45f:
                        str.Append("  % words	Power-Up Message			DATA");
                        break;


                    case 0xe4ad:
                        str.Append("  % -	Patch for BASIC Call to CHKOUT");
                        break;


                    case 0xe4b7:
                        str.Append("  % -	Unused Bytes For Future Patches		EMPTY");
                        break;


                    case 0xe4da:
                        str.Append("  % -	Reset Character Colour");
                        break;


                    case 0xe4e0:
                        str.Append("  % -	Pause After Finding Tape File");
                        break;


                    case 0xe4ec:
                        str.Append("  % -	RS-232 Timing Table -- PAL		DATA");
                        break;


                    case 0xe500:
                        str.Append("  % iobase	Get I/O Address");
                        break;


                    case 0xe505:
                        str.Append("  % screen	Get Screen Size");
                        break;


                    case 0xe50a:
                        str.Append("  % plot	Put / Get Row And Column");
                        break;


                    case 0xe518:
                        str.Append("  % cint1	Initialize I/O");
                        break;


                    case 0xe544:
                        str.Append("  % -	Clear Screen");
                        break;


                    case 0xe566:
                        str.Append("  % -	Home Cursor");
                        break;


                    case 0xe56c:
                        str.Append("  % -	Set Screen Pointers");
                        break;


                    case 0xe59a:
                        str.Append("  % -	Set I/O Defaults (Unused Entry)");
                        break;


                    case 0xe5a0:
                        str.Append("  % -	Set I/O Defaults");
                        break;


                    case 0xe5b4:
                        str.Append("  % lp2	Get Character From Keyboard Buffer");
                        break;


                    case 0xe5ca:
                        str.Append("  % -	Input From Keyboard");
                        break;


                    case 0xe632:
                        str.Append("  % -	Input From Screen or Keyboard");
                        break;


                    case 0xe684:
                        str.Append("  % -	Quotes Test");
                        break;


                    case 0xe691:
                        str.Append("  % -	Set Up Screen Print");
                        break;


                    case 0xe6b6:
                        str.Append("  % -	Advance Cursor");
                        break;


                    case 0xe6ed:
                        str.Append("  % -	Retreat Cursor");
                        break;


                    case 0xe701:
                        str.Append("  % -	Back on to Previous Line");
                        break;


                    case 0xe716:
                        str.Append("  % -	Output to Screen");
                        break;


                    case 0xe72a:
                        str.Append("  % -	-unshifted characters-");
                        break;


                    case 0xe7d4:
                        str.Append("  % -	-shifted characters-");
                        break;


                    case 0xe87c:
                        str.Append("  % -	Go to Next Line");
                        break;


                    case 0xe891:
                        str.Append("  % -	Output ");
                        break;


                    case 0xe8a1:
                        str.Append("  % -	Check Line Decrement");
                        break;


                    case 0xe8b3:
                        str.Append("  % -	Check Line Increment");
                        break;


                    case 0xe8cb:
                        str.Append("  % -	Set Colour Code");
                        break;


                    case 0xe8da:
                        str.Append("  % -	Colour Code Table");
                        break;


                    case 0xe8ea:
                        str.Append("  % -	Scroll Screen");
                        break;


                    case 0xe965:
                        str.Append("  % -	Open A Space On The Screen");
                        break;


                    case 0xe9c8:
                        str.Append("  % -	Move A Screen Line");
                        break;


                    case 0xe9e0:
                        str.Append("  % -	Syncronise Colour Transfer");
                        break;


                    case 0xe9f0:
                        str.Append("  % -	Set Start of Line");
                        break;


                    case 0xe9ff:
                        str.Append("  % -	Clear Screen Line");
                        break;


                    case 0xea13:
                        str.Append("  % -	Print To Screen");
                        break;


                    case 0xea24:
                        str.Append("  % -	Syncronise Colour Pointer");
                        break;


                    case 0xea31:
                        str.Append("  % -	Main IRQ Entry Point");
                        break;


                    case 0xea87:
                        str.Append("  % scnkey	Scan Keyboard");
                        break;


                    case 0xeadd:
                        str.Append("  % -	Process Key Image");
                        break;


                    case 0xeb79:
                        str.Append("  % -	Pointers to Keyboard decoding tables	WORD");
                        break;


                    case 0xeb81:
                        str.Append("  % -	Keyboard 1 -- unshifted			DATA");
                        break;


                    case 0xebc2:
                        str.Append("  % -	Keyboard 2 -- Shifted			DATA");
                        break;


                    case 0xec03:
                        str.Append("  % -	Keyboard 3 -- Commodore			DATA");
                        break;


                    case 0xec44:
                        str.Append("  % -	Graphics/Text Control");
                        break;


                    case 0xec78:
                        str.Append("  % -	Keyboard 4 -- Control			DATA");
                        break;


                    case 0xecb9:
                        str.Append("  % -	Video Chip Setup Table			DATA");
                        break;


                    case 0xece7:
                        str.Append("  % -	Shift-Run Equivalent");
                        break;


                    case 0xecf0:
                        str.Append("  % -	Low Byte Screen Line Addresses		DATA");
                        break;


                    case 0xed09:
                        str.Append("  % talk	Send TALK Command on Serial Bus");
                        break;


                    case 0xed0c:
                        str.Append("  % listn	Send LISTEN Command on Serial Bus");
                        break;


                    case 0xed40:
                        str.Append("  % -	Send Data On Serial Bus");
                        break;


                    case 0xedad:
                        str.Append("  % -	Flag Errors");
                        break;


                    case 0xf707:
                        str.Append("  % -	Status #80 - device not present");
                        break;


                    case 0xedb0:
                        str.Append("  % -	Status #03 - write timeout");
                        break;


                    case 0xedb9:
                        str.Append("  % second	Send LISTEN Secondary Address");
                        break;


                    case 0xedbe:
                        str.Append("  % -	Clear ATN");
                        break;


                    case 0xedc7:
                        str.Append("  % tksa	Send TALK Secondary Address");
                        break;


                    case 0xedcc:
                        str.Append("  % -	Wait For Clock");
                        break;


                    case 0xeddd:
                        str.Append("  % ciout	Send Serial Deferred");
                        break;


                    case 0xedef:
                        str.Append("  % untlk	Send UNTALK / UNLISTEN");
                        break;


                    case 0xee13:
                        str.Append("  % acptr	Receive From Serial Bus");
                        break;


                    case 0xee85:
                        str.Append("  % -	Serial Clock On");
                        break;


                    case 0xee8e:
                        str.Append("  % -	Serial Clock Off");
                        break;


                    case 0xee97:
                        str.Append("  % -	Serial Output 1");
                        break;


                    case 0xeea0:
                        str.Append("  % -	Serial Output 0");
                        break;


                    case 0xeea9:
                        str.Append("  % -	Get Serial Data And Clock In");
                        break;


                    case 0xeeb3:
                        str.Append("  % -	Delay 1 ms");
                        break;


                    case 0xeebb:
                        str.Append("  % -	RS-232 Send");
                        break;


                    case 0xef06:
                        str.Append("  % -	Send New RS-232 Byte");
                        break;


                    case 0xef2e:
                        str.Append("  % -	'No DSR' / 'No CTS' Error");
                        break;


                    case 0xef39:
                        str.Append("  % -	Disable Timer");
                        break;


                    case 0xef4a:
                        str.Append("  % -	Compute Bit Count");
                        break;


                    case 0xef59:
                        str.Append("  % -	RS-232 Receive");
                        break;


                    case 0xef7e:
                        str.Append("  % -	Set Up To Receive");
                        break;


                    case 0xef90:
                        str.Append("  % -	Process RS-232 Byte");
                        break;


                    case 0xefe1:
                        str.Append("  % -	Submit to RS-232");
                        break;


                    case 0xf00d:
                        str.Append("  % -	No DSR (Data Set Ready) Error");
                        break;


                    case 0xf017:
                        str.Append("  % -	Send to RS-232 Buffer");
                        break;


                    case 0xf04d:
                        str.Append("  % -	Input From RS-232");
                        break;


                    case 0xf086:
                        str.Append("  % -	Get From RS-232");
                        break;


                    case 0xf0a4:
                        str.Append("  % -	Serial Bus Idle");
                        break;


                    case 0xf0bd:
                        str.Append("  % -	Table of Kernal I/O Messages		DATA");
                        break;


                    case 0xf12b:
                        str.Append("  % -	Print Message if Direct");
                        break;


                    case 0xf12f:
                        str.Append("  % -	Print Message");
                        break;


                    case 0xf13e:
                        str.Append("  % getin	Get a byte");
                        break;


                    case 0xf157:
                        str.Append("  % chrin	Input a byte");
                        break;


                    case 0xf199:
                        str.Append("  % -	Get From Tape / Serial / RS-232");
                        break;


                    case 0xf1ca:
                        str.Append("  % chrout	Output One Character");
                        break;


                    case 0xf20e:
                        str.Append("  % chkin	Set Input Device");
                        break;


                    case 0xf250:
                        str.Append("  % chkout	Set Output Device");
                        break;


                    case 0xf291:
                        str.Append("  % close	Close File");
                        break;


                    case 0xf30f:
                        str.Append("  % -	Find File");
                        break;


                    case 0xf31f:
                        str.Append("  % -	Set File values");
                        break;


                    case 0xf32f:
                        str.Append("  % clall	Abort All Files");
                        break;


                    case 0xf333:
                        str.Append("  % clrchn	Restore Default I/O");
                        break;


                    case 0xf34a:
                        str.Append("  % open	Open File");
                        break;


                    case 0xf3d5:
                        str.Append("  % -	Send Secondary Address");
                        break;


                    case 0xf409:
                        str.Append("  % -	Open RS-232");
                        break;


                    case 0xf49e:
                        str.Append("  % load	Load RAM");
                        break;


                    case 0xf4b8:
                        str.Append("  % -	Load File From Serial Bus");
                        break;


                    case 0xf533:
                        str.Append("  % -	Load File From Tape");
                        break;


                    case 0xf5af:
                        str.Append("  % -	Print 'SEARCHING'");
                        break;


                    case 0xf5c1:
                        str.Append("  % -	Print Filename");
                        break;


                    case 0xf5d2:
                        str.Append("  % -	Print 'LOADING / VERIFYING'");
                        break;


                    case 0xf5dd:
                        str.Append("  % save	Save RAM");
                        break;


                    case 0xf5fa:
                        str.Append("  % -	Save to Serial Bus");
                        break;


                    case 0xf659:
                        str.Append("  % -	Save to Tape");
                        break;


                    case 0xf68f:
                        str.Append("  % -	Print 'SAVING'");
                        break;


                    case 0xf69b:
                        str.Append("  % udtim	Bump Clock");
                        break;


                    case 0xf6dd:
                        str.Append("  % rdtim	Get Time");
                        break;


                    case 0xf6e4:
                        str.Append("  % settim	Set Time");
                        break;


                    case 0xf6ed:
                        str.Append("  % stop	Check STOP Key");
                        break;

                    //				case 0xf6fb:
                    //				str.append("  % -	Output I/O Error Messages");
                    //				break;


                    case 0xf6fb:
                        str.Append("  % -	'too many files'");
                        break;


                    case 0xf6fe:
                        str.Append("  % -	'file open'");
                        break;


                    case 0xf701:
                        str.Append("  % -	'file not open'");
                        break;


                    case 0xf704:
                        str.Append("  % -	'file not found'");
                        break;

                    //				case 0xf707:
                    //				str.append("  % -	'device not present'");
                    //				break;


                    case 0xf70a:
                        str.Append("  % -	'not input file'");
                        break;


                    case 0xf70d:
                        str.Append("  % -	'not output file'");
                        break;


                    case 0xf710:
                        str.Append("  % -	'missing filename'");
                        break;


                    case 0xf713:
                        str.Append("  % -	'illegal device number'");
                        break;


                    case 0xf72d:
                        str.Append("  % -	Find Any Tape Header");
                        break;


                    case 0xf76a:
                        str.Append("  % -	Write Tape Header");
                        break;


                    case 0xf7d0:
                        str.Append("  % -	Get Buffer Address");
                        break;


                    case 0xf7d7:
                        str.Append("  % -	Set Buffer Stat / End Pointers");
                        break;


                    case 0xf7ea:
                        str.Append("  % -	Find Specific Tape Header");
                        break;


                    case 0xf80d:
                        str.Append("  % -	Bump Tape Pointer");
                        break;


                    case 0xf817:
                        str.Append("  % -	Print 'PRESS PLAY ON TAPE'");
                        break;


                    case 0xf82e:
                        str.Append("  % -	Check Tape Status");
                        break;


                    case 0xf838:
                        str.Append("  % -	Print 'PRESS RECORD...'");
                        break;


                    case 0xf841:
                        str.Append("  % -	Initiate Tape Read");
                        break;


                    case 0xf864:
                        str.Append("  % -	Initiate Tape Write");
                        break;


                    case 0xf875:
                        str.Append("  % -	Common Tape Code");
                        break;


                    case 0xf8d0:
                        str.Append("  % -	Check Tape Stop");
                        break;


                    case 0xf8e2:
                        str.Append("  % -	Set Read Timing");
                        break;


                    case 0xf92c:
                        str.Append("  % -	Read Tape Bits");
                        break;


                    case 0xfa60:
                        str.Append("  % -	Store Tape Characters");
                        break;


                    case 0xfb8e:
                        str.Append("  % -	Reset Tape Pointer");
                        break;


                    case 0xfb97:
                        str.Append("  % -	New Character Setup");
                        break;


                    case 0xfba6:
                        str.Append("  % -	Send Tone to Tape");
                        break;


                    case 0xfbc8:
                        str.Append("  % -	Write Data to Tape");
                        break;


                    case 0xfbcd:
                        str.Append("  % -	IRQ Entry Point");
                        break;


                    case 0xfc57:
                        str.Append("  % -	Write Tape Leader");
                        break;


                    case 0xfc93:
                        str.Append("  % -	Restore Normal IRQ");
                        break;


                    case 0xfcb8:
                        str.Append("  % -	Set IRQ Vector");
                        break;


                    case 0xfcca:
                        str.Append("  % -	Kill Tape Motor");
                        break;


                    case 0xfcd1:
                        str.Append("  % -	Check Read / Write Pointer");
                        break;


                    case 0xfcdb:
                        str.Append("  % -	Bump Read / Write Pointer");
                        break;


                    case 0xfce2:
                        str.Append("  % -	Power-Up RESET Entry");
                        break;


                    case 0xfd02:
                        str.Append("  % -	Check for a cartridge");
                        break;


                    case 0xfd12:
                        str.Append("  % -	8-ROM Mask '80CBM'			DATA");
                        break;


                    case 0xfd15:
                        str.Append("  % restor	Restore Kernal Vectors (at 0314)");
                        break;


                    case 0xfd1a:
                        str.Append("  % vector	Change Vectors For User");
                        break;


                    case 0xfd30:
                        str.Append("  % -	Kernal Reset Vectors			WORD");
                        break;


                    case 0xfd50:
                        str.Append("  % ramtas	Initialise System Constants");
                        break;


                    case 0xfd9b:
                        str.Append("  % -	IRQ Vectors For Tape I/O		WORD");
                        break;


                    case 0xfda3:
                        str.Append("  % ioinit	Initialise I/O");
                        break;


                    case 0xfddd:
                        str.Append("  % -	Enable Timer");
                        break;


                    case 0xfdf9:
                        str.Append("  % setnam	Set Filename");
                        break;


                    case 0xfe00:
                        str.Append("  % setlfs	Set Logical File Parameters");
                        break;


                    case 0xfe07:
                        str.Append("  % readst	Get I/O Status Word");
                        break;


                    case 0xfe18:
                        str.Append("  % setmsg	Control OS Messages");
                        break;


                    case 0xfe21:
                        str.Append("  % settmo	Set IEEE Timeout");
                        break;


                    case 0xfe25:
                        str.Append("  % memtop	Read / Set Top of Memory");
                        break;


                    case 0xfe34:
                        str.Append("  % membot	Read / Set Bottom of Memory");
                        break;


                    case 0xfe43:
                        str.Append("  % -	NMI Transfer Entry");
                        break;


                    case 0xfe66:
                        str.Append("  % -	Warm Start Basic [BRK]");
                        break;


                    case 0xfebc:
                        str.Append("  % -	Exit Interrupt");
                        break;


                    case 0xfec2:
                        str.Append("  % -	RS-232 Timing Table - NTSC	DATA");
                        break;


                    case 0xfed6:
                        str.Append("  % -	NMI RS-232 In");
                        break;


                    case 0xff07:
                        str.Append("  % -	NMI RS-232 Out");
                        break;


                    case 0xff43:
                        str.Append("  % -	Fake IRQ Entry");
                        break;


                    case 0xff48:
                        str.Append("  % -	IRQ Entry");
                        break;


                    case 0xff5b:
                        str.Append("  % cint	Initialize screen editor");
                        break;


                    case 0xff80:
                        str.Append("  % -	Kernal Version Number [03]	DATA");
                        break;


                    case 0xff81:
                        str.Append("  % cint		Init Editor & Video Chips");
                        break;


                    case 0xff84:
                        str.Append("  % ioinit		Init I/O Devices, Ports & Timers");
                        break;


                    case 0xff87:
                        str.Append("  % ramtas		Init Ram & Buffers");
                        break;


                    case 0xff8a:
                        str.Append("  % restor		Restore Vectors");
                        break;


                    case 0xff8d:
                        str.Append("  % vector		Change Vectors For User");
                        break;


                    case 0xff90:
                        str.Append("  % setmsg		Control OS Messages");
                        break;


                    case 0xff93:
                        str.Append("  % secnd		Send SA After Listen");
                        break;


                    case 0xff96:
                        str.Append("  % tksa		Send SA After Talk");
                        break;


                    case 0xff99:
                        str.Append("  % memtop		Set/Read System RAM Top");
                        break;


                    case 0xff9c:
                        str.Append("  % membot		Set/Read System RAM Bottom");
                        break;


                    case 0xff9f:
                        str.Append("  % scnkey		Scan Keyboard");
                        break;


                    case 0xffa2:
                        str.Append("  % settmo		Set Timeout In IEEE");
                        break;


                    case 0xffa5:
                        str.Append("  % acptr		Handshake Serial Byte In");
                        break;


                    case 0xffa8:
                        str.Append("  % ciout		Handshake Serial Byte Out");
                        break;


                    case 0xffab:
                        str.Append("  % untalk		Command Serial Bus UNTALK");
                        break;


                    case 0xffae:
                        str.Append("  % unlsn		Command Serial Bus UNLISTEN");
                        break;


                    case 0xffb1:
                        str.Append("  % listn		Command Serial Bus LISTEN");
                        break;


                    case 0xffb4:
                        str.Append("  % talk		Command Serial Bus TALK");
                        break;


                    case 0xffb7:
                        str.Append("  % readss		Read I/O Status Word");
                        break;


                    case 0xffba:
                        str.Append("  % setlfs		Set Logical File Parameters");
                        break;


                    case 0xffbd:
                        str.Append("  % setnam		Set Filename");
                        break;


                    case 0xffc0:
                        str.Append("  % (iopen)		Open Vector [f34a]");
                        break;


                    case 0xffc3:
                        str.Append("  % (iclose)   	Close Vector [f291]");
                        break;


                    case 0xffc6:
                        str.Append("  % (ichkin)   	Set Input [f20e]");
                        break;


                    case 0xffc9:
                        str.Append("  % (ichkout)	Set Output [f250]");
                        break;


                    case 0xffcc:
                        str.Append("  % (iclrch)	Restore I/O Vector [f333]");
                        break;


                    case 0xffcf:
                        str.Append("  % (ichrin)	Input Vector, chrin [f157]");
                        break;


                    case 0xffd2:
                        str.Append("  % (ichrout)	Output Vector, chrout [f1ca]");
                        break;


                    case 0xffd5:
                        str.Append("  % load		Load RAM From Device");
                        break;


                    case 0xffd8:
                        str.Append("  % save		Save RAM To Device");
                        break;


                    case 0xffdb:
                        str.Append("  % settim		Set Real-Time Clock");
                        break;


                    case 0xffde:
                        str.Append("  % rdtim		Read Real-Time Clock");
                        break;


                    case 0xffe1:
                        str.Append("  % (istop)		Test-Stop Vector [f6ed]");
                        break;


                    case 0xffe4:
                        str.Append("  % (igetin)	Get From Keyboad [f13e]");
                        break;


                    case 0xffe7:
                        str.Append("  % (iclall)	Close All Channels And Files [f32f]");
                        break;


                    case 0xffea:
                        str.Append("  % udtim		Increment Real-Time Clock");
                        break;


                    case 0xffed:
                        str.Append("  % screen		Return Screen Organization");
                        break;


                    case 0xfff0:
                        str.Append("  % plot		Read / Set Cursor X/Y Position");
                        break;


                    case 0xfff3:
                        str.Append("  % iobase		Return I/O Base Address");
                        break;


                    case 0xfff6:
                        str.Append("  % - [5252]");
                        break;


                    case 0xfff8:
                        str.Append("  % SYSTEM [5942]");
                        break;


                    case 0xfffa:
                        str.Append("  % NMI [fe43]");
                        break;


                    case 0xfffc:
                        str.Append("  % RESET [fce2]");
                        break;


                    case 0xfffe:
                        str.Append("  % IRQ [ff48]");
                        break;


                    case 0xbdcd:
                        str.Append("  % Print number from AX");
                        break;
                }

                Console.WriteLine(str.ToString());
            }
        }

        #region public properties

        public bool Enabled
        {
            get
            {
                return DEBUG;
            }

            set
            {
                DEBUG = value;
            }

        }

        public int Level
        {
            get
            {
                return level;
            }

            set
            {
                this.level = value;
            }

        }

        public int InterruptLevel
        {
            get { return _lastInterrupt.Count; }
        }

        #endregion public properties

        #region private members

        private String absolute(int pc, int acc, int x, int y, byte status)
        {
            int adr = (_cpu.ReadByte(pc + 1) << 8) + _cpu.ReadByte(pc);
            return String.Format(" ${0:X4}  ({1:X2})", adr, _cpu.ReadByte(adr));
            //return " $" + Convert.ToString(adr, 16) + "  (" + _cpu.ReadByte(adr) + ")";
        }

        private String indirect(int pc, int acc, int x, int y, byte status)
        {
            int adr = (_cpu.ReadByte(pc + 1) << 8) + _cpu.ReadByte(pc);
            return String.Format(" ({0:X4}) = {1:X4}", adr, _cpu.ReadByte(adr) + (_cpu.ReadByte(adr + 1) << 8));
            //return String.Concat(String.Concat(String.Concat(" (", Convert.ToString(adr, 16)), ")  = "), Convert.ToString(_cpu.ReadByte(adr) + (_cpu.ReadByte(adr + 1) << 8), 16));
        }

        private String indirect_y(int pc, int acc, int x, int y, byte status)
        {
            int adr = (_cpu.ReadByte(pc + 1) << 8) + _cpu.ReadByte(pc);
            return String.Format(" ({0:X4}), Y = ?{1:X4}", adr, (y + _cpu.ReadByte(adr) + (_cpu.ReadByte(adr + 1) << 8)));
            //return String.Concat(String.Concat(String.Concat(" (", Convert.ToString(adr, 16)), "), Y  = ?"), Convert.ToString(y + _cpu.ReadByte(adr) + (_cpu.ReadByte(adr + 1) << 8), 16));
        }

        private String indirect_zero(int pc, int acc, int x, int y, byte status)
        {
            int adr = _cpu.ReadByte(pc);
            adr = (_cpu.ReadByte(adr + 1) << 8) + _cpu.ReadByte(adr);
            return String.Format(" ({0:X2}) = {1}", _cpu.ReadByte(pc), adr);
            //return " (" + Convert.ToString(_cpu.ReadByte(pc), 16) + ") = " + Convert.ToString(adr, 16);
        }

        private String zero(int pc, int acc, int x, int y, byte status)
        {
            int adr = _cpu.ReadByte(pc);
            return String.Format(" ${0:X2}  ({1:X2})", adr, _cpu.ReadByte(adr));
            //return " $" + Convert.ToString(adr, 16) + "  (" + Convert.ToString(_cpu.ReadByte(adr), 16) + ")";
        }

        private String immediate(int pc, int acc, int x, int y, byte status)
        {
            int data = _cpu.ReadByte(pc);
            return String.Format(" #${0:X2}", data);
            //return " #" + "$" + Convert.ToString(data, 16);
        }

        private String branch(int pc, int acc, int x, int y, byte status)
        {
            int data = _cpu.ReadByte(pc);
            int newpc = data > 0x7f ? data - 0xff : data + 1;
            return String.Format(" {0} ${1:X4}", newpc, pc + newpc);
            //return " " + Convert.ToString(newpc) + " $" + Convert.ToString(pc + newpc, 16);
        }

        #endregion private members

        #region private fields

        private bool DEBUG = false;
        private bool debugIntructions = true;
        private int level = 10;

        Stack<InterruptType> _lastInterrupt;

        #endregion
    }
}