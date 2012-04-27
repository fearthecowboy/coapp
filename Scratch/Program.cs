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


        }


    }
}
