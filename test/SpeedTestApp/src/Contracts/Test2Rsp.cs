
// Copyright (c) https://github.com/steforster/Remact.Net

using System.Collections.Generic;
using Newtonsoft.Json;
using Remact.Net.Bms1Serializer;

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


        //----------------------------------------------------------------------------------------------
        #region BMS1 serializer

        public static Test2Rsp ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock(() =>
                {
                    var dto = new Test2Rsp();
                    dto.Items = reader.ReadBlocks(Test2MessageItem.ReadFromBms1Stream);
                    return dto;
                });
        }

        public static void WriteToBms1Stream(object obj, IBms1Writer writer)
        {
            writer.WriteBlock(() => 
                {
                    var dto = (Test2Rsp)obj;
                    writer.WriteBlocks(0, dto.Items, Test2MessageItem.WriteToBms1Stream);
                });
        }

        #endregion
   }


    /// <summary>
    /// One of the items contained in Test2Rsp. To demonstrate a more complex message type.
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


        //----------------------------------------------------------------------------------------------
        #region BMS1 serializer

        public static Test2MessageItem ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock(() =>
                {
                    var dto = new Test2MessageItem(0, null, null);
                    dto.Index = reader.ReadInt32();
                    dto.ItemName = reader.ReadString();
                    dto.Parameter = reader.ReadBlocks<object>(
                        (bmsReader) =>
                        {
                            if (bmsReader.Internal.TagEnum == Net.Bms1Serializer.Internal.Bms1Tag.Int32)
                            {
                                return bmsReader.ReadInt32();
                            }
                            else
                            {
                                return bmsReader.ReadString();
                            }
                        });
                    return dto;
                });
        }

        public static void WriteToBms1Stream(object obj, IBms1Writer writer)
        {
            writer.WriteBlock(() => 
                {
                    var dto = (Test2MessageItem)obj;
                    writer.WriteInt32(dto.Index);
                    writer.WriteString(dto.ItemName);
                    writer.WriteBlocks<object>(0, dto.Parameter,
                        (param, bmsWriter) =>
                        {
                            if (param is int)
                            {
                                bmsWriter.WriteInt32((int)param);
                            }
                            else
                            {
                                bmsWriter.WriteString(param.ToString());
                            }
                        });
                });
        }

        #endregion
    }
}
