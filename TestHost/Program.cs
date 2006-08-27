using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using SharpC64;

namespace TestHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Frodo frodo = new Frodo();
            frodo.ReadyToRun();
        }
    }
}
