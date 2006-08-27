using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class Prefs
    {
        public Prefs()
        {
            NormalCycles = 63;
            BadLineCycles = 23;
            CIACycles = 63;
            FloppyCycles = 64;
            SkipFrames = 2;
            LatencyMin = 80;
            LatencyMax = 120;
            LatencyAvg = 280;
            ScalingNumerator = 2;
            ScalingDenominator = 2;

            for (int i = 0; i < 4; i++)
                DriveType[i] = SharpC64.DriveType.DRVTYPE_DIR;

            DrivePath[0] = "64prgs";

            ViewPort = "Default";
            DisplayMode ="Default";

            SIDType = SharpC64.SIDType.SIDTYPE_NONE;
            REUSize = SharpC64.REUSize.REU_NONE;
            DisplayType = SharpC64.DisplayType.DISPTYPE_WINDOW;

            SpritesOn = true;
            SpriteCollisions = true;
            Joystick1On = false;
            Joystick2On = false;
            JoystickSwap = false;
            LimitSpeed = true;
            FastReset = false;
            CIAIRQHack = false;
            MapSlash = true;
            Emul1541Proc = false;
            SIDFilters = true;
            DoubleScan = true;
            HideCursor = false;
            DirectSound = true;
            ExclusiveSound = false;
            AutoPause = false;
            PrefsAtStartup = false;
            SystemMemory = false;
            AlwaysCopy = false;
            SystemKeys = true;
            ShowLEDs = true;
        }

        public bool ShowEditor(bool startup, string prefs_name)
        {
            throw new NotImplementedException();
        }

        public void Check()
        {
            throw new NotImplementedException();
        }

        public void Load(string filename)
        {
            throw new NotImplementedException();
        }

        public bool Save(string filename)
        {
            throw new NotImplementedException();
        }

        #region Operators
        public static bool operator ==(Prefs lhs, Prefs rhs)
        {
            throw new NotImplementedException();
        }

        public static bool operator !=(Prefs lhs, Prefs rhs)
        {
            throw new NotImplementedException();
        }

        #endregion

        public int NormalCycles;		// Available CPU cycles in normal raster lines
        public int BadLineCycles;		// Available CPU cycles in Bad Lines
        public int CIACycles;			// CIA timer ticks per raster line
        public int FloppyCycles;		// Available 1541 CPU cycles per line
        public int SkipFrames;			// Draw every n-th frame

        public DriveType[] DriveType = new DriveType[4];		// Type of drive 8..11

        public string[] DrivePath = new string[4];	// Path for drive 8..11

        public string ViewPort;		    // Size of the C64 screen to display (Win32)
        public string DisplayMode;	    // Video mode to use for full screen (Win32)

        public SIDType SIDType;			// SID emulation type
        public REUSize REUSize;			// Size of REU
        public DisplayType DisplayType;	// Display type (BeOS)
        public int LatencyMin;			// Min msecs ahead of sound buffer (Win32)
        public int LatencyMax;			// Max msecs ahead of sound buffer (Win32)
        public int LatencyAvg;			// Averaging interval in msecs (Win32)
        public int ScalingNumerator;	// Window scaling numerator (Win32)
        public int ScalingDenominator;	// Window scaling denominator (Win32)

        public bool SpritesOn;			// Sprite display is on
        public bool SpriteCollisions;	// Sprite collision detection is on
        public bool Joystick1On;		// Joystick connected to port 1 of host
        public bool Joystick2On;		// Joystick connected to port 2 of host
        public bool JoystickSwap;		// Swap joysticks 1<->2
        public bool LimitSpeed;		    // Limit speed to 100%
        public bool FastReset;			// Skip RAM test on reset
        public bool CIAIRQHack;		    // Write to CIA ICR clears IRQ
        public bool MapSlash;			// Map '/' in C64 filenames
        public bool Emul1541Proc;		// Enable processor-level 1541 emulation
        public bool SIDFilters;		    // Emulate SID filters
        public bool DoubleScan;		    // Double scan lines (BeOS, if DisplayType == DISPTYPE_SCREEN)
        public bool HideCursor;		    // Hide mouse cursor when visible (Win32)
        public bool DirectSound;		// Use direct sound (instead of wav) (Win32)
        public bool ExclusiveSound;	    // Use exclusive mode with direct sound (Win32)
        public bool AutoPause;			// Auto pause when not foreground app (Win32)
        public bool PrefsAtStartup;	    // Show prefs dialog at startup (Win32)
        public bool SystemMemory;		// Put view work surface in system mem (Win32)
        public bool AlwaysCopy;		    // Always use a work surface (Win32)
        public bool SystemKeys;		    // Enable system keys and menu keys (Win32)
        public bool ShowLEDs;			// Show LEDs (Win32)
    }

    public static class GlobalPrefs
    {
        public static Prefs ThePrefs = new Prefs();
        public static Prefs ThePrefsOnDisk = new Prefs();

        static GlobalPrefs()
        {
            //ThePrefs.DrivePath[0] = @"C:\mysourcecode\Emulation\sharp-c64\TestHost\bin\Release\64prgs";
            //ThePrefs.Emul1541Proc = false;
            //ThePrefs.DriveType[0] = DriveType.DRVTYPE_DIR;
            ThePrefs.SIDType = SIDType.SIDTYPE_DIGITAL;
        }
    }

    // Drive types
    public enum DriveType
    {
        DRVTYPE_DIR,	// 1541 emulation in host file system
        DRVTYPE_D64,	// 1541 emulation in .d64 file
        DRVTYPE_T64		// 1541 emulation in .t64 file
    };


    // SID types
    public enum SIDType
    {
        SIDTYPE_NONE,		// SID emulation off
        SIDTYPE_DIGITAL,	// Digital SID emulation
        SIDTYPE_SIDCARD		// SID card
    };


    // REU sizes
    public enum REUSize
    {
        REU_NONE,		// No REU
        REU_128K,		// 128K
        REU_256K,		// 256K
        REU_512K		// 512K
    };


    // Display types (BeOS)
    public enum DisplayType
    {
        DISPTYPE_WINDOW,	// BWindow
        DISPTYPE_SCREEN		// BWindowScreen
    };
}
