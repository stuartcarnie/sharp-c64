using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Runtime.InteropServices;

namespace SharpC64
{
    public class FSDrive : Drive
    {
        public FSDrive(IEC iec, string filepath)
            : base(iec)
        {
            orig_dir_path = filepath;
            dir_path = String.Empty;

            if (change_dir(orig_dir_path))
            {
                for (int i = 0; i < 16; i++)
                    file[i] = null;

                Reset();

                Ready = true;
            }
        }

        public override byte Open(int channel, byte[] aFilename)
        {
            using (BytePtr filename = new BytePtr(aFilename))
            {
                set_error(ErrorCode1541.ERR_OK);

                // Channel 15: Execute file name as command
                if (channel == 15)
                {
                    execute_command(filename);
                    return (byte)C64StatusCode.ST_OK;
                }

                // Close previous file if still open
                if (file[channel] != null)
                {
                    file[channel].Dispose();
                    file[channel] = null;
                }

                if (filename[0] == '$')
                    unsafe
                    {
                        return open_directory(channel, filename.Pointer + 1);
                    }

                if (filename[0] == '#')
                {
                    set_error(ErrorCode1541.ERR_NOCHANNEL);
                    return (byte)C64StatusCode.ST_OK;
                }

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

            if (file[channel] != null)
            {
                file[channel].Dispose();
                file[channel] = null;
            }

            return (byte)C64StatusCode.ST_OK;
        }

        unsafe public override byte Read(int channel, ref byte abyte)
        {
            int c;

            // Channel 15: Error channel
            if (channel == 15)
            {
                abyte = *error_ptr++;

                if (abyte != (byte)'\r')
                    return (byte)C64StatusCode.ST_OK;
                else
                {	// End of message
                    set_error(ErrorCode1541.ERR_OK);
                    return (byte)C64StatusCode.ST_EOF;
                }
            }

            if (file[channel] == null) return (byte)C64StatusCode.ST_READ_TIMEOUT;

            // Read one byte
            abyte = read_char[channel];
            c = file[channel].ReadByte();
            if (c == -1)
                return (byte)C64StatusCode.ST_EOF;
            else
            {
                read_char[channel] = (byte)c;
                return (byte)C64StatusCode.ST_OK;
            }
        }

        public override byte Write(int channel, byte abyte, bool eoi)
        {
            // Channel 15: Collect chars and execute command on EOI
            if (channel == 15)
            {
                if (cmd_len >= 40)
                    return (byte)C64StatusCode.ST_TIMEOUT;

                cmd_buffer[cmd_len++] = abyte;

                if (eoi)
                {
                    cmd_buffer[cmd_len] = 0;
                    cmd_len = 0;
                    execute_command(cmd_buffer);
                }
                return (byte)C64StatusCode.ST_OK;
            }

            if (file[channel] == null)
            {
                set_error(ErrorCode1541.ERR_FILENOTOPEN);
                return (byte)C64StatusCode.ST_TIMEOUT;
            }

            if (!file[channel].CanWrite)
            {
                set_error(ErrorCode1541.ERR_WRITEERROR);
                return (byte)C64StatusCode.ST_TIMEOUT;
            }
            else
                file[channel].WriteByte(abyte);

            return (byte)C64StatusCode.ST_OK;
        }

        public override void Reset()
        {
            close_all_channels();
            cmd_len = 0;
            set_error(ErrorCode1541.ERR_STARTUP);
        }

        #region private methods

        bool change_dir(string dirpath)
        {
            if (dirpath == null) return false;

            DirectoryInfo dir = new DirectoryInfo(dirpath);

            if (dir.Exists)
            {
                if (dirpath.EndsWith("\\"))
                    dir_path = dirpath.Substring(0, dirpath.Length - 1);
                else
                    dir_path = dirpath;
                    
                dir_title = dir_path.Substring(dir_path.LastIndexOf('\\') + 1);
                return true;
            }
            else
                return false;
        }

        byte open_file(int channel, BytePtr filename)
        {
            using (BytePtr plainname = new BytePtr(256))
            {
                FileAccessMode filemode = FileAccessMode.FMODE_READ;
                FileType filetype = FileType.FTYPE_PRG;
                bool wildflag = false;
                FileMode fmode = FileMode.Open;
                FileAccess faccess = FileAccess.Read;

                unsafe
                {
                    convert_filename(filename, plainname, ref filemode, ref filetype, ref wildflag);
                }

                // Channel 0 is READ PRG, channel 1 is WRITE PRG
                if (channel == 0)
                {
                    filemode = FileAccessMode.FMODE_READ;
                    filetype = FileType.FTYPE_PRG;
                }
                else if (channel == 1)
                {
                    filemode = FileAccessMode.FMODE_WRITE;
                    filetype = FileType.FTYPE_PRG;
                }

                // Wildcards are only allowed on reading
                if (wildflag)
                {
                    if (filemode != FileAccessMode.FMODE_READ)
                    {
                        set_error(ErrorCode1541.ERR_SYNTAX33);
                        return (byte)C64StatusCode.ST_OK;
                    }

                    find_first_file(plainname);
                }

                // Select fopen() mode according to file mode
                switch (filemode)
                {
                    case FileAccessMode.FMODE_READ:
                        fmode = FileMode.Open;
                        faccess = FileAccess.Read;
                        break;
                    case FileAccessMode.FMODE_WRITE:
                        fmode = FileMode.OpenOrCreate;
                        faccess = FileAccess.ReadWrite;
                        break;
                    case FileAccessMode.FMODE_APPEND:
                        fmode = FileMode.Append;
                        faccess = FileAccess.ReadWrite;
                        break;
                }

                try
                {
                    string fullpath = Path.Combine(dir_path, plainname.ToString());

                    file[channel] = new FileStream(fullpath, fmode, faccess);

                    if (filemode == FileAccessMode.FMODE_READ)	// Read and buffer first byte
                        read_char[channel] = (byte)file[channel].ReadByte();
                    else
                        Environment.CurrentDirectory = Assembly.GetExecutingAssembly().Location;
                }
                catch (DirectoryNotFoundException)
                {
                    set_error(ErrorCode1541.ERR_NOTREADY);
                }
                catch (FileNotFoundException)
                {
                    set_error(ErrorCode1541.ERR_FILENOTFOUND);
                }
            }

            return (byte)C64StatusCode.ST_OK;
        }

        unsafe byte open_directory(int channel, byte* filename)
        {
            using (BytePtr buf = new BytePtr(Encoding.ASCII.GetBytes("\u0001\u0004\u0001\u0001\u0000\u0000\u0012\u0022                \u0022 00 2A\0")),
                pattern = new BytePtr(IEC.NAMEBUF_LENGTH))
            {
                //char str[NAMEBUF_LENGTH];
                byte* p, q;
                int i;
                FileAccessMode filemode = FileAccessMode.FMODE_READ;
                FileType filetype = FileType.FTYPE_PRG;
                bool wildflag = false;
                string str;

                // Special treatment for "$0"
                if (filename[0] == '0' && filename[1] == 0)
                    filename += 1;

                // Convert filename ('$' already stripped), filemode/type are ignored
                convert_filename(filename, pattern, ref filemode, ref filetype, ref wildflag);

                DirectoryInfo dir = new DirectoryInfo(dir_path);
                if (!dir.Exists)
                {
                    set_error(ErrorCode1541.ERR_NOTREADY);
                    return (byte)C64StatusCode.ST_OK;
                }

                FileSystemInfo[] files = dir.GetFileSystemInfos();

                file[channel] = new FileStream(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.ReadWrite);

                p = (byte*)buf.Pointer + 8;

                for (i = 0; i < 16 & i < dir_title.Length; i++)
                {
                    *p++ = conv_to_64((byte)dir_title[i], false);
                }

                file[channel].Write(buf, 0, 32);

                IEnumerator fenum = files.GetEnumerator();
                while (fenum.MoveNext() && (((FileSystemInfo)fenum.Current).Name == "." || ((FileSystemInfo)fenum.Current).Name == "..")) ;

                do
                {
                    FileSystemInfo fsi = (FileSystemInfo)fenum.Current;

                    if (match(pattern.ToString(), fsi.Name))
                    {
                        // Clear line with spaces and terminate with null byte
                        for (i = 0; i < buf.Length; i++)
                            buf[i] = (byte)' ';

                        buf[31] = 0;

                        p = buf;
                        *p++ = 0x01;	// Dummy line link
                        *p++ = 0x01;

                        if (fsi is FileInfo)
                        {
                            FileInfo fi = (FileInfo)fsi;

                            // Calculate size in blocks (254 bytes each)
                            i = (int)((fi.Length + 254) / 254);
                            *p++ = (byte)(i & 0xff);
                            *p++ = (byte)((i >> 8) & 0xff);

                            p++;
                            if (i < 10) p++;	// Less than 10: add one space
                            if (i < 100) p++;	// Less than 100: add another space

                            str = fi.Name;
                            // Convert and insert file name
                            *p++ = (byte)'\"';
                            q = p;
                            for (i = 0; i < 16 && i < str.Length; i++)
                                *q++ = conv_to_64((byte)str[i], true);
                            *q++ = (byte)'\"';
                            p += 18;
                        }
                        // File type
                        if (fsi is DirectoryInfo)
                        {
                            *p++ = (byte)'D';
                            *p++ = (byte)'I';
                            *p++ = (byte)'R';
                        }
                        else
                        {
                            *p++ = (byte)'P';
                            *p++ = (byte)'R';
                            *p++ = (byte)'G';
                        }

                        // Write line
                        file[channel].Write(buf, 0, 32);
                    }

                } while (fenum.MoveNext());
            }

            // Final line
            
            file[channel].Write(Encoding.ASCII.GetBytes("\u0001\u0001\0\0BLOCKS FREE.             \0\0\0"), 0, 32);

            file[channel].Position = 0;
            read_char[channel] = (byte)file[channel].ReadByte();

            return (byte)C64StatusCode.ST_OK;
        }

        unsafe void convert_filename(byte* srcname, byte* destname, ref FileAccessMode filemode, ref FileType filetype, ref bool wildflag)
        {
            byte* p, q;

            // Search for ':', p points to first character after ':'
            if ((p = CharFunctions.strchr(srcname, ':')) != null)
                p++;
            else
                p = srcname;

            // Convert char set of the remaining string -> destname
            q = destname;
            for (int i = 0; i < IEC.NAMEBUF_LENGTH && (*q++ = conv_from_64(*p++, true)) != 0; i++) ;

            // Look for mode parameters seperated by ','
            p = destname;
            while ((p = CharFunctions.strchr(p, ',')) != null)
            {

                // Cut string after the first ','
                *p++ = 0;
                switch ((Char)(*p))
                {
                    case 'p':
                        filetype = FileType.FTYPE_PRG;
                        break;
                    case 's':
                        filetype = FileType.FTYPE_SEQ;
                        break;
                    case 'r':
                        filemode = FileAccessMode.FMODE_READ;
                        break;
                    case 'w':
                        filemode = FileAccessMode.FMODE_WRITE;
                        break;
                    case 'a':
                        filemode = FileAccessMode.FMODE_APPEND;
                        break;
                }
            }

            // Search for wildcards
            wildflag = CharFunctions.strchr(destname, '?') != null || CharFunctions.strchr(destname, '*') != null;
        }

        void find_first_file(BytePtr aName)
        {
            DirectoryInfo dir = new DirectoryInfo(dir_path);

            if (!dir.Exists)
                return;

            FileInfo[] files = dir.GetFiles();

            string name = aName.ToString();

            IEnumerator fenum = files.GetEnumerator();
            while (fenum.MoveNext() && (((FileInfo)fenum.Current).Name == "." || ((FileInfo)fenum.Current).Name == "..")) ;

            do
            {
                FileInfo fi = (FileInfo)fenum.Current;

                // Match found? Then copy real file name
                if (match(name, fi.Name))
                {
                    CharFunctions.strncpy(aName, fi.Name);
                    return;
                }


            } while (fenum.MoveNext());
        }

        // Return true if name 'n' matches pattern 'p'
        bool match(string p, string n)
        {
            if (p.Length == 0)		// Null pattern matches everything
                return true;
            int i = 0, j = 0;
            do
            {
                if (p[i] == '*')	// Wildcard '*' matches all following characters
                    return true;
                if ((p[i] != n[j]) && (p[i] != '?'))	// Wildcard '?' matches single character
                    return false;
                i++; j++;
            } while (i < p.Length);

            return n[j] == 0xa0;
        }

        void close_all_channels()
        {
            for (int i = 0; i < 15; i++)
                Close(i);

            cmd_len = 0;
        }

        void execute_command(BytePtr command)
        {
            switch ((char)command[0])
            {
                case 'I':
                    close_all_channels();
                    set_error(ErrorCode1541.ERR_OK);
                    break;

                case 'U':
                    if ((command[1] & 0x0f) == 0x0a)
                    {
                        Reset();
                    }
                    else
                        set_error(ErrorCode1541.ERR_SYNTAX30);
                    break;

                case 'G':
                    if ((char)command[1] != ':')
                        set_error(ErrorCode1541.ERR_SYNTAX30);
                    else
                    {
                        unsafe
                        {
                            string dir = new string((sbyte*)(command.Pointer + 2));
                            chdir_cmd(dir);
                        }
                    }
                    break;

                default:
                    set_error(ErrorCode1541.ERR_SYNTAX30);
                    break;
            }
        }

        void chdir_cmd(string dirpath)
        {
            string str = String.Empty;
            int p = 0;
            close_all_channels();

            // G:. resets the directory path to its original setting
            if (dirpath[0] == '.' && dirpath[1] == 0)
            {
                change_dir(orig_dir_path);
            }
            else
            {
                // Convert directory name
                for (int i = 0; i < dirpath.Length; i++)
                    str += (char)conv_from_64((byte)dirpath[i], false);

                if (!change_dir(str))
                    set_error(ErrorCode1541.ERR_NOTREADY);
            }
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

        byte conv_to_64(byte c, bool map_slash)
        {
            if ((c >= 'A') && (c <= 'Z') || (c >= 'a') && (c <= 'z'))
                return (byte)(c ^ 0x20);

            if ((c == '\\') && map_slash && GlobalPrefs.ThePrefs.MapSlash)
                return (byte)'/';

            return c;
        }

        #endregion

        #region private fields

        string orig_dir_path;
        string dir_path;
        string dir_title;

        BytePtr cmd_buffer = new BytePtr(44);	    // Buffer for incoming command strings
        int cmd_len;			                    // Length of received command

        Stream[] file = new Stream[16];

        BytePtr read_char = new BytePtr(16);

        #endregion
    }
}
