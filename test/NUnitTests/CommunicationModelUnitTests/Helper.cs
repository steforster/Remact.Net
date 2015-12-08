
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Forms;

namespace Remact.Net.UnitTests
{
    public static class Helper
    {
        // On Mono, only the WinFormsSyncContext is fully implemented.
        //          test output must be sent to Console only.
        static public void RunInWinFormsSyncContext(Func<Task> function)
        {
            Console.WriteLine(DateTime.Now.ToString("--- dd.MM.yy  HH:mm:ss.fff")
                + " --- Start " + function.Method.Name);

            var previousSyncContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                var task = function(); // start it
                while (!task.IsCompleted)
                {
                    Application.DoEvents();
                }
                task.GetAwaiter().GetResult(); // throw exception in case task.IsFaulted 
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousSyncContext);
            }
        }

        /* new DispatcherSynchronizationContext() is not implemented in Mono
        static public void RunInWpfSyncContext(Func<Task> function)
        {
            Trace.WriteLine(DateTime.Now.ToString("--- dd.MM.yy  HH:mm:ss.fff")
                + " --- Start " + function.Method.ReflectedType.FullName + "." + function.Method.Name);
                
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var task = function(); // start it
            var frame = new DispatcherFrame();
            var t2 = task.ContinueWith(x => { frame.Continue = false; }, TaskScheduler.Default);
            Dispatcher.PushFrame(frame);

            task.GetAwaiter().GetResult(); // throw exception in case task.IsFaulted 
        }*/

        public static async Task<bool> WhenTimeout(this Task taskToDo, int milliseconds)
        {
            Task firstFinished = await Task.WhenAny(taskToDo, Task.Delay(milliseconds));
            return taskToDo != firstFinished  // timeout
                || !taskToDo.IsCompleted;      // failed
        }
    }
}
