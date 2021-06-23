using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CCompilerNet.Parser
{
    public class FunctionSymbol
    {
        public List<string> ParmTypeList { get; }
        public string Type { get; }
        public MethodBuilder MethodBuilder { get; }

        public FunctionSymbol(List<string> parmTypeList, string type, MethodBuilder methodBuilder)
        {
            ParmTypeList = parmTypeList;
            Type = type;
            MethodBuilder = methodBuilder;
        }
    }
}
