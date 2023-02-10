using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQEmu_Patcher
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class PatcherUrl : Attribute
    {
        public string Value { get; set; }
        public PatcherUrl(string value)
        {
            Value = value;
        }
    }
}
