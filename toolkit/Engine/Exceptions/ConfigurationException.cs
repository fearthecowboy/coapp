//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Engine.Exceptions {
    using Toolkit.Exceptions;

    /// <summary>
    ///   An exception that gets thrown when the CoApp configuration is unable to be modified or accessed.
    /// </summary>
    /// <remarks>
    /// </remarks>
    internal class ConfigurationException : CoAppException {
        /// <summary>
        ///   Gets or sets the registry key valuename.
        /// </summary>
        /// <value>The key.</value>
        /// <remarks>
        /// </remarks>
        public string Key { get; set; }

        /// <summary>
        ///   Gets or sets the detail text.
        /// </summary>
        /// <value>The detail.</value>
        /// <remarks>
        /// </remarks>
        public string Detail { get; set; }

        /// <summary>
        ///   Initializes a new instance of the <see cref = "ConfigurationException" /> class.
        /// </summary>
        /// <param name = "message">The message.</param>
        /// <param name = "key">The key.</param>
        /// <param name = "detail">The detail.</param>
        /// <remarks>
        /// </remarks>
        public ConfigurationException(string message, string key, string detail) : base(message) {
            Key = key;
            Detail = detail;
        }
    }
}