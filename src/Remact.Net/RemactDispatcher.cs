
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

namespace Remact.Net
{
    //----------------------------------------------------------------------------------------------
    #region == class RemactDispatcher ==

    /// <summary>
    /// Dispatches a <see cref="RemactMessage"/> to a matching method and converts the incoming payload.
    /// Each port has a dispatcher for incoming messages.
    /// </summary>
    public class RemactDispatcher
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public RemactDispatcher()
        {
            _methods = new Dictionary<string, RemactMethod>();
        }

        private Dictionary<string, RemactMethod> _methods;

        /// <summary>
        /// Call the method addressed by <see cref="RemactMessage.DestinationMethod"/> name. Convert the <see cref="RemactMessage.Payload"/> to the type defined by the called methods first parameter. 
        /// Internally used by Remact.Net.
        /// </summary>
        /// <param name="msg">The incoming RemactMessage. It is returned as null, when processed.</param>
        /// <param name="context">The context object defined by a <see cref="RemactPortProxy{TOC}"/> or <see cref="RemactPortService{TSC}"/></param>
        /// <returns>The message when not processed. Null, when message is processed.</returns>
        public async Task<RemactMessage> CallMethod(RemactMessage msg, object context)
        {
            RemactMethod method;
            if (string.IsNullOrEmpty(msg.DestinationMethod)
             || !_methods.TryGetValue(msg.DestinationMethod, out method))
            {
                return msg; // message not processed
            }

            var parameters = method.Parameters(msg, context);
            object reply = method.Target.Invoke(method.Implementation, parameters);
            if (method.IsAsync)
            {
                var task = (Task)reply;
                await task;
                if (method.AsyncResultProperty != null)
                {
                    reply = method.AsyncResultProperty.GetValue(task); // see also https://github.com/dotnet/roslyn/issues/2981
                }
                else
                {
                    reply = null; // Task has no reply
                }
            }

            if (reply != null)
            {
                msg.SendResponse(reply);
            }
            return null; // message processed
        }

        /// <summary>
        /// Add methods of an interface to the dispatcher.
        /// A method of the implementation object will be invoked by an incoming RemactMessage.
        /// The given implementation object must not implement the interface exactly
        /// but all method names of the interface must be uniquely found in the implementation object.
        /// All these implemented, remotly callable methods must have 2 or 3 parameters. 
        /// The first parameter must match the parameter of the equally named method in the interface.
        /// The second parameter must be of type <see cref="RemactMessage"/>.
        /// The third parameter is optional. When provided it must match the context type
        /// of the corresponding <see cref="RemactPortProxy{TOC}"/> or <see cref="RemactPortService{TSC}"/>.
        /// The return type of the implemented method must be equal to the return type T of the interface method.
        /// When implemented as an asynchronous service, the return type may also be of type <see cref="Task{M}"/> 
        /// where M is <see cref="RemactMessage{T}"/> and T is the return type of the interface method.
        /// All these constraints are checked at runtime, when <see cref="AddActorInterface"/> is called.
        /// </summary>
        /// <param name="actorInterface">One of the port input interface contracts (no attributes are needed).</param>
        /// <param name="implementation">The port implementation for incoming messages.</param>
        public void AddActorInterface(Type actorInterface, object implementation)
        {
            var mTargetList = implementation.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy
                                                                | BindingFlags.Instance | BindingFlags.Static);
            var mInterfaceList = actorInterface.GetMethods();

            foreach (var mInterface in mInterfaceList)
            {
                try
                {
                    var mTarget = mTargetList.Single((m) => m.Name == mInterface.Name);
                    var param = mTarget.GetParameters();

                    if (param.Length < 2 || param.Length > 3)
                    {
                        throw new InvalidOperationException("method '" + mTarget.Name + "' must have 2 to 3 parameters");
                    }

                    if (param[1].ParameterType != typeof(RemactMessage))
                    {
                        throw new InvalidOperationException("method '" + mTarget.Name + "', second parameter must be of type 'RemactMessage'");
                    }

                    if (_methods.ContainsKey(mTarget.Name))
                    {
                        throw new InvalidOperationException("more than one method '" + mTarget.Name + "' found in " + actorInterface.FullName);
                    }

                    var innerIf = InnerType(mInterface.ReturnType);
                    var innerTgt = InnerType(mTarget.ReturnType);
                    if (innerIf != innerTgt)
                    {
                        throw new InvalidOperationException("return type of method '" + mTarget.Name + "' does not match the interface. Note, Task<T> is accepted.");
                    }

                    var method = new RemactMethod
                    {
                        Target = mTarget,
                        Implementation = implementation,
                        PayloadType = param[0].ParameterType,
                        IsAsync = typeof(Task).IsAssignableFrom(mTarget.ReturnType),
                    };

                    if (method.IsAsync)
                    {
                        // method returning Task or Task<T>
                        method.AsyncResultProperty = mTarget.ReturnType.GetProperty("Result");
                    }

                    if (param.Length == 3)
                    {
                        method.ContextType = param[2].ParameterType;
                    }

                    _methods.Add(mTarget.Name, method);
                }
                catch (Exception ex)
                {
                    string s = "Cannot add interface '" + actorInterface.FullName + "' to '" + implementation.GetType().FullName + "'";
                    RaLog.Exception(s, ex); // TODO log + gather exceptions for all methods
                    throw new InvalidOperationException(s, ex);
                }
            }
        }
        
        /// <summary>
        /// Searches the InputDispatcher for the method name. Returns the expected payload type or null, when not found.
        /// </summary>
        /// <param name="destinationMethod">The name of the method to call.</param>
        internal Type FindPayloadTypeByDestination(string destinationMethod)
        {
            RemactMethod method;
            if (string.IsNullOrEmpty(destinationMethod)
            || !_methods.TryGetValue(destinationMethod, out method))
            {
                return null; // destination not found
            }

            return method.PayloadType;
        }

        private Type InnerType (Type type)
        {
            while (type.IsGenericType)
            {
                type = type.GetGenericArguments()[0];
            }
            return type;
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class RemactMethod ==

    /// <summary>
    /// Represents a method that is callable by a (remote) Actor.
    /// </summary>
    internal class RemactMethod
    {
        public MethodInfo Target;
        public object Implementation;
        public Type PayloadType;
        public Type ContextType;
        public bool IsAsync;
        public PropertyInfo AsyncResultProperty;

        public object[] Parameters(RemactMessage msg, object context)
        {
            object[] param;
            if (ContextType == null)
            {
                param = new object[2];
            }
            else
            {
                param = new object[3];
                param[2] = context;
            }

            param[1] = msg;

            if (msg.SerializationPayload != null)
            {
                param[0] = msg.SerializationPayload.TryReadAs(this.PayloadType);
                if (param[0] == null)
                {
                    param[0] = msg.Payload; // raw
                }
            }
            else
            {
                param[0] = msg.Payload; // process local call
            }

            return param;
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
}
