//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------


using System.ComponentModel;
using System.IO;

namespace CoApp.Toolkit.Console {
    using System;
    using System.Collections.Generic;
    using System.Resources;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Linq;
    using Exceptions;
    using Extensions;
    using Tasks;
    using OperationCanceledException = System.OperationCanceledException;

    public abstract class AsyncConsoleProgram {
        protected abstract ResourceManager Res { get; }
        protected int Counter = 0;

        protected abstract int Main(IEnumerable<string> args);
        protected CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

        protected virtual int Startup(IEnumerable<string> args) {
            var task = Task.Factory.StartNew(() => {

                Main(args);
            }, CancellationTokenSource.Token);

            try {
                Console.CancelKeyPress += (x, y) => {
                    if (!CancellationTokenSource.IsCancellationRequested) {
                        Console.WriteLine("Operation Canceled...");
                        CancellationTokenSource.Cancel();
                        if (y.SpecialKey == ConsoleSpecialKey.ControlBreak) {
                            // can't cancel so we just block on the task.
                            return;
                            // task.Wait();
                        }
                    }
                    y.Cancel = true;

                };
                task.Wait(CancellationTokenSource.Token);
            } catch( Exception e ) {
                 HandleException(e.Unwrap());
                 return 1;
            }
            FilesystemExtensions.RemoveTemporaryFiles();
            return 0;
        }

        bool HandleException(Exception ex ) {
            if (ex is ConsoleException) {
                Fail(ex.Message);
                return true;
            }

            if( ex is OperationCompletedBeforeResultException) {
                // assumably, this has been actually handled elsewhere.. right?
                return true;
            }

            if (ex is TaskCanceledException) {
                // assumably, this has been actually handled elsewhere.. right?
                return true;
            }

            if (ex is OperationCanceledException) {
                // this has been handled before.
                return true;
            }

            Fail("Unexpected Exception: {0} {1}\r\n{2}", ex.GetType() ,ex.Message, ex.StackTrace);
            return false;
        }

        

        #region fail/help/logo

        /// <summary>
        ///   Displays a failure message.
        /// </summary>
        /// <param name = "text">
        ///   The text format string.
        /// </param>
        /// <param name = "par">
        ///   The parameters for the formatted string.
        /// </param>
        /// <returns>
        ///   returns 1 (usually passed out as the process end code)
        /// </returns>
        protected int Fail(string text, params object[] par) {
            Logo();
            using (new ConsoleColors(ConsoleColor.Red, ConsoleColor.Black)) {
                Console.WriteLine("Error: {0}", text.format(par));
            }
            CancellationTokenSource.Cancel();
            return 1;
        }

        /// <summary>
        ///   Displays the program help.
        /// </summary>
        /// <returns>
        ///   returns 0.
        /// </returns>
        protected int Help() {
            Logo();
            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                Res.GetString("HelpText").Print();
            }

            return 0;
        }

        /// <summary>
        ///   Displays the program logo.
        /// </summary>
        protected void Logo() {
            using (new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.Black)) {
                this.Assembly().Logo().Print();
            }

            this.Assembly().SetLogo(string.Empty);
        }

        #endregion
    }
}