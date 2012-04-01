using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Toolkit.Engine.Exceptions {
    using Client;
    using Toolkit.Exceptions;

    public class ConflictedPackagesException : CoAppException {
        public readonly IEnumerable<Package[]> Packages;
        public ConflictedPackagesException(IEnumerable<Package[]> packages) {
            Packages = packages;
        }
    }
}
