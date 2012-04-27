//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------


namespace CoApp.Packaging.Service.Exceptions {
    using Toolkit.Exceptions;

    /// <summary>
    ///   Exception thrown when the location of a feed isn't recognizable as a package feed.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class InvaildFeedLocationException : CoAppException {
        /// <summary>
        ///   The location attemped to use as a feed location
        /// </summary>
        public string Location;

        /// <summary>
        ///   Initializes a new instance of the <see cref="InvaildFeedLocationException" /> class.
        /// </summary>
        /// <param name="location"> The location. </param>
        /// <remarks>
        /// </remarks>
        public InvaildFeedLocationException(string location) {
            Location = location;
        }
    }
}