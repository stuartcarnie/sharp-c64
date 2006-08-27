using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;

namespace SharpC64
{
    public class D64Drive : Drive, IDisposable
    {
        #region constants

        // Number of tracks/sectors
        public const int NUM_TRACKS = 35;
        public const int NUM_SECTORS = 683;

        // Number of sectors of each track
        public static readonly int[] num_sectors = {
	        0,
	        21,21,21,21,21,21,21,21,21,21,21,21,21,21,21,21,21,
	        19,19,19,19,19,19,19,
	        18,18,18,18,18,18,
	        17,17,17,17,17
        };

        // Sector offset of start of track in .d64 file
        public static readonly int[] sector_offset = {
	        0,
	        0,21,42,63,84,105,126,147,168,189,210,231,252,273,294,315,336,
	        357,376,395,414,433,452,471,
	        490,508,526,544,562,580,
	        598,615,632,649,666
        };

        #endregion

        #region public methods

        public D64Drive(IEC iec, string filepath)
            : base(iec)
        {
            the_file = null;
            ram = null;

            Ready = false;
            orig_d64_name = filepath;

            for (int i = 0; i < chan_mode.Length - 1; i++)
            {
                chan_mode[i] = ChannelMode.CHMOD_FREE;
            }
            chan_mode[15] = ChannelMode.CHMOD_COMMAND;

            // Open .d64 file
            open_close_d64_file(filepath);
            if (the_file != null)
            {

                // Allocate 1541 RAM
                ram = new BytePtr(0x800);
                unsafe
                {
                    bam = (BAM*)(ram.Pointer + 0x700);
                }
                Reset();
                Ready = true;
            }
        }

        public override byte Open(int channel, byte[] afilename)
        {
            using (BytePtr filename = new BytePtr(afilename))
            {
                set_error(ErrorCode1541.ERR_OK);

                // Channel 15: execute file name as command
                if (channel == 15)
                {
                    execute_command(filename);
                    return (byte)C64StatusCode.ST_OK;
                }

                if (chan_mode[channel] != ChannelMode.CHMOD_FREE)
                {
                    set_error(ErrorCode1541.ERR_NOCHANNEL);
                    return (byte)C64StatusCode.ST_OK;
                }

                if (filename[0] == '$')
                    if (channel != 0)
                        return open_file_ts(channel, 18, 0);
                    else
                    {
                        unsafe
                        {
                            return open_directory(filename.Pointer + 1);
                        }
                    }

                if (filename[0] == '#')
                    return open_direct(channel, filename);

                return open_file(channel, filename);
            }
        }

        public override byte Close(int channel)
        {
            if (channel == 15)
            {
                close_all_channels();
                return (byte)C64StatusCode.ST_OK;
            }

            unsafe
            {
                switch (chan_mode[channel])
                {
                    case ChannelMode.CHMOD_FREE:
                        break;

                    case ChannelMode.CHMOD_DIRECT:
                        free_buffer(chan_buf_num[channel]);
                        chan_buf[channel] = null;
                        chan_mode[channel] = ChannelMode.CHMOD_FREE;
                        break;

                    default:
                        DeallocateChannelBuffer(channel);
                        chan_mode[channel] = ChannelMode.CHMOD_FREE;
                        break;
                }
            }
            return (byte)C64StatusCode.ST_OK;
        }

        unsafe public override byte Read(int channel, ref byte abyte)
        {
            switch (chan_mode[channel])
            {
                case ChannelMode.CHMOD_COMMAND:
                    abyte = *error_ptr++;
                    if (--error_len != 0)
                        return (byte)C64StatusCode.ST_OK;
                    else
                    {
                        set_error(ErrorCode1541.ERR_OK);
                        return (byte)C64StatusCode.ST_EOF;
                    }

                case ChannelMode.CHMOD_FILE:
                    // Read next block if necessary
                    if (chan_buf[channel][0] != 0 && buf_len[channel] == 0)
                    {
                        if (!read_sector(chan_buf[channel][0], chan_buf[channel][1], chan_buf[channel]))
                            return (byte)C64StatusCode.ST_READ_TIMEOUT;
                        buf_ptr[channel] = chan_buf[channel] + 2;

                        // Determine block length
                        buf_len[channel] = chan_buf[channel][0] != 0 ? 254 : (byte)chan_buf[channel][1] - 1;
                    }

                    if (buf_len[channel] > 0)
                    {
                        abyte = *buf_ptr[channel]++;
                        if (--buf_len[channel] == 0 && chan_buf[channel][0] == 0)
                            return (byte)C64StatusCode.ST_EOF;
                        else
                            return (byte)C64StatusCode.ST_OK;
                    }
                    else
                        return (byte)C64StatusCode.ST_READ_TIMEOUT;

                case ChannelMode.CHMOD_DIRECTORY:
                case ChannelMode.CHMOD_DIRECT:
                    if (buf_len[channel] > 0)
                    {
                        abyte = *buf_ptr[channel]++;
                        if (--buf_len[channel] != 0)
                            return (byte)C64StatusCode.ST_OK;
                        else
                            return (byte)C64StatusCode.ST_EOF;
                    }
                    else
                        return (byte)C64StatusCode.ST_READ_TIMEOUT;
            }
            return (byte)C64StatusCode.ST_READ_TIMEOUT;
        }

        public override byte Write(int channel, byte abyte, bool eoi)
        {
            switch (chan_mode[channel])
            {
                case ChannelMode.CHMOD_FREE:
                    set_error(ErrorCode1541.ERR_FILENOTOPEN);
                    break;

                case ChannelMode.CHMOD_COMMAND:
                    // Collect characters and execute command on EOI
                    if (cmd_len >= 40)
                        return (byte)C64StatusCode.ST_TIMEOUT;

                    cmd_buffer[cmd_len++] = abyte;

                    if (eoi)
                    {
                        cmd_buffer[cmd_len++] = 0;
                        cmd_len = 0;
                        execute_command(cmd_buffer);
                    }
                    return (byte)C64StatusCode.ST_OK;

                case ChannelMode.CHMOD_DIRECTORY:
                    set_error(ErrorCode1541.ERR_WRITEFILEOPEN);
                    break;
            }
            return (byte)C64StatusCode.ST_TIMEOUT;
        }

        public override void Reset()
        {
            close_all_channels();

            unsafe
            {
                read_sector(18, 0, (byte*)bam);
            }

            cmd_len = 0;
            for (int i = 0; i < buf_free.Length; i++)
                buf_free[i] = true;

            set_error(ErrorCode1541.ERR_STARTUP);
        }

        #endregion

        #region private methods

        void open_close_d64_file(string d64name)
        {
            long size;
            byte[] magic = new byte[4];

            // Close old .d64, if open
            if (the_file != null)
            {
                close_all_channels();
                the_file.Dispose();
                the_file = null;
            }

            // Open new .d64 file
            if (d64name.Length != 0)
            {
                the_file = new FileStream(d64name, FileMode.Open, FileAccess.Read);

                // Check length
                size = the_file.Length;
                // Check length
                if (size < NUM_SECTORS * 256)
                {
                    the_file.Dispose();
                    the_file = null;
                    return;
                }

                // x64 image?
                the_file.Read(magic, 0, 4);
                if (magic[0] == 0x43 && magic[1] == 0x15 && magic[2] == 0x41 && magic[3] == 0x64)
                    image_header = 64;
                else
                    image_header = 0;

                // Preset error info (all sectors no error)
                Array.Clear(error_info, 0, error_info.Length);

                // Load sector error info from .d64 file, if present
                if (image_header == 0 && size == NUM_SECTORS * 257)
                {
                    the_file.Seek(NUM_SECTORS * 256, SeekOrigin.Begin);
                    the_file.Read(error_info, 0, NUM_SECTORS);
                }
            }
        }

        unsafe byte open_file(int channel, BytePtr filename)
        {
            using (BytePtr plainname = new BytePtr(256))
            {

                FileAccessMode filemode = FileAccessMode.FMODE_READ;
                FileType filetype = FileType.FTYPE_PRG;
                int track = -1, sector = -1;

                convert_filename(filename, plainname, ref filemode, ref filetype);

                // Channel 0 is READ PRG, channel 1 is WRITE PRG
                if (channel == 0)
                {
                    filemode = FileAccessMode.FMODE_READ;
                    filetype = FileType.FTYPE_PRG;
                }
                if (channel == 1)
                {
                    filemode = FileAccessMode.FMODE_WRITE;
                    filetype = FileType.FTYPE_PRG;
                }

                // Allow only read accesses
                if (filemode != FileAccessMode.FMODE_READ)
                {
                    set_error(ErrorCode1541.ERR_WRITEPROTECT);
                    return (byte)C64StatusCode.ST_OK;
                }

                // Find file in directory and open it
                if (find_file(plainname, ref track, ref sector))
                    return open_file_ts(channel, track, sector);
                else
                    set_error(ErrorCode1541.ERR_FILENOTFOUND);

                return (byte)C64StatusCode.ST_OK;
            }
        }

        unsafe void convert_filename(BytePtr srcname, BytePtr destname, ref FileAccessMode filemode, ref FileType filetype)
        {
            byte* p;

            // Search for ':', p points to first character after ':'
            if ((p = CharFunctions.strchr(srcname, ':')) != null)
                p++;
            else
                p = srcname;

            // Remaining string -> destname
            CharFunctions.strncpy(destname, srcname, p);

            // Look for mode parameters seperated by ','
            p = destname;
            while ((p = CharFunctions.strchr(p, ',')) != null)
            {

                // Cut string after the first ','
                *p++ = 0;
                switch ((Char)(*p))
                {
                    case 'P':
                        filetype = FileType.FTYPE_PRG;
                        break;
                    case 'S':
                        filetype = FileType.FTYPE_SEQ;
                        break;
                    case 'U':
                        filetype = FileType.FTYPE_USR;
                        break;
                    case 'L':
                        filetype = FileType.FTYPE_REL;
                        break;
                    case 'R':
                        filemode = FileAccessMode.FMODE_READ;
                        break;
                    case 'W':
                        filemode = FileAccessMode.FMODE_WRITE;
                        break;
                    case 'A':
                        filemode = FileAccessMode.FMODE_APPEND;
                        break;
                }
            }
        }

        unsafe bool find_file(BytePtr filename, ref int track, ref int sector)
        {
            int i, j;
            byte* p, q;
            DirEntry* de;

            fixed (Directory* dd = &dir)
            {
                // Scan all directory blocks
                dir.next_track = bam->dir_track;
                dir.next_sector = bam->dir_sector;

                while (dir.next_track != 0)
                {
                    if (!read_sector(dir.next_track, dir.next_sector, &dd->next_track))
                        return false;

                    DirEntry* ade = (DirEntry*)dd->entry;
                    // Scan all 8 entries of a block
                    for (j = 0; j < 8; j++)
                    {
                        de = &ade[j];
                        track = de->track;
                        sector = de->sector;

                        if (de->type != 0)
                        {
                            p = (byte*)filename;
                            q = de->name;
                            for (i = 0; i < 16 && (*p != 0); i++, p++, q++)
                            {
                                if (*p == '*')	// Wildcard '*' matches all following characters
                                    return true;
                                if (*p != *q)
                                {
                                    if (*p != '?') goto next_entry;	// Wildcard '?' matches single character
                                    if (*q == 0xa0) goto next_entry;
                                }
                            }

                            if (i == 16 || *q == 0xa0)
                                return true;
                        }
                    next_entry: ;
                    }
                }
            }
            return false;
        }

        unsafe byte open_file_ts(int channel, int track, int sector)
        {
            AllocateChannelBuffer(channel, 256);
            chan_mode[channel] = ChannelMode.CHMOD_FILE;

            // On the next call to Read, the first block will be read
            chan_buf[channel][0] = (byte)track;
            chan_buf[channel][1] = (byte)sector;
            buf_len[channel] = 0;

            return (byte)C64StatusCode.ST_OK;
        }

        /*
         *  Prepare directory as BASIC program (channel 0)
         */

        static readonly byte[] type_char_1 = Encoding.ASCII.GetBytes("DSPUREERSELQGRL?");
        static readonly byte[] type_char_2 = Encoding.ASCII.GetBytes("EERSELQGRL??????");
        static readonly byte[] type_char_3 = Encoding.ASCII.GetBytes("LQGRL???????????");

        // Return true if name 'n' matches pattern 'p'
        unsafe static bool match(byte* p, byte* n)
        {
            if (p[0] == 0x00)		// Null pattern matches everything
                return true;

            do
            {
                if (*p == '*')	// Wildcard '*' matches all following characters
                    return true;
                if ((*p != *n) && (*p != '?'))	// Wildcard '?' matches single character
                    return false;
                p++; n++;
            } while (*p != 0x00);

            return *n == 0xa0;

        }


        unsafe byte open_directory(byte* pattern)
        {
            int i, j, n, m;
            byte* p, q;
            DirEntry* de;
            byte c;
            byte* tmppat;

            // Special treatment for "$0"
            if (pattern[0] == '0' && pattern[1] == 0)
                pattern += 1;

            // Skip everything before the ':' in the pattern
            if ((tmppat = CharFunctions.strchr(pattern, ':')) != null)
                pattern = tmppat + 1;

            AllocateChannelBuffer(0, 8192);

            p = buf_ptr[0] = chan_buf[0];

            chan_mode[0] = ChannelMode.CHMOD_DIRECTORY;

            // Create directory title
            *p++ = 0x01;	// Load address $0401 (from PET days :-)
            *p++ = 0x04;
            *p++ = 0x01;	// Dummy line link
            *p++ = 0x01;
            *p++ = 0;		// Drive number (0) as line number
            *p++ = 0;
            *p++ = 0x12;	// RVS ON
            *p++ = (byte)'\"';

            q = bam->disk_name;
            for (i = 0; i < 23; i++)
            {
                if ((c = *q++) == 0xa0)
                    *p++ = (byte)' ';		// Replace 0xa0 by space
                else
                    *p++ = c;
            }
            *(p - 7) = (byte)'\"';
            *p++ = 0;

            // Scan all directory blocks
            dir.next_track = bam->dir_track;
            dir.next_sector = bam->dir_sector;

            fixed (Directory* dd = &dir)
            {

                while (dir.next_track != 0x00)
                {
                    if (!read_sector(dir.next_track, dir.next_sector, &dd->next_track))
                        return (byte)C64StatusCode.ST_OK;

                    DirEntry* ade = (DirEntry*)dd->entry;
                    // Scan all 8 entries of a block
                    for (j = 0; j < 8; j++)
                    {
                        de = &ade[j];

                        if (de->type != 0 && match(pattern, de->name))
                        {
                            *p++ = 0x01; // Dummy line link
                            *p++ = 0x01;

                            *p++ = de->num_blocks_l; // Line number
                            *p++ = de->num_blocks_h;

                            *p++ = (byte)' ';
                            n = (de->num_blocks_h << 8) + de->num_blocks_l;
                            if (n < 10) *p++ = (byte)' ';
                            if (n < 100) *p++ = (byte)' ';

                            *p++ = (byte)'\"';
                            q = de->name;
                            m = 0;
                            for (i = 0; i < 16; i++)
                            {
                                if ((c = *q++) == 0xa0)
                                {
                                    if (m != 0)
                                        *p++ = (byte)' ';		// Replace all 0xa0 by spaces
                                    else
                                        m = *p++ = (byte)'\"';	// But the first by a '"'
                                }
                                else
                                    *p++ = c;
                            }
                            if (m != 0)
                                *p++ = (byte)' ';
                            else
                                *p++ = (byte)'\"';			// No 0xa0, then append a space

                            if ((de->type & 0x80) != 0)
                                *p++ = (byte)' ';
                            else
                                *p++ = (byte)'*';

                            *p++ = type_char_1[de->type & 0x0f];
                            *p++ = type_char_2[de->type & 0x0f];
                            *p++ = type_char_3[de->type & 0x0f];

                            if ((de->type & 0x40) != 0)
                                *p++ = (byte)'<';
                            else
                                *p++ = (byte)' ';

                            *p++ = (byte)' ';
                            if (n >= 10) *p++ = (byte)' ';
                            if (n >= 100) *p++ = (byte)' ';
                            *p++ = 0;
                        }
                    }
                }

            }
            // Final line
            q = p;
            for (i = 0; i < 29; i++)
                *q++ = (byte)' ';

            n = 0;
            for (i = 0; i < 35; i++)
                n += bam->bitmap[i * 4];

            *p++ = 0x01;		// Dummy line link
            *p++ = 0x01;
            *p++ = (byte)(n & 0xff);	// Number of free blocks as line number
            *p++ = (byte)((n >> 8) & 0xff);

            *p++ = (byte)'B';
            *p++ = (byte)'L';
            *p++ = (byte)'O';
            *p++ = (byte)'C';
            *p++ = (byte)'K';
            *p++ = (byte)'S';
            *p++ = (byte)' ';
            *p++ = (byte)'F';
            *p++ = (byte)'R';
            *p++ = (byte)'E';
            *p++ = (byte)'E';
            *p++ = (byte)'.';

            p = q;
            *p++ = 0;
            *p++ = 0;
            *p++ = 0;

            buf_len[0] = (int)(p - chan_buf[0]);

            return (byte)C64StatusCode.ST_OK;
        }

        unsafe private void AllocateChannelBuffer(int channel, int size)
        {
            chan_buf_alloc[channel] = new BytePtr(size);
            chan_buf[channel] = chan_buf_alloc[channel];
        }

        unsafe private void DeallocateChannelBuffer(int channel)
        {
            chan_buf_alloc[channel].Dispose();
            chan_buf[channel] = null;
        }

        byte open_direct(int channel, BytePtr filename)
        {
            int buf = -1;

            if (filename[1] == 0)
                buf = alloc_buffer(-1);
            else
                if ((filename[1] >= '0') && (filename[1] <= '3') && (filename[2] == 0))
                    buf = alloc_buffer(filename[1] - '0');

            if (buf == -1)
            {
                set_error(ErrorCode1541.ERR_NOCHANNEL);
                return (byte)C64StatusCode.ST_OK;
            }

            unsafe
            {
                // The buffers are in the 1541 RAM at $300 and are 256 bytes each
                chan_buf[channel] = buf_ptr[channel] = (byte*)ram + 0x300 + (buf << 8);
                chan_mode[channel] = ChannelMode.CHMOD_DIRECT;
                chan_buf_num[channel] = buf;

                // Store actual buffer number in buffer
                *chan_buf[channel] = (byte)(buf + '0');
                buf_len[channel] = 1;
            }

            return (byte)C64StatusCode.ST_OK;
        }

        void close_all_channels()
        {
            for (int i = 0; i < 15; i++)
                Close(i);

            cmd_len = 0;
        }

        unsafe void execute_command(BytePtr command)
        {
            UInt16 adr;
            int len;

            switch ((char)command[0])
            {
                case 'B':
                    if (command[1] != '-')
                        set_error(ErrorCode1541.ERR_SYNTAX30);
                    else
                        switch ((char)command[2])
                        {
                            case 'R':
                                block_read_cmd(command.Pointer + 3);
                                break;

                            case 'P':
                                buffer_ptr_cmd(command.Pointer + 3);
                                break;

                            case 'A':
                            case 'F':
                            case 'W':
                                set_error(ErrorCode1541.ERR_WRITEPROTECT);
                                break;

                            default:
                                set_error(ErrorCode1541.ERR_SYNTAX30);
                                break;
                        }
                    break;

                case 'M':
                    if (command[1] != '-')
                        set_error(ErrorCode1541.ERR_SYNTAX30);
                    else
                        switch ((char)command[2])
                        {
                            case 'R':
                                adr = (UInt16)(((byte)command[4] << 8) | ((byte)command[3]));
                                error_ptr = (byte*)((byte*)ram + (adr & 0x07ff));
                                if ((error_len = (byte)command[5]) == 0)
                                    error_len = 1;
                                break;

                            case 'W':
                                adr = (UInt16)(((byte)command[4] << 8) | ((byte)command[3]));
                                len = (byte)command[5];
                                for (int i = 0; i < len; i++)
                                    ram[adr + i] = (byte)command[i + 6];
                                break;

                            default:
                                set_error(ErrorCode1541.ERR_SYNTAX30);
                                break;
                        }
                    break;

                case 'I':
                    close_all_channels();
                    read_sector(18, 0, (byte*)bam);
                    set_error(ErrorCode1541.ERR_OK);
                    break;

                case 'U':
                    switch (command[1] & 0x0f)
                    {
                        case 1:		// U1/UA: Block-Read
                            block_read_cmd(command.Pointer + 2);
                            break;

                        case 2:		// U2/UB: Block-Write
                            set_error(ErrorCode1541.ERR_WRITEPROTECT);
                            break;

                        case 10:	// U:/UJ: Reset
                            Reset();
                            break;

                        default:
                            set_error(ErrorCode1541.ERR_SYNTAX30);
                            break;
                    }
                    break;

                case 'G':
                    if (command[1] != ':')
                        set_error(ErrorCode1541.ERR_SYNTAX30);
                    else
                        chd64_cmd(command.Pointer + 2);
                    break;

                case 'C':
                case 'N':
                case 'R':
                case 'S':
                case 'V':
                    set_error(ErrorCode1541.ERR_WRITEPROTECT);
                    break;

                default:
                    set_error(ErrorCode1541.ERR_SYNTAX30);
                    break;
            }
        }

        unsafe void block_read_cmd(byte* command)
        {
            int channel = 0, drvnum = 0, track = 0, sector = 0;

            if (parse_bcmd(command, ref channel, ref drvnum, ref track, ref sector))
            {
                if (chan_mode[channel] == ChannelMode.CHMOD_DIRECT)
                {
                    read_sector(track, sector, buf_ptr[channel] = chan_buf[channel]);
                    buf_len[channel] = 256;
                    set_error(ErrorCode1541.ERR_OK);
                }
                else
                    set_error(ErrorCode1541.ERR_NOCHANNEL);
            }
            else
                set_error(ErrorCode1541.ERR_SYNTAX30);
        }

        unsafe void buffer_ptr_cmd(byte* command)
        {
            int channel = 0, pointer = 0, i = 0;

            if (parse_bcmd(command, ref channel, ref pointer, ref i, ref i))
            {
                if (chan_mode[channel] == ChannelMode.CHMOD_DIRECT)
                {
                    buf_ptr[channel] = chan_buf[channel] + pointer;
                    buf_len[channel] = 256 - pointer;
                    set_error(ErrorCode1541.ERR_OK);
                }
                else
                    set_error(ErrorCode1541.ERR_NOCHANNEL);
            }
            else
                set_error(ErrorCode1541.ERR_SYNTAX30);
        }

        unsafe bool parse_bcmd(byte* cmd, ref int arg1, ref int arg2, ref int arg3, ref int arg4)
        {
            int i;

            if (*cmd == ':') cmd++;

            // Read four parameters separated by space, cursor right or comma
            while (*cmd == ' ' || *cmd == 0x1d || *cmd == 0x2c) cmd++;
            if (*cmd == 0) return false;

            i = 0;
            while (*cmd >= 0x30 && *cmd < 0x40)
            {
                i *= 10;
                i += *cmd++ & 0x0f;
            }
            arg1 = i & 0xff;

            while (*cmd == ' ' || *cmd == 0x1d || *cmd == 0x2c) cmd++;
            if (*cmd == 0) return false;

            i = 0;
            while (*cmd >= 0x30 && *cmd < 0x40)
            {
                i *= 10;
                i += *cmd++ & 0x0f;
            }
            arg2 = i & 0xff;

            while (*cmd == ' ' || *cmd == 0x1d || *cmd == 0x2c) cmd++;
            if (*cmd == 0) return false;

            i = 0;
            while (*cmd >= 0x30 && *cmd < 0x40)
            {
                i *= 10;
                i += *cmd++ & 0x0f;
            }
            arg3 = i & 0xff;

            while (*cmd == ' ' || *cmd == 0x1d || *cmd == 0x2c) cmd++;
            if (*cmd == 0) return false;

            i = 0;
            while (*cmd >= 0x30 && *cmd < 0x40)
            {
                i *= 10;
                i += *cmd++ & 0x0f;
            }
            arg4 = i & 0xff;

            return true;
        }

        unsafe void chd64_cmd(byte* d64name)
        {
            using (BytePtr str = new BytePtr(IEC.NAMEBUF_LENGTH))
            {
                byte* p = str;

                // Convert .d64 file name
                for (int i = 0; i < IEC.NAMEBUF_LENGTH && (*p++ = conv_from_64(*d64name++, false)) != 0; i++) ;

                close_all_channels();

                // G:. resets the .d64 file name to its original setting
                if (str[0] == '.' && str[1] == 0)
                    open_close_d64_file(orig_d64_name);
                else
                    open_close_d64_file(str.ToString());

                // Read BAM
                read_sector(18, 0, (byte*)bam);
            }
        }

        int alloc_buffer(int want)
        {
            if (want == -1)
            {
                for (want = 3; want >= 0; want--)
                    if (buf_free[want])
                    {
                        buf_free[want] = false;
                        return want;
                    }
                return -1;
            }

            if (want < 4)
                if (buf_free[want])
                {
                    buf_free[want] = false;
                    return want;
                }
                else
                    return -1;
            else
                return -1;
        }

        void free_buffer(int buf)
        {
            buf_free[buf] = true;
        }

        unsafe bool read_sector(int track, int sector, byte* buffer)
        {
            int offset;

            // Convert track/sector to byte offset in file
            if ((offset = offset_from_ts(track, sector)) < 0)
            {
                set_error(ErrorCode1541.ERR_ILLEGALTS);
                return false;
            }

            if (the_file == null)
            {
                set_error(ErrorCode1541.ERR_NOTREADY);
                return false;
            }

            the_file.Seek(offset + image_header, SeekOrigin.Begin);
            byte[] tmp = new byte[256];
            the_file.Read(tmp, 0, 256);
            Marshal.Copy(tmp, 0, (IntPtr)buffer, 256);
            return true;
        }

        int offset_from_ts(int track, int sector)
        {
            if ((track < 1) || (track > NUM_TRACKS) || (sector < 0) || (sector >= num_sectors[track]))
                return -1;

            return (sector_offset[track] + sector) << 8;
        }

        byte conv_from_64(byte c, bool map_slash)
        {
            if ((c >= 'A') && (c <= 'Z') || (c >= 'a') && (c <= 'z'))
                return (byte)(c ^ 0x20);

            if ((c >= 0xc1) && (c <= 0xda))
                return (byte)(c ^ 0x80);

            if ((c == '/') && map_slash && GlobalPrefs.ThePrefs.MapSlash)
                return (byte)'\\';

            return c;
        }

        #endregion

        #region private fields

        string orig_d64_name;                       // Original path of .d64 file

        Stream the_file;			                // File pointer for .d64 file

        BytePtr ram;				                // 2KB 1541 RAM

        unsafe BAM* bam;				            // Pointer to BAM
        Directory dir;			                    // Buffer for directory blocks

        ChannelMode[] chan_mode = new ChannelMode[16];		// Channel mode
        int[] chan_buf_num = new int[16];	        // Buffer number of channel (for direct access channels)
        unsafe byte*[] chan_buf = new byte*[16];	        // Pointer to buffer
        BytePtr[] chan_buf_alloc = new BytePtr[16];

        unsafe byte*[] buf_ptr = new byte*[16];		// Pointer in buffer
        int[] buf_len = new int[16];		        // Remaining bytes in buffer

        bool[] buf_free = new bool[4];		        // Buffer 0..3 free?

        BytePtr cmd_buffer = new BytePtr(44);	    // Buffer for incoming command strings
        int cmd_len;			                    // Length of received command

        int image_header;		                    // Length of .d64 file header

        byte[] error_info = new byte[683];	        // Sector error information (1 byte/sector)

        #endregion

        #region IDisposable Members

        bool disposed = false;

        ~D64Drive()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                }

                disposed = true;
            }
        }

        #endregion
    }

    // BAM structure
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct BAM
    {
        public byte dir_track;		// Track...
        public byte dir_sector;		// ...and sector of first directory block
        public sbyte fmt_type;		// Format type
        public sbyte pad0;
        public fixed byte bitmap[4 * 35];	// Sector allocation
        public fixed byte disk_name[18];	// Disk name
        public fixed byte id[2];			// Disk ID
        public sbyte pad1;
        public fixed byte fmt_char[2];	// Format characters
        public fixed sbyte pad2[4];
        public fixed sbyte pad3[85];
    };

    // Directory entry structure
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct DirEntry
    {
        public byte type;			// File type
        public byte track;			// Track...
        public byte sector;			// ...and sector of first data block
        public fixed byte name[16];		// File name
        public byte side_track;		// Track...
        public byte side_sector;	// ...and sector of first side sector
        public byte rec_len;		// Record length
        public fixed sbyte pad0[4];
        public byte ovr_track;		// Track...
        public byte ovr_sector;		// ...and sector on overwrite
        public byte num_blocks_l;	// Number of blocks, LSB
        public byte num_blocks_h;	// Number of blocks, MSB
        public fixed sbyte pad1[2];
    } ;

    // Directory block structure
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct Directory
    {
        public fixed byte padding[2];		// Keep DirEntry word-aligned
        public byte next_track;
        public byte next_sector;
        public fixed byte entry[8 * 32];   // array of 8 DirEntry structs (sizeof(DirEntry) = 32)
    } ;

    // Channel modes (IRC users listen up :-)
    enum ChannelMode
    {
        CHMOD_FREE,			// Channel free
        CHMOD_COMMAND,		// Command/error channel
        CHMOD_DIRECTORY,	// Reading directory
        CHMOD_FILE,			// Sequential file open
        CHMOD_DIRECT		// Direct buffer access ('#')
    };

    // Access modes
    enum FileAccessMode
    {
        FMODE_READ, FMODE_WRITE, FMODE_APPEND
    };

    // File types
    enum FileType
    {
        FTYPE_PRG, FTYPE_SEQ, FTYPE_USR, FTYPE_REL
    };
}
