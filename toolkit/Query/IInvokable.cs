namespace CoApp.Toolkit.Query {
    public interface IInvokable<in T> {
           bool Invoke(T item);
    }
}