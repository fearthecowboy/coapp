using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scratch {
    
    class Program {
        
        [STAThread]
        static void Main(string[] args) {

            var _methods = typeof(Program).GetMethods();

            foreach (var method in _methods) {
                var parameters = method.GetParameters();
                Console.WriteLine("\r\nMethod: {0}", method.Name);

                foreach (var parameter in parameters) {
                    var name = parameter.Name;
                    var type = parameter.ParameterType;
                    var t = type;

                    Console.Write("   {0} => {1} Primitive:{2} Generic:{3}", name, type.Name, type.IsPrimitive, type.IsGenericType);
                    if (t.IsAssignableFrom(typeof(string)) || typeof(string).IsAssignableFrom(t)) {
                        Console.Write(" is stringy!");
                    }

                    

                    if (type.IsGenericType) {

                        //Console.Write(" Generic Type [{0}]" , type.FullName);
                        var genargs = type.GetGenericArguments();
                        switch (genargs.Length) {
                            case 1:
                                Console.Write(" GenericArg: {0}", genargs[0].Name);
                                Console.Write(" ValueType: {0}", genargs[0].IsValueType);
                                Console.Write(" Primitive: {0}", genargs[0].IsPrimitive);
                                break;

                            case 2:
                                Console.Write(" GenericArg: {0}, {1}", genargs[0].Name, genargs[1].Name);
                                break;

                            default:
                                Console.WriteLine("=== TOO MANY ARGS ===");
                                continue;
                        }


                    }
                    //type.IsPrimitive
                }

            }


            Console.ReadLine();


            /*
            Thread.Sleep(3000);
            var appDomain = AppDomain.CreateDomain("tmp" + DateTime.Now.Ticks);

             appDomain.CreateInstanceFromAndUnwrap(Path.Combine(Environment.CurrentDirectory, "CoApp.Client.dll"), "CoApp.Toolkit.Engine.Client.Installer", false, BindingFlags.Default, null, args, null, null);
            */

            //  FilesystemExtensions.RemoveTemporaryFiles();

            //appDomain.CreateInstanceAndUnwrap("CoApp.Client, Version=1.2.0.94, Culture=neutral, PublicKeyToken=1e373a58e25250cb",
            //     "CoApp.Toolkit.Engine.Client.Installer", false, BindingFlags.Default, null, new[] { args[0] }, null, null);
#if FALSE

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
            
#endif
        }


        public void Method1(string arg) {
        }

        public void Method2(int arg1, int? arg2) {

        }

        public void Method3(IEnumerable<string> args) {
        }

        public void Method4(string[] args) {
        }

        public void Method5(Dictionary<string, string> args) {
        }
    }
}
