
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Remact.Net
{
    //----------------------------------------------------------------------------------------------
    #region == class MethodDispatch ==

    /// <summary>
    /// Dispatches an <see cref="ActorMessage"/> to a <see cref="ActorMethod"/>.
    /// </summary>
    public class Dispatcher
    {
        public Dispatcher()
        {
            _methods = new Dictionary<string, ActorMethod>();
        }

        private Dictionary<string, ActorMethod> _methods;


        public ActorMessage CallMethod(ActorMessage msg, object context)
        {
            if (string.IsNullOrEmpty(msg.DestinationMethod)) return msg;

            ActorMethod method;
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

                    if (param[1].ParameterType != typeof(ActorMessage))
                    {
                        throw new InvalidOperationException("method '" + mTarget.Name + "', second parameter must be of type 'ActorMessage'");
                    }

                    if (_methods.ContainsKey(mTarget.Name))
                    {
                        throw new InvalidOperationException("more than one method '" + mTarget.Name + "' found in " + actorInterface.FullName);
                    }

                    var innerIf = InnerType(mInterface.ReturnType);
                    var innerTgt = InnerType(mTarget.ReturnType);
                    if (innerIf != innerTgt)
                    {
                        throw new InvalidOperationException("return type of method '" + mTarget.Name + "' does not match the interface. Note, Task<ActorMessage<T>> is accepted.");
                    }

                    var method = new ActorMethod
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
                    RaLog.Exception("Cannot add interface '" + actorInterface.FullName + "' to '" + implementation.GetType().FullName + "'", ex); // TODO logger + gathering exceptions for all methods
                    throw;
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
    public class ActorMethod
    {
        public MethodInfo Target;
        public object Implementation;
        public Type PayloadType;
        public Type ContextType;

        public object[] Parameters(ActorMessage msg, object context)
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
            param[0] = msg.Payload; // raw

            var jToken = msg.Payload as JToken;
            if (jToken != null)
            {
                param[0] = jToken.ToObject(this.PayloadType); // deserialized
            }

            return param;
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
}
