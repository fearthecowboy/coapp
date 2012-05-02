using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scratch {
    using CoApp.Packaging.Common;

    class Program {
        
        [STAThread]
        static void Main(string[] args) {
            CanonicalName c = new CanonicalName("coapp.toolkit-1.2.0.203-any-1e373a58e25250cb");

            Console.WriteLine(c.GeneralName);
            
            Console.ReadLine();


        }


    }
}
