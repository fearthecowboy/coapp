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

namespace CoApp.Toolkit.Pipes {
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Extensions;

    /// <summary>
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static class AsyncPipeExtensions {
        /// <summary>
        ///   Read from a stream asynchronously.
        /// </summary>
        /// <param name="stream"> The stream. </param>
        /// <param name="buffer"> An array of bytes to be filled by the read operation. </param>
        /// <param name="offset"> The offset at which data should be stored. </param>
        /// <param name="count"> The number of bytes to be read. </param>
        /// <returns> A Task containing the number of bytes read. </returns>
        /// <remarks>
        /// </remarks>
        public static Task<int> ReadAsync(this Stream stream, byte[] buffer, int offset, int count) {
            if (stream == null) {
                throw new ArgumentNullException("stream");
            }
            return Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, buffer, offset, count, stream /* object state */);
        }

        /// <summary>
        ///   Write to a stream asynchronously.
        /// </summary>
        /// <param name="stream"> The stream. </param>
        /// <param name="buffer"> An array of bytes to be written. </param>
        /// <param name="offset"> The offset from which data should be read to be written. </param>
        /// <param name="count"> The number of bytes to be written. </param>
        /// <returns> A Task representing the completion of the asynchronous operation. </returns>
        /// <remarks>
        /// </remarks>
        public static Task WriteAsync(this Stream stream, byte[] buffer, int offset, int count) {
            if (stream == null) {
                throw new ArgumentNullException("stream");
            }
            return Task.Factory.FromAsync(stream.BeginWrite, stream.EndWrite, buffer, offset, count, stream, TaskCreationOptions.AttachedToParent);
        }

        /// <summary>
        ///   Writes the line async.
        /// </summary>
        /// <param name="stream"> The stream. </param>
        /// <param name="message"> The message. </param>
        /// <param name="objs"> The objs. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static Task WriteLineAsync(this Stream stream, string message, params object[] objs) {
            var bytes = (message.format(objs).Trim()).ToByteArray();
            return stream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}