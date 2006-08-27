using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public partial class MOS6569
    {
        unsafe internal bool EmulateCycle()
        {
            byte mask;
            int i;

            switch (cycle)
            {

                // Fetch sprite pointer 3, increment raster counter, trigger raster IRQ,
                // test for Bad Line, reset BA if sprites 3 and 4 off, read data of sprite 3
                case 1:
                    if (raster_y == TOTAL_RASTERS - 1)

                        // Trigger VBlank in cycle 2
                        vblanking = true;

                    else
                    {

                        // Increment raster counter
                        raster_y++;

                        // Trigger raster IRQ if IRQ line reached
                        if (raster_y == irq_raster)
                            raster_irq();

                        // In line $30, the DEN bit controls if Bad Lines can occur
                        if (raster_y == 0x30)
                            bad_lines_enabled = (ctrl1 & 0x10) != 0;

                        // Bad Line condition?
                        is_bad_line = (raster_y >= FIRST_DMA_LINE && raster_y <= LAST_DMA_LINE && ((raster_y & 7) == y_scroll) && bad_lines_enabled);

                        // Don't draw all lines, hide some at the top and bottom
                        draw_this_line = (raster_y >= FIRST_DISP_LINE && raster_y <= LAST_DISP_LINE && !frame_skipped);
                    }

                    // First sample of border state
                    border_on_sample[0] = border_on;

                    SprPtrAccess(3);
                    SprDataAccess(3, 0);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x18) == 0)
                        the_cpu.BALow = false;
                    break;

                // Set BA for sprite 5, read data of sprite 3
                case 2:
                    if (vblanking)
                    {

                        // Vertical blank, reset counters
                        raster_y = vc_base = 0;
                        ref_cnt = 0xff;
                        lp_triggered = vblanking = false;

                        skip_counter--;
                        frame_skipped = skip_counter == 0;
                        if (!frame_skipped)
                            skip_counter = GlobalPrefs.ThePrefs.SkipFrames;

                        the_c64.VBlank(!frame_skipped);

                        // Get bitmap pointer for next frame. This must be done
                        // after calling the_c64.VBlank() because the preferences
                        // and screen configuration may have been changed there
                        chunky_line_start = the_display.BitmapBase;
                        xmod = the_display.BitmapXMod;

                        // Trigger raster IRQ if IRQ in line 0
                        if (irq_raster == 0)
                            raster_irq();

                    }

                    // Our output goes here
                    chunky_ptr = chunky_line_start;

                    // Clear foreground mask
                    //memset(fore_mask_buf_ptr, 0, C64Display.DISPLAY_X / 8);
                    Array.Clear(fore_mask_buf, 0, fore_mask_buf.Length);
                     fore_mask_ptr = fore_mask_buf_ptr;

                    SprDataAccess(3, 1);
                    SprDataAccess(3, 2);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x20) !=0)
                        SetBALow();
                    break;

                // Fetch sprite pointer 4, reset BA is sprite 4 and 5 off
                case 3:
                    SprPtrAccess(4);
                    SprDataAccess(4, 0);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x30) == 0)
                        the_cpu.BALow = false;
                    break;

                // Set BA for sprite 6, read data of sprite 4 
                case 4:
                    SprDataAccess(4, 1);
                    SprDataAccess(4, 2);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x40) !=0)
                        SetBALow();
                    break;

                // Fetch sprite pointer 5, reset BA if sprite 5 and 6 off
                case 5:
                    SprPtrAccess(5);
                    SprDataAccess(5, 0);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x60) == 0)
                        the_cpu.BALow = false;
                    break;

                // Set BA for sprite 7, read data of sprite 5
                case 6:
                    SprDataAccess(5, 1);
                    SprDataAccess(5, 2);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x80) != 0)
                        SetBALow();
                    break;

                // Fetch sprite pointer 6, reset BA if sprite 6 and 7 off
                case 7:
                    SprPtrAccess(6);
                    SprDataAccess(6, 0);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0xc0) ==0)
                        the_cpu.BALow = false;
                    break;

                // Read data of sprite 6
                case 8:
                    SprDataAccess(6, 1);
                    SprDataAccess(6, 2);
                    DisplayIfBadLine();
                    break;

                // Fetch sprite pointer 7, reset BA if sprite 7 off
                case 9:
                    SprPtrAccess(7);
                    SprDataAccess(7, 0);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x80) == 0)
                        the_cpu.BALow = false;
                    break;

                // Read data of sprite 7
                case 10:
                    SprDataAccess(7, 1);
                    SprDataAccess(7, 2);
                    DisplayIfBadLine();
                    break;

                // Refresh, reset BA
                case 11:
                    RefreshAccess();
                    DisplayIfBadLine();
                    the_cpu.BALow = false;
                    break;

                // Refresh, turn on matrix access if Bad Line
                case 12:
                    RefreshAccess();
                    FetchIfBadLine();
                    break;

                // Refresh, turn on matrix access if Bad Line, reset raster_x, graphics display starts here
                case 13:
                    if (draw_this_line)
                    {
                        draw_background();
                        SampleBorder();
                    }
                    RefreshAccess();
                    FetchIfBadLine();
                    raster_x = 0xfffc;
                    break;

                // Refresh, VCBASE.VCCOUNT, turn on matrix access and reset RC if Bad Line
                case 14:
                    if (draw_this_line)
                    {
                        draw_background();
                        SampleBorder();
                    }
                    RefreshAccess();
                    RCIfBadLine();
                    vc = vc_base;
                    break;

                // Refresh and matrix access, increment mc_base by 2 if y expansion flipflop is set
                case 15:
                    if (draw_this_line)
                    {
                        draw_background();
                        SampleBorder();
                    }
                    RefreshAccess();
                    FetchIfBadLine();

                    for (i = 0; i < 8; i++)
                        if ((spr_exp_y & (1 << i)) != 0)
                            mc_base[i] += 2;

                    ml_index = 0;
                    matrix_access();
                    break;

                // Graphics and matrix access, increment mc_base by 1 if y expansion flipflop is set
                // and check if sprite DMA can be turned off
                case 16:
                    if (draw_this_line)
                    {
                        draw_background();
                        SampleBorder();
                    }
                    graphics_access();
                    FetchIfBadLine();

                    mask = 1;
                    for (i = 0; i < 8; i++, mask <<= 1)
                    {
                        if ((spr_exp_y & mask) != 0)
                            mc_base[i]++;
                        if ((mc_base[i] & 0x3f) == 0x3f)
                            spr_dma_on &= (byte)~mask;
                    }

                    matrix_access();
                    break;

                // Graphics and matrix access, turn off border in 40 column mode, display window starts here
                case 17:
                    if ((ctrl2 & 8) != 0)
                    {
                        if (raster_y == dy_stop)
                            ud_border_on = true;
                        else
                        {
                            if ((ctrl1 & 0x10) != 0)
                            {
                                if (raster_y == dy_start)
                                    border_on = ud_border_on = false;
                                else
                                    if (!ud_border_on)
                                        border_on = false;
                            }
                            else
                                if (!ud_border_on)
                                    border_on = false;
                        }
                    }

                    // Second sample of border state
                    border_on_sample[1] = border_on;

                    if (draw_this_line)
                        draw_background();
                    draw_graphics();
                    if (draw_this_line) SampleBorder();
                    graphics_access();
                    FetchIfBadLine();
                    matrix_access();
                    break;

                // Turn off border in 38 column mode
                case 18:
                    if ((ctrl2 & 8) == 0)
                    {
                        if (raster_y == dy_stop)
                            ud_border_on = true;
                        else
                        {
                            if ((ctrl1 & 0x10) != 0)
                            {
                                if (raster_y == dy_start)
                                    border_on = ud_border_on = false;
                                else
                                    if (!ud_border_on)
                                        border_on = false;
                            }
                            else
                                if (!ud_border_on)
                                    border_on = false;
                        }
                    }

                    // Third sample of border state
                    border_on_sample[2] = border_on;

                    // Falls through
                    goto case 19;

                // Graphics and matrix access
                case 19:
                case 20:
                case 21:
                case 22:
                case 23:
                case 24:
                case 25:
                case 26:
                case 27:
                case 28:
                case 29:
                case 30:
                case 31:
                case 32:
                case 33:
                case 34:
                case 35:
                case 36:
                case 37:
                case 38:
                case 39:
                case 40:
                case 41:
                case 42:
                case 43:
                case 44:
                case 45:
                case 46:
                case 47:
                case 48:
                case 49:
                case 50:
                case 51:
                case 52:
                case 53:
                case 54:	// Gnagna...
                    draw_graphics();
                    if (draw_this_line) SampleBorder();
                    graphics_access();
                    FetchIfBadLine();
                    matrix_access();
                    last_char_data = char_data;
                    break;

                // Last graphics access, turn off matrix access, turn on sprite DMA if Y coordinate is
                // right and sprite is enabled, handle sprite y expansion, set BA for sprite 0
                case 55:
                    draw_graphics();
                    if (draw_this_line) SampleBorder();
                    graphics_access();
                    DisplayIfBadLine();

                    // Invert y expansion flipflop if bit in MYE is set
                    mask = 1;
                    for (i = 0; i < 8; i++, mask <<= 1)
                        if ((mye & mask) != 0)
                            spr_exp_y ^= mask;
                    CheckSpriteDMA();

                    if ((spr_dma_on & 0x01) != 0)
                    {	// Don't remove these braces!
                        SetBALow();
                    }
                    else
                        the_cpu.BALow = false;
                    break;

                // Turn on border in 38 column mode, turn on sprite DMA if Y coordinate is right and
                // sprite is enabled, set BA for sprite 0, display window ends here
                case 56:
                    if ((ctrl2 & 8) == 0)
                        border_on = true;

                    // Fourth sample of border state
                    border_on_sample[3] = border_on;

                    draw_graphics();
                    if (draw_this_line) SampleBorder();
                    IdleAccess();
                    DisplayIfBadLine();
                    CheckSpriteDMA();

                    if ((spr_dma_on & 0x01) != 0)
                        SetBALow();
                    break;

                // Turn on border in 40 column mode, set BA for sprite 1, paint sprites
                case 57:
                    if ((ctrl2 & 8) != 0)
                        border_on = true;

                    // Fifth sample of border state
                    border_on_sample[4] = border_on;

                    // Sample spr_disp_on and spr_data for sprite drawing
                    if ((spr_draw = spr_disp_on) != 0)
                        memcpy(spr_draw_data_ptr, spr_data_ptr, 8 * 4);

                    // Turn off sprite display if DMA is off
                    mask = 1;
                    for (i = 0; i < 8; i++, mask <<= 1)
                        if ((spr_disp_on & mask) != 0 && (spr_dma_on & mask) == 0)
                            spr_disp_on &= (byte)~mask;

                    if (draw_this_line)
                    {
                        draw_background();
                        SampleBorder();
                    }
                    IdleAccess();
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x02) != 0)
                        SetBALow();
                    break;

                // Fetch sprite pointer 0, mc_base.mc, turn on sprite display if necessary,
                // turn off display if RC=7, read data of sprite 0
                case 58:
                    if (draw_this_line)
                    {
                        draw_background();
                        SampleBorder();
                    }

                    mask = 1;
                    for (i = 0; i < 8; i++, mask <<= 1)
                    {
                        mc[i] = mc_base[i];
                        if ((spr_dma_on & mask) != 0 && (raster_y & 0xff) == my[i])
                            spr_disp_on |= mask;
                    }
                    SprPtrAccess(0);
                    SprDataAccess(0, 0);

                    if (rc == 7)
                    {
                        vc_base = vc;
                        display_state = false;
                    }
                    if (is_bad_line || display_state)
                    {
                        display_state = true;
                        rc = (UInt16)((rc + 1) & 7);
                    }
                    break;

                // Set BA for sprite 2, read data of sprite 0
                case 59:
                    if (draw_this_line)
                    {
                        draw_background();
                        SampleBorder();
                    }
                    SprDataAccess(0, 1);
                    SprDataAccess(0, 2);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x04) != 0)
                        SetBALow();
                    break;

                // Fetch sprite pointer 1, reset BA if sprite 1 and 2 off, graphics display ends here
                case 60:
                    if (draw_this_line)
                    {
                        draw_background();

                        SampleBorder();
                        // Draw sprites
                        if (spr_draw != 0 && GlobalPrefs.ThePrefs.SpritesOn)
                            draw_sprites();

                        // Draw border
                        if (border_on_sample[0])
                            for (i = 0; i < 4; i++)
                            {
                                byte* p = chunky_line_start + i * 8;
                                p[0] = p[1] = p[2] = p[3] = p[4] = p[5] = p[6] = p[7] = border_color_sample[i];
                            }
                        if (border_on_sample[1])
                        {
                            byte* p = chunky_line_start + 4 * 8;
                            p[0] = p[1] = p[2] = p[3] = p[4] = p[5] = p[6] = p[7] = border_color_sample[4];
                        }
                        if (border_on_sample[2])
                            for (i = 5; i < 43; i++)
                            {
                                byte* p = chunky_line_start + i * 8;
                                p[0] = p[1] = p[2] = p[3] = p[4] = p[5] = p[6] = p[7] = border_color_sample[i];
                            }
                        if (border_on_sample[3])
                        {
                            byte* p = chunky_line_start + 43 * 8;
                            p[0] = p[1] = p[2] = p[3] = p[4] = p[5] = p[6] = p[7] = border_color_sample[43];
                        }
                        if (border_on_sample[4])
                            for (i = 44; i < C64Display.DISPLAY_X / 8; i++)
                            {
                                byte* p = chunky_line_start + i * 8;
                                p[0] = p[1] = p[2] = p[3] = p[4] = p[5] = p[6] = p[7] = border_color_sample[i];
                            }


                        // Increment pointer in chunky buffer
                        chunky_line_start += xmod;
                    }

                    SprPtrAccess(1);
                    SprDataAccess(1, 0);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x06) == 0)
                        the_cpu.BALow = false;
                    break;

                // Set BA for sprite 3, read data of sprite 1
                case 61:
                    SprDataAccess(1, 1);
                    SprDataAccess(1, 2);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x08) != 0)
                        SetBALow();
                    break;

                // Read sprite pointer 2, reset BA if sprite 2 and 3 off, read data of sprite 2
                case 62:
                    SprPtrAccess(2);
                    SprDataAccess(2, 0);
                    DisplayIfBadLine();
                    if ((spr_dma_on & 0x0c) == 0)
                        the_cpu.BALow = false;
                    break;

                // Set BA for sprite 4, read data of sprite 2
                case 63:
                    SprDataAccess(2, 1);
                    SprDataAccess(2, 2);
                    DisplayIfBadLine();

                    if (raster_y == dy_stop)
                        ud_border_on = true;
                    else
                        if ((ctrl1 & 0x10) != 0 && raster_y == dy_start)
                            ud_border_on = false;

                    if ((spr_dma_on & 0x10) != 0)
                        SetBALow();

                    // Last cycle
                    raster_x += 8;
                    cycle = 1;
                    return true;
            }

            // Next cycle
            raster_x += 8;
            cycle++;
            return false;
        }

        unsafe private void memcpy(byte* dest, byte* src, int count)
        {
            while (count-- != 0)
                *dest++ = *src++;
        }

        unsafe private void memset(byte* dest, byte val, int count)
        {
            while (count-- != 0)
                *dest++ = val;
        }

        unsafe private void SampleBorder()
        {
            if (border_on)
                border_color_sample[cycle - 13 ] = ec_color;
            chunky_ptr += 8;
            fore_mask_ptr++;
        }

        unsafe private void SprDataAccess(int num, int bytenum)
        {
            if ((spr_dma_on & (1 << num)) != 0)
            {
                spr_data_ptr[num * 4 + bytenum] = read_byte((UInt16)(mc[num] & 0x3f | spr_ptr[num]));
                mc[num]++;
            }
            else if (bytenum == 1)
                IdleAccess();
        }

        private void SprPtrAccess(int num)
        {
            spr_ptr[num] = (UInt16)(read_byte((UInt16)(matrix_base | 0x03f8 | num)) << 6);
        }

        private void CheckSpriteDMA()
        {
            byte mask = 1;
            for (int i = 0; i < 8; i++, mask <<= 1)
                if ((me & mask) != 0 && (raster_y & 0xff) == my[i])
                {
                    spr_dma_on |= mask;
                    mc_base[i] = 0;
                    if ((mye & mask) != 0)
                        spr_exp_y &= (byte)~mask;
                }
        }

        private void RefreshAccess()
        {
            read_byte((UInt16)(0x3f00 | ref_cnt--));
        }

        private void IdleAccess()
        {
            read_byte(0x3fff);
        }

        private void RCIfBadLine()
        {
            if (is_bad_line)
            {
                display_state = true;
                rc = 0;
                SetBALow();
            }
        }

        private void FetchIfBadLine()
        {
            if (is_bad_line)
            {
                display_state = true;
                SetBALow();
            }
        }

        private void DisplayIfBadLine()
        {
            if (is_bad_line)
                display_state = true;
        }

        private void SetBALow()
        {
            if (!the_cpu.BALow)
            {
                first_ba_cycle = the_c64.CycleCounter;
                the_cpu.BALow = true;
            }
        }


    }
}
