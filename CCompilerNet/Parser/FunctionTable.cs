using CCompilerNet.CodeGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CCompilerNet.Parser
{
    public class FunctionTable
    {
        // ID : FunctionSymbol
        private Dictionary<string, FunctionSymbol> _fs;
        private TypeBuilder _typeBuilder;
        private List<string> _lastIds;

        public FunctionTable(TypeBuilder typeBuilder)
        {
            _fs = new Dictionary<string, FunctionSymbol>();
            _typeBuilder = typeBuilder;
            _lastIds = new List<string>();

            // putting default functions: print and put
            _fs.Add("print", new FunctionSymbol(null, "void", null));
            _fs.Add("put", new FunctionSymbol(null, "void", null));
        }

        public string GetLastId()
        {
            return _lastIds.Count >= 2 ? _lastIds[_lastIds.Count - 2] : "";
        }

        public void Define(ASTNode root)
        {
            string type = SemanticHelper.GetFunctionType(root);
            string id = SemanticHelper.GetFunctionId(root);

            if (FunctionSymbolExists(id))
            {
                ErrorHandler.Error($"{id}, a function with the same name already exists");
                return;
            }

            _lastIds.Add(id);
            List<string> parmTypeList = SemanticHelper.GetFunctionParmTypes(root);
            List<string> clearParmTypeList = new List<string>(parmTypeList);

            // clearing the arr suffix
            clearParmTypeList = clearParmTypeList.Select((x) => x.Replace(" arr", "")).ToList();

            _fs.Add(id, new FunctionSymbol(
                    clearParmTypeList,
                    type, 
                    _typeBuilder.DefineMethod(
                        id, 
                        System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static,
                        VMWriter.ConvertToType(type, false),
                        VMWriter.ConvertToType(parmTypeList?.ToArray())
                    )
                )
            );
        }

        public FunctionSymbol GetFunctionSymbol(string name)
        {
            if (!FunctionSymbolExists(name))
            {
                return null;
            }

            return _fs[name];
        }

        public bool FunctionSymbolExists(string name)
        {
            return name != null && _fs.ContainsKey(name);
        }
    }
}
