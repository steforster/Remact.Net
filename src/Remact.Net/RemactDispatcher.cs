
// Copyright (c) 2014, github.com/steforster/Remact.Net

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
    /// Dispatches an <see cref="RemactMessage"/> to a <see cref="RemactMethod"/>.
    /// </summary>
    public class RemactDispatcher
    {
        public RemactDispatcher()
        {
            _methods = new Dictionary<string, RemactMethod>();
        }

        private Dictionary<string, RemactMethod> _methods;


        public RemactMessage CallMethod(RemactMessage msg, object context)
        {
            if (string.IsNullOrEmpty(msg.DestinationMethod)) return msg;

            RemactMethod method;
            if (!_methods.TryGetValue(msg.DestinationMethod, out method))
            {
                return msg;
            }

            var parameters = method.Parameters(msg, context);
            var reply = method.Target.Invoke(method.Implementation, parameters);
            if (reply != null)
            {
                msg.SendResponse(reply);
            }
            return null;
        }


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
                        throw new InvalidOperationException("return type of method '" + mTarget.Name + "' does not match the interface. Note, Task<RemactMessage<T>> is accepted.");
                    }

                    var method = new RemactMethod
                    {
                        Target = mTarget,
                        Implementation = implementation,
                        PayloadType = param[0].ParameterType,
                    };

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
    #region == class ActorMethod ==

    /// <summary>
    /// Represents a method that is callable by a (remote) Actor.
    /// </summary>
    internal class RemactMethod
    {
        public MethodInfo Target;
        public object Implementation;
        public Type PayloadType;
        public Type ContextType;

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
            param[0] = msg.SerializationPayload.TryReadAs(this.PayloadType);
            if (param[0] == null)
            {
                param[0] = msg.Payload; // raw
            }

            return param;
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
}
