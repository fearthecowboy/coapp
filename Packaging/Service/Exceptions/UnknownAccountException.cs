namespace CoApp.Packaging.Service.Exceptions {
    using CoApp.Toolkit.Extensions;
    using Toolkit.Exceptions;

    public class UnknownAccountException : CoAppException{
        internal string _account ;
        public UnknownAccountException(string account) : base("Unknown account '{0}'".format(account)) {
            _account = account;
        }
    }
}
