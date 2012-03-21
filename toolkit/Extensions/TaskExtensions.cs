using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoApp.Toolkit.Exceptions;

namespace CoApp.Toolkit.Extensions {
    public static class TaskExtensions {
        public static void ThrowOnFaultOrCancel(this Task antecedent) {
            if( !antecedent.IsCompleted ) {
                antecedent.Wait();
            }

            if (antecedent.IsCanceled) {
                throw new OperationCompletedBeforeResultException();
            }

            if (antecedent.IsFaulted) {
                throw antecedent.Exception.Unwrap();
            } 
        }
    }
}
