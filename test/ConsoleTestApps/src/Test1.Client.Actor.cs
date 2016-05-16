
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Threading.Tasks;
using Remact.Net;
using Test1.Contracts;

namespace Test1.Client
{
    /// <summary>
    /// MyActor has one input and one output port. It accepts <see cref="Test1CommandMessage"/>'s on the <see cref="TestInput"/> port.
    /// It simply passes the message to the <see cref="TestOutput"/> port.
    /// The TestOutput port is connected to a partner actor. The response from this partner is awaited and logged.
    /// Processing of input-, output- and response messages runs safely on a single thread. 
    /// </summary>
    class MyActor
    {
        public RemactPortService m_testInput;
        public RemactPortProxy m_testOutput;

        /// <summary>
        /// Create a new actor instance.
        /// </summary>
        /// <param name="inputName">Name of the input port.</param>
        /// <param name="outputName">Name of the output port.</param>
        public MyActor(string inputName, string outputName)
        {
            // Here we link the two ports to methods that are called when an incoming message is not handled otherwise.
            m_testInput = new RemactPortService(inputName, OnMessageFromInputPort);
            m_testOutput = new RemactPortProxy(outputName, OnMessageFromOutputPort);
        }

        /// <summary>
        /// The public interface of the input port.
        /// </summary>
        public IRemactPortService TestInput {get { return m_testInput; } }

        /// <summary>
        /// The public interface of the output port.
        /// </summary>
        public IRemactPortProxy TestOutput{get { return m_testOutput; } }

        /// <summary>
        /// Called by managment code on the thread that will run all message handling of the actor.
        /// The method picks up the current thread synchronization context and opens the connections that where previously linked by management code.
        /// </summary>
        public async Task<bool> OnStartup()
        {
            Console.Write("\n\r Thread=" + Thread.CurrentThread.ManagedThreadId + ", is connecting...");
            m_testInput.Open();

            await m_testOutput.ConnectAsync();

            // After successful connection, this task continuation will be called.
            Console.WriteLine("\n\r Thread=" + Thread.CurrentThread.ManagedThreadId + ", TestOutput is connected to remote service.");
            Console.Write("\n\r\n\r(1) send command > ");
            return true;
        }

        /// <summary>
        /// OnMessageFromInputPort is called by Remact.Net when a message is received from main thread.
        /// The method runs on the same thread as OnStartup.
        /// </summary>
        /// <param name="msg">The property <see cref="RemactMessage.Payload"/> contains the object sent from <see cref="Test1Client.Main()"/>.</param>
        private Task OnMessageFromInputPort(RemactMessage msg)
        {
            Console.Write("\n\r Thread=" + Thread.CurrentThread.ManagedThreadId + ", received input: " + msg.ToString());

            Test1CommandMessage testMessage;
            if (msg.IsNotification && msg.TryConvertPayload(out testMessage))
            {
                // The user sends a Test1CommandMessage
                PortState s = m_testOutput.OutputState;
                if (s == PortState.Disconnected || s == PortState.Faulted)
                {
                    // The connection is not established, we retry...
                    OnStartup();
                }
                else if (s == PortState.Connecting)
                {
                    // A connection attempt is pending, we skip this command...
                    Console.Write("\n\r - cannot send, still connecting...");
                }
                else
                {
                    // Our output is connected. We can pass the incoming message to the output.
                    // First we create an id to demonstrate that the response is handled asynchronously in the closure.
                    int closureId = m_testOutput.LastRequestIdSent + 1000;

                    m_testOutput.SendReceiveAsync(null, testMessage,
                            // this delegate is called asynchronously when the response to this request arrives.
                            delegate (ReadyMessage response, RemactMessage rsp)
                            {
                                Console.Write("\n\r Thread=" + Thread.CurrentThread.ManagedThreadId);
                                Console.WriteLine(", received ready message in closure #" + closureId);
                                Console.Write("\n\r\n\r(3) send command > ");
                            });

                    Console.Write(", closure #" + closureId + "...");
                    return null;
                }
            }
            else
            {
                Console.Write(", UNEXPECTED message type from " + msg.Source.Name);
            }

            // In case we did not send a request, we have to ask the user to enter another command.
            Console.Write("\n\r\n\r(2) send command > ");
            return null;
        }

        
        /// <summary>
        /// OnMessageFromOutputPort is called by Remact.Net when a message is received from the remote service.
        /// The method runs on the same thread as OnStartup.
        /// When a response message is handled in a closure, then this method is not called.
        /// </summary>
        private Task OnMessageFromOutputPort(RemactMessage msg)
        {
            Console.Write("\n\r Thread=" + Thread.CurrentThread.ManagedThreadId + ", received UNEXPECTED: " + msg.ToString());
            ErrorMessage error;
            if (msg.IsError && msg.TryConvertPayload(out error))
            {
                Console.Write("\n\r " + error.ToString());
            }
            Console.Write("\n\r\n\r(4) send command > ");
            return null;
        }
    }
}
