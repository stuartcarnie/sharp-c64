using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class MOS6569State
    {
        public byte m0x;				// Sprite coordinates
        public byte m0y;
        public byte m1x;
        public byte m1y;
        public byte m2x;
        public byte m2y;
        public byte m3x;
        public byte m3y;
        public byte m4x;
        public byte m4y;
        public byte m5x;
        public byte m5y;
        public byte m6x;
        public byte m6y;
        public byte m7x;
        public byte m7y;
        public byte mx8;

        public byte ctrl1;			// Control registers
        public byte raster;
        public byte lpx;
        public byte lpy;
        public byte me;
        public byte ctrl2;
        public byte mye;
        public byte vbase;
        public byte irq_flag;
        public byte irq_mask;
        public byte mdp;
        public byte mmc;
        public byte mxe;
        public byte mm;
        public byte md;

        public byte ec;				// Color registers
        public byte b0c;
        public byte b1c;
        public byte b2c;
        public byte b3c;
        public byte mm0;
        public byte mm1;
        public byte m0c;
        public byte m1c;
        public byte m2c;
        public byte m3c;
        public byte m4c;
        public byte m5c;
        public byte m6c;
        public byte m7c;
        // Additional registers
        public byte pad0;
        public UInt16 irq_raster;		// IRQ raster line
        public UInt16 vc;				// Video counter
        public UInt16 vc_base;			// Video counter base
        public byte rc;				// Row counter
        public byte spr_dma;			// 8 Flags: Sprite DMA active
        public byte spr_disp;			// 8 Flags: Sprite display active
        public byte[] mc = new byte[8];			// Sprite data counters
        public byte[] mc_base = new byte[8];		// Sprite data counter bases
        public bool display_state;		// true: Display state, false: Idle state
        public bool bad_line;			// Flag: Bad Line state
        public bool bad_line_enable;	// Flag: Bad Lines enabled for this frame
        public bool lp_triggered;		// Flag: Lightpen was triggered in this frame
        public bool border_on;			// Flag: Upper/lower border on (Frodo SC: Main border flipflop)

        public UInt16 bank_base;		// VIC bank base address
        public UInt16 matrix_base;		// Video matrix base
        public UInt16 char_base;		// Character generator base
        public UInt16 bitmap_base;		// Bitmap base
        public UInt16[] sprite_base = new UInt16[8];	// Sprite bases

        // Frodo SC:
        public int cycle;				// Current cycle in line (1..63)
        public UInt16 raster_x;		// Current raster x position
        public int ml_index;			// Index in matrix/color_line[]
        public byte ref_cnt;			// Refresh counter
        public byte last_vic_byte;	// Last byte read by VIC
        public bool ud_border_on;		// Flag: Upper/lower border on
    }
}
