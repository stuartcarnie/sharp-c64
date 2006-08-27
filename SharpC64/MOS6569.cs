using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace SharpC64
{
    /// <summary>
    /// Emulates the VIC-II graphics CPU
    /// </summary>
    public partial class MOS6569 : IDisposable
    {
        #region Public Constants

        // Total number of raster lines (PAL)
        public const UInt32 TOTAL_RASTERS = 0x138;
        // Screen refresh frequency (PAL)
        public const UInt32 SCREEN_FREQ = 50;

        #endregion

        #region Private constants
        // First and last displayed line
        const int FIRST_DISP_LINE = 0x10;
        const int LAST_DISP_LINE = 0x11f;

        // First and last possible line for Bad Lines
        const int FIRST_DMA_LINE = 0x30;
        const int LAST_DMA_LINE = 0xf7;

        // Display window coordinates
        const int ROW25_YSTART = 0x33;
        const int ROW25_YSTOP = 0xfb;
        const int ROW24_YSTART = 0x37;
        const int ROW24_YSTOP = 0xf7;

        const int COL40_XSTART = 0x20;
        const int COL40_XSTOP = 0x160;
        const int COL38_XSTART = 0x27;
        const int COL38_XSTOP = 0x157;

        #endregion

        #region Public methods

        public MOS6569(C64 c64, C64Display disp, MOS6510 CPU, byte[] RAM, byte[] Char, byte[] Color)
        {
            the_c64 = c64;
            the_display = disp;
            the_cpu = CPU;
            ram = RAM;
            char_rom = Char;
            color_ram = Color;

            int i;

            matrix_base = 0;
            char_base = 0;
            bitmap_base = 0;

            // Allocate GC Handles, to pin buffers
            AllocateHandles();

            // Get bitmap info
            unsafe
            {
                chunky_ptr = chunky_line_start = the_display.BitmapBase;
            }
            xmod = disp.BitmapXMod;

            // Initialize VIC registers
            mx8 = 0;
            ctrl1 = ctrl2 = 0;
            lpx = lpy = 0;
            me = mxe = mye = mdp = mmc = 0;
            vbase = irq_flag = irq_mask = 0;
            clx_spr = clx_bgr = 0;
            cia_vabase = 0;
            ec = b0c = b1c = b2c = b3c = mm0 = mm1 = 0;

            // already 0 for .NET
            //for (i = 0; i < 8; i++) mx[i] = my[i] = sc[i] = 0;

            // Initialize other variables
            raster_y = (UInt16)(TOTAL_RASTERS - 1);
            rc = 7;
            irq_raster = vc = vc_base = x_scroll = y_scroll = 0;
            dy_start = ROW24_YSTART;
            dy_stop = ROW24_YSTOP;
            ml_index = 0;

            cycle = 1;
            display_idx = 0;
            display_state = false;
            border_on = ud_border_on = vblanking = false;
            lp_triggered = draw_this_line = false;

            spr_dma_on = spr_disp_on = 0;
            for (i = 0; i < 8; i++)
            {
                mc[i] = 63;
                spr_ptr[i] = 0;
            }

            frame_skipped = false;
            skip_counter = 1;

            // CLR initializez to zero
            //memset(spr_coll_buf, 0, 0x180);
            //memset(fore_mask_buf, 0, 0x180 / 8);

            // Preset colors to black
            disp.InitColors(colors);
            ec_color = b0c_color = b1c_color = b2c_color = b3c_color = mm0_color = mm1_color = colors[0];
            for (i = 0; i < spr_color.Length; i++) spr_color[i] = colors[0];

        }

        public byte ReadRegister(UInt16 adr)
        {
            switch (adr)
            {
                case 0x00:
                case 0x02:
                case 0x04:
                case 0x06:
                case 0x08:
                case 0x0a:
                case 0x0c:
                case 0x0e:
                    return (byte)mx[adr >> 1];

                case 0x01:
                case 0x03:
                case 0x05:
                case 0x07:
                case 0x09:
                case 0x0b:
                case 0x0d:
                case 0x0f:
                    return my[adr >> 1];

                case 0x10:	// Sprite X position MSB
                    return mx8;

                case 0x11:	// Control register 1
                    return (byte)((ctrl1 & 0x7f) | ((raster_y & 0x100) >> 1));

                case 0x12:	// Raster counter
                    return (byte)raster_y;

                case 0x13:	// Light pen X
                    return lpx;

                case 0x14:	// Light pen Y
                    return lpy;

                case 0x15:	// Sprite enable
                    return me;

                case 0x16:	// Control register 2
                    return (byte)(ctrl2 | 0xc0);

                case 0x17:	// Sprite Y expansion
                    return mye;

                case 0x18:	// Memory pointers
                    return (byte)(vbase | 0x01);

                case 0x19:	// IRQ flags
                    return (byte)(irq_flag | 0x70);

                case 0x1a:	// IRQ mask
                    return (byte)(irq_mask | 0xf0);

                case 0x1b:	// Sprite data priority
                    return mdp;

                case 0x1c:	// Sprite multicolor
                    return mmc;

                case 0x1d:	// Sprite X expansion
                    return mxe;

                case 0x1e:
                    {	// Sprite-sprite collision
                        byte ret = clx_spr;
                        clx_spr = 0;	// Read and clear
                        return ret;
                    }

                case 0x1f:
                    {	// Sprite-background collision
                        byte ret = clx_bgr;
                        clx_bgr = 0;	// Read and clear
                        return ret;
                    }

                case 0x20: return (byte)(ec | 0xf0);
                case 0x21: return (byte)(b0c | 0xf0);
                case 0x22: return (byte)(b1c | 0xf0);
                case 0x23: return (byte)(b2c | 0xf0);
                case 0x24: return (byte)(b3c | 0xf0);
                case 0x25: return (byte)(mm0 | 0xf0);
                case 0x26: return (byte)(mm1 | 0xf0);

                case 0x27:
                case 0x28:
                case 0x29:
                case 0x2a:
                case 0x2b:
                case 0x2c:
                case 0x2d:
                case 0x2e:
                    return (byte)(sc[adr - 0x27] | 0xf0);

                default:
                    return 0xff;
            }
        }

        public void WriteRegister(UInt16 adr, byte abyte)
        {
            switch (adr)
            {
                case 0x00:
                case 0x02:
                case 0x04:
                case 0x06:
                case 0x08:
                case 0x0a:
                case 0x0c:
                case 0x0e:
                    mx[adr >> 1] = (UInt16)((mx[adr >> 1] & 0xff00) | abyte);
                    break;

                case 0x10:
                    {
                        int i, j;
                        mx8 = abyte;
                        for (i = 0, j = 1; i < 8; i++, j <<= 1)
                        {
                            if ((mx8 & j) != 0)
                                mx[i] |= 0x100;
                            else
                                mx[i] &= 0xff;
                        }
                        break;
                    }

                case 0x01:
                case 0x03:
                case 0x05:
                case 0x07:
                case 0x09:
                case 0x0b:
                case 0x0d:
                case 0x0f:
                    my[adr >> 1] = abyte;
                    break;

                case 0x11:
                    {	// Control register 1
                        ctrl1 = abyte;
                        y_scroll = (UInt16)(abyte & 7);

                        UInt16 new_irq_raster = (UInt16)((irq_raster & 0xff) | ((abyte & 0x80) << 1));
                        if (irq_raster != new_irq_raster && raster_y == new_irq_raster)
                            raster_irq();
                        irq_raster = new_irq_raster;

                        if ((abyte & 8) != 0)
                        {
                            dy_start = ROW25_YSTART;
                            dy_stop = ROW25_YSTOP;
                        }
                        else
                        {
                            dy_start = ROW24_YSTART;
                            dy_stop = ROW24_YSTOP;
                        }

                        // In line $30, the DEN bit controls if Bad Lines can occur
                        if (raster_y == 0x30 && (abyte & 0x10) != 0)
                            bad_lines_enabled = true;

                        // Bad Line condition?
                        is_bad_line = (raster_y >= FIRST_DMA_LINE && raster_y <= LAST_DMA_LINE && ((raster_y & 7) == y_scroll) && bad_lines_enabled);

                        display_idx = ((ctrl1 & 0x60) | (ctrl2 & 0x10)) >> 4;
                        break;
                    }

                case 0x12:
                    {	// Raster counter
                        UInt16 new_irq_raster = (UInt16)((irq_raster & 0xff00) | abyte);
                        if (irq_raster != new_irq_raster && raster_y == new_irq_raster)
                            raster_irq();
                        irq_raster = new_irq_raster;
                        break;
                    }

                case 0x15:	// Sprite enable
                    me = abyte;
                    break;

                case 0x16:	// Control register 2
                    ctrl2 = abyte;
                    x_scroll = (UInt16)(abyte & 7);
                    display_idx = ((ctrl1 & 0x60) | (ctrl2 & 0x10)) >> 4;
                    break;

                case 0x17:	// Sprite Y expansion
                    mye = abyte;
                    spr_exp_y |= (byte)~abyte;
                    break;

                case 0x18:	// Memory pointers
                    vbase = abyte;
                    matrix_base = (UInt16)((abyte & 0xf0) << 6);
                    char_base = (UInt16)((abyte & 0x0e) << 10);
                    bitmap_base = (UInt16)((abyte & 0x08) << 10);
                    break;

                case 0x19: // IRQ flags
                    irq_flag = (byte)(irq_flag & (~abyte & 0x0f));
                    if ((irq_flag & irq_mask) != 0)	// Set master bit if allowed interrupt still pending
                        irq_flag |= 0x80;
                    else
                        the_cpu.ClearVICIRQ();	// Else clear interrupt
                    break;

                case 0x1a:	// IRQ mask
                    irq_mask = (byte)(abyte & 0x0f);
                    if ((irq_flag & irq_mask) != 0)
                    {	// Trigger interrupt if pending and now allowed
                        irq_flag |= 0x80;
                        the_cpu.TriggerVICIRQ();
                    }
                    else
                    {
                        irq_flag &= 0x7f;
                        the_cpu.ClearVICIRQ();
                    }
                    break;

                case 0x1b:	// Sprite data priority
                    mdp = abyte;
                    break;

                case 0x1c:	// Sprite multicolor
                    mmc = abyte;
                    break;

                case 0x1d:	// Sprite X expansion
                    mxe = abyte;
                    break;

                case 0x20: ec_color = colors[ec = abyte]; break;
                case 0x21: b0c_color = colors[b0c = abyte]; break;
                case 0x22: b1c_color = colors[b1c = abyte]; break;
                case 0x23: b2c_color = colors[b2c = abyte]; break;
                case 0x24: b3c_color = colors[b3c = abyte]; break;
                case 0x25: mm0_color = colors[mm0 = abyte]; break;
                case 0x26: mm1_color = colors[mm1 = abyte]; break;

                case 0x27:
                case 0x28:
                case 0x29:
                case 0x2a:
                case 0x2b:
                case 0x2c:
                case 0x2d:
                case 0x2e:
                    spr_color[adr - 0x27] = colors[sc[adr - 0x27] = abyte];
                    break;
            }
        }

        public void ChangedVA(UInt16 new_va)
        {
            cia_vabase = (UInt16)(new_va << 14);
            WriteRegister(0x18, vbase); // Force update of memory pointers
        }

        public void TriggerLightpen()
        {
            if (!lp_triggered)
            {		// Lightpen triggers only once per frame
                lp_triggered = true;

                lpx = (byte)(raster_x >> 1);	// Latch current coordinates
                lpy = (byte)raster_y;

                irq_flag |= 0x08;		// Trigger IRQ
                if ((irq_mask & 0x08) != 0)
                {
                    irq_flag |= 0x80;
                    the_cpu.TriggerVICIRQ();
                }
            }
        }

        unsafe internal void ReInitColors()
        {
            int i;

            // Build inverse color table.
            byte[] xlate_colors = new byte[256];

            for (i = 0; i < 16; i++)
                xlate_colors[colors[i]] = (byte)i;

            // Get the new colors.
            the_display.InitColors(colors);

            // Build color translation table.
            for (i = 0; i < 256; i++)
                xlate_colors[i] = colors[xlate_colors[i]];

            // Translate all the old colors variables.
            ec_color = colors[ec];
            b0c_color = colors[b0c];
            b1c_color = colors[b1c];
            b2c_color = colors[b2c];
            b3c_color = colors[b3c];
            mm0_color = colors[mm0];
            mm1_color = colors[mm1];
            for (i = 0; i < 8; i++)
                spr_color[i] = colors[sc[i]];

            // Translate the border color sample buffer.
            for (int x = 0; x < border_color_sample.Length; x++)
                border_color_sample[x] = xlate_colors[border_color_sample[x]];

            // Translate the chunky buffer.
            byte* scanline = the_display.BitmapBase;
            for (int y = 0; y < C64Display.DISPLAY_Y; y++)
            {
                for (int x = 0; x < C64Display.DISPLAY_X; x++)
                    scanline[x] = xlate_colors[scanline[x]];
                scanline += xmod;
            }
        }

        #endregion

        #region Public properties

        public byte LastVICByte
        {
            get { return _LastVICByte; }
            set { _LastVICByte = value; }
        }

        public MOS6569State State
        {
            get
            {
                MOS6569State vd = new MOS6569State();
                int i;

                vd.m0x = (byte)(mx[0] & 0xff); vd.m0y = my[0];
                vd.m1x = (byte)(mx[1] & 0xff); vd.m1y = my[1];
                vd.m2x = (byte)(mx[2] & 0xff); vd.m2y = my[2];
                vd.m3x = (byte)(mx[3] & 0xff); vd.m3y = my[3];
                vd.m4x = (byte)(mx[4] & 0xff); vd.m4y = my[4];
                vd.m5x = (byte)(mx[5] & 0xff); vd.m5y = my[5];
                vd.m6x = (byte)(mx[6] & 0xff); vd.m6y = my[6];
                vd.m7x = (byte)(mx[7] & 0xff); vd.m7y = my[7];
                vd.mx8 = mx8;

                vd.ctrl1 = (byte)((ctrl1 & 0x7f) | ((raster_y & 0x100) >> 1));
                vd.raster = (byte)(raster_y & 0xff);
                vd.lpx = lpx; vd.lpy = lpy;
                vd.ctrl2 = ctrl2;
                vd.vbase = vbase;
                vd.irq_flag = irq_flag;
                vd.irq_mask = irq_mask;

                vd.me = me; vd.mxe = mxe; vd.mye = mye; vd.mdp = mdp; vd.mmc = mmc;
                vd.mm = clx_spr; vd.md = clx_bgr;

                vd.ec = ec;
                vd.b0c = b0c; vd.b1c = b1c; vd.b2c = b2c; vd.b3c = b3c;
                vd.mm0 = mm0; vd.mm1 = mm1;
                vd.m0c = sc[0];
                vd.m1c = sc[1];
                vd.m2c = sc[2];
                vd.m3c = sc[3];
                vd.m4c = sc[4];
                vd.m5c = sc[5];
                vd.m6c = sc[6];
                vd.m7c = sc[7];

                vd.pad0 = 0;
                vd.irq_raster = irq_raster;
                vd.vc = vc;
                vd.vc_base = vc_base;
                vd.rc = (byte)rc;
                vd.spr_dma = spr_dma_on;
                vd.spr_disp = spr_disp_on;
                for (i = 0; i < 8; i++)
                {
                    vd.mc[i] = (byte)mc[i];
                    vd.mc_base[i] = (byte)mc_base[i];
                }
                vd.display_state = display_state;
                vd.bad_line = raster_y >= FIRST_DMA_LINE && raster_y <= LAST_DMA_LINE && ((raster_y & 7) == y_scroll) && bad_lines_enabled;
                vd.bad_line_enable = bad_lines_enabled;
                vd.lp_triggered = lp_triggered;
                vd.border_on = border_on;

                vd.bank_base = cia_vabase;
                vd.matrix_base = (UInt16)(((vbase & 0xf0) << 6) | cia_vabase);
                vd.char_base = (UInt16)(((vbase & 0x0e) << 10) | cia_vabase);
                vd.bitmap_base = (UInt16)(((vbase & 0x08) << 10) | cia_vabase);
                for (i = 0; i < 8; i++)
                    vd.sprite_base[i] = (UInt16)(spr_ptr[i] | cia_vabase);

                vd.cycle = cycle;
                vd.raster_x = raster_x;
                vd.ml_index = ml_index;
                vd.ref_cnt = ref_cnt;
                vd.last_vic_byte = LastVICByte;
                vd.ud_border_on = ud_border_on;

                return vd;
            }

            set
            {
                int i, j;

                mx[0] = value.m0x; my[0] = value.m0y;
                mx[1] = value.m1x; my[1] = value.m1y;
                mx[2] = value.m2x; my[2] = value.m2y;
                mx[3] = value.m3x; my[3] = value.m3y;
                mx[4] = value.m4x; my[4] = value.m4y;
                mx[5] = value.m5x; my[5] = value.m5y;
                mx[6] = value.m6x; my[6] = value.m6y;
                mx[7] = value.m7x; my[7] = value.m7y;
                mx8 = value.mx8;
                for (i = 0, j = 1; i < 8; i++, j <<= 1)
                {
                    if ((mx8 & j) != 0)
                        mx[i] |= 0x100;
                    else
                        mx[i] &= 0xff;
                }

                ctrl1 = value.ctrl1;
                ctrl2 = value.ctrl2;
                x_scroll = (UInt16)(ctrl2 & 7);
                y_scroll = (UInt16)(ctrl1 & 7);
                if ((ctrl1 & 8) != 0)
                {
                    dy_start = ROW25_YSTART;
                    dy_stop = ROW25_YSTOP;
                }
                else
                {
                    dy_start = ROW24_YSTART;
                    dy_stop = ROW24_YSTOP;
                }
                display_idx = ((ctrl1 & 0x60) | (ctrl2 & 0x10)) >> 4;

                raster_y = 0;
                lpx = value.lpx; lpy = value.lpy;

                vbase = value.vbase;
                cia_vabase = value.bank_base;
                matrix_base = (UInt16)((vbase & 0xf0) << 6);
                char_base = (UInt16)((vbase & 0x0e) << 10);
                bitmap_base = (UInt16)((vbase & 0x08) << 10);

                irq_flag = value.irq_flag;
                irq_mask = value.irq_mask;

                me = value.me; mxe = value.mxe; mye = value.mye; mdp = value.mdp; mmc = value.mmc;
                clx_spr = value.mm; clx_bgr = value.md;

                ec = value.ec;
                ec_color = colors[ec];

                b0c = value.b0c; b1c = value.b1c; b2c = value.b2c; b3c = value.b3c;
                b0c_color = colors[b0c];
                b1c_color = colors[b1c];
                b2c_color = colors[b2c];
                b3c_color = colors[b3c];

                mm0 = value.mm0; mm1 = value.mm1;
                mm0_color = colors[mm0];
                mm1_color = colors[mm1];

                sc[0] = value.m0c; sc[1] = value.m1c;
                sc[2] = value.m2c; sc[3] = value.m3c;
                sc[4] = value.m4c; sc[5] = value.m5c;
                sc[6] = value.m6c; sc[7] = value.m7c;
                for (i = 0; i < 8; i++)
                    spr_color[i] = colors[sc[i]];

                irq_raster = value.irq_raster;
                vc = value.vc;
                vc_base = value.vc_base;
                rc = value.rc;
                spr_dma_on = value.spr_dma;
                spr_disp_on = value.spr_disp;
                for (i = 0; i < 8; i++)
                {
                    mc[i] = value.mc[i];
                    mc_base[i] = value.mc_base[i];
                    spr_ptr[i] = (UInt16)(value.sprite_base[i] & 0x3fff);
                }
                display_state = value.display_state;
                bad_lines_enabled = value.bad_line_enable;
                lp_triggered = value.lp_triggered;
                border_on = value.border_on;

                cycle = value.cycle;
                raster_x = value.raster_x;
                ml_index = value.ml_index;
                ref_cnt = value.ref_cnt;
                LastVICByte = value.last_vic_byte;
                ud_border_on = value.ud_border_on;
            }
        }

        #endregion

        #region Private members

        void vblank()
        {
        }

        void raster_irq()
        {
            irq_flag |= 0x01;
            if ((irq_mask & 0x01) != 0)
            {
                irq_flag |= 0x80;
                the_cpu.TriggerVICIRQ();
            }
        }

        byte read_byte(UInt16 adr)
        {
            UInt16 va = (UInt16)(adr | cia_vabase);
            if ((va & 0x7000) == 0x1000)
                return LastVICByte = char_rom[va & 0x0fff];
            else
                return LastVICByte = ram[va];
        }

        void matrix_access()
        {
            if (the_cpu.BALow)
            {
                if (the_c64.CycleCounter - first_ba_cycle < 3)
                    matrix_line[ml_index] = color_line[ml_index] = 0xff;
                else
                {
                    UInt16 adr = (UInt16)((vc & 0x03ff) | matrix_base);
                    matrix_line[ml_index] = read_byte(adr);
                    color_line[ml_index] = color_ram[adr & 0x03ff];
                }
            }
        }

        void graphics_access()
        {
            if (display_state)
            {
                UInt16 adr;
                if ((ctrl1 & 0x20) != 0)
                    // Bitmap
                    adr = (UInt16)(((vc & 0x03ff) << 3) | bitmap_base | rc);
                else
                    // Text
                    adr = (UInt16)((matrix_line[ml_index] << 3) | char_base | rc);

                if ((ctrl1 & 0x40) != 0)
                    // ECM
                    adr &= 0xf9ff;
                gfx_data = read_byte(adr);
                char_data = matrix_line[ml_index];
                color_data = color_line[ml_index];
                ml_index++;
                vc++;

            }
            else
            {
                // Display is off
                gfx_data = read_byte((UInt16)((ctrl1 & 0x40) != 0 ? 0x39ff : 0x3fff));
                char_data = color_data = 0;
            }
        }

        unsafe void draw_graphics()
        {
            if (!draw_this_line)
                return;

            if (ud_border_on)
            {
                draw_background();
                return;
            }

            byte* p = chunky_ptr + x_scroll;
            byte* c = stackalloc byte[4];
            byte data;

            switch (display_idx)
            {

                case 0:		// Standard text
                    c[0] = b0c_color;
                    c[1] = colors[color_data];
                    goto draw_std;

                case 1:		// Multicolor text
                    if ((color_data & 8) != 0)
                    {
                        c[0] = b0c_color;
                        c[1] = b1c_color;
                        c[2] = b2c_color;
                        c[3] = colors[color_data & 7];
                        goto draw_multi;
                    }
                    else
                    {
                        c[0] = b0c_color;
                        c[1] = colors[color_data];
                        goto draw_std;
                    }

                case 2:		// Standard bitmap
                    c[0] = colors[char_data];
                    c[1] = colors[char_data >> 4];
                    goto draw_std;

                case 3:		// Multicolor bitmap
                    c[0] = b0c_color;
                    c[1] = colors[char_data >> 4];
                    c[2] = colors[char_data];
                    c[3] = colors[color_data];
                    goto draw_multi;

                case 4:		// ECM text
                    if ((char_data & 0x80) != 0)
                        if ((char_data & 0x40) != 0)
                            c[0] = b3c_color;
                        else
                            c[0] = b2c_color;
                    else
                        if ((char_data & 0x40) != 0)
                            c[0] = b1c_color;
                        else
                            c[0] = b0c_color;
                    c[1] = colors[color_data];
                    goto draw_std;

                case 5:		// Invalid multicolor text
                    p[0] = p[1] = p[2] = p[3] = p[4] = p[5] = p[6] = p[7] = colors[0];
                    if ((color_data & 8) != 0)
                    {
                        fore_mask_ptr[0] |= (byte)(((gfx_data & 0xaa) | (gfx_data & 0xaa) >> 1) >> x_scroll);
                        fore_mask_ptr[1] |= (byte)(((gfx_data & 0xaa) | (gfx_data & 0xaa) >> 1) << (8 - x_scroll));
                    }
                    else
                    {
                        fore_mask_ptr[0] |= (byte)(gfx_data >> x_scroll);
                        fore_mask_ptr[1] |= (byte)(gfx_data << (7 - x_scroll));
                    }
                    return;

                case 6:		// Invalid standard bitmap
                    p[0] = p[1] = p[2] = p[3] = p[4] = p[5] = p[6] = p[7] = colors[0];
                    fore_mask_ptr[0] |= (byte)(gfx_data >> x_scroll);
                    fore_mask_ptr[1] |= (byte)(gfx_data << (7 - x_scroll));
                    return;

                case 7:		// Invalid multicolor bitmap
                    p[0] = p[1] = p[2] = p[3] = p[4] = p[5] = p[6] = p[7] = colors[0];
                    fore_mask_ptr[0] |= (byte)(((gfx_data & 0xaa) | (gfx_data & 0xaa) >> 1) >> x_scroll);
                    fore_mask_ptr[1] |= (byte)(((gfx_data & 0xaa) | (gfx_data & 0xaa) >> 1) << (8 - x_scroll));
                    return;

                default:	// Can't happen
                    return;
            }

        draw_std:

            fore_mask_ptr[0] |= (byte)(gfx_data >> x_scroll);
            fore_mask_ptr[1] |= (byte)(gfx_data << (7 - x_scroll));

            data = gfx_data;
            p[7] = c[data & 1]; data >>= 1;
            p[6] = c[data & 1]; data >>= 1;
            p[5] = c[data & 1]; data >>= 1;
            p[4] = c[data & 1]; data >>= 1;
            p[3] = c[data & 1]; data >>= 1;
            p[2] = c[data & 1]; data >>= 1;
            p[1] = c[data & 1]; data >>= 1;
            p[0] = c[data];
            return;

        draw_multi:

            fore_mask_ptr[0] |= (byte)(((gfx_data & 0xaa) | (gfx_data & 0xaa) >> 1) >> x_scroll);
            fore_mask_ptr[1] |= (byte)(((gfx_data & 0xaa) | (gfx_data & 0xaa) >> 1) << (8 - x_scroll));

            data = gfx_data;
            p[7] = p[6] = c[data & 3]; data >>= 2;
            p[5] = p[4] = c[data & 3]; data >>= 2;
            p[3] = p[2] = c[data & 3]; data >>= 2;
            p[1] = p[0] = c[data];
            return;
        }

        unsafe void draw_sprites()
        {
            int i;
            int snum, sbit;		// Sprite number/bit mask
            int spr_coll = 0, gfx_coll = 0;

            // Clear sprite collision buffer
            {
                UInt32* lp = (UInt32*)spr_coll_buf_ptr - 1;
                for (i = 0; i < C64Display.DISPLAY_X / 4; i++)
                    *++lp = 0;
            }

            // Loop for all sprites
            for (snum = 0, sbit = 1; snum < 8; snum++, sbit <<= 1)
            {

                // Is sprite visible?
                if ((spr_draw & sbit) != 0 && mx[snum] <= C64Display.DISPLAY_X - 32)
                {
                    byte* p = chunky_line_start + mx[snum] + 8;
                    byte* q = (byte*)spr_coll_buf_ptr + mx[snum] + 8;
                    byte color = spr_color[snum];

                    // Fetch sprite data and mask
                    UInt32 sdata = (UInt32)((spr_draw_data_ptr[snum * 4] << 24) | (spr_draw_data_ptr[snum * 4 + 1] << 16) | (spr_draw_data_ptr[snum * 4 + 2] << 8));

                    int spr_mask_pos = mx[snum] + 8;	// Sprite bit position in fore_mask_buf

                    byte* fmbp = fore_mask_buf_ptr + (spr_mask_pos / 8);
                    int sshift = spr_mask_pos & 7;
                    UInt32 fore_mask = (UInt32)((((*(fmbp + 0) << 24) | (*(fmbp + 1) << 16) | (*(fmbp + 2) << 8)
                                    | (*(fmbp + 3))) << sshift) | (*(fmbp + 4) >> (8 - sshift)));

                    if ((mxe & sbit) != 0)
                    {		// X-expanded
                        if (mx[snum] > C64Display.DISPLAY_X - 56)
                            continue;

                        UInt32 sdata_l = 0, sdata_r = 0, fore_mask_r;
                        fore_mask_r = (UInt32)((((*(fmbp + 4) << 24) | (*(fmbp + 5) << 16) | (*(fmbp + 6) << 8)
                                | (*(fmbp + 7))) << sshift) | (*(fmbp + 8) >> (8 - sshift)));

                        if ((mmc & sbit) != 0)
                        {	// Multicolor mode
                            UInt32 plane0_l, plane0_r, plane1_l, plane1_r;

                            // Expand sprite data
                            sdata_l = (UInt32)(MultiExpTable_ptr[sdata >> 24 & 0xff] << 16 | MultiExpTable_ptr[sdata >> 16 & 0xff]);
                            sdata_r = (UInt32)(MultiExpTable_ptr[sdata >> 8 & 0xff] << 16);

                            // Convert sprite chunky pixels to bitplanes
                            plane0_l = (sdata_l & 0x55555555) | (sdata_l & 0x55555555) << 1;
                            plane1_l = (sdata_l & 0xaaaaaaaa) | (sdata_l & 0xaaaaaaaa) >> 1;
                            plane0_r = (sdata_r & 0x55555555) | (sdata_r & 0x55555555) << 1;
                            plane1_r = (sdata_r & 0xaaaaaaaa) | (sdata_r & 0xaaaaaaaa) >> 1;

                            // Collision with graphics?
                            if ((fore_mask & (plane0_l | plane1_l)) != 0 || (fore_mask_r & (plane0_r | plane1_r)) != 0)
                            {
                                gfx_coll |= sbit;
                                if ((mdp & sbit) != 0)
                                {
                                    plane0_l &= ~fore_mask;	// Mask sprite if in background
                                    plane1_l &= ~fore_mask;
                                    plane0_r &= ~fore_mask_r;
                                    plane1_r &= ~fore_mask_r;
                                }
                            }

                            // Paint sprite
                            for (i = 0; i < 32; i++, plane0_l <<= 1, plane1_l <<= 1)
                            {
                                byte col;
                                if ((plane1_l & 0x80000000) != 0)
                                {
                                    if ((plane0_l & 0x80000000) != 0)
                                        col = mm1_color;
                                    else
                                        col = color;
                                }
                                else
                                {
                                    if ((plane0_l & 0x80000000) != 0)
                                        col = mm0_color;
                                    else
                                        continue;
                                }
                                if (q[i] != 0)
                                    spr_coll |= q[i] | sbit;
                                else
                                {
                                    p[i] = col;
                                    q[i] = (byte)sbit;
                                }
                            }
                            for (; i < 48; i++, plane0_r <<= 1, plane1_r <<= 1)
                            {
                                byte col;
                                if ((plane1_r & 0x80000000) != 0)
                                {
                                    if ((plane0_r & 0x80000000) != 0)
                                        col = mm1_color;
                                    else
                                        col = color;
                                }
                                else
                                {
                                    if ((plane0_r & 0x80000000) != 0)
                                        col = mm0_color;
                                    else
                                        continue;
                                }
                                if (q[i] != 0)
                                    spr_coll |= q[i] | sbit;
                                else
                                {
                                    p[i] = col;
                                    q[i] = (byte)sbit;
                                }
                            }

                        }
                        else
                        {			// Standard mode

                            // Expand sprite data
                            sdata_l = (UInt32)(ExpTable_ptr[sdata >> 24 & 0xff] << 16 | ExpTable_ptr[sdata >> 16 & 0xff]);
                            sdata_r = (UInt32)(ExpTable_ptr[sdata >> 8 & 0xff] << 16);

                            // Collision with graphics?
                            if ((fore_mask & sdata_l) != 0 || (fore_mask_r & sdata_r) != 0)
                            {
                                gfx_coll |= sbit;
                                if ((mdp & sbit) != 0)
                                {
                                    sdata_l &= ~fore_mask;	// Mask sprite if in background
                                    sdata_r &= ~fore_mask_r;
                                }
                            }

                            // Paint sprite
                            for (i = 0; i < 32; i++, sdata_l <<= 1)
                                if ((sdata_l & 0x80000000) != 0)
                                {
                                    if (q[i] != 0)	// Collision with sprite?
                                        spr_coll |= q[i] | sbit;
                                    else
                                    {		// Draw pixel if no collision
                                        p[i] = color;
                                        q[i] = (byte)sbit;
                                    }
                                }
                            for (; i < 48; i++, sdata_r <<= 1)
                                if ((sdata_r & 0x80000000) != 0)
                                {
                                    if (q[i] != 0) 	// Collision with sprite?
                                        spr_coll |= q[i] | sbit;
                                    else
                                    {		// Draw pixel if no collision
                                        p[i] = color;
                                        q[i] = (byte)sbit;
                                    }
                                }
                        }

                    }
                    else
                    {				// Unexpanded

                        if ((mmc & sbit) != 0)
                        {	// Multicolor mode
                            UInt32 plane0, plane1;

                            // Convert sprite chunky pixels to bitplanes
                            plane0 = (sdata & 0x55555555) | (sdata & 0x55555555) << 1;
                            plane1 = (sdata & 0xaaaaaaaa) | (sdata & 0xaaaaaaaa) >> 1;

                            // Collision with graphics?
                            if ((fore_mask & (plane0 | plane1)) != 0)
                            {
                                gfx_coll |= sbit;
                                if ((mdp & sbit) != 0)
                                {
                                    plane0 &= ~fore_mask;	// Mask sprite if in background
                                    plane1 &= ~fore_mask;
                                }
                            }

                            // Paint sprite
                            for (i = 0; i < 24; i++, plane0 <<= 1, plane1 <<= 1)
                            {
                                byte col;
                                if ((plane1 & 0x80000000) != 0)
                                {
                                    if ((plane0 & 0x80000000) != 0)
                                        col = mm1_color;
                                    else
                                        col = color;
                                }
                                else
                                {
                                    if ((plane0 & 0x80000000) != 0)
                                        col = mm0_color;
                                    else
                                        continue;
                                }
                                if (q[i] != 0)
                                    spr_coll |= q[i] | sbit;
                                else
                                {
                                    p[i] = col;
                                    q[i] = (byte)sbit;
                                }
                            }

                        }
                        else
                        {			// Standard mode

                            // Collision with graphics?
                            if ((fore_mask & sdata) != 0)
                            {
                                gfx_coll |= sbit;
                                if ((mdp & sbit) != 0)
                                    sdata &= ~fore_mask;	// Mask sprite if in background
                            }

                            // Paint sprite
                            for (i = 0; i < 24; i++, sdata <<= 1)
                                if ((sdata & 0x80000000) != 0)
                                {
                                    if (q[i] != 0)
                                    {	// Collision with sprite?
                                        spr_coll |= q[i] | sbit;
                                    }
                                    else
                                    {		// Draw pixel if no collision
                                        p[i] = color;
                                        q[i] = (byte)sbit;
                                    }
                                }
                        }
                    }
                }
            }

            if (GlobalPrefs.ThePrefs.SpriteCollisions)
            {

                // Check sprite-sprite collisions
                if (clx_spr != 0)
                    clx_spr |= (byte)spr_coll;
                else
                {
                    clx_spr |= (byte)spr_coll;
                    irq_flag |= 0x04;
                    if ((irq_mask & 0x04) != 0)
                    {
                        irq_flag |= 0x80;
                        the_cpu.TriggerVICIRQ();
                    }
                }

                // Check sprite-background collisions
                if (clx_bgr != 0)
                    clx_bgr |= (byte)gfx_coll;
                else
                {
                    clx_bgr |= (byte)gfx_coll;
                    irq_flag |= 0x02;
                    if ((irq_mask & 0x02) != 0)
                    {
                        irq_flag |= 0x80;
                        the_cpu.TriggerVICIRQ();
                    }
                }
            }
        }

        unsafe void draw_background()
        {
            byte c;

            if (!draw_this_line)
                return;

            switch (display_idx)
            {
                case 0:		// Standard text
                case 1:		// Multicolor text
                case 3:		// Multicolor bitmap
                    c = b0c_color;
                    break;

                case 2:		// Standard bitmap
                    c = colors[last_char_data];
                    break;

                case 4:		// ECM text
                    if ((last_char_data & 0x80) != 0)
                        if ((last_char_data & 0x40) != 0)
                            c = b3c_color;
                        else
                            c = b2c_color;
                    else
                        if ((last_char_data & 0x40) != 0)
                            c = b1c_color;
                        else
                            c = b0c_color;
                    break;

                default:
                    c = colors[0];
                    break;
            }

            byte* p = chunky_ptr;
            p[0] = p[1] = p[2] = p[3] = p[4] = p[5] = p[6] = p[7] = c;
        }

        unsafe private void AllocateHandles()
        {
            spr_coll_buf_handle = GCHandle.Alloc(spr_coll_buf, GCHandleType.Pinned);
            spr_coll_buf_ptr = (byte*)spr_coll_buf_handle.AddrOfPinnedObject();

            fore_mask_buf_handle = GCHandle.Alloc(fore_mask_buf, GCHandleType.Pinned);
            fore_mask_buf_ptr = (byte*)fore_mask_buf_handle.AddrOfPinnedObject();

            text_chunky_buf_handle = GCHandle.Alloc(text_chunky_buf, GCHandleType.Pinned);
            text_chunky_buf_ptr = (byte*)text_chunky_buf_handle.AddrOfPinnedObject();

            spr_data_handle = GCHandle.Alloc(spr_data, GCHandleType.Pinned);
            spr_data_ptr = (byte*)spr_data_handle.AddrOfPinnedObject();

            spr_draw_data_handle = GCHandle.Alloc(spr_draw_data, GCHandleType.Pinned);
            spr_draw_data_ptr = (byte*)spr_draw_data_handle.AddrOfPinnedObject();

            ExpTable_handle = GCHandle.Alloc(ExpTable, GCHandleType.Pinned);
            ExpTable_ptr = (UInt16*)ExpTable_handle.AddrOfPinnedObject();

            MultiExpTable_handle = GCHandle.Alloc(MultiExpTable, GCHandleType.Pinned);
            MultiExpTable_ptr = (UInt16*)MultiExpTable_handle.AddrOfPinnedObject();
        }

        private void DeallocateHandles()
        {
            spr_coll_buf_handle.Free();
            fore_mask_buf_handle.Free();
            text_chunky_buf_handle.Free();
            spr_data_handle.Free();
            spr_draw_data_handle.Free();
            ExpTable_handle.Free();
            MultiExpTable_handle.Free();
        }

        #endregion Private members

        #region Private fields

        UInt16[] mx = new UInt16[8];				// VIC registers
        byte[] my = new byte[8];
        byte mx8;
        byte ctrl1, ctrl2;
        byte lpx, lpy;
        byte me, mxe, mye, mdp, mmc;
        byte vbase;
        byte irq_flag, irq_mask;
        byte clx_spr, clx_bgr;
        byte ec, b0c, b1c, b2c, b3c, mm0, mm1;
        byte[] sc = new byte[8];

        byte[] ram, char_rom, color_ram;            // Pointers to RAM and ROM
        C64 the_c64;				                // Pointer to C64
        C64Display the_display;	                    // Pointer to C64Display
        MOS6510 the_cpu;			                // Pointer to 6510

        byte[] colors = new byte[256];			    // Indices of the 16 C64 colors (16 times mirrored to avoid "& 0x0f")

        byte ec_color, b0c_color, b1c_color,
              b2c_color, b3c_color;	                // Indices for exterior/background colors
        byte mm0_color, mm1_color;	                // Indices for MOB multicolors
        byte[] spr_color = new byte[8];			    // Indices for MOB colors

        UInt32 ec_color_long;		                // ec_color expanded to 32 bits

        byte[] matrix_line = new byte[40];		    // Buffer for video line, read in Bad Lines
        byte[] color_line = new byte[40];		    // Buffer for color line, read in Bad Lines

        byte _LastVICByte;

        unsafe byte* chunky_line_start;	            // Pointer to start of current line in bitmap buffer       

        int xmod;					                // Number of bytes per row

        UInt16 raster_y;				            // Current raster line
        UInt16 irq_raster;			                // Interrupt raster line
        UInt16 dy_start;				            // Comparison values for border logic
        UInt16 dy_stop;
        UInt16 rc;					                // Row counter
        UInt16 vc;					                // Video counter
        UInt16 vc_base;				                // Video counter base
        UInt16 x_scroll;				            // X scroll value
        UInt16 y_scroll;				            // Y scroll value
        UInt16 cia_vabase;			                // CIA VA14/15 video base

        // TODO: Change to pointer logic for perf
        UInt16[] mc = new UInt16[8];				// Sprite data counters

        int display_idx;			                // Index of current display mode
        int skip_counter;			                // Counter for frame-skipping

        byte[] spr_coll_buf = new byte[0x180];	    // Buffer for sprite-sprite collisions and priorities
        GCHandle spr_coll_buf_handle;               // fixed pointer
        unsafe byte* spr_coll_buf_ptr;

        byte[] fore_mask_buf = new byte[0x180 / 8];	// Foreground mask for sprite-graphics collisions and priorities
        GCHandle fore_mask_buf_handle;
        unsafe byte* fore_mask_buf_ptr;

        byte[] text_chunky_buf = new byte[40 * 8];	// Line graphics buffer
        GCHandle text_chunky_buf_handle;
        unsafe byte* text_chunky_buf_ptr;

        bool display_state;			// true: Display state, false: Idle state
        bool border_on;				// Flag: Upper/lower border on (Frodo SC: Main border flipflop)
        bool frame_skipped;			// Flag: Frame is being skipped
        bool bad_lines_enabled;	    // Flag: Bad Lines enabled for this frame
        bool lp_triggered;			// Flag: Lightpen was triggered in this frame

        // FRODO SC section

        int cycle;					// Current cycle in line (1..63)

        unsafe byte* chunky_ptr;			// Pointer in chunky bitmap buffer (this is where out output goes)

        unsafe byte* fore_mask_ptr;		// Pointer in fore_mask_buf

        UInt16 matrix_base;			// Video matrix base
        UInt16 char_base;			// Character generator base
        UInt16 bitmap_base;			// Bitmap base

        bool is_bad_line;			// Flag: Current line is bad line
        bool draw_this_line;		// Flag: This line is drawn on the screen
        bool ud_border_on;			// Flag: Upper/lower border on
        bool vblanking;				// Flag: VBlank in next cycle

        // TODO: Change to pointer logic
        bool[] border_on_sample = new bool[5];	// Samples of border state at different cycles (1, 17, 18, 56, 57)
        byte[] border_color_sample = new byte[0x180 / 8];	// Samples of border color at each "displayed" cycle

        byte ref_cnt;				                // Refresh counter
        byte spr_exp_y;			                    // 8 sprite y expansion flipflops
        byte spr_dma_on;			                // 8 flags: Sprite DMA active
        byte spr_disp_on;			                // 8 flags: Sprite display active
        byte spr_draw;				                // 8 flags: Draw sprite in this line
        UInt16[] spr_ptr = new UInt16[8];			// Sprite data pointers
        UInt16[] mc_base = new UInt16[8];			// Sprite data counter bases

        UInt16 raster_x;			                // Current raster x position

        int ml_index;				                // Index in matrix/color_line[]
        byte gfx_data, char_data, color_data, last_char_data;

        // TODO: should these be JAGGED ARRAYS?
        byte[,] spr_data = new byte[8, 4];		    // Sprite data read
        GCHandle spr_data_handle;
        unsafe byte* spr_data_ptr;

        byte[,] spr_draw_data = new byte[8, 4];	    // Sprite data for drawing
        GCHandle spr_draw_data_handle;
        unsafe byte* spr_draw_data_ptr;


        UInt32 first_ba_cycle;		                // Cycle when BA first went low

        // END FRODO SC section

        #endregion

        #region IDisposable / Destructor Members

        ~MOS6569()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool disposed = false;
        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // dispose managed resources
                }

                DeallocateHandles();

                disposed = true;
            }
        }

        #endregion
    }
}
