using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    // States for addressing modes/operations (Frodo SC)
    public enum CPUState : byte
    {
        NONE = 0x01,

        // Read effective address, no extra cycles
        A_ZERO = 0x18,
        A_ZEROX, A_ZEROX1,
        A_ZEROY, A_ZEROY1,
        A_ABS, A_ABS1,
        A_ABSX, A_ABSX1, A_ABSX2, A_ABSX3,
        A_ABSY, A_ABSY1, A_ABSY2, A_ABSY3,
        A_INDX, A_INDX1, A_INDX2, A_INDX3,
        A_INDY, A_INDY1, A_INDY2, A_INDY3, A_INDY4,

        // Read effective address, extra cycle on page crossing
        AE_ABSX, AE_ABSX1, AE_ABSX2,
        AE_ABSY, AE_ABSY1, AE_ABSY2,
        AE_INDY, AE_INDY1, AE_INDY2, AE_INDY3,

        // Read operand and write it back (for RMW instructions), no extra cycles
        M_ZERO,
        M_ZEROX, M_ZEROX1,
        M_ZEROY, M_ZEROY1,
        M_ABS, M_ABS1,
        M_ABSX, M_ABSX1, M_ABSX2, M_ABSX3,
        M_ABSY, M_ABSY1, M_ABSY2, M_ABSY3,
        M_INDX, M_INDX1, M_INDX2, M_INDX3,
        M_INDY, M_INDY1, M_INDY2, M_INDY3, M_INDY4,
        RMW_DO_IT, RMW_DO_IT1,

        // Operations (_I = Immediate/Indirect, _A = Accumulator)
        O_LDA, O_LDA_I, O_LDX, O_LDX_I, O_LDY, O_LDY_I,
        O_STA, O_STX, O_STY,
        O_TAX, O_TXA, O_TAY, O_TYA, O_TSX, O_TXS,
        O_ADC, O_ADC_I, O_SBC, O_SBC_I,
        O_INX, O_DEX, O_INY, O_DEY, O_INC, O_DEC,
        O_AND, O_AND_I, O_ORA, O_ORA_I, O_EOR, O_EOR_I,
        O_CMP, O_CMP_I, O_CPX, O_CPX_I, O_CPY, O_CPY_I,
        O_BIT,
        O_ASL, O_ASL_A, O_LSR, O_LSR_A, O_ROL, O_ROL_A, O_ROR, O_ROR_A,
        O_PHA, O_PHA1, O_PLA, O_PLA1, O_PLA2,
        O_PHP, O_PHP1, O_PLP, O_PLP1, O_PLP2,
        O_JMP, O_JMP1, O_JMP_I, O_JMP_I1,
        O_JSR, O_JSR1, O_JSR2, O_JSR3, O_JSR4,
        O_RTS, O_RTS1, O_RTS2, O_RTS3, O_RTS4,
        O_RTI, O_RTI1, O_RTI2, O_RTI3, O_RTI4,
        O_BRK, O_BRK1, O_BRK2, O_BRK3, O_BRK4, O_BRK5, O_BRK5NMI,
        O_BCS, O_BCC, O_BEQ, O_BNE, O_BVS, O_BVC, O_BMI, O_BPL,
        O_BRANCH_NP, O_BRANCH_BP, O_BRANCH_BP1, O_BRANCH_FP, O_BRANCH_FP1,
        O_SEC, O_CLC, O_SED, O_CLD, O_SEI, O_CLI, O_CLV,
        O_NOP,

        O_NOP_I, O_NOP_A,
        O_LAX, O_SAX,
        O_SLO, O_RLA, O_SRE, O_RRA, O_DCP, O_ISB,
        O_ANC_I, O_ASR_I, O_ARR_I, O_ANE_I, O_LXA_I, O_SBX_I,
        O_LAS, O_SHS, O_SHY, O_SHX, O_SHA,
        O_EXT
    };

    public class CPUCommon
    {
        public static CPUState[] ModeTab = {
            CPUState.O_BRK,    CPUState.A_INDX,   CPUState.NONE,     CPUState.M_INDX,   CPUState.A_ZERO,   CPUState.A_ZERO,   CPUState.M_ZERO,   CPUState.M_ZERO,   // 00
	        CPUState.O_PHP,    CPUState.O_ORA_I,  CPUState.O_ASL_A,  CPUState.O_ANC_I,  CPUState.A_ABS,    CPUState.A_ABS,    CPUState.M_ABS,    CPUState.M_ABS,    
	        CPUState.O_BPL,    CPUState.AE_INDY,  CPUState.NONE,	 CPUState.M_INDY,   CPUState.A_ZEROX,  CPUState.A_ZEROX,  CPUState.M_ZEROX,  CPUState.M_ZEROX,  // 10
	        CPUState.O_CLC,    CPUState.AE_ABSY,  CPUState.O_NOP,    CPUState.M_ABSY,   CPUState.AE_ABSX,  CPUState.AE_ABSX,  CPUState.M_ABSX,   CPUState.M_ABSX,   
	        CPUState.O_JSR,    CPUState.A_INDX,   CPUState.NONE,     CPUState.M_INDX,   CPUState.A_ZERO,   CPUState.A_ZERO,   CPUState.M_ZERO,   CPUState.M_ZERO,   // 20
	        CPUState.O_PLP,    CPUState.O_AND_I,  CPUState.O_ROL_A,  CPUState.O_ANC_I,  CPUState.A_ABS,    CPUState.A_ABS,    CPUState.M_ABS,    CPUState.M_ABS,    
	        CPUState.O_BMI,    CPUState.AE_INDY,  CPUState.NONE,     CPUState.M_INDY,   CPUState.A_ZEROX,  CPUState.A_ZEROX,  CPUState.M_ZEROX,  CPUState.M_ZEROX,  // 30
	        CPUState.O_SEC,    CPUState.AE_ABSY,  CPUState.O_NOP,    CPUState.M_ABSY,   CPUState.AE_ABSX,  CPUState.AE_ABSX,  CPUState.M_ABSX,   CPUState.M_ABSX,   
	        CPUState.O_RTI,    CPUState.A_INDX,   CPUState.NONE,     CPUState.M_INDX,   CPUState.A_ZERO,   CPUState.A_ZERO,   CPUState.M_ZERO,   CPUState.M_ZERO,   // 40
	        CPUState.O_PHA,    CPUState.O_EOR_I,  CPUState.O_LSR_A,  CPUState.O_ASR_I,  CPUState.O_JMP,    CPUState.A_ABS,    CPUState.M_ABS,    CPUState.M_ABS,    
	        CPUState.O_BVC,    CPUState.AE_INDY,  CPUState.NONE,     CPUState.M_INDY,   CPUState.A_ZEROX,  CPUState.A_ZEROX,  CPUState.M_ZEROX,  CPUState.M_ZEROX,  // 50
	        CPUState.O_CLI,    CPUState.AE_ABSY,  CPUState.O_NOP,    CPUState.M_ABSY,   CPUState.AE_ABSX,  CPUState.AE_ABSX,  CPUState.M_ABSX,   CPUState.M_ABSX,   
	        CPUState.O_RTS,    CPUState.A_INDX,   CPUState.NONE,     CPUState.M_INDX,   CPUState.A_ZERO,   CPUState.A_ZERO,   CPUState.M_ZERO,   CPUState.M_ZERO,   // 60
	        CPUState.O_PLA,    CPUState.O_ADC_I,  CPUState.O_ROR_A,  CPUState.O_ARR_I,  CPUState.A_ABS,    CPUState.A_ABS,    CPUState.M_ABS,    CPUState.M_ABS,    
	        CPUState.O_BVS,    CPUState.AE_INDY,  CPUState.NONE,     CPUState.M_INDY,   CPUState.A_ZEROX,  CPUState.A_ZEROX,  CPUState.M_ZEROX,  CPUState.M_ZEROX,  // 70
	        CPUState.O_SEI,    CPUState.AE_ABSY,  CPUState.O_NOP,    CPUState.M_ABSY,   CPUState.AE_ABSX,  CPUState.AE_ABSX,  CPUState.M_ABSX,   CPUState.M_ABSX,   
	        CPUState.O_NOP_I,  CPUState.A_INDX,   CPUState.O_NOP_I,  CPUState.A_INDX,   CPUState.A_ZERO,   CPUState.A_ZERO,   CPUState.A_ZERO,   CPUState.A_ZERO,   // 80
	        CPUState.O_DEY,    CPUState.O_NOP_I,  CPUState.O_TXA,    CPUState.O_ANE_I,  CPUState.A_ABS,    CPUState.A_ABS,    CPUState.A_ABS,    CPUState.A_ABS,    
	        CPUState.O_BCC,    CPUState.A_INDY,   CPUState.NONE,     CPUState.A_INDY,   CPUState.A_ZEROX,  CPUState.A_ZEROX,  CPUState.A_ZEROY,  CPUState.A_ZEROY,  // 90
	        CPUState.O_TYA,    CPUState.A_ABSY,   CPUState.O_TXS,    CPUState.A_ABSY,   CPUState.A_ABSX,   CPUState.A_ABSX,   CPUState.A_ABSY,   CPUState.A_ABSY,   
	        CPUState.O_LDY_I,  CPUState.A_INDX,   CPUState.O_LDX_I,  CPUState.A_INDX,   CPUState.A_ZERO,   CPUState.A_ZERO,   CPUState.A_ZERO,   CPUState.A_ZERO,   // a0
	        CPUState.O_TAY,    CPUState.O_LDA_I,  CPUState.O_TAX,    CPUState.O_LXA_I,  CPUState.A_ABS,    CPUState.A_ABS,    CPUState.A_ABS,    CPUState.A_ABS,    
	        CPUState.O_BCS,    CPUState.AE_INDY,  CPUState.NONE,     CPUState.AE_INDY,  CPUState.A_ZEROX,  CPUState.A_ZEROX,  CPUState.A_ZEROY,  CPUState.A_ZEROY,  // b0
	        CPUState.O_CLV,    CPUState.AE_ABSY,  CPUState.O_TSX,    CPUState.AE_ABSY,  CPUState.AE_ABSX,  CPUState.AE_ABSX,  CPUState.AE_ABSY,  CPUState.AE_ABSY,  
	        CPUState.O_CPY_I,  CPUState.A_INDX,   CPUState.O_NOP_I,  CPUState.M_INDX,   CPUState.A_ZERO,   CPUState.A_ZERO,   CPUState.M_ZERO,   CPUState.M_ZERO,   // c0
	        CPUState.O_INY,    CPUState.O_CMP_I,  CPUState.O_DEX,    CPUState.O_SBX_I,  CPUState.A_ABS,    CPUState.A_ABS,    CPUState.M_ABS,    CPUState.M_ABS,    
	        CPUState.O_BNE,    CPUState.AE_INDY,  CPUState.NONE,     CPUState.M_INDY,   CPUState.A_ZEROX,  CPUState.A_ZEROX,  CPUState.M_ZEROX,  CPUState.M_ZEROX,  // d0
	        CPUState.O_CLD,    CPUState.AE_ABSY,  CPUState.O_NOP,    CPUState.M_ABSY,   CPUState.AE_ABSX,  CPUState.AE_ABSX,  CPUState.M_ABSX,   CPUState.M_ABSX,   
	        CPUState.O_CPX_I,  CPUState.A_INDX,   CPUState.O_NOP_I,  CPUState.M_INDX,   CPUState.A_ZERO,   CPUState.A_ZERO,   CPUState.M_ZERO,   CPUState.M_ZERO,   // e0
	        CPUState.O_INX,    CPUState.O_SBC_I,  CPUState.O_NOP,    CPUState.O_SBC_I,  CPUState.A_ABS,    CPUState.A_ABS,    CPUState.M_ABS,    CPUState.M_ABS,    
	        CPUState.O_BEQ,    CPUState.AE_INDY,  CPUState.O_EXT,    CPUState.M_INDY,   CPUState.A_ZEROX,  CPUState.A_ZEROX,  CPUState.M_ZEROX,  CPUState.M_ZEROX,  // f0
	        CPUState.O_SED,    CPUState.AE_ABSY,  CPUState.O_NOP,    CPUState.M_ABSY,   CPUState.AE_ABSX,  CPUState.AE_ABSX,  CPUState.M_ABSX,   CPUState.M_ABSX
        };

        // Operation for each opcode (second part of execution) (Frodo SC)
        public static CPUState[] OpTab = {
	        CPUState.NONE,		CPUState.O_ORA,     CPUState.NONE,		CPUState.O_SLO,    CPUState.O_NOP_A,  CPUState.O_ORA,    CPUState.O_ASL,    CPUState.O_SLO,    // 00
	        CPUState.NONE,		CPUState.NONE,		CPUState.NONE,		CPUState.NONE,	   CPUState.O_NOP_A,  CPUState.O_ORA,    CPUState.O_ASL,    CPUState.O_SLO,    
	        CPUState.NONE,		CPUState.O_ORA,     CPUState.NONE,		CPUState.O_SLO,    CPUState.O_NOP_A,  CPUState.O_ORA,    CPUState.O_ASL,    CPUState.O_SLO,    // 10
	        CPUState.NONE,		CPUState.O_ORA,     CPUState.NONE,		CPUState.O_SLO,    CPUState.O_NOP_A,  CPUState.O_ORA,    CPUState.O_ASL,    CPUState.O_SLO,    
	        CPUState.NONE,		CPUState.O_AND,     CPUState.NONE,		CPUState.O_RLA,    CPUState.O_BIT,    CPUState.O_AND,    CPUState.O_ROL,    CPUState.O_RLA,    // 20
	        CPUState.NONE,		CPUState.NONE,		CPUState.NONE,		CPUState.NONE,	   CPUState.O_BIT,    CPUState.O_AND,    CPUState.O_ROL,    CPUState.O_RLA,    
	        CPUState.NONE,		CPUState.O_AND,     CPUState.NONE,		CPUState.O_RLA,    CPUState.O_NOP_A,  CPUState.O_AND,    CPUState.O_ROL,    CPUState.O_RLA,    // 30
	        CPUState.NONE,		CPUState.O_AND,     CPUState.NONE,		CPUState.O_RLA,    CPUState.O_NOP_A,  CPUState.O_AND,    CPUState.O_ROL,    CPUState.O_RLA,    
	        CPUState.NONE,		CPUState.O_EOR,     CPUState.NONE,		CPUState.O_SRE,    CPUState.O_NOP_A,  CPUState.O_EOR,    CPUState.O_LSR,    CPUState.O_SRE,    // 40
	        CPUState.NONE,		CPUState.NONE,		CPUState.NONE,		CPUState.NONE,	   CPUState.NONE,	  CPUState.O_EOR,    CPUState.O_LSR,    CPUState.O_SRE,    
	        CPUState.NONE,		CPUState.O_EOR,     CPUState.NONE,		CPUState.O_SRE,    CPUState.O_NOP_A,  CPUState.O_EOR,    CPUState.O_LSR,    CPUState.O_SRE,    // 50
	        CPUState.NONE,		CPUState.O_EOR,     CPUState.NONE,		CPUState.O_SRE,    CPUState.O_NOP_A,  CPUState.O_EOR,    CPUState.O_LSR,    CPUState.O_SRE,    
	        CPUState.NONE,		CPUState.O_ADC,     CPUState.NONE,		CPUState.O_RRA,    CPUState.O_NOP_A,  CPUState.O_ADC,    CPUState.O_ROR,    CPUState.O_RRA,    // 60
	        CPUState.NONE,		CPUState.NONE,		CPUState.NONE,		CPUState.NONE,	   CPUState.O_JMP_I,  CPUState.O_ADC,    CPUState.O_ROR,    CPUState.O_RRA,    
	        CPUState.NONE,		CPUState.O_ADC,     CPUState.NONE,		CPUState.O_RRA,    CPUState.O_NOP_A,  CPUState.O_ADC,    CPUState.O_ROR,    CPUState.O_RRA,    // 70
	        CPUState.NONE,		CPUState.O_ADC,     CPUState.NONE,		CPUState.O_RRA,    CPUState.O_NOP_A,  CPUState.O_ADC,    CPUState.O_ROR,    CPUState.O_RRA,    
	        CPUState.NONE,		CPUState.O_STA,     CPUState.NONE,		CPUState.O_SAX,    CPUState.O_STY,    CPUState.O_STA,    CPUState.O_STX,    CPUState.O_SAX,    // 80
	        CPUState.NONE,		CPUState.NONE,		CPUState.NONE,		CPUState.NONE,	   CPUState.O_STY,    CPUState.O_STA,    CPUState.O_STX,    CPUState.O_SAX,    
	        CPUState.NONE,		CPUState.O_STA,     CPUState.NONE,		CPUState.O_SHA,    CPUState.O_STY,    CPUState.O_STA,    CPUState.O_STX,    CPUState.O_SAX,    // 90
	        CPUState.NONE,		CPUState.O_STA,     CPUState.NONE,		CPUState.O_SHS,    CPUState.O_SHY,    CPUState.O_STA,    CPUState.O_SHX,    CPUState.O_SHA,    
	        CPUState.NONE,		CPUState.O_LDA,     CPUState.NONE,		CPUState.O_LAX,    CPUState.O_LDY,    CPUState.O_LDA,    CPUState.O_LDX,    CPUState.O_LAX,    // a0
	        CPUState.NONE,		CPUState.NONE,		CPUState.NONE,		CPUState.NONE,	   CPUState.O_LDY,    CPUState.O_LDA,    CPUState.O_LDX,    CPUState.O_LAX,    
	        CPUState.NONE,		CPUState.O_LDA,     CPUState.NONE,		CPUState.O_LAX,    CPUState.O_LDY,    CPUState.O_LDA,    CPUState.O_LDX,    CPUState.O_LAX,    // b0
	        CPUState.NONE,		CPUState.O_LDA,     CPUState.NONE,		CPUState.O_LAS,    CPUState.O_LDY,    CPUState.O_LDA,    CPUState.O_LDX,    CPUState.O_LAX,    
	        CPUState.NONE,		CPUState.O_CMP,     CPUState.NONE,		CPUState.O_DCP,    CPUState.O_CPY,    CPUState.O_CMP,    CPUState.O_DEC,    CPUState.O_DCP,    // c0
	        CPUState.NONE,		CPUState.NONE,		CPUState.NONE,		CPUState.NONE,	   CPUState.O_CPY,    CPUState.O_CMP,    CPUState.O_DEC,    CPUState.O_DCP,    
	        CPUState.NONE,		CPUState.O_CMP,     CPUState.NONE,		CPUState.O_DCP,    CPUState.O_NOP_A,  CPUState.O_CMP,    CPUState.O_DEC,    CPUState.O_DCP,    // d0
	        CPUState.NONE,		CPUState.O_CMP,     CPUState.NONE,		CPUState.O_DCP,    CPUState.O_NOP_A,  CPUState.O_CMP,    CPUState.O_DEC,    CPUState.O_DCP,    
	        CPUState.NONE,		CPUState.O_SBC,     CPUState.NONE,		CPUState.O_ISB,    CPUState.O_CPX,    CPUState.O_SBC,    CPUState.O_INC,    CPUState.O_ISB,    // e0
	        CPUState.NONE,		CPUState.NONE,		CPUState.NONE,		CPUState.NONE,	   CPUState.O_CPX,    CPUState.O_SBC,    CPUState.O_INC,    CPUState.O_ISB,    
	        CPUState.NONE,		CPUState.O_SBC,     CPUState.NONE,		CPUState.O_ISB,    CPUState.O_NOP_A,  CPUState.O_SBC,    CPUState.O_INC,    CPUState.O_ISB,    // f0
	        CPUState.NONE,		CPUState.O_SBC,     CPUState.NONE,		CPUState.O_ISB,    CPUState.O_NOP_A,  CPUState.O_SBC,    CPUState.O_INC,    CPUState.O_ISB
};
    }
}
