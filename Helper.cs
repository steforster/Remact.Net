
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading; // WPF Dispatcher from assembly 'WindowsBase'
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.Async;
using Remact.Net;

namespace Remact.Net.UnitTests.CommunicationModel
{
    public static class Helper
    {
        static public ActionThread ServiceThread
        {
            get
            {
                if (m_serviceThread == null)
                {
                    m_serviceThread = new ActionThread();
                    m_serviceThread.Name = "ServiceThread";
                    m_serviceThread.IsBackground = true;
                    m_serviceThread.Start();
                }
                return m_serviceThread;
            }
        }
        static public Exception ServiceException;
        static private ActionThread m_serviceThread;
        static private int m_testThreadId;

        /*  // Notes concerning MS-UnitTests in Visual Studio 2012:
            // Test methods are called on a threadpool thread without sync context --> start a Nito.Async.ActionThread.
            // Solution inspired Stephen Toub's http://blogs.msdn.com/b/pfxteam/archive/2012/01/21/10259307.aspx,
            using System.Windows.Threading; // WPF Dispatcher from assembly 'WindowsBase'
 
            public static void RunInWpfSyncContext( Func<Task> function )
            {
                if (function == null) throw new ArgumentNullException("function");

                var prevCtx = SynchronizationContext.Current;
                try
                {
                    var syncCtx = new DispatcherSynchronizationContext();
                    SynchronizationContext.SetSynchronizationContext(syncCtx);

                    var task = function();
                    if (task == null) throw new InvalidOperationException();

                    var frame = new DispatcherFrame();
                    var t2 = task.ContinueWith(x => { frame.Continue = false; }, TaskScheduler.Default);
                    Dispatcher.PushFrame(frame);

                    task.GetAwaiter().GetResult();
                }
                finally
                { 
                    SynchronizationContext.SetSynchronizationContext(prevCtx);
                }
            } */

        static public void RunTestInWpfSyncContext(Func<Task> function)
        {
            Trace.WriteLine("--------------------------------------------------------");
            Trace.WriteLine(DateTime.Now.ToString("--- dd.MM.yy  HH:mm:ss.fff")
                + " --- Start " + function.Method.ReflectedType.FullName + " . " + function.Method.Name);
            if (Thread.CurrentThread.Name == null) Thread.CurrentThread.Name = "TestThread";
            m_testThreadId = Thread.CurrentThread.ManagedThreadId;
            Assert.AreNotEqual(m_testThreadId, ServiceThread.ManagedThreadId); // start ServiceThread
            ServiceException = null;
            //-------------------
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());

            var task = function();
            if (task == null) throw new InvalidOperationException();

            var frame = new DispatcherFrame();
            var t2 = task.ContinueWith(x => { frame.Continue = false; }, TaskScheduler.Default);
            Dispatcher.PushFrame(frame);

            task.GetAwaiter().GetResult();
            //-------------------
            // end of test
            m_serviceThread.Dispose();
            m_serviceThread = null;
            AssertRunningOnClientThread();

            if (ServiceException != null)
            {
                RaLog.Exception("Test failed on service side", ServiceException);
                throw new Exception("Test failed on service side", ServiceException);
            }
        }


        public static void AssertRunningOnClientThread()
        {
            Assert.AreEqual(m_testThreadId, Thread.CurrentThread.ManagedThreadId, "not running on test thread");
        }


        public static void AssertRunningOnServiceThread()
        {
            Assert.AreEqual(ServiceThread.ManagedThreadId, Thread.CurrentThread.ManagedThreadId, "not running on service thread");
        }


        public static void AssertTraceCount(int errors, int warnings)
        {
            Assert.AreEqual(errors, RaLog.ErrorCount, " errors in test output");
            Assert.AreEqual(warnings, RaLog.WarningCount, " warnings in test output");
        }


        public static async Task<bool> WhenTimeout(this Task taskToDo, int milliseconds)
        {
            Task firstFinished = await Task.WhenAny(taskToDo, Task.Delay(milliseconds));
            return taskToDo != firstFinished  // timeout
                || !taskToDo.IsCompleted;      // failed
        }

    }
}
