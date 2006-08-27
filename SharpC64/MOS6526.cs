using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public abstract partial class MOS6526
    {
        #region Public methods

        public MOS6526(MOS6510 CPU)
        {
            the_cpu = CPU;
        }

        public void Reset()
        {
            pra = prb = ddra = ddrb = 0;

            ta = tb = 0xffff;
            latcha = latchb = 1;

            tod_10ths = tod_sec = tod_min = tod_hr = 0;
            alm_10ths = alm_sec = alm_min = alm_hr = 0;

            sdr = icr = cra = crb = int_mask = 0;

            tod_halt = false;
            tod_divider = 0;

            ta_cnt_phi2 = tb_cnt_phi2 = tb_cnt_ta = false;

            ta_irq_next_cycle = tb_irq_next_cycle = false;
            ta_state = tb_state = TimerState.T_STOP;
        }

#if TIMERS
        public int ta_interrupts = 0;
        public int tb_interrupts = 0;
#endif
        // FRODO SC
        public void CheckIRQs()
        {
            // Trigger pending interrupts
            if (ta_irq_next_cycle)
            {
#if TIMERS
                ta_interrupts++;
#endif
                ta_irq_next_cycle = false;
                TriggerInterrupt(1);
            }

            if (tb_irq_next_cycle)
            {
#if TIMERS
                tb_interrupts++;
#endif
                tb_irq_next_cycle = false;
                TriggerInterrupt(2);
            }
        }

        public void CountTOD()
        {
            byte lo, hi;

            // Decrement frequency divider
            if (tod_divider != 0)
                tod_divider--;
            else
            {

                // Reload divider according to 50/60 Hz flag
                if ((cra & 0x80) > 0)
                    tod_divider = 4;
                else
                    tod_divider = 5;

                // 1/10 seconds
                tod_10ths++;
                if (tod_10ths > 9)
                {
                    tod_10ths = 0;

                    // Seconds
                    lo = (byte)((tod_sec & 0x0f) + 1);
                    hi = (byte)(tod_sec >> 4);
                    if (lo > 9)
                    {
                        lo = 0;
                        hi++;
                    }

                    if (hi > 5)
                    {
                        tod_sec = 0;

                        // Minutes
                        lo = (byte)((tod_min & 0x0f) + 1);
                        hi = (byte)(tod_min >> 4);

                        if (lo > 9)
                        {
                            lo = 0;
                            hi++;
                        }

                        if (hi > 5)
                        {
                            tod_min = 0;

                            // Hours
                            lo = (byte)((tod_hr & 0x0f) + 1);
                            hi = (byte)((tod_hr >> 4) & 1);
                            tod_hr &= 0x80;		// Keep AM/PM flag

                            if (lo > 9)
                            {
                                lo = 0;
                                hi++;
                            }

                            tod_hr |= (byte)((hi << 4) | lo);

                            if ((tod_hr & 0x1f) > 0x11)
                                tod_hr = (byte)(tod_hr & 0x80 ^ 0x80);
                        }
                        else
                            tod_min = (byte)((hi << 4) | lo);
                    }
                    else
                        tod_sec = (byte)((hi << 4) | lo);
                }

                // Alarm time reached? Trigger interrupt if enabled
                if (tod_10ths == alm_10ths && tod_sec == alm_sec &&
                    tod_min == alm_min && tod_hr == alm_hr)
                    TriggerInterrupt(4);
            }
        }

        public abstract void TriggerInterrupt(int bit);

        #endregion

        #region Public properties

        public MOS6526State State
        {
            get
            {
                MOS6526State cs = new MOS6526State();

                cs.pra = pra;
                cs.prb = prb;
                cs.ddra = ddra;
                cs.ddrb = ddrb;

                cs.ta_lo = (byte)(ta & 0xff);
                cs.ta_hi = (byte)(ta >> 8);
                cs.tb_lo = (byte)(tb & 0xff);
                cs.tb_hi = (byte)(tb >> 8);
                cs.latcha = latcha;
                cs.latchb = latchb;
                cs.cra = cra;
                cs.crb = crb;

                cs.tod_10ths = tod_10ths;
                cs.tod_sec = tod_sec;
                cs.tod_min = tod_min;
                cs.tod_hr = tod_hr;
                cs.alm_10ths = alm_10ths;
                cs.alm_sec = alm_sec;
                cs.alm_min = alm_min;
                cs.alm_hr = alm_hr;

                cs.sdr = sdr;

                cs.int_data = icr;
                cs.int_mask = int_mask;

                return cs;
            }

            set
            {
                pra = value.pra;
                prb = value.prb;
                ddra = value.ddra;
                ddrb = value.ddrb;

                ta = (UInt16)((value.ta_hi << 8) | value.ta_lo);
                tb = (UInt16)((value.tb_hi << 8) | value.tb_lo);
                latcha = value.latcha;
                latchb = value.latchb;
                cra = value.cra;
                crb = value.crb;

                tod_10ths = value.tod_10ths;
                tod_sec = value.tod_sec;
                tod_min = value.tod_min;
                tod_hr = value.tod_hr;
                alm_10ths = value.alm_10ths;
                alm_sec = value.alm_sec;
                alm_min = value.alm_min;
                alm_hr = value.alm_hr;

                sdr = value.sdr;

                icr = value.int_data;
                int_mask = value.int_mask;

                tod_halt = false;
                ta_cnt_phi2 = ((cra & 0x20) == 0x00);
                tb_cnt_phi2 = ((crb & 0x60) == 0x00);
                tb_cnt_ta = ((crb & 0x60) == 0x40);

                ta_state = (cra & 1) > 0 ? TimerState.T_COUNT : TimerState.T_STOP;
                tb_state = (crb & 1) > 0 ? TimerState.T_COUNT : TimerState.T_STOP;
            }
        }

        #endregion

        #region Protected fields

        protected MOS6510 the_cpu;	// Pointer to 6510

        protected byte pra, prb, ddra, ddrb;

        protected UInt16 ta, tb, latcha, latchb;

        protected byte tod_10ths, tod_sec, tod_min, tod_hr;
        protected byte alm_10ths, alm_sec, alm_min, alm_hr;

        protected byte sdr, icr, cra, crb;
        protected byte int_mask;

        protected int tod_divider;	                // TOD frequency divider

        protected bool tod_halt,		            // Flag: TOD halted
             ta_cnt_phi2,	                        // Flag: Timer A is counting Phi 2
             tb_cnt_phi2,	                        // Flag: Timer B is counting Phi 2
             tb_cnt_ta;		                        // Flag: Timer B is counting underflows of Timer A

        // FRODO_SC (single cycle)
        protected bool ta_irq_next_cycle,		    // Flag: Trigger TA IRQ in next cycle
             tb_irq_next_cycle,		                // Flag: Trigger TB IRQ in next cycle
             has_new_cra,			                // Flag: New value for CRA pending
             has_new_crb;			                // Flag: New value for CRB pending
        protected TimerState ta_state, tb_state;	        // Timer A/B states
        protected byte new_cra, new_crb;		    // New values for CRA/CRB

        #endregion
    }

    // Timer states
    public enum TimerState : byte
    {
        T_STOP,
        T_WAIT_THEN_COUNT,
        T_LOAD_THEN_STOP,
        T_LOAD_THEN_COUNT,
        T_LOAD_THEN_WAIT_THEN_COUNT,
        T_COUNT,
        T_COUNT_THEN_STOP,
    };
}
