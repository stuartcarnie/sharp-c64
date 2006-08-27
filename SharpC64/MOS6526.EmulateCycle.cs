using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public partial class MOS6526
    {
        public void EmulateCycle()
        {
            bool ta_underflow = false;

            bool ta_interrupt = false;
            
            // Timer A state machine
            switch (ta_state)
            {
                case TimerState.T_WAIT_THEN_COUNT:
                    ta_state = TimerState.T_COUNT;		// fall through
                    goto case TimerState.T_STOP;

                case TimerState.T_STOP:
                    goto ta_idle;

                case TimerState.T_LOAD_THEN_STOP:
                    ta_state = TimerState.T_STOP;
                    ta = latcha;			// Reload timer
                    goto ta_idle;

                case TimerState.T_LOAD_THEN_COUNT:
                    ta_state = TimerState.T_COUNT;
                    ta = latcha;			// Reload timer
                    goto ta_idle;

                case TimerState.T_LOAD_THEN_WAIT_THEN_COUNT:
                    ta_state = TimerState.T_WAIT_THEN_COUNT;
                    if (ta == 1)
                    {
                        ta_interrupt = true;	// Interrupt if timer == 1
                        break;
                    }
                    else
                    {
                        ta = latcha;		// Reload timer
                        goto ta_idle;
                    }
                case TimerState.T_COUNT:
                    goto ta_count;
                case TimerState.T_COUNT_THEN_STOP:
                    ta_state = TimerState.T_STOP;
                    goto ta_count;
            }

            // Count timer A
        ta_count:
            if (ta_cnt_phi2 || ta_interrupt)
            {
                if (ta == 0 || --ta == 0 || ta_interrupt)
                {
                    // Decrement timer, underflow?
                    if (ta_state != TimerState.T_STOP || ta_interrupt)
                    {
                        ta = latcha;			    // Reload timer
                        ta_irq_next_cycle = true;   // Trigger interrupt in next cycle
                        icr |= 1;				    // But set ICR bit now

                        if ((cra & 8) > 0)
                        {
                            // One-shot?
                            cra &= 0xfe;		    // Yes, stop timer
                            new_cra &= 0xfe;
                            ta_state = TimerState.T_LOAD_THEN_STOP;	// Reload in next cycle
                        }
                        else
                            ta_state = TimerState.T_LOAD_THEN_COUNT;	// No, delay one cycle (and reload)
                    }
                    ta_underflow = true;
                }
            }
        // Delayed write to CRA?
        ta_idle:
            if (has_new_cra)
            {
                switch (ta_state)
                {
                    case TimerState.T_STOP:
                    case TimerState.T_LOAD_THEN_STOP:
                        if ((new_cra & 1) > 0)
                        {		// Timer started, wasn't running
                            if ((new_cra & 0x10) > 0)	// Force load
                                ta_state = TimerState.T_LOAD_THEN_WAIT_THEN_COUNT;
                            else				// No force load
                                ta_state = TimerState.T_WAIT_THEN_COUNT;
                        }
                        else
                        {				// Timer stopped, was already stopped
                            if ((new_cra & 0x10) > 0)	// Force load
                                ta_state = TimerState.T_LOAD_THEN_STOP;
                        }
                        break;
                    case TimerState.T_COUNT:
                        if ((new_cra & 1) > 0)
                        {		// Timer started, was already running
                            if ((new_cra & 0x10) > 0)	// Force load
                                ta_state = TimerState.T_LOAD_THEN_WAIT_THEN_COUNT;
                        }
                        else
                        {				// Timer stopped, was running
                            if ((new_cra & 0x10) > 0)	// Force load
                                ta_state = TimerState.T_LOAD_THEN_STOP;
                            else				// No force load
                                ta_state = TimerState.T_COUNT_THEN_STOP;
                        }
                        break;
                    case TimerState.T_LOAD_THEN_COUNT:
                    case TimerState.T_WAIT_THEN_COUNT:
                        if ((new_cra & 1) > 0)
                        {
                            if ((new_cra & 8) > 0)
                            {		// One-shot?
                                new_cra &= 0xfe;	// Yes, stop timer
                                ta_state = TimerState.T_STOP;
                            }
                            else if ((new_cra & 0x10) > 0)	// Force load
                                ta_state = TimerState.T_LOAD_THEN_WAIT_THEN_COUNT;
                        }
                        else
                        {
                            ta_state = TimerState.T_STOP;
                        }
                        break;
                }
                cra = (byte)(new_cra & 0xef);
                has_new_cra = false;
            }

            bool tb_interrupt = false;
            // Timer B state machine
            switch (tb_state)
            {
                case TimerState.T_WAIT_THEN_COUNT:
                    tb_state = TimerState.T_COUNT;		// fall through
                    goto case TimerState.T_STOP;

                case TimerState.T_STOP:
                    goto tb_idle;

                case TimerState.T_LOAD_THEN_STOP:
                    tb_state = TimerState.T_STOP;
                    tb = latchb;			// Reload timer
                    goto tb_idle;

                case TimerState.T_LOAD_THEN_COUNT:
                    tb_state = TimerState.T_COUNT;
                    tb = latchb;			// Reload timer
                    goto ta_idle;

                case TimerState.T_LOAD_THEN_WAIT_THEN_COUNT:
                    tb_state = TimerState.T_WAIT_THEN_COUNT;
                    if (tb == 1)
                    {
                        tb_interrupt = true;	// Interrupt if timer == 1
                        break;
                    }
                    else
                    {
                        tb = latchb;		// Reload timer
                        goto tb_idle;
                    }

                case TimerState.T_COUNT:
                    goto tb_count;

                case TimerState.T_COUNT_THEN_STOP:
                    tb_state = TimerState.T_STOP;
                    goto tb_count;
            }

            // Count timer B
        tb_count:
            if (tb_cnt_phi2 || (tb_cnt_ta && ta_underflow) || tb_interrupt)
            {
                if (tb == 0 || --tb == 0)
                {
                    // Decrement timer, underflow?
                    if (tb_state != TimerState.T_STOP || tb_interrupt)
                    {
                        tb = latchb;			// Reload timer
                        tb_irq_next_cycle = true; // Trigger interrupt in next cycle
                        icr |= 2;				// But set ICR bit now

                        if ((crb & 8) > 0)
                        {			// One-shot?
                            crb &= 0xfe;		// Yes, stop timer
                            new_crb &= 0xfe;
                            tb_state = TimerState.T_LOAD_THEN_STOP;	// Reload in next cycle
                        }
                        else
                            tb_state = TimerState.T_LOAD_THEN_COUNT;	// No, delay one cycle (and reload)
                    }
                }
            }

            // Delayed write to CRB?
        tb_idle:
            if (has_new_crb)
            {
                switch (tb_state)
                {
                    case TimerState.T_STOP:
                    case TimerState.T_LOAD_THEN_STOP:
                        if ((new_crb & 1) > 0)
                        {
                            // Timer started, wasn't running
                            if ((new_crb & 0x10) > 0)	// Force load
                                tb_state = TimerState.T_LOAD_THEN_WAIT_THEN_COUNT;
                            else				// No force load
                                tb_state = TimerState.T_WAIT_THEN_COUNT;
                        }
                        else
                        {
                            // Timer stopped, was already stopped
                            if ((new_crb & 0x10) > 0)	// Force load
                                tb_state = TimerState.T_LOAD_THEN_STOP;
                        }
                        break;
                    case TimerState.T_COUNT:
                        if ((new_crb & 1) > 0)
                        {
                            // Timer started, was already running
                            if ((new_crb & 0x10) > 0)	// Force load
                                tb_state = TimerState.T_LOAD_THEN_WAIT_THEN_COUNT;
                        }
                        else
                        {
                            // Timer stopped, was running
                            if ((new_crb & 0x10) > 0)	// Force load
                                tb_state = TimerState.T_LOAD_THEN_STOP;
                            else				// No force load
                                tb_state = TimerState.T_COUNT_THEN_STOP;
                        }
                        break;
                    case TimerState.T_LOAD_THEN_COUNT:
                    case TimerState.T_WAIT_THEN_COUNT:
                        if ((new_crb & 1) > 0)
                        {
                            if ((new_crb & 8) > 0)
                            {
                                // One-shot?
                                new_crb &= 0xfe;	// Yes, stop timer
                                tb_state = TimerState.T_STOP;
                            }
                            else if ((new_crb & 0x10) > 0)	// Force load
                                tb_state = TimerState.T_LOAD_THEN_WAIT_THEN_COUNT;
                        }
                        else
                        {
                            tb_state = TimerState.T_STOP;
                        }
                        break;
                }
                crb = (byte)(new_crb & 0xef);
                has_new_crb = false;
            }
        }
    }
}
