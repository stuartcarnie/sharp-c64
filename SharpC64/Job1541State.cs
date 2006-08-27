using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class Job1541State
    {
        public int current_halftrack;
        public UInt32 gcr_ptr;
        public bool write_protected;
        public bool disk_changed;
    }
}
