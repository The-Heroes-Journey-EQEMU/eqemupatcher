using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EQEmu_Patcher
{
    class IniLibrary
    {
        public static IniLibrary instance;
        public string AutoPatch { get; set; }
        public string AutoPlay { get; set; }
        public VersionTypes ClientVersion { get; set; }
        public string LastPatchedVersion { get; set; }

        
        public static void Save()
        {
            var serializerBuilder = new SerializerBuilder().WithNamingConvention(new CamelCaseNamingConvention());
            var serializer = serializerBuilder.Build();
            string body = serializer.Serialize(instance);
            
            Console.WriteLine(body);
            File.WriteAllText(@"eqemupatcher.yml", body);
        }

        public static void Load()
        {
            try {
                using (var input = File.OpenText("eqemupatcher.yml"))
                {
                    var deserializerBuilder = new DeserializerBuilder().WithNamingConvention(new CamelCaseNamingConvention());

                    var deserializer = deserializerBuilder.Build();

                    instance = deserializer.Deserialize<IniLibrary>(input);
                }
                                
                if (instance == null) {
                    ResetDefaults();
                    Save();
                }
            } catch (System.IO.FileNotFoundException) {                
                ResetDefaults();
                Save();
            }
        }

        public static void ResetDefaults()
        {
            instance = new IniLibrary();
        }
    }
}
