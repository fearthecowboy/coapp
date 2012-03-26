using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scratch {
    using System.IO;
    using System.Reflection;
    using System.Threading;

    class Program {

        
        [STAThread]
        static void Main(string[] args) {
            Thread.Sleep(3000);
            var appDomain = AppDomain.CreateDomain("tmp" + DateTime.Now.Ticks);
            appDomain.CreateInstanceFromAndUnwrap(Path.Combine(Environment.CurrentDirectory, "CoApp.Client.dll"), "CoApp.Toolkit.Engine.Client.Installer", false, BindingFlags.Default, null, args, null, null);
        }
    }
}
