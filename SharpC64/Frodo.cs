using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using SdlDotNet;

namespace SharpC64
{
    public class Frodo
    {
        #region Private Constants
        string BASIC_ROM_FILE	= "Basic ROM";
        string KERNAL_ROM_FILE	= "Kernal ROM";
        string CHAR_ROM_FILE	= "Char ROM";
        string FLOPPY_ROM_FILE  = "1541 ROM";
        #endregion

        #region Public methods

        public Frodo()
        {
            _TheC64 = new C64();
            _TheC64.Initialize();
        }

        public void ReadyToRun()
        {
            load_rom_files();

            _TheC64.Run();
        }

        #endregion

        #region Public Properties

        public C64 TheC64
        {
            get { return _TheC64; }
            set { _TheC64 = value; }
        }

        #endregion

        private bool load_rom_files()
        {
            Stream file;
                       
            // Load Basic ROM
            try
            {
                using (file = new FileStream(BASIC_ROM_FILE, FileMode.Open))
                {
                    BinaryReader br = new BinaryReader(file);
                    br.Read(TheC64.Basic, 0, 0x2000);
                }
            }
            catch (IOException)
            {
                TheC64.TheDisplay.ShowRequester("Can't read 'Basic ROM'.", "Quit");
                return false;
            }

            // Load Kernal ROM
            try
            {
                using (file = new FileStream(KERNAL_ROM_FILE, FileMode.Open))
                {
                    BinaryReader br = new BinaryReader(file);
                    br.Read(TheC64.Kernal, 0, 0x2000);
                }
            }
            catch (IOException)
            {
                TheC64.TheDisplay.ShowRequester("Can't read 'Kernal ROM'.", "Quit");
                return false;
            }
                      

            // Load Char ROM
            try
            {
                using (file = new FileStream(CHAR_ROM_FILE, FileMode.Open))
                {
                    BinaryReader br = new BinaryReader(file);
                    br.Read(TheC64.Char, 0, 0x1000);
                }
            }
            catch (IOException)
            {
                TheC64.TheDisplay.ShowRequester("Can't read 'Char ROM'.", "Quit");
                return false;
            }          

            // Load 1541 ROM
            try
            {
                using (file = new FileStream(FLOPPY_ROM_FILE, FileMode.Open))
                {
                    BinaryReader br = new BinaryReader(file);
                    br.Read(TheC64.ROM1541, 0, 0x4000);
                }
            }
            catch (IOException)
            {
                TheC64.TheDisplay.ShowRequester("Can't read '1541 ROM'.", "Quit");
                return false;
            }          

            return true;
        }

        C64 _TheC64;

        public void Shutdown()
        {
            Console.Out.WriteLine("Fordo: Shutdown");

            Video.Close();
            Events.QuitApplication();
        }
    }
}
