using System;

namespace System.Windows.Threading
{
    public class Dispatcher
    {
        public static Dispatcher CurrentDispatcher { get; } = new Dispatcher();
        public void Invoke(Action action) => action();
        public System.Threading.Tasks.Task InvokeAsync(Action action)
        {
            action();
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
