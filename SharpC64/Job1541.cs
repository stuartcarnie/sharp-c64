using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SharpC64
{
    public class Job1541
    {
        #region constants

        // Size of GCR encoded data
        const int GCR_SECTOR_SIZE = 1 + 10 + 9 + 1 + 325 + 8;			// SYNC Header Gap SYNC Data Gap (should be 5 SYNC bytes each)
        const int GCR_TRACK_SIZE = GCR_SECTOR_SIZE * 21;	// Each track in gcr_data has 21 sectors
        const int GCR_DISK_SIZE = GCR_TRACK_SIZE * D64Drive.NUM_TRACKS;

        // Job return codes
        const int RET_OK = 1;				// No error
        const int RET_NOT_FOUND = 2;		// Block not found
        const int RET_NOT_READY = 15;		// Drive not ready

        /*
         *  Convert 4 bytes to 5 GCR encoded bytes
        */
        static readonly UInt16[] gcr_table = {
	        0x0a, 0x0b, 0x12, 0x13, 0x0e, 0x0f, 0x16, 0x17,
	        0x09, 0x19, 0x1a, 0x1b, 0x0d, 0x1d, 0x1e, 0x15
        };

        #endregion

        #region public methods

        public Job1541(byte[] RAM)
        {
            ram = RAM;

            the_file = null;

            gcr_data = new byte[GCR_DISK_SIZE];
            gcr_ptr = gcr_track_start = 0;
            gcr_track_end = gcr_track_start + GCR_TRACK_SIZE;
            current_halftrack = 2;

            disk_changed = true;

            if (GlobalPrefs.ThePrefs.Emul1541Proc)
                open_d64_file(GlobalPrefs.ThePrefs.DrivePath[0]);
        }


        public void NewPrefs(Prefs prefs)
        {
            // 1541 emulation turned off?
            if (!prefs.Emul1541Proc)
                close_d64_file();

            // 1541 emulation turned on?
            else if (!GlobalPrefs.ThePrefs.Emul1541Proc && prefs.Emul1541Proc)
                open_d64_file(prefs.DrivePath[0]);

            // .d64 file name changed?
            else if (GlobalPrefs.ThePrefs.DrivePath[0] != prefs.DrivePath[0])
            {
                close_d64_file();
                open_d64_file(prefs.DrivePath[0]);
                disk_changed = true;
            }
        }

        public void MoveHeadOut()
        {
            if (current_halftrack == 2)
                return;
            current_halftrack--;
            gcr_ptr = gcr_track_start = (UInt32)(((current_halftrack >> 1) - 1) * GCR_TRACK_SIZE);
            gcr_track_end = (UInt32)(gcr_track_start + D64Drive.num_sectors[current_halftrack >> 1] * GCR_SECTOR_SIZE);
        }

        public void MoveHeadIn()
        {
            if (current_halftrack == D64Drive.NUM_TRACKS * 2)
                return;
            current_halftrack++;
            gcr_ptr = gcr_track_start = (UInt32)(((current_halftrack >> 1) - 1) * GCR_TRACK_SIZE);
            gcr_track_end = (UInt32)(gcr_track_start + D64Drive.num_sectors[current_halftrack >> 1] * GCR_SECTOR_SIZE);
        }

        public bool SyncFound()
        {
            if (gcr_data[gcr_ptr] == 0xff)
                return true;
            else
            {
                gcr_ptr++;		// Rotate disk
                if (gcr_ptr == gcr_track_end)
                    gcr_ptr = gcr_track_start;
                return false;
            }
        }

        public byte ReadGCRByte()
        {
            byte abyte = gcr_data[gcr_ptr++];	// Rotate disk
            if (gcr_ptr == gcr_track_end)
                gcr_ptr = gcr_track_start;
            return abyte;
        }

        public byte WPState()
        {
            if (disk_changed)
            {	// Disk change -> WP sensor strobe
                disk_changed = false;
                return (byte)(write_protected ? 0x10 : 0);
            }
            else
                return (byte)(write_protected ? 0 : 0x10);
        }

        public void WriteSector()
        {
            int track = ram[0x18];
            int sector = ram[0x19];
            UInt16 buf = (UInt16)(ram[0x30] | (ram[0x31] << 8));

            if (buf <= 0x0700)
                if (write_sector(track, sector, ram, buf))
                    sector2gcr(track, sector);
        }

        public void FormatTrack()
        {
            int track = ram[0x51];

            // Get new ID
            byte bufnum = ram[0x3d];
            id1 = ram[0x12 + bufnum];
            id2 = ram[0x13 + bufnum];

            // Create empty block
            byte[] buf = new byte[256];
            memset(buf, 1);
            buf[0] = 0x4b;

            // Write block to all sectors on track
            for (int sector = 0; sector < D64Drive.num_sectors[track]; sector++)
            {
                write_sector(track, sector, buf, 0);
                sector2gcr(track, sector);
            }

            // Clear error info (all sectors no error)
            if (track == 35)
                memset(error_info, 1);
            // Write error_info to disk?
        }

        #endregion public methods

        #region public properties

        public Job1541State State
        {
            get
            {
                Job1541State state = new Job1541State();

                state.current_halftrack = current_halftrack;
                state.gcr_ptr = gcr_ptr;
                state.write_protected = write_protected;
                state.disk_changed = disk_changed;

                return state;
            }

            set
            {
                current_halftrack = value.current_halftrack;
                gcr_ptr = value.gcr_ptr;
                gcr_track_start = (UInt32)(((current_halftrack >> 1) - 1) * GCR_TRACK_SIZE);
                gcr_track_end = (UInt32)(gcr_track_start + D64Drive.num_sectors[current_halftrack >> 1] * GCR_SECTOR_SIZE);
                write_protected = value.write_protected;
                disk_changed = value.disk_changed;
            }
        }

        #endregion public properties

        #region private members

        void open_d64_file(string filepath)
        {
            long size;
            byte[] magic = new byte[4];
            byte[] bam = new byte[256];

            // Clear GCR buffer
            memset(gcr_data, 0x55);

            // Try opening the file for reading/writing first, then for reading only
            write_protected = false;
            try
            {
                the_file = new FileStream(filepath, FileMode.Open, FileAccess.ReadWrite);
            }
            catch (UnauthorizedAccessException)
            {
                write_protected = true;
            }

            if (the_file == null)
                the_file = new FileStream(filepath, FileMode.Open, FileAccess.Read);

            size = the_file.Length;
            // Check length
            if (size < D64Drive.NUM_SECTORS * 256)
            {
                close_d64_file();
                return;
            }

            // x64 image?
            the_file.Read(magic, 0, 4);
            if (magic[0] == 0x43 && magic[1] == 0x15 && magic[2] == 0x41 && magic[3] == 0x64)
                image_header = 64;
            else
                image_header = 0;

            // Preset error info (all sectors no error)
            memset(error_info, 1);

            // Load sector error info from .d64 file, if present
            if (image_header == 0 && size == D64Drive.NUM_SECTORS * 257)
            {
                the_file.Seek(D64Drive.NUM_SECTORS * 256, SeekOrigin.Begin);
                the_file.Read(error_info, 0, D64Drive.NUM_SECTORS);
            };

            // Read BAM and get ID
            read_sector(18, 0, bam);
            id1 = bam[162];
            id2 = bam[163];

            // Create GCR encoded disk data from image
            disk2gcr();

        }

        private void memset(byte[] data, byte val)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = val;
        }

        private void memset(byte[] data, int start, byte val, int count)
        {
            for (int i = start; i < count + start; i++)
                data[i] = val;
        }

        void close_d64_file()
        {
            if (the_file != null)
            {
                the_file.Dispose();
                the_file = null;
            }
        }

        bool read_sector(int track, int sector, byte[] buffer)
        {
            int offset;

            // Convert track/sector to byte offset in file
            if ((offset = offset_from_ts(track, sector)) < 0)
                return false;

            the_file.Seek(offset + image_header, SeekOrigin.Begin);
            the_file.Read(buffer, 0, 256);
            return true;
        }

        bool write_sector(int track, int sector, byte[] buffer, int buf_ofs)
        {
            int offset;

            // Convert track/sector to byte offset in file
            if ((offset = offset_from_ts(track, sector)) < 0)
                return false;

            the_file.Seek(offset + image_header, SeekOrigin.Begin);
            the_file.Write(buffer, buf_ofs, 256);
            return true;
        }

        int secnum_from_ts(int track, int sector)
        {
            return D64Drive.sector_offset[track] + sector;
        }

        int offset_from_ts(int track, int sector)
        {
            if ((track < 1) || (track > D64Drive.NUM_TRACKS) || (sector < 0) || (sector >= D64Drive.num_sectors[track]))
                return -1;

            return (D64Drive.sector_offset[track] + sector) << 8;
        }

        void gcr_conv4(byte[] from, byte[] to, int f, int t)
        {
            UInt16 g;

            g = (UInt16)((gcr_table[from[f] >> 4] << 5) | gcr_table[from[f] & 15]);
            to[t++] = (byte)(g >> 2);
            to[t] = (byte)((g << 6) & 0xc0);
            f++;

            g = (UInt16)((gcr_table[from[f] >> 4] << 5) | gcr_table[from[f] & 15]);
            to[t++] |= (byte)((g >> 4) & 0x3f);
            to[t] = (byte)((g << 4) & 0xf0);
            f++;

            g = (UInt16)((gcr_table[from[f] >> 4] << 5) | gcr_table[from[f] & 15]);
            to[t++] |= (byte)((g >> 6) & 0x0f);
            to[t] = (byte)((g << 2) & 0xfc);
            f++;

            g = (UInt16)((gcr_table[from[f] >> 4] << 5) | gcr_table[from[f] & 15]);
            to[t++] |= (byte)((g >> 8) & 0x03);
            to[t] = (byte)g;
        }

        void sector2gcr(int track, int sector)
        {
            byte[] block = new byte[256];
            byte[] buf = new byte[4];
            int p = (track - 1) * GCR_TRACK_SIZE + sector * GCR_SECTOR_SIZE;

            read_sector(track, sector, block);

            // Create GCR header
            gcr_data[p++] = 0xff;						// SYNC
            buf[0] = 0x08;							    // Header mark
            buf[1] = (byte)(sector ^ track ^ id2 ^ id1);// Checksum
            buf[2] = (byte)(sector);
            buf[3] = (byte)(track);
            gcr_conv4(buf, gcr_data, 0, p);
            buf[0] = id2;
            buf[1] = id1;
            buf[2] = 0x0f;
            buf[3] = 0x0f;
            gcr_conv4(buf, gcr_data, 0, p + 5);
            p += 10;
            memset(gcr_data, p, 0x55, 9);			// Gap
            p += 9;

            // Create GCR data
            byte sum;
            gcr_data[p++] = 0xff;					// SYNC
            buf[0] = 0x07;							// Data mark
            sum = buf[1] = block[0];
            sum ^= buf[2] = block[1];
            sum ^= buf[3] = block[2];
            gcr_conv4(buf, gcr_data, 0, p);
            p += 5;
            for (int i = 3; i < 255; i += 4)
            {
                sum ^= buf[0] = block[i];
                sum ^= buf[1] = block[i + 1];
                sum ^= buf[2] = block[i + 2];
                sum ^= buf[3] = block[i + 3];
                gcr_conv4(buf, gcr_data, 0, p);
                p += 5;
            }
            sum ^= buf[0] = block[255];
            buf[1] = sum;							// Checksum
            buf[2] = 0;
            buf[3] = 0;
            gcr_conv4(buf, gcr_data, 0, p);
            p += 5;
            memset(gcr_data, p, 0x55, 8);           // Gap
        }

        void disk2gcr()
        {
            // Convert all tracks and sectors
            for (int track = 1; track <= D64Drive.NUM_TRACKS; track++)
                for (int sector = 0; sector < D64Drive.num_sectors[track]; sector++)
                    sector2gcr(track, sector);
        }

        #endregion

        #region private fields

        byte[] ram;				    // Pointer to 1541 RAM
        Stream the_file;		    // File stream for .d64 file
        int image_header;		    // Length of .d64/.x64 file header


        byte id1, id2;			    // ID of disk
        byte[] error_info = new byte[683];	// Sector error information (1 byte/sector)

        byte[] gcr_data;		    // Pointer to GCR encoded disk data
        UInt32 gcr_ptr;			    // Pointer to GCR data under R/W head
        UInt32 gcr_track_start;	    // Pointer to start of GCR data of current track
        UInt32 gcr_track_end;	    // Pointer to end of GCR data of current track
        int current_halftrack;	    // Current halftrack number (2..70)

        bool write_protected;	    // Flag: Disk write-protected
        bool disk_changed;		    // Flag: Disk changed (WP sensor strobe control)

        #endregion
    }
}
