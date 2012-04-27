namespace CoApp.Toolkit.Exceptions {
    using System;

    public class PathIsNotFileUriException : CoAppException {
        public string Path;
        public Uri Uri;

        public PathIsNotFileUriException(string path, Uri uri) {
            Path = path;
            Uri = uri;
        }
    }
}