using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class IEC
    {
        public const int NAMEBUF_LENGTH = 256;
        
        #region Public methods

        public IEC(C64Display display)
        {
            the_display = display;

            int i;

            // Create drives 8..11
            for (i = 0; i < drive.Length; i++)
                drive[i] = null;	// Important because UpdateLEDs is called from the drive constructors (via set_error)

            if (!GlobalPrefs.ThePrefs.Emul1541Proc)
            {
                DriveType[] prefDrives = GlobalPrefs.ThePrefs.DriveType;

                for (i = 0; i < prefDrives.Length; i++)
                {
                    if (prefDrives[i] == DriveType.DRVTYPE_DIR)
                        drive[i] = new FSDrive(this, GlobalPrefs.ThePrefs.DrivePath[i]);
                    else if (prefDrives[i] == DriveType.DRVTYPE_D64)
                        drive[i] = new D64Drive(this, GlobalPrefs.ThePrefs.DrivePath[i]);
                    else
                        drive[i] = new T64Drive(this, GlobalPrefs.ThePrefs.DrivePath[i]);
                }
            }

            listener_active = talker_active = false;
            listening = false;
        }

        public void Reset()
        {
            for (int i = 0; i < drive.Length; i++)
                if (drive[i] != null && drive[i].Ready)
                    drive[i].Reset();

            UpdateLEDs();
        }

        public void NewPrefs(Prefs prefs)
        {
            // Delete and recreate all changed drives
            for (int i = 0; i < 4; i++)
                if ((GlobalPrefs.ThePrefs.DriveType[i] != prefs.DriveType[i]) || GlobalPrefs.ThePrefs.DrivePath[i] != prefs.DrivePath[i] || GlobalPrefs.ThePrefs.Emul1541Proc != prefs.Emul1541Proc)
                {
                    drive[i] = null;	// Important because UpdateLEDs is called from drive constructors (via set_error())
                    if (!prefs.Emul1541Proc)
                    {
                        if (prefs.DriveType[i] == DriveType.DRVTYPE_DIR)
                            drive[i] = new FSDrive(this, prefs.DrivePath[i]);
                        else if (prefs.DriveType[i] == DriveType.DRVTYPE_D64)
                            drive[i] = new D64Drive(this, prefs.DrivePath[i]);
                        else
                            drive[i] = new T64Drive(this, prefs.DrivePath[i]);
                    }
                }

            UpdateLEDs();
        }

        public void UpdateLEDs()
        {
            if (drive[0] != null && drive[1] != null && drive[2] != null && drive[3] != null)
                the_display.UpdateLEDs(drive[0].LED, drive[1].LED, drive[2].LED, drive[3].LED);
        }

        public byte Out(byte abyte, bool eoi)
        {
            if (listener_active)
            {
                if (received_cmd == IECCommandCode.CMD_OPEN)
                    return open_out(abyte, eoi);
                if (received_cmd == IECCommandCode.CMD_DATA)
                    return data_out(abyte, eoi);
                return (byte)C64StatusCode.ST_TIMEOUT;
            }
            else
                return (byte)C64StatusCode.ST_TIMEOUT;
        }

        public byte OutATN(byte abyte)
        {
            received_cmd = 0;
            sec_addr = 0;	// Command is sent with secondary address
            switch ((IECATNCode)(abyte & 0xf0))
            {
                case IECATNCode.ATN_LISTEN:
                    listening = true;
                    return listen(abyte & 0x0f);

                case IECATNCode.ATN_UNLISTEN:
                    listening = false;
                    return unlisten();

                case IECATNCode.ATN_TALK:
                    listening = false;
                    return talk(abyte & 0x0f);

                case IECATNCode.ATN_UNTALK:
                    listening = false;
                    return untalk();
            }

            return (byte)C64StatusCode.ST_TIMEOUT;
        }

        public byte OutSec(byte abyte)
        {
            if (listening)
            {
                if (listener_active)
                {
                    sec_addr = (byte)(abyte & 0x0f);
                    received_cmd = (IECCommandCode)(abyte & 0xf0);
                    return sec_listen();
                }
            }
            else
            {
                if (talker_active)
                {
                    sec_addr = (byte)(abyte & 0x0f);
                    received_cmd = (IECCommandCode)(abyte & 0xf0);
                    return sec_talk();
                }
            }
            return (byte)C64StatusCode.ST_TIMEOUT;
        }

        public byte In(ref byte abyte)
        {
            if (talker_active && (received_cmd == IECCommandCode.CMD_DATA))
                return data_in(ref abyte);

            abyte = 0;
            return (byte)C64StatusCode.ST_TIMEOUT;
        }

        public void SetATN()
        {
            // Only needed for real IEC
        }

        public void RelATN()
        {
            // Only needed for real IEC
        }

        public void Turnaround()
        {
            // Only needed for real IEC
        }

        public void Release()
        {
            // Only needed for real IEC
        }

        #endregion Public methods

        #region Private Members

        byte listen(int device)
        {
            if ((device >= 8) && (device <= 11))
            {
                if ((listener = drive[device - 8]) != null && listener.Ready)
                {
                    listener_active = true;
                    return (byte)C64StatusCode.ST_OK;
                }
            }

            listener_active = false;
            return (byte)C64StatusCode.ST_NOTPRESENT;
        }

        byte talk(int device)
        {
            if ((device >= 8) && (device <= 11))
            {
                if ((talker = drive[device - 8]) != null && talker.Ready)
                {
                    talker_active = true;
                    return (byte)C64StatusCode.ST_OK;
                }
            }

            talker_active = false;
            return (byte)C64StatusCode.ST_NOTPRESENT;
        }

        byte unlisten()
        {
            listener_active = false;
            return (byte)C64StatusCode.ST_OK;
        }

        byte untalk()
        {
            talker_active = false;
            return (byte)C64StatusCode.ST_OK;
        }

        byte sec_listen()
        {
            switch (received_cmd)
            {

                case IECCommandCode.CMD_OPEN:	     // Prepare for receiving the file name
                    name_ptr = 0;
                    name_len = 0;
                    return (byte)C64StatusCode.ST_OK;

                case IECCommandCode.CMD_CLOSE: // Close channel
                    if (listener.LED != DriveLEDState.DRVLED_ERROR)
                    {
                        listener.LED = DriveLEDState.DRVLED_OFF;		// Turn off drive LED
                        UpdateLEDs();
                    }
                    return listener.Close(sec_addr);
            }
            return (byte)C64StatusCode.ST_OK;
        }

        byte sec_talk()
        {
            return (byte)C64StatusCode.ST_OK;
        }

        byte open_out(byte abyte, bool eoi)
        {
            if (name_len < NAMEBUF_LENGTH)
            {
                name_buf[name_ptr++] = abyte;
                name_len++;
            }

            if (eoi)
            {
                name_buf[name_ptr] = 0;				// End string
                listener.LED = DriveLEDState.DRVLED_ON;	// Turn on drive LED
                UpdateLEDs();
                return listener.Open(sec_addr, name_buf);
            }

            return (byte)C64StatusCode.ST_OK;
        }

        byte data_out(byte abyte, bool eoi)
        {
            return listener.Write(sec_addr, abyte, eoi);
        }

        byte data_in(ref byte abyte)
        {
            return talker.Read(sec_addr, ref abyte);
        }

        #endregion

        #region Private Fields

        C64Display the_display;

        byte[] name_buf = new byte[NAMEBUF_LENGTH];// Buffer for file names and command strings
        int name_ptr;
        int name_len;			        // Received length of file name

        Drive[] drive = new Drive[4];   // 4 drives (8..11)

        Drive listener;		            // Pointer to active listener
        Drive talker;			        // Pointer to active talker

        bool listener_active;	        // Listener selected, listener_data is valid
        bool talker_active;		        // Talker selected, talker_data is valid
        bool listening;			        // Last ATN was listen (to decide between sec_listen/sec_talk)

        IECCommandCode received_cmd;		        // Received command code ($x0)
        byte sec_addr;			        // Received secondary address ($0x)

        #endregion
    }

    // C64 status codes
    public enum C64StatusCode : byte
    {
        ST_OK = 0,				// No error
        ST_READ_TIMEOUT = 0x02,	// Timeout on reading
        ST_TIMEOUT = 0x03,		// Timeout
        ST_EOF = 0x40,			// End of file
        ST_NOTPRESENT = 0x80	// Device not present
    };


    // 1541 error codes
    public enum ErrorCode1541 : byte
    {
        ERR_OK,				// 00 OK
        ERR_WRITEERROR,		// 25 WRITE ERROR
        ERR_WRITEPROTECT,	// 26 WRITE PROTECT ON
        ERR_SYNTAX30,		// 30 SYNTAX ERROR (unknown command)
        ERR_SYNTAX33,		// 33 SYNTAX ERROR (wildcards on writing)
        ERR_WRITEFILEOPEN,	// 60 WRITE FILE OPEN
        ERR_FILENOTOPEN,	// 61 FILE NOT OPEN
        ERR_FILENOTFOUND,	// 62 FILE NOT FOUND
        ERR_ILLEGALTS,		// 67 ILLEGAL TRACK OR SECTOR
        ERR_NOCHANNEL,		// 70 NO CHANNEL
        ERR_STARTUP,		// 73 Power-up message
        ERR_NOTREADY		// 74 DRIVE NOT READY
    };


    // IEC command codes
    public enum IECCommandCode : byte
    {
        CMD_DATA = 0x60,	// Data transfer
        CMD_CLOSE = 0xe0,	// Close channel
        CMD_OPEN = 0xf0		// Open channel
    };


    // IEC ATN codes
    public enum IECATNCode : byte
    {
        ATN_LISTEN = 0x20,
        ATN_UNLISTEN = 0x30,
        ATN_TALK = 0x40,
        ATN_UNTALK = 0x50
    };


    // Drive LED states
    public enum DriveLEDState
    {
        DRVLED_OFF,		// Inactive, LED off
        DRVLED_ON,		// Active, LED on
        DRVLED_ERROR,	// Error, blink LED
        DRVLED_ERROROFF // LED blinking, currently off
    };
}
