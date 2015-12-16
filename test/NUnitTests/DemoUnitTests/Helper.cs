
// Copyright (c) https://github.com/steforster/Remact.Net

using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Forms;
using Nito.Async;
using Remact.Net;

namespace DemoUnitTest
{
    public static class Helper
    {
        static public ActionThread ServiceThread
        {
            get
            {
                if (_serviceThread == null)
                {
                    _serviceThread = new ActionThread();
                    _serviceThread.Name = "ServiceThread";
                    _serviceThread.IsBackground = true;
                    _serviceThread.Start();
                }
                return _serviceThread;
            }
        }

        static public Exception ServiceException;

        static private ActionThread _serviceThread;
        static private int _testThreadId;


        // On Mono, only the WinFormsSyncContext is fully implemented.
        //          test output must be sent to Console only.
        static public void RunInWinFormsSyncContext(Func<Task> function)
        {
            Console.WriteLine(DateTime.Now.ToString("--- dd.MM.yy  HH:mm:ss.fff")
                + " --- Start " + function.Method.Name);

            _testThreadId = Thread.CurrentThread.ManagedThreadId;
            Assert.AreNotEqual(_testThreadId, ServiceThread.ManagedThreadId); // starts ServiceThread
            ServiceException = null;
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
            _serviceThread.Dispose();
            _serviceThread = null;
            AssertRunningOnClientThread();

            if (ServiceException != null)
            {
                RaLog.Exception("Test failed on service side", ServiceException);
                throw new Exception("Test failed on service side", ServiceException);
            }
        }


        public static void AssertRunningOnClientThread()
        {
            Assert.AreEqual(_testThreadId, Thread.CurrentThread.ManagedThreadId, "not running on test thread");
        }


        public static void AssertRunningOnServiceThread()
        {
            Assert.AreEqual( ServiceThread.ManagedThreadId, Thread.CurrentThread.ManagedThreadId, "not running on service thread" );
        }


        public static void AssertTraceCount( int errors, int warnings )
        {
            Assert.AreEqual( errors,   RaLog.ErrorCount,   " errors in test output" );
            Assert.AreEqual( warnings, RaLog.WarningCount, " warnings in test output" );
        }


        public static async Task<bool> WhenTimeout( this Task taskToDo, int milliseconds )
        {
            Task  firstFinished = await Task.WhenAny(taskToDo, Task.Delay(milliseconds));
            return  taskToDo != firstFinished  // timeout
                || !taskToDo.IsCompleted;      // failed
        }


        public static void LoadPluginDll(string fileName)
        {
            #if(DEBUG)
                var path = @"../../../../../src/bin/Debug/" + fileName;
            #else
                var path = @"../../../../../src/bin/Release/" + fileName;
            #endif

            var disposable = RemactConfigDefault.LoadPluginAssembly(path);
            Assert.IsNotNull(disposable, "cannot dynamically load dll: " + path);
            RaLog.Info("RemactConfigDefault.LoadPluginAssembly", fileName);

            /* TODO:
             * Newtonsoft.Json (Replacement) is problematic, because it can be copied to the test output directory.
             * In the original folder (src/bin) it overwrites the Newtonsoft.Json when compiled after Json-Plugin.
             *                               
             * Does LoadPluginAssembly link to same Remact.Net as test assembly ?
             * 
             * How to execte tests with both bluginc (Json + BMS) ?
             */
        }
    }
}
