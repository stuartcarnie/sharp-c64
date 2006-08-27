#define NSCANLINE

using System;
using System.Collections.Generic;
using System.Text;
using SdlDotNet;
using System.Drawing;
using System.Diagnostics;

namespace SharpC64
{
    public class C64Display
    {
        #region Public constants
        public const int DISPLAY_X = 0x180;
        public const int DISPLAY_Y = 0x110;
        #endregion

        #region Private Constants
        const byte joystate = 0xff;
        #endregion

        #region Public methods

        public C64Display(C64 c64)
        {
            _TheC64 = c64;
        }

        internal void Update()
        {

            // Draw speedometer/LEDs
            Rectangle r = new Rectangle(0, DISPLAY_Y, DISPLAY_X, 15);
            _c64Screen.DrawFilledBox(r, Color.Gray);

            r.Width = DISPLAY_X; r.Height = 1;
            _c64Screen.DrawFilledBox(r, Color.LightGray);

            r.Y = DISPLAY_Y + 14;
            _c64Screen.DrawFilledBox(r, Color.DarkGray);
            r.Width = 16;

            for (int i = 2; i < 6; i++)
            {
                r.X = DISPLAY_X * i / 5 - 24; r.Y = DISPLAY_Y + 4;
                _c64Screen.DrawFilledBox(r, Color.DarkGray);
                r.Y = DISPLAY_Y + 10;
                _c64Screen.DrawFilledBox(r, Color.LightGray);
            }

            r.Y = DISPLAY_Y; r.Width = 1; r.Height = 15;
            for (int i = 0; i < 5; i++)
            {
                r.X = DISPLAY_X * i / 5;
                _c64Screen.DrawFilledBox(r, Color.LightGray);
                r.X = DISPLAY_X * (i + 1) / 5 - 1;
                _c64Screen.DrawFilledBox(r, Color.DarkGray);
            }

            r.Y = DISPLAY_Y + 4; r.Height = 7;
            for (int i = 2; i < 6; i++)
            {
                r.X = DISPLAY_X * i / 5 - 24;
                _c64Screen.DrawFilledBox(r, Color.DarkGray);
                r.X = DISPLAY_X * i / 5 - 9;
                _c64Screen.DrawFilledBox(r, Color.LightGray);
            }
            r.Y = DISPLAY_Y + 5; r.Width = 14; r.Height = 5;
            for (int i = 0; i < 4; i++)
            {
                r.X = DISPLAY_X * (i + 2) / 5 - 23;
                Color c;
                switch (led_state[i])
                {
                    case DriveLEDState.DRVLED_ON:
                        c = Color.Green;
                        break;
                    case DriveLEDState.DRVLED_ERROR:
                        c = Color.Red;
                        break;
                    default:
                        c = Color.Black;
                        break;
                }
                _c64Screen.DrawFilledBox(r, c);
            }

            draw_string(DISPLAY_X * 1 / 5 + 8, DISPLAY_Y + 4, "D\x12 8", (byte)Color.Black.ToArgb(), (byte)Color.Gray.ToArgb());
            draw_string(DISPLAY_X * 2 / 5 + 8, DISPLAY_Y + 4, "D\x12 9", (byte)Color.Black.ToArgb(), (byte)Color.Gray.ToArgb());
            draw_string(DISPLAY_X * 3 / 5 + 8, DISPLAY_Y + 4, "D\x12 10", (byte)Color.Black.ToArgb(), (byte)Color.Gray.ToArgb());
            draw_string(DISPLAY_X * 4 / 5 + 8, DISPLAY_Y + 4, "D\x12 11", (byte)Color.Black.ToArgb(), (byte)Color.Gray.ToArgb());
            draw_string(24, DISPLAY_Y + 4, speedometer_string, (byte)Color.Black.ToArgb(), (byte)Color.Gray.ToArgb());

#if SCANLINE
            unsafe
            {
                byte* srcscanline = (byte*)_c64Screen.Pixels;
                byte* destscanline = (byte*)_videoDisplay.Pixels;

                short srcstride = _c64Screen.Pitch;
                short deststride = (short)(_videoDisplay.Pitch * 2);

                for (int y = 0; y < DISPLAY_Y + 17; y++)
                {
                    for (int x = 0; x < DISPLAY_X; x++)
                    {
                        destscanline[x*2] = srcscanline[x];
                        destscanline[x*2+1] = srcscanline[x];
                    }

                    srcscanline += srcstride;
                    destscanline += deststride;
                }
            }
            _videoDisplay.Flip();
#else
            _c64Screen.Flip();
#endif
        }

        public void UpdateLEDs(DriveLEDState l0, DriveLEDState l1, DriveLEDState l2, DriveLEDState l3)
        {
            led_state[0] = l0;
            led_state[1] = l1;
            led_state[2] = l2;
            led_state[3] = l3;
        }

        internal void Speedometer(int speed)
        {
            speedometer_string = String.Format("{0}%", speed);
        }

        unsafe internal byte* BitmapBase
        {
            get
            {
                return (byte*)_c64Screen.Pixels;
            }
        }

        public int BitmapXMod
        {
            get
            {
                return (int)_c64Screen.Pitch;
            }
        }

        public void PollKeyboard(byte[] key_matrix, byte[] rev_matrix, ref byte joystick)
        {
            SdlEventArgs[] events = Events.Retrieve();
            KeyboardEventArgs keyEvent;

            foreach (SdlEventArgs evt in events)
            {
                switch (evt.Type)
                {

                    // Key pressed
                    case EventTypes.KeyDown:
                        keyEvent = (KeyboardEventArgs)evt;
                        switch (keyEvent.Key)
                        {

                            case Key.F9:	// F9: Invoke SAM
                                //SAM(TheC64);
#if DEBUG_INSTRUCTIONS
                                TheC64.TheCPU.debugLogger.Enabled = !TheC64.TheCPU.debugLogger.Enabled;
#endif
                                break;

                            case Key.F10:	// F10: Quit
                                quit_requested = true;
                                break;

                            case Key.F11:	// F11: NMI (Restore)
                                _TheC64.NMI();
                                break;

                            case Key.F12:	// F12: Reset
                                TheC64.Reset();
                                break;

                            case Key.CapsLock:
                                swapjoysticks = true;
                                break;

                            case Key.KeypadPlus:	// '+' on keypad: Increase SkipFrames
                                GlobalPrefs.ThePrefs.SkipFrames++;
                                break;

                            case Key.KeypadMinus:	// '-' on keypad: Decrease SkipFrames
                                if (GlobalPrefs.ThePrefs.SkipFrames > 1)
                                    GlobalPrefs.ThePrefs.SkipFrames--;
                                break;

                            case Key.KeypadMultiply:	// '*' on keypad: Toggle speed limiter
                                GlobalPrefs.ThePrefs.LimitSpeed = !GlobalPrefs.ThePrefs.LimitSpeed;
                                break;

                            default:
                                translate_key(keyEvent.Key, false, key_matrix, rev_matrix, ref joystick);
                                break;
                        }
                        break;

                    // Key released
                    case EventTypes.KeyUp:
                        keyEvent = (KeyboardEventArgs)evt;
                        if (keyEvent.Key == Key.CapsLock)
                            swapjoysticks = false;
                        else
                            translate_key(keyEvent.Key, true, key_matrix, rev_matrix, ref joystick);
                        break;

                    // Quit Frodo
                    case EventTypes.Quit:
                        quit_requested = true;
                        break;
                }
            }
        }

        int MATRIX(int a, int b)
        {
            return (((a) << 3) | (b));
        }

        private void translate_key(Key key, bool key_up, byte[] key_matrix, byte[] rev_matrix, ref byte joystick)
        {
            int c64_key = -1;
            switch (key)
            {
                case Key.A: c64_key = MATRIX(1, 2); break;
                case Key.B: c64_key = MATRIX(3, 4); break;
                case Key.C: c64_key = MATRIX(2, 4); break;
                case Key.D: c64_key = MATRIX(2, 2); break;
                case Key.E: c64_key = MATRIX(1, 6); break;
                case Key.F: c64_key = MATRIX(2, 5); break;
                case Key.G: c64_key = MATRIX(3, 2); break;
                case Key.H: c64_key = MATRIX(3, 5); break;
                case Key.I: c64_key = MATRIX(4, 1); break;
                case Key.J: c64_key = MATRIX(4, 2); break;
                case Key.K: c64_key = MATRIX(4, 5); break;
                case Key.L: c64_key = MATRIX(5, 2); break;
                case Key.M: c64_key = MATRIX(4, 4); break;
                case Key.N: c64_key = MATRIX(4, 7); break;
                case Key.O: c64_key = MATRIX(4, 6); break;
                case Key.P: c64_key = MATRIX(5, 1); break;
                case Key.Q: c64_key = MATRIX(7, 6); break;
                case Key.R: c64_key = MATRIX(2, 1); break;
                case Key.S: c64_key = MATRIX(1, 5); break;
                case Key.T: c64_key = MATRIX(2, 6); break;
                case Key.U: c64_key = MATRIX(3, 6); break;
                case Key.V: c64_key = MATRIX(3, 7); break;
                case Key.W: c64_key = MATRIX(1, 1); break;
                case Key.X: c64_key = MATRIX(2, 7); break;
                case Key.Y: c64_key = MATRIX(3, 1); break;
                case Key.Z: c64_key = MATRIX(1, 4); break;

                case Key.Zero: c64_key = MATRIX(4, 3); break;
                case Key.One: c64_key = MATRIX(7, 0); break;
                case Key.Two: c64_key = MATRIX(7, 3); break;
                case Key.Three: c64_key = MATRIX(1, 0); break;
                case Key.Four: c64_key = MATRIX(1, 3); break;
                case Key.Five: c64_key = MATRIX(2, 0); break;
                case Key.Six: c64_key = MATRIX(2, 3); break;
                case Key.Seven: c64_key = MATRIX(3, 0); break;
                case Key.Eight: c64_key = MATRIX(3, 3); break;
                case Key.Nine: c64_key = MATRIX(4, 0); break;

                case Key.Space: c64_key = MATRIX(7, 4); break;
                case Key.BackQuote: c64_key = MATRIX(7, 1); break;
                case Key.Backslash: c64_key = MATRIX(6, 6); break;
                case Key.Comma: c64_key = MATRIX(5, 7); break;
                case Key.Period: c64_key = MATRIX(5, 4); break;
                case Key.Minus: c64_key = MATRIX(5, 0); break;
                case Key.Equals: c64_key = MATRIX(5, 3); break;
                case Key.LeftBracket: c64_key = MATRIX(5, 6); break;
                case Key.RightBracket: c64_key = MATRIX(6, 1); break;
                case Key.Semicolon: c64_key = MATRIX(5, 5); break;
                case Key.Quote: c64_key = MATRIX(6, 2); break;
                case Key.Slash: c64_key = MATRIX(6, 7); break;

                case Key.Escape: c64_key = MATRIX(7, 7); break;
                case Key.Return: c64_key = MATRIX(0, 1); break;
                case Key.Backspace:
                case Key.Delete: c64_key = MATRIX(0, 0); break;
                case Key.Insert: c64_key = MATRIX(6, 3); break;
                case Key.Home: c64_key = MATRIX(6, 3); break;
                case Key.End: c64_key = MATRIX(6, 0); break;
                case Key.PageUp: c64_key = MATRIX(6, 0); break;
                case Key.PageDown: c64_key = MATRIX(6, 5); break;

                case Key.LeftControl: 
                case Key.Tab: c64_key = MATRIX(7, 2); break;
                case Key.RightControl: c64_key = MATRIX(7, 5); break;
                case Key.LeftShift: c64_key = MATRIX(1, 7); break;
                case Key.RightShift: c64_key = MATRIX(6, 4); break;
                case Key.LeftAlt:
                case Key.LeftMeta: c64_key = MATRIX(7, 5); break;
                case Key.RightAlt:
                case Key.RightMeta: c64_key = MATRIX(7, 5); break;

                case Key.UpArrow: c64_key = MATRIX(0, 7) | 0x80; break;
                case Key.DownArrow: c64_key = MATRIX(0, 7); break;
                case Key.LeftArrow: c64_key = MATRIX(0, 2) | 0x80; break;

                case Key.RightArrow: c64_key = MATRIX(0, 2); break;

                case Key.F1: c64_key = MATRIX(0, 4); break;
                case Key.F2: c64_key = MATRIX(0, 4) | 0x80; break;
                case Key.F3: c64_key = MATRIX(0, 5); break;
                case Key.F4: c64_key = MATRIX(0, 5) | 0x80; break;
                case Key.F5: c64_key = MATRIX(0, 6); break;
                case Key.F6: c64_key = MATRIX(0, 6) | 0x80; break;
                case Key.F7: c64_key = MATRIX(0, 3); break;
                case Key.F8: c64_key = MATRIX(0, 3) | 0x80; break;

                case Key.Keypad0:
                case Key.Keypad5: c64_key = 0x10 | 0x40; break;
                case Key.Keypad1: c64_key = 0x06 | 0x40; break;
                case Key.Keypad2: c64_key = 0x02 | 0x40; break;
                case Key.Keypad3: c64_key = 0x0a | 0x40; break;
                case Key.Keypad4: c64_key = 0x04 | 0x40; break;
                case Key.Keypad6: c64_key = 0x08 | 0x40; break;
                case Key.Keypad7: c64_key = 0x05 | 0x40; break;
                case Key.Keypad8: c64_key = 0x01 | 0x40; break;
                case Key.Keypad9: c64_key = 0x09 | 0x40; break;

                case Key.KeypadDivide: c64_key = MATRIX(6, 7); break;
                case Key.KeypadEnter: c64_key = MATRIX(0, 1); break;
            }

            if (c64_key < 0)
                return;

            // Handle joystick emulation
            if ((c64_key & 0x40) != 0)
            {
                c64_key &= 0x1f;
                if (key_up)
                    joystick |= (byte)c64_key;
                else
                    joystick &= (byte)~c64_key;
                return;
            }

            // Handle other keys
            bool shifted = (c64_key & 0x80) != 0;
            int c64_byte = (c64_key >> 3) & 7;
            int c64_bit = c64_key & 7;
            if (key_up)
            {
                if (shifted)
                {
                    key_matrix[6] |= 0x10;
                    rev_matrix[4] |= 0x40;
                }
                key_matrix[c64_byte] |= (byte)(1 << c64_bit);
                rev_matrix[c64_bit] |= (byte)(1 << c64_byte);
            }
            else
            {
                if (shifted)
                {
                    key_matrix[6] &= 0xef;
                    rev_matrix[4] &= 0xbf;
                }
                key_matrix[c64_byte] &= (byte)~(1 << c64_bit);
                rev_matrix[c64_bit] &= (byte)~(1 << c64_byte);
            }
        }

        public void InitColors(byte[] colors)
        {
            Tao.Sdl.Sdl.SDL_Color[] palette = new Tao.Sdl.Sdl.SDL_Color[21];
            for (int i = 0; i < 16; i++)
            {
                palette[i].r = palette_red[i];
                palette[i].g = palette_green[i];
                palette[i].b = palette_blue[i];
            }

            IntPtr current = Tao.Sdl.Sdl.SDL_GetVideoSurface();
            Tao.Sdl.Sdl.SDL_SetColors(current, palette, 0, 21);

            for (int i = 0; i < 256; i++)
                colors[i] = (byte)(i & 0x0f);
        }

        internal void NewPrefs(Prefs prefs)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        internal void WaitUntilActive()
        {

        }

        internal void Initialize()
        {
            init_graphics();

        }

        internal void ShowRequester(string a, string button1, string button2)
        {
            ShowRequester(a, button1);
        }

        internal void ShowRequester(string a, string button1)
        {
            Console.WriteLine("{0}: {1}", a, button1);
        }

        #endregion Public methods

        #region Public properties

        internal bool SwapJoysticks
        {
            [DebuggerStepThrough]
            get { return swapjoysticks; }
            [DebuggerStepThrough]
            set { swapjoysticks = value; }
        }

        public C64 TheC64
        {
            [DebuggerStepThrough]
            get { return _TheC64; }
            [DebuggerStepThrough]
            set { _TheC64 = value; }
        }

        public bool QuitRequested
        {
            [DebuggerStepThrough]
            get { return quit_requested; }
            [DebuggerStepThrough]
            set { quit_requested = value; }
        }

        public Surface C64Screen
        {
            get { return _c64Screen; }
        }

        #endregion

        #region Private methods

        Surface _c64Screen;
#if SCANLINE
        Surface _videoDisplay;
#endif
        int init_graphics()
        {
            // Init SDL
            Video.Initialize();

#if SCANLINE
            _c64Screen = Video.CreateRgbSurface(DISPLAY_X, DISPLAY_Y + 17, 8, 0xff, 0xff, 0xff, 0x00, false);
            _videoDisplay = Video.SetVideoModeWindow(DISPLAY_X * 2, (DISPLAY_Y + 17) * 2, 8);
#else
            _c64Screen = Video.SetVideoModeWindow(DISPLAY_X, DISPLAY_Y + 17, 8);
#endif

            // Open window
            Video.WindowCaption = "Sharp-C64";

            return 1;
        }

        unsafe private void draw_string(int x, int y, string str, byte front_color, byte back_color)
        {
            byte* pb = (byte*)_c64Screen.Pixels + _c64Screen.Pitch * y + x;
            char c;
            fixed (byte* qq = TheC64.Char)
            {
                for (int i = 0; i < str.Length; i++)
                {
                    c = str[i];
                    byte* q = qq + c * 8 + 0x800;
                    byte* p = pb;
                    for (int j = 0; j < 8; j++)
                    {
                        byte v = *q++;
                        p[0] = (v & 0x80) != 0 ? front_color : back_color;
                        p[1] = (v & 0x40) != 0 ? front_color : back_color;
                        p[2] = (v & 0x20) != 0 ? front_color : back_color;
                        p[3] = (v & 0x10) != 0 ? front_color : back_color;
                        p[4] = (v & 0x08) != 0 ? front_color : back_color;
                        p[5] = (v & 0x04) != 0 ? front_color : back_color;
                        p[6] = (v & 0x02) != 0 ? front_color : back_color;
                        p[7] = (v & 0x01) != 0 ? front_color : back_color;
                        p += _c64Screen.Pitch;
                    }
                    pb += 8;
                }
            }
        }

        #endregion

        #region Private fields

        C64 _TheC64;

        DriveLEDState[] led_state = new DriveLEDState[4];
        DriveLEDState[] old_led_state = new DriveLEDState[4];

        bool swapjoysticks;

        string speedometer_string = String.Empty;

        bool quit_requested = false;

        #endregion Private fields

#if USE_THEORETICAL_COLORS

        // C64 color palette (theoretical values)
        static readonly byte[] palette_red = {
	        0x00, 0xff, 0xff, 0x00, 0xff, 0x00, 0x00, 0xff, 0xff, 0x80, 0xff, 0x40, 0x80, 0x80, 0x80, 0xc0
        };

        static readonly byte[] palette_green = {
	        0x00, 0xff, 0x00, 0xff, 0x00, 0xff, 0x00, 0xff, 0x80, 0x40, 0x80, 0x40, 0x80, 0xff, 0x80, 0xc0
        };

        static readonly byte[] palette_blue = {
	        0x00, 0xff, 0x00, 0xff, 0xff, 0x00, 0xff, 0x00, 0x00, 0x00, 0x80, 0x40, 0x80, 0x80, 0xff, 0xc0
        };

#else

        // C64 color palette (more realistic looking colors)
        static readonly byte[] palette_red = {
	        0x00, 0xff, 0x99, 0x00, 0xcc, 0x44, 0x11, 0xff, 0xaa, 0x66, 0xff, 0x40, 0x80, 0x66, 0x77, 0xc0
        };

        static readonly byte[] palette_green = {
	        0x00, 0xff, 0x00, 0xff, 0x00, 0xcc, 0x00, 0xff, 0x55, 0x33, 0x66, 0x40, 0x80, 0xff, 0x77, 0xc0
        };

        static readonly byte[] palette_blue = {
	        0x00, 0xff, 0x00, 0xcc, 0xcc, 0x44, 0x99, 0x00, 0x00, 0x00, 0x66, 0x40, 0x80, 0x66, 0xff, 0xc0
        };

#endif

    }

    
}
