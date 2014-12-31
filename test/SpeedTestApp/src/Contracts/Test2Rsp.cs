
// Copyright (c) github.com/steforster/Remact.Net

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Remact.SpeedTest.Contracts
{
    /// <summary>
    /// The response message for the speed test application.
    /// </summary>
    public class Test2Rsp
    {
        /// <summary>
        /// This member is serialized to be transferred over network.
        /// </summary>
        public List<Test2MessageItem> Items;

        /// <summary>
        /// This member is not serialized.
        /// </summary>
        [JsonIgnore]
        public int Index = 0;

        /// <summary>
        /// Initializes a new instance of the Test2Rsp class.
        /// </summary>
        public Test2Rsp()
        {
            Items = new List<Test2MessageItem>(20);
        }


        public void AddItem(string name, int p1, int p2, int p3, string p4)
        {
            Items.Add(new Test2MessageItem(++Index, name, p1, p2, p3, p4));
        }
   }


    /// <summary>
    /// One item contained in Test2Rsp. To demonstrate a more complex message type.
    /// </summary>
    public class Test2MessageItem
    {
        // These 3 public members are serialized.
        public int Index;
        public string ItemName;
        public List<object> Parameter;

        /// <summary>
        /// Initializes a new instance of the Test2MessageItem class.
        /// </summary>
        public Test2MessageItem(int index, string itemName, params object[] parameterList)
        {
            Index = index;
            ItemName = itemName;
            if (parameterList != null)
            {
                Parameter = new List<object>(parameterList.Length);

                foreach (object p in parameterList)
                {
                    Parameter.Add(p);
                }
            }
            else
            {
                Parameter = new List<object>();
            }
        }
    }
}
