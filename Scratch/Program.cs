using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scratch {
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using CoApp.Toolkit.Extensions;

    class Program {
        
        [STAThread]
        static void Main(string[] args) {
#if FALSE
            Thread.Sleep(3000);
            var appDomain = AppDomain.CreateDomain("tmp" + DateTime.Now.Ticks);
            appDomain.CreateInstanceFromAndUnwrap(Path.Combine(Environment.CurrentDirectory, "CoApp.Client.dll"), "CoApp.Toolkit.Engine.Client.Installer", false, BindingFlags.Default, null, args, null, null);
#endif 
            
            var t = Task.Factory.StartNew(
                () => {
                    Console.WriteLine("Task 1");
                    return 100;
                });

            var q = t.Continue(
                result => {
                    Console.WriteLine("Result from previous task: {0}", result);
                });

            var s = q.Continue(
                () => {
                    Console.WriteLine("Gonna throw an error here.");
                    throw new Exception("Crap Happens.");
                    return 200;
                });
            
#if FALSE
            var onfail = s.OnFail(
                (exception) => {
                    // this is called when the antecedent task throws an exception
                });

            var oncan = s.OnCanceled(
                () => {
                    // this is called when the antecedent task is cancelled either by token, or by not being called.
                });

#endif

            var u = s.Continue(
                (result) => {
                    Console.WriteLine("Shouldn't see this: {0}.", result);
                });

            var sfails = s.ContinueOnFail(
                ex => {
                    Console.WriteLine("Happens when you fail: {0}\r\n{1}.", ex.Unwrap().Message, ex.Unwrap().StackTrace);
                });

            var v = u.Continue(
                () => {
                    Console.WriteLine("Shouldn't see this either.");
                });

            try {
                v.Wait();
            } catch( Exception e) {
                e = e.Unwrap();
                Console.WriteLine("{0}\r\n{1}",e.Message, e.StackTrace);
            }
            
        }
    }
}
