using System;
using System.Text;
using UnityEngine;

namespace DeadQuarantine.PvPvE.Networking
{
    [Serializable] public sealed class PvPvEHello
    {
        public string mode,version,name;
        public int character=-1;
        public static byte[] Encode(PvPvENetworkSettings config,string name)=>Encoding.UTF8.GetBytes(JsonUtility.ToJson(new PvPvEHello{mode=config.SessionPool,version=config.Compatibility,name=CleanName(name),character=CharacterSelection.SelectedIndex}));
        public static bool TryRead(byte[] payload,PvPvENetworkSettings config,out string name)
        =>TryRead(payload,config,out name,out _);
        public static bool TryRead(byte[] payload,PvPvENetworkSettings config,out string name,out int character)
        {
            name="";character=-1;if(payload==null || payload.Length==0 || payload.Length>512)return false;
            try{var data=JsonUtility.FromJson<PvPvEHello>(new UTF8Encoding(false,true).GetString(payload));if(data==null || data.mode!=config.SessionPool || data.version!=config.Compatibility || !config.IsValidCharacter(data.character))return false;character=data.character;name=CleanName(data.name);return true;}catch(Exception){return false;}
        }
        public static string CleanName(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return "Superviviente";
            var builder=new StringBuilder();
            foreach(char c in value.Trim()) {if(char.IsControl(c)||char.IsSurrogate(c)||c=='<'||c=='>')continue;if(builder.Length>=24 || Encoding.UTF8.GetByteCount(builder.ToString()+c)>60)break;builder.Append(c);}
            return builder.Length>0?builder.ToString():"Superviviente";
        }
    }
}
