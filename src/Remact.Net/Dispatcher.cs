
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            var reply = method.Info.Invoke(method.Implementation, parameters);
            if (reply != null)
            {
                msg.SendResponse(reply);
            }
            return null;
        }


        public void AddActorInterface(Type actorInterface, object implementation)
        {
            if (!actorInterface.IsAssignableFrom (implementation.GetType()))
            {
                throw new ArgumentException(implementation.GetType().FullName + " does not implement interface " + actorInterface.FullName);
            }

            var list = actorInterface.GetMethods();
            foreach (var m in list)
            {
                var param = m.GetParameters();
                if (param.Length < 2 || param.Length > 3) continue;
                if (param[1].ParameterType != typeof(ActorMessage)) continue;

                if (_methods.ContainsKey (m.Name))
                {
                    throw new InvalidOperationException("more than one method '"+m.Name+"' found when adding interface " + actorInterface.FullName);
                }

                var method = new ActorMethod
                {
                    Info = m,
                    Implementation = implementation,
                    PayloadType = param[0].ParameterType,
                };

                if (param.Length == 3)
                {
                    method.ContextType = param[2].ParameterType;
                }

                _methods.Add(m.Name, method);
           }
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
        public MethodInfo Info;
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

            if (msg.PayloadType == null)
            {
                var jToken = msg.Payload as JToken;
                if (jToken != null)
                {
                    param[0] = jToken.ToObject(this.PayloadType); // deserialized
                }
            }

            return param;
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
}
