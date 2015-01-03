
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading; // WPF Dispatcher from assembly 'WindowsBase'
using System.Windows.Forms;

namespace Remact.Net.UnitTests
{
    public static class Helper
    {
        //static public ActionThread ServiceThread
        //{
        //    get
        //    {
        //        if (m_serviceThread == null)
        //        {
        //            m_serviceThread = new ActionThread();
        //            m_serviceThread.Name = "ServiceThread";
        //            m_serviceThread.IsBackground = true;
        //            m_serviceThread.Start();
        //        }
        //        return m_serviceThread;
        //    }
        //}
        //static public Exception ServiceException;
        //static private ActionThread m_serviceThread;
        //static private int _testThreadId;


        // On Mono, only the WinFormsSyncContext is fully implemented.
        //          also test output must be sent to Console only.
        static public void RunInWinFormsSyncContext(Func<Task> function)
        {
            Console.WriteLine(DateTime.Now.ToString("--- dd.MM.yy  HH:mm:ss.fff")
                + " --- Start " /*+ function.Method.ReflectedType.FullName + "."*/ 
                + function.Method.Name);
            //_testThreadId = Thread.CurrentThread.ManagedThreadId;
            //Assert.AreNotEqual(_testThreadId, ServiceThread.ManagedThreadId); // start ServiceThread
            //ServiceException = null;
            //-------------------
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

            //-------------------
            // end of test
            //m_serviceThread.Dispose();
            //m_serviceThread = null;
            //AssertRunningOnClientThread();

            //if (ServiceException != null)
            //{
            //    RaLog.Exception("Test failed on service side", ServiceException);
            //    throw new Exception("Test failed on service side", ServiceException);
            //}
        }

        /* new DispatcherSynchronizationContext() is not implemented in Mono
        static public void RunInWpfSyncContext(Func<Task> function)
        {
            Trace.WriteLine(DateTime.Now.ToString("--- dd.MM.yy  HH:mm:ss.fff")
                + " --- Start " + function.Method.ReflectedType.FullName + "." + function.Method.Name);
            _testThreadId = Thread.CurrentThread.ManagedThreadId;
            //Assert.AreNotEqual(_testThreadId, ServiceThread.ManagedThreadId); // start ServiceThread
            //ServiceException = null;
            //-------------------
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());

            var task = function(); // start it
            var frame = new DispatcherFrame();
            var t2 = task.ContinueWith(x => { frame.Continue = false; }, TaskScheduler.Default);
            Dispatcher.PushFrame(frame);

            task.GetAwaiter().GetResult(); // throw exception in case task.IsFaulted 
            //-------------------
            // end of test
            //m_serviceThread.Dispose();
            //m_serviceThread = null;
            //AssertRunningOnClientThread();

            //if (ServiceException != null)
            //{
            //    RaLog.Exception("Test failed on service side", ServiceException);
            //    throw new Exception("Test failed on service side", ServiceException);
            //}
        }*/


        //public static void AssertRunningOnClientThread()
        //{
        //    Assert.AreEqual(_testThreadId, Thread.CurrentThread.ManagedThreadId, "not running on test thread");
        //}


        //public static void AssertRunningOnServiceThread()
        //{
        //    Assert.AreEqual(ServiceThread.ManagedThreadId, Thread.CurrentThread.ManagedThreadId, "not running on service thread");
        //}


        //public static void AssertTraceCount(int errors, int warnings)
        //{
        //    Assert.AreEqual(errors, RaLog.ErrorCount, " errors in test output");
        //    Assert.AreEqual(warnings, RaLog.WarningCount, " warnings in test output");
        //}


        public static async Task<bool> WhenTimeout(this Task taskToDo, int milliseconds)
        {
            Task firstFinished = await Task.WhenAny(taskToDo, Task.Delay(milliseconds));
            return taskToDo != firstFinished  // timeout
                || !taskToDo.IsCompleted;      // failed
        }

    }
}
