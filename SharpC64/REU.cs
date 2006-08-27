using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class REU
    {
        #region public methods

        public REU(MOS6510 CPU)
        {
            the_cpu = CPU;

            int i;

            // Init registers
            regs[0] = 0x40;
            for (i = 1; i < 11; i++)
                regs[i] = 0;
            for (i = 11; i < 16; i++)
                regs[i] = 0xff;

            ram_size = ram_mask = 0;

            // Allocate RAM
            open_close_reu(REUSize.REU_NONE, GlobalPrefs.ThePrefs.REUSize);
        }

        public void NewPrefs(Prefs prefs)
        {
            open_close_reu(GlobalPrefs.ThePrefs.REUSize, prefs.REUSize);
        }

        public void Reset()
        {
            int i;

            for (i = 1; i < 11; i++)
                regs[i] = 0;
            for (i = 11; i < 16; i++)
                regs[i] = 0xff;

            if (ram_size > 0x20000)
                regs[0] = 0x50;
            else
                regs[0] = 0x40;
        }

        public byte ReadRegister(UInt16 adr)
        {
            if (ex_ram == null)
                return 0xff;        // TODO: Was originally rand();

            switch (adr)
            {
                case 0:
                    {
                        byte ret = regs[0];
                        regs[0] &= 0x1f;
                        return ret;
                    }
                case 6:
                    return (byte)(regs[6] | 0xf8);
                case 9:
                    return (byte)(regs[9] | 0x1f);
                case 10:
                    return (byte)(regs[10] | 0x3f);
                default:
                    return (byte)(regs[adr]);
            }
        }

        public void WriteRegister(UInt16 adr, byte abyte)
        {
            if (ex_ram == null)
                return;

            switch (adr)
            {
                case 0:		// Status register is read-only
                case 11:	// Unconnected registers
                case 12:
                case 13:
                case 14:
                case 15:
                    break;
                case 1:		// Command register
                    regs[1] = abyte;
                    if ((abyte & 0x90) == 0x90)
                        execute_dma();
                    break;
                default:
                    regs[adr] = abyte;
                    break;
            }
        }

        public void FF00Trigger()
        {
            if (ex_ram == null)
                return;

            if ((regs[1] & 0x90) == 0x80)
                execute_dma();
        }

        #endregion

        #region private methods

        void open_close_reu(REUSize old_size, REUSize new_size)
        {
            if (old_size == new_size)
                return;

            // Free old RAM
            if (old_size != REUSize.REU_NONE)
            {
                ex_ram = null;
            }

            // Allocate new RAM
            if (new_size != REUSize.REU_NONE)
            {
                switch (new_size)
                {
                    case REUSize.REU_128K:
                        ram_size = 0x20000;
                        break;
                    case REUSize.REU_256K:
                        ram_size = 0x40000;
                        break;
                    case REUSize.REU_512K:
                        ram_size = 0x80000;
                        break;
                }
                ram_mask = ram_size - 1;
                ex_ram = new byte[ram_size];

                // Set size bit in status register
                if (ram_size > 0x20000)
                    regs[0] |= 0x10;
                else
                    regs[0] &= 0xef;
            }
        }

        void execute_dma()
        {
            // Get C64 and REU transfer base addresses
            UInt16 c64_adr = (UInt16)(regs[2] | (regs[3] << 8));
            UInt32 reu_adr = (UInt32)(regs[4] | (regs[5] << 8) | (regs[6] << 16));

            // Calculate transfer length
            int length = regs[7] | (regs[8] << 8);
            if (length == 0)
                length = 0x10000;

            // Calculate address increments
            UInt16 c64_inc = (UInt16)((regs[10] & 0x80) != 0x00 ? 0 : 1);
            UInt32 reu_inc = (UInt32)((regs[10] & 0x40) != 0x00 ? 0 : 1);

            // Do transfer
            switch (regs[1] & 3)
            {

                case 0:		// C64 -> REU
                    for (; length-- > 0; c64_adr += c64_inc, reu_adr += reu_inc)
                        ex_ram[reu_adr & ram_mask] = the_cpu.REUReadByte(c64_adr);
                    break;

                case 1:		// C64 <- REU
                    for (; length-- > 0; c64_adr += c64_inc, reu_adr += reu_inc)
                        the_cpu.REUWriteByte(c64_adr, ex_ram[reu_adr & ram_mask]);
                    break;

                case 2:		// C64 <-> REU
                    for (; length-- > 0; c64_adr += c64_inc, reu_adr += reu_inc)
                    {
                        byte tmp = the_cpu.REUReadByte(c64_adr);
                        the_cpu.REUWriteByte(c64_adr, ex_ram[reu_adr & ram_mask]);
                        ex_ram[reu_adr & ram_mask] = tmp;
                    }
                    break;

                case 3:		// Compare
                    for (; length-- > 0; c64_adr += c64_inc, reu_adr += reu_inc)
                        if (ex_ram[reu_adr & ram_mask] != the_cpu.REUReadByte(c64_adr))
                        {
                            regs[0] |= 0x20;
                            break;
                        }
                    break;
            }

            // Update address and length registers if autoload is off
            if ((regs[1] & 0x20) == 0)
            {
                regs[2] = (byte)c64_adr;
                regs[3] = (byte)(c64_adr >> 8);
                regs[4] = (byte)(reu_adr);
                regs[5] = (byte)(reu_adr >> 8);
                regs[6] = (byte)(reu_adr >> 16);
                regs[7] = (byte)(length + 1);
                regs[8] = (byte)((length + 1) >> 8);
            }

            // Set complete bit in status register
            regs[0] |= 0x40;

            // Clear execute bit in command register
            regs[1] &= 0x7f;
        }

        #endregion

        #region private fields

        MOS6510 the_cpu;	            // Pointer to 6510

        byte[] ex_ram;		            // REU expansion RAM

        UInt32 ram_size;		        // Size of expansion RAM
        UInt32 ram_mask;		        // Expansion RAM address bit mask


        byte[] regs = new byte[16];		// REU registers
        #endregion
    }
}
