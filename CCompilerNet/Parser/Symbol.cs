using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CCompilerNet.Parser
{
    public class Symbol
    {
        public string Type { get; set; }
        public Kind Kind { get; set; }
        public int Index { get; set; }
        public LocalBuilder LocalBuilder { get; set; }
        public FieldBuilder FieldBuilder { get; set; }
        public bool IsArray { get; set; }
        public int ArrayLength { get; set; }

        public Symbol(string type, Kind kind)
        {
            Type = type;
            Kind = kind;
            Index = 0;
            IsArray = false;
            ArrayLength = 0;
        }

        public Symbol(string type, Kind kind, int arrayLength) : this(type, kind)
        {
            IsArray = true;
            ArrayLength = arrayLength;
        }
    }
}
