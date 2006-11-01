#define NPRECOMPUTE_RESONANCE
#define NUSE_FIXPOINT_MATHS

// TODO: Test FIXPOINT.  This is currently untested, but does compile

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using SdlDotNet;
//using SGC.SDL;

namespace SharpC64
{
    internal partial class DigitalRenderer : SIDRenderer
    {
        #region constants
        static int sndbufsize = 2048;

        const UInt32 SAMPLE_FREQ = 44100;	                // Sample output frequency in Hz

        const UInt32 SID_FREQ = 985248;		                // SID frequency in Hz
        const UInt32 CALC_FREQ = 50;			            // Frequency at which calc_buffer is called in Hz (should be 50Hz)
        const UInt32 SID_CYCLES = SID_FREQ / SAMPLE_FREQ;	// # of SID clocks per sample frame
        const int SAMPLE_BUF_SIZE = 0x138 * 2;              // Size of buffer for sampled voice (double buffered)

        #endregion

        #region Public methods

        public DigitalRenderer()
        {
            for (int i = 0; i < voice.Length; i++)
                voice[i] = new DRVoice();

            // Link voices together
            voice[0].mod_by = voice[2];
            voice[1].mod_by = voice[0];
            voice[2].mod_by = voice[1];
            voice[0].mod_to = voice[1];
            voice[1].mod_to = voice[2];
            voice[2].mod_to = voice[0];

            // Calculate triangle table
            for (int i = 0; i < 0x1000; i++)
            {
                TriTable[i] = (UInt16)((i << 4) | (i >> 8));
                TriTable[0x1fff - i] = (UInt16)((i << 4) | (i >> 8));
            }

#if PRECOMPUTE_RESONANCE
#if USE_FIXPOINT_MATHS
            // slow floating point doesn't matter much on startup!
            for (int i = 0; i < 256; i++)
            {
                resonanceLP[i] = FixPoint.FixNo((227.755 - 1.7635 * i - 0.0176385 * i * i + 0.00333484 * i * i * i - 9.05683E-6 * i * i * i * i));
                resonanceHP[i] = FixPoint.FixNo((366.374 - 14.0052 * i + 0.603212 * i * i - 0.000880196 * i * i * i));
            }
            // Pre-compute the quotient. No problem since int-part is small enough
            sidquot = (Int32)((((double)SID_FREQ) * 65536) / SAMPLE_FREQ);
            // compute lookup table for Math.Sin and Math.Cos
            FixPoint.InitFixSinTab();
#else
	        for (int i=0; i<256; i++) {
	          resonanceLP[i] = (227.755 - 1.7635 * i - 0.0176385 * i * i + 0.00333484 * i * i * i - 9.05683E-6 * i * i * i * i);
	          resonanceHP[i] = (366.374 - 14.0052 * i + 0.603212 * i * i - 0.000880196 * i * i * i);
	        }
#endif
#endif

            Reset();

            // System specific initialization
            init_sound();
        }

        public override void Reset()
        {
            volume = 0;
            v3_mute = false;

            for (int v = 0; v < voice.Length; v++)
            {
                voice[v].wave = SIDWaveForm.WAVE_NONE;
                voice[v].eg_state = EGState.EG_IDLE;
                voice[v].count = voice[v].add = 0;
                voice[v].freq = voice[v].pw = 0;
                voice[v].eg_level = voice[v].s_level = 0;
                voice[v].a_add = voice[v].d_sub = voice[v].r_sub = EGTable[0];
                voice[v].gate = voice[v].ring = voice[v].test = false;
                voice[v].filter = voice[v].sync = false;
            }

            f_type = FilterType.FILT_NONE;
            f_freq = f_res = 0;

#if USE_FIXPOINT_MATHS
            f_ampl = FixPoint.FixNo(1);
            d1 = d2 = g1 = g2 = 0;
            xn1 = xn2 = yn1 = yn2 = 0;
#else
            f_ampl = 1.0f;
            d1 = d2 = g1 = g2 = 0.0f;
            xn1 = xn2 = yn1 = yn2 = 0.0f;
#endif

            sample_in_ptr = 0;
            Array.Clear(sample_buf, 0, sample_buf.Length);
        }

        public override void EmulateLine()
        {
            if (!ready)
                return;

            sample_buf[sample_in_ptr] = volume;
            sample_in_ptr = (sample_in_ptr + 1) % SAMPLE_BUF_SIZE;
        }

        public override void VBlank()
        {
            if (!ready)
                return;

            // Convert latency preferences from milliseconds to frags.
            int lead_hiwater = GlobalPrefs.ThePrefs.LatencyMax;
            int lead_lowater = GlobalPrefs.ThePrefs.LatencyMin;

            // If we're getting too far ahead of the audio skip a frag.
            if (audiostream.RemainingMilliseconds > lead_hiwater)
            {
                return;
            }

            // Calculate one frag.
            calc_buffer(sound_buffer, 0, sndbufsize);
            audiostream.Write(sound_buffer);


            // If we're getting too far behind the audio add an extra frag.
            if (audiostream.RemainingMilliseconds < lead_lowater)
            {
                calc_buffer(sound_buffer, 0, sndbufsize);
                audiostream.Write(sound_buffer);
            }

        }

        public override void WriteRegister(ushort adr, byte abyte)
        {
            if (!ready)
                return;

            int v = adr / 7;	// Voice number

            switch (adr)
            {
                case 0:
                case 7:
                case 14:
                    voice[v].freq = (UInt16)((voice[v].freq & 0xff00) | abyte);
#if USE_FIXPOINT_MATHS
                    voice[v].add = (UInt32)sidquot.imul((int)voice[v].freq);
#else
                    voice[v].add = (UInt32)((float)voice[v].freq * SID_FREQ / SAMPLE_FREQ);
#endif
                    break;

                case 1:
                case 8:
                case 15:
                    voice[v].freq = (UInt16)((voice[v].freq & 0xff) | (abyte << 8));
#if USE_FIXPOINT_MATHS
                    voice[v].add = (UInt32)sidquot.imul((int)voice[v].freq);
#else
                    voice[v].add = (UInt32)((float)voice[v].freq * SID_FREQ / SAMPLE_FREQ);
#endif
                    break;

                case 2:
                case 9:
                case 16:
                    voice[v].pw = (UInt16)((voice[v].pw & 0x0f00) | abyte);
                    break;

                case 3:
                case 10:
                case 17:
                    voice[v].pw = (UInt16)((voice[v].pw & 0xff) | ((abyte & 0xf) << 8));
                    break;

                case 4:
                case 11:
                case 18:
                    voice[v].wave = (SIDWaveForm)((abyte >> 4) & 0xf);
                    if ((abyte & 1) == 1 != voice[v].gate)
                        if ((abyte & 1) != 0)	// Gate turned on
                            voice[v].eg_state = EGState.EG_ATTACK;
                        else			// Gate turned off
                            if (voice[v].eg_state != EGState.EG_IDLE)
                                voice[v].eg_state = EGState.EG_RELEASE;
                    voice[v].gate = (abyte & 1) != 0;
                    voice[v].mod_by.sync = (abyte & 2) != 0;
                    voice[v].ring = (abyte & 4) != 0;
                    if ((voice[v].test = (abyte & 8) != 0))
                        voice[v].count = 0;
                    break;

                case 5:
                case 12:
                case 19:
                    voice[v].a_add = EGTable[abyte >> 4];
                    voice[v].d_sub = EGTable[abyte & 0xf];
                    break;

                case 6:
                case 13:
                case 20:
                    voice[v].s_level = (UInt32)((abyte >> 4) * 0x111111);
                    voice[v].r_sub = EGTable[abyte & 0xf];
                    break;

                case 22:
                    if (abyte != f_freq)
                    {
                        f_freq = abyte;
                        if (GlobalPrefs.ThePrefs.SIDFilters)
                            calc_filter();
                    }
                    break;

                case 23:
                    voice[0].filter = (abyte & 1) != 0;
                    voice[1].filter = (abyte & 2) != 0;
                    voice[2].filter = (abyte & 4) != 0;
                    if ((abyte >> 4) != f_res)
                    {
                        f_res = (byte)(abyte >> 4);
                        if (GlobalPrefs.ThePrefs.SIDFilters)
                            calc_filter();
                    }
                    break;

                case 24:
                    volume = (byte)(abyte & 0xf);
                    v3_mute = (abyte & 0x80) != 0;
                    if (((abyte >> 4) & 7) != (int)f_type)
                    {
                        f_type = (FilterType)((abyte >> 4) & 7);
#if USE_FIXPOINT_MATHS
                        xn1 = xn2 = yn1 = yn2 = 0;
#else
                        xn1 = xn2 = yn1 = yn2 = 0.0f;
#endif
                        if (GlobalPrefs.ThePrefs.SIDFilters)
                            calc_filter();
                    }
                    break;
            }
        }

        public override void NewPrefs(Prefs prefs)
        {
            calc_filter();
        }

        public override void Pause()
        {
            Audio.Paused = true;
        }

        public override void Resume()
        {
            Audio.Paused = false;
        }

        #endregion public methods

        #region private

        void init_sound()
        {
            audiostream = Audio.OpenAudioStream((int)SAMPLE_FREQ, AudioFormat.Unsigned16Little, SoundChannel.Mono, (short)sndbufsize);
            Audio.Paused = false;

            ready = Audio.AudioStatus == AudioStatus.Playing;
        }

        void calc_filter()
        {
#if USE_FIXPOINT_MATHS
            FixPoint fr, arg;

            if (f_type == FilterType.FILT_ALL)
            {
                d1 = 0; d2 = 0; g1 = 0; g2 = 0; f_ampl = FixPoint.FixNo(1); return;
            }
            else if (f_type == FilterType.FILT_NONE)
            {
                d1 = 0; d2 = 0; g1 = 0; g2 = 0; f_ampl = 0; return;
            }
#else
            float fr, arg;

            // Check for some trivial cases
            if (f_type == FilterType.FILT_ALL)
            {
                d1 = 0.0f; d2 = 0.0f;
                g1 = 0.0f; g2 = 0.0f;
                f_ampl = 1.0f;
                return;
            }
            else if (f_type == FilterType.FILT_NONE)
            {
                d1 = 0.0f; d2 = 0.0f;
                g1 = 0.0f; g2 = 0.0f;
                f_ampl = 0.0f;
                return;
            }
#endif

            // Calculate resonance frequency
            if (f_type == FilterType.FILT_LP || f_type == FilterType.FILT_LPBP)
#if PRECOMPUTE_RESONANCE
                fr = resonanceLP[f_freq];
#else
                fr = (float)(227.755 - 1.7635 * f_freq - 0.0176385 * f_freq * f_freq + 0.00333484 * f_freq * f_freq * f_freq - 9.05683E-6 * f_freq * f_freq * f_freq * f_freq);
#endif
            else
#if PRECOMPUTE_RESONANCE
                fr = resonanceHP[f_freq];
#else
                fr = (float)(366.374 - 14.0052 * f_freq + 0.603212 * f_freq * f_freq - 0.000880196 * f_freq * f_freq * f_freq);
#endif

#if USE_FIXPOINT_MATHS
            // explanations see below.
            arg = fr / (SAMPLE_FREQ >> 1);
            if (arg > FixPoint.FixNo(0.99)) { arg = FixPoint.FixNo(0.99); }
            if (arg < FixPoint.FixNo(0.01)) { arg = FixPoint.FixNo(0.01); }

            g2 = FixPoint.FixNo(0.55) + FixPoint.FixNo(1.2) * arg * (arg - 1) + FixPoint.FixNo(0.0133333333) * (int)f_res;
            g1 = FixPoint.FixNo(-2) * g2.sqrt() * FixPoint.fixcos(arg);

            if (f_type == FilterType.FILT_LPBP || f_type == FilterType.FILT_HPBP) { g2 += FixPoint.FixNo(0.1); }

            if (g1.abs() >= g2 + 1)
            {
                if (g1 > 0) { g1 = g2 + FixPoint.FixNo(0.99); }
                else { g1 = -(g2 + FixPoint.FixNo(0.99)); }
            }

            switch (f_type)
            {
                case FilterType.FILT_LPBP:
                case FilterType.FILT_LP:
                    d1 = FixPoint.FixNo(2); d2 = FixPoint.FixNo(1); f_ampl = FixPoint.FixNo(0.25) * (1 + g1 + g2); break;
                case FilterType.FILT_HPBP:
                case FilterType.FILT_HP:
                    d1 = FixPoint.FixNo(-2); d2 = FixPoint.FixNo(1); f_ampl = FixPoint.FixNo(0.25) * (1 - g1 + g2); break;
                case FilterType.FILT_BP:
                    d1 = 0; d2 = FixPoint.FixNo(-1);
                    f_ampl = FixPoint.FixNo(0.25) * (1 + g1 + g2) * (1 + FixPoint.fixcos(arg)) / FixPoint.fixsin(arg);
                    break;
                case FilterType.FILT_NOTCH:
                    d1 = FixPoint.FixNo(-2) * FixPoint.fixcos(arg); d2 = FixPoint.FixNo(1);
                    f_ampl = FixPoint.FixNo(0.25) * (1 + g1 + g2) * (1 + FixPoint.fixcos(arg)) / FixPoint.fixsin(arg);
                    break;
                default: break;
            }

#else

            // Limit to <1/2 sample frequency, avoid div by 0 in case FILT_BP below
            arg = fr / (float)(SAMPLE_FREQ >> 1);
            if (arg > 0.99)
                arg = 0.99f;
            if (arg < 0.01)
                arg = 0.01f;

            // Calculate poles (resonance frequency and resonance)
            g2 = (float)(0.55 + 1.2 * arg * arg - 1.2 * arg + (float)f_res * 0.0133333333);
            g1 = (float)(-2.0 * Math.Sqrt(g2) * Math.Cos(Math.PI * arg));

            // Increase resonance if LP/HP combined with BP
            if (f_type == FilterType.FILT_LPBP || f_type == FilterType.FILT_HPBP)
                g2 += 0.1f;

            // Stabilize filter
            if (Math.Abs(g1) >= g2 + 1.0)
                if (g1 > 0.0)
                    g1 = g2 + 0.99f;
                else
                    g1 = (float)-(g2 + 0.99);

            // Calculate roots (filter characteristic) and input attenuation
            switch (f_type)
            {

                case FilterType.FILT_LPBP:
                case FilterType.FILT_LP:
                    d1 = 2.0f; d2 = 1.0f;
                    f_ampl = (float)(0.25 * (1.0 + g1 + g2));
                    break;

                case FilterType.FILT_HPBP:
                case FilterType.FILT_HP:
                    d1 = -2.0f; d2 = 1.0f;
                    f_ampl = (float)(0.25 * (1.0 - g1 + g2));
                    break;

                case FilterType.FILT_BP:
                    d1 = 0.0f; d2 = -1.0f;
                    f_ampl = (float)(0.25 * (1.0 + g1 + g2) * (1 + Math.Cos(Math.PI * arg)) / Math.Sin(Math.PI * arg));
                    break;

                case FilterType.FILT_NOTCH:
                    d1 = (float)(-2.0 * Math.Cos(Math.PI * arg)); d2 = 1.0f;
                    f_ampl = (float)(0.25 * (1.0 + g1 + g2) * (1 + Math.Cos(Math.PI * arg)) / (Math.Sin(Math.PI * arg)));
                    break;

                default:
                    break;
            }
#endif
        }

        void calc_buffer(Int16[] buf, int buf_pos, long count)
        {
            // Get filter coefficients, so the emulator won't change
            // them in the middle of our calculations
#if USE_FIXPOINT_MATHS
	        FixPoint cf_ampl = f_ampl;
	        FixPoint cd1 = d1, cd2 = d2, cg1 = g1, cg2 = g2;
#else
            float cf_ampl = f_ampl;
            float cd1 = d1, cd2 = d2, cg1 = g1, cg2 = g2;
#endif

            // Index in sample_buf for reading, 16.16 fixed
            UInt32 sample_count = (UInt32)((sample_in_ptr + SAMPLE_BUF_SIZE / 2) << 16);

            //count >>= 1;	// 16 bit mono output, count is in bytes

            while (count-- > 0)
            {
                Int32 sum_output;
                Int32 sum_output_filter = 0;

                // Get current master volume from sample buffer,
                // calculate sampled voice
                byte master_volume = sample_buf[(sample_count >> 16) % SAMPLE_BUF_SIZE];
                sample_count += ((0x138 * 50) << 16) / SAMPLE_FREQ;
                sum_output = SampleTab[master_volume] << 8;

                // Loop for all three voices
                for (int j = 0; j < voice.Length; j++)
                {
                    DRVoice v = voice[j];

                    // Envelope generators
                    UInt16 envelope;

                    switch (v.eg_state)
                    {
                        case EGState.EG_ATTACK:
                            v.eg_level += v.a_add;
                            if (v.eg_level > 0xffffff)
                            {
                                v.eg_level = 0xffffff;
                                v.eg_state = EGState.EG_DECAY;
                            }
                            break;
                        case EGState.EG_DECAY:
                            if (v.eg_level <= v.s_level || v.eg_level > 0xffffff)
                                v.eg_level = v.s_level;
                            else
                            {
                                v.eg_level -= v.d_sub >> EGDRShift[v.eg_level >> 16];
                                if (v.eg_level <= v.s_level || v.eg_level > 0xffffff)
                                    v.eg_level = v.s_level;
                            }
                            break;
                        case EGState.EG_RELEASE:
                            v.eg_level -= v.r_sub >> EGDRShift[v.eg_level >> 16];
                            if (v.eg_level > 0xffffff)
                            {
                                v.eg_level = 0;
                                v.eg_state = EGState.EG_IDLE;
                            }
                            break;
                        case EGState.EG_IDLE:
                            v.eg_level = 0;
                            break;
                    }
                    envelope = (UInt16)((v.eg_level * master_volume) >> 20);

                    // Waveform generator
                    UInt16 output;

                    if (!v.test)
                        v.count += v.add;

                    if (v.sync && (v.count > 0x1000000))
                        v.mod_to.count = 0;

                    v.count &= 0xffffff;

                    switch (v.wave)
                    {
                        case SIDWaveForm.WAVE_TRI:
                            if (v.ring)
                                output = TriTable[(v.count ^ (v.mod_by.count & 0x800000)) >> 11];
                            else
                                output = TriTable[v.count >> 11];
                            break;
                        case SIDWaveForm.WAVE_SAW:
                            output = (UInt16)(v.count >> 8);
                            break;
                        case SIDWaveForm.WAVE_RECT:
                            if (v.count > (UInt32)(v.pw << 12))
                                output = 0xffff;
                            else
                                output = 0;
                            break;
                        case SIDWaveForm.WAVE_TRISAW:
                            output = TriSawTable[v.count >> 16];
                            break;
                        case SIDWaveForm.WAVE_TRIRECT:
                            if (v.count > (UInt32)(v.pw << 12))
                                output = TriRectTable[v.count >> 16];
                            else
                                output = 0;
                            break;
                        case SIDWaveForm.WAVE_SAWRECT:
                            if (v.count > (UInt32)(v.pw << 12))
                                output = SawRectTable[v.count >> 16];
                            else
                                output = 0;
                            break;
                        case SIDWaveForm.WAVE_TRISAWRECT:
                            if (v.count > (UInt32)(v.pw << 12))
                                output = TriSawRectTable[v.count >> 16];
                            else
                                output = 0;
                            break;
                        case SIDWaveForm.WAVE_NOISE:
                            if (v.count > 0x100000)
                            {
                                output = (UInt16)(v.noise = (UInt32)(MOS6581.sid_random() << 8));
                                v.count &= 0xfffff;
                            }
                            else
                                output = (UInt16)v.noise;
                            break;
                        default:
                            output = 0x8000;
                            break;
                    }
                    if (v.filter)
                        sum_output_filter += (Int16)(output ^ 0x8000) * envelope;
                    else
                        sum_output += (Int16)(output ^ 0x8000) * envelope;
                }

                // Filter
                if (GlobalPrefs.ThePrefs.SIDFilters)
                {
#if USE_FIXPOINT_MATHS
			    Int32 xn = cf_ampl.imul(sum_output_filter);
			    Int32 yn = xn+cd1.imul(xn1)+cd2.imul(xn2)-cg1.imul(yn1)-cg2.imul(yn2);
			    yn2 = yn1; yn1 = yn; xn2 = xn1; xn1 = xn;
			    sum_output_filter = yn;
#else
                    float xn = (float)sum_output_filter * cf_ampl;
                    float yn = xn + cd1 * xn1 + cd2 * xn2 - cg1 * yn1 - cg2 * yn2;
                    yn2 = yn1; yn1 = yn; xn2 = xn1; xn1 = xn;
                    sum_output_filter = (Int32)yn;
#endif
                }

                // Write to buffer
                buf[buf_pos++] = (Int16)((sum_output + sum_output_filter) >> 10);
            }
        }

        bool ready;						// Flag: Renderer has initialized and is ready
        byte volume;					// Master volume
        bool v3_mute;					// Voice 3 muted

        static UInt16[] TriTable = new UInt16[0x1000 * 2];	// Tables for certain waveforms

        DRVoice[] voice = new DRVoice[3];				// Data for 3 voices

        FilterType f_type;					// Filter type
        byte f_freq;					// SID filter frequency (upper 8 bits)
        byte f_res;					// Filter resonance (0..15)

#if USE_FIXPOINT_MATHS

        FixPoint f_ampl;
        FixPoint d1, d2, g1, g2;
        Int32 xn1, xn2, yn1, yn2;		// can become very large
        FixPoint sidquot;

#if PRECOMPUTE_RESONANCE
        FixPoint[] resonanceLP = new FixPoint[256];
        FixPoint[] resonanceHP = new FixPoint[256];
#endif

#else
        float f_ampl;					// IIR filter input attenuation
        float d1, d2, g1, g2;			// IIR filter coefficients
        float xn1, xn2, yn1, yn2;		// IIR filter previous input/output signal

#if PRECOMPUTE_RESONANCE
	float[] resonanceLP = new float[256];			// shortcut for calc_filter
	float[] resonanceHP = new float[256];
#endif

#endif

        byte[] sample_buf = new byte[SAMPLE_BUF_SIZE];  // Buffer for sampled voice
        int sample_in_ptr;				                // Index in sample_buf for writing
        Int16[] sound_buffer = new Int16[sndbufsize];
        AudioStream audiostream;


        #endregion private
    }

    // SID waveforms (some of them :-)
    enum SIDWaveForm : int
    {
        WAVE_NONE,
        WAVE_TRI,
        WAVE_SAW,
        WAVE_TRISAW,
        WAVE_RECT,
        WAVE_TRIRECT,
        WAVE_SAWRECT,
        WAVE_TRISAWRECT,
        WAVE_NOISE
    };

    // EG states
    enum EGState : int
    {
        EG_IDLE,
        EG_ATTACK,
        EG_DECAY,
        EG_RELEASE
    };

    // Filter types
    enum FilterType : byte
    {
        FILT_NONE,
        FILT_LP,
        FILT_BP,
        FILT_LPBP,
        FILT_HP,
        FILT_NOTCH,
        FILT_HPBP,
        FILT_ALL
    };

    // Structure for one voice
    internal class DRVoice
    {
        internal SIDWaveForm wave;		// Selected waveform
        internal EGState eg_state;	// Current state of EG
        internal DRVoice mod_by;	// Voice that modulates this one
        internal DRVoice mod_to;	// Voice that is modulated by this one

        internal UInt32 count;	// Counter for waveform generator, 8.16 fixed
        internal UInt32 add;		// Added to counter in every frame

        internal UInt16 freq;		// SID frequency value
        internal UInt16 pw;		// SID pulse-width value

        internal UInt32 a_add;	// EG parameters
        internal UInt32 d_sub;
        internal UInt32 s_level;
        internal UInt32 r_sub;
        internal UInt32 eg_level;	// Current EG level, 8.16 fixed

        internal UInt32 noise;	// Last noise generator output value

        internal bool gate;		// EG gate bit
        internal bool ring;		// Ring modulation bit
        internal bool test;		// Test bit
        internal bool filter;	// Flag: Voice filtered

        // The following bit is set for the modulating
        // voice, not for the modulated one (as the SID bits)
        internal bool sync;		// Sync modulation bit
    };

}
