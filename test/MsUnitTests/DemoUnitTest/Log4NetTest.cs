
// Copyright (c) 2014, github.com/steforster/Remact.Net
/*
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remact.Net;
using log4net;


namespace DemoUnitTest
{
    /// <summary>
    /// This class is used to adapt the RaLog output to the Log4Net package.
    /// You can use it as an example for an adapter to any other logging framework.
    /// In order to test this class, the "log4net" package must be installed e.g. from http://nuget.org/packages/log4net
    /// </summary>
    public class Log4NetAdapter : RaLog.ITracePlugin
    {
        private ILog m_defaultLogger;


        public Log4NetAdapter(ILog defaultLogger)
        {
            m_defaultLogger = defaultLogger;
        }


        public void Info(string group, string text, object logger)
        {
            var log = logger as ILog;
            if (log == null) log = m_defaultLogger;

            log.Info(string.Concat(group, ", ", text));
        }

        public void Warning(string group, string text, object logger)
        {
            var log = logger as ILog;
            if (log == null) log = m_defaultLogger;

            log.Warn(string.Concat(group, ", ", text));
        }

        public void Error(string group, string text, object logger)
        {
            var log = logger as ILog;
            if (log == null) log = m_defaultLogger;

            log.Error(string.Concat(group, ", ", text));
        }

        public void Exception(string text, Exception ex, object logger)
        {
            var log = logger as ILog;
            if (log == null) log = m_defaultLogger;

            log.Error(text, ex);
        }

        public void Start(int appInstance)
        {
        }

        public void Run()
        {
        }

        public void Stop()
        {
        }
    }


    [TestClass]
    public class Log4NetTest
    {
        [ClassInitialize] // run once when creating this class
        public static void ClassInitialize(TestContext testContext)
        {
            ILog defaultLogger = LogManager.GetLogger(typeof(Log4NetTest));

            // appending log4net to Visual Studio output
            var appender = new log4net.Appender.TraceAppender();
            appender.Layout = new log4net.Layout.SimpleLayout();
            log4net.Config.BasicConfigurator.Configure(appender);
            defaultLogger.Debug("Hello to the world of log4net");

            // redirect all trace output of Remact.Net to the Log4NetAdapter:
            RaLog.UsePlugin(new Log4NetAdapter(defaultLogger));

            WcfMessage.AddKnownType(typeof(DelayActor.Request));
            WcfMessage.AddKnownType(typeof(DelayActor.Response));

            ActorInput.DisableRouterClient = true;
        }

        [TestInitialize] // run before each TestMethod
        public void TestInitialize()
        {
            RaLog.ResetCount();
        }

        [TestCleanup] // run after each TestMethod
        public void TestCleanup()
        {
            ActorPort.DisconnectAll();
        }

        DelayActor m_foreignActor;

        [TestMethod]
        public void When10ClientsSendTo1AsyncService_ThenUseLog4Net()
        {
            m_foreignActor = new DelayActor();

            // trace all message flow
            m_foreignActor.InputAsync.TraceSend = true;
            m_foreignActor.InputAsync.TraceReceive = true;
            m_foreignActor.InputAsync.LinkInputToNetwork("DelayActorInputAsync", tcpPort: 40001, publishToRouter: false);

            // we provide the logger object to use by the foreign actor
            m_foreignActor.Open(LogManager.GetLogger("Foreign-DelayActor"));

            Helper.RunTestInWpfSyncContext(async () =>
            {
                // this is the logger for our own actor
                var log4netLogger = LogManager.GetLogger("MyActor");

                // we can directly write to the logger
                log4netLogger.Info("Started When10ClientsSendTo1AsyncService_ThenUseLog4NetAsync TestMethod ...");

                // we also can write through the Remact.Net tracing system (as the library does)
                RaLog.Info("Remact.Net Trace", "redirected trace line to log4netLogger ...", log4netLogger);

                // now we run a communication test, trace output must be checked manually ...
                Helper.AssertRunningOnClientThread();
                int clientCount = 10;
                var output = new ActorOutput[clientCount];
                var sendOp = new Task<WcfReqIdent>[clientCount];

                for (int i = 0; i < clientCount; i++)
                {
                    output[i] = new ActorOutput(string.Format("OUT{0:00}", i + 1));
                    output[i].TraceSend = true;
                    output[i].TraceReceive = true;
                    output[i].Logger = log4netLogger;
                    output[i].LinkOutputToRemoteService(new Uri("net.tcp://localhost:40001/Remact.Net/DelayActorInputAsync"));
                    sendOp[i] = output[i].TryConnectAsync();
                }

                if (await Task.WhenAll(sendOp).WhenTimeout(10000))
                {
                    Assert.Fail("Timeout while opening");
                }

                for (int i = 0; i < clientCount; i++)
                {
                    sendOp[i] = output[i].SendReceiveAsync(new DelayActor.Request());
                }

                if (await Task.WhenAll(sendOp).WhenTimeout(900))
                {
                    Assert.Fail("Timeout, actor does not interleave successive requests");
                }

                Assert.AreEqual(clientCount, m_foreignActor.StartedCount, "not all operations started");
                Assert.AreEqual(clientCount, m_foreignActor.FinishedCount, "not all operations finished");
                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsInstanceOfType(sendOp[i].Result.Message, typeof(DelayActor.Response), "wrong response type received");
                }
                Helper.AssertRunningOnClientThread();
                Helper.AssertTraceCount(0, 0);
            });
            m_foreignActor.Close();
        }
    }
}*/