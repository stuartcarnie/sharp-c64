using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class MOS6581State
    {
        public byte freq_lo_1;
        public byte freq_hi_1;
        public byte pw_lo_1;
        public byte pw_hi_1;
        public byte ctrl_1;
        public byte AD_1;
        public byte SR_1;

        public byte freq_lo_2;
        public byte freq_hi_2;
        public byte pw_lo_2;
        public byte pw_hi_2;
        public byte ctrl_2;
        public byte AD_2;
        public byte SR_2;

        public byte freq_lo_3;
        public byte freq_hi_3;
        public byte pw_lo_3;
        public byte pw_hi_3;
        public byte ctrl_3;
        public byte AD_3;
        public byte SR_3;

        public byte fc_lo;
        public byte fc_hi;
        public byte res_filt;
        public byte mode_vol;

        public byte pot_x;
        public byte pot_y;
        public byte osc_3;
        public byte env_3;
    }
}
