
// Copyright (c) https://github.com/steforster/Remact.Net

using Newtonsoft.Json;
using Remact.Net.Bms1Serializer;

namespace Remact.SpeedTest.Contracts
{
    /// <summary>
    /// The request message for the speed test application.
    /// </summary>
    public class Test2Req
    {
        #region Message Data Members

        /// <summary>
        /// 'RequestCode' is streamed as int in order to make it reverse compatible to older communication partners.
        /// </summary>
        [JsonProperty]
        private uint z_requestcode = 0; // ERequestCode

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Enums

        /// <summary>
        /// Demonstrate how to make enum fields forward/backward compatible
        /// </summary>
        public enum ERequestCode
        {
            Unknown = 0,
            Normal,
            Emergency,
            // Later versions may add a new code here
            Unused
        }

        [JsonIgnore]
        public ERequestCode RequestCode
        {
            get
            {
                if (z_requestcode >= (uint)ERequestCode.Unused) return ERequestCode.Unknown; // a old service receives an new, unknown code
                else return (ERequestCode)z_requestcode;
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the Test2Req class.
        /// </summary>
        public Test2Req(ERequestCode code)
        {
            z_requestcode = (uint)code;
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region BMS1 serializer

        public static Test2Req ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock(() =>
                {
                    var dto = new Test2Req(ERequestCode.Unknown);
                    dto.z_requestcode = reader.ReadUInt32();
                    return dto;
                });
        }

        public static void WriteToBms1Stream(object obj, IBms1Writer writer)
        {
            writer.WriteBlock(() => 
                {
                    var dto = (Test2Req)obj;
                    writer.WriteUInt32(dto.z_requestcode);
                });
        }

        #endregion
    }
}
