using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace SharpC64
{
    /// <summary>
    /// Emulates the 6581 SID chip
    /// </summary>
    public class MOS6581
    {
        #region Public methods

        public MOS6581(C64 c64)
        {
            the_c64 = c64;
            the_renderer = null;
            for (int i = 0; i < regs.Length; i++)
                regs[i] = 0;

            // Open the renderer
            open_close_renderer(SIDType.SIDTYPE_NONE, GlobalPrefs.ThePrefs.SIDType);
        }

        public void Reset()
        {
            for (int i = 0; i < regs.Length; i++)
                regs[i] = 0;
            last_sid_byte = 0;

            // Reset the renderer
            if (the_renderer != null)
                the_renderer.Reset();
        }

        public byte ReadRegister(UInt16 adr)
        {
            // A/D converters
            if (adr == 0x19 || adr == 0x1a)
            {
                last_sid_byte = 0;
                return 0xff;
            }

            // Voice 3 oscillator/EG readout
            if (adr == 0x1b || adr == 0x1c)
            {
                last_sid_byte = 0;
                return (byte)rand.Next();
            }

            // Write-only register: Return last value written to SID
            return last_sid_byte;
        }

        public void WriteRegister(UInt16 adr, byte abyte)
        {
            // Keep a local copy of the register values
            last_sid_byte = regs[adr] = abyte;

            if (the_renderer != null)
                the_renderer.WriteRegister(adr, abyte);
        }

        public void NewPrefs(Prefs prefs)
        {
            open_close_renderer(GlobalPrefs.ThePrefs.SIDType, prefs.SIDType);
            if (the_renderer != null)
                the_renderer.NewPrefs(prefs);
        }

        public void PauseSound()
        {
            if (the_renderer != null)
                the_renderer.Pause();
        }

        public void ResumeSound()
        {
            if (the_renderer != null)
                the_renderer.Resume();
        }

        public MOS6581State State
        {
            get
            {
                MOS6581State ss = new MOS6581State();
                ss.freq_lo_1 = regs[0];
                ss.freq_hi_1 = regs[1];
                ss.pw_lo_1 = regs[2];
                ss.pw_hi_1 = regs[3];
                ss.ctrl_1 = regs[4];
                ss.AD_1 = regs[5];
                ss.SR_1 = regs[6];

                ss.freq_lo_2 = regs[7];
                ss.freq_hi_2 = regs[8];
                ss.pw_lo_2 = regs[9];
                ss.pw_hi_2 = regs[10];
                ss.ctrl_2 = regs[11];
                ss.AD_2 = regs[12];
                ss.SR_2 = regs[13];

                ss.freq_lo_3 = regs[14];
                ss.freq_hi_3 = regs[15];
                ss.pw_lo_3 = regs[16];
                ss.pw_hi_3 = regs[17];
                ss.ctrl_3 = regs[18];
                ss.AD_3 = regs[19];
                ss.SR_3 = regs[20];

                ss.fc_lo = regs[21];
                ss.fc_hi = regs[22];
                ss.res_filt = regs[23];
                ss.mode_vol = regs[24];

                ss.pot_x = 0xff;
                ss.pot_y = 0xff;
                ss.osc_3 = 0;
                ss.env_3 = 0;

                return ss;
            }
            set
            {
                regs[0] = value.freq_lo_1;
                regs[1] = value.freq_hi_1;
                regs[2] = value.pw_lo_1;
                regs[3] = value.pw_hi_1;
                regs[4] = value.ctrl_1;
                regs[5] = value.AD_1;
                regs[6] = value.SR_1;

                regs[7] = value.freq_lo_2;
                regs[8] = value.freq_hi_2;
                regs[9] = value.pw_lo_2;
                regs[10] = value.pw_hi_2;
                regs[11] = value.ctrl_2;
                regs[12] = value.AD_2;
                regs[13] = value.SR_2;

                regs[14] = value.freq_lo_3;
                regs[15] = value.freq_hi_3;
                regs[16] = value.pw_lo_3;
                regs[17] = value.pw_hi_3;
                regs[18] = value.ctrl_3;
                regs[19] = value.AD_3;
                regs[20] = value.SR_3;

                regs[21] = value.fc_lo;
                regs[22] = value.fc_hi;
                regs[23] = value.res_filt;
                regs[24] = value.mode_vol;

                // Stuff the new register values into the renderer
                if (the_renderer != null)
                    for (UInt16 i = 0; i < 25; i++)
                        the_renderer.WriteRegister(i, regs[i]);
            }
        }

        public void EmulateLine()
        {
            if (the_renderer != null)
                the_renderer.EmulateLine();
        }

        public void VBlank()
        {
            if (the_renderer != null)
                the_renderer.VBlank();
        }

        #endregion public methods

        #region private methods

        private void open_close_renderer(SIDType old_type, SIDType new_type)
        {
            if (old_type == new_type)
                return;

            the_renderer = null;

            // Create new renderer
            if (new_type == SIDType.SIDTYPE_DIGITAL)
                the_renderer = new DigitalRenderer();
            else
                the_renderer = null;

            // Stuff the current register values into the new renderer
            if (the_renderer != null)
                for (UInt16 i = 0; i < 25; i++)
                    the_renderer.WriteRegister(i, regs[i]);
        }

        #endregion private methods

        #region private fields

        C64 the_c64;				    // Pointer to C64 object
        SIDRenderer the_renderer;       // Reference to current renderer
        byte[] regs = new byte[32];		// Copies of the 25 write-only SID registers
        byte last_sid_byte;		        // Last value written to SID
        Random rand = new Random();

        #endregion private fields

        #region static methods

        static UInt32 seed = 1;
        public static byte sid_random()
        {
            seed = seed * 1103515245 + 12345;
            return (byte)(seed >> 16);
        }

        #endregion

    }
}
