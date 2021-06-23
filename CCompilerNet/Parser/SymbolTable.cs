using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CCompilerNet.Parser
{
    public enum Kind
    {
        STATIC,
        LOCAL,
        ARG,
        GLOBAL
    }

    public class SymbolTable
    {
        private Dictionary<string, Symbol> _st;

        private SymbolTable _head;
        private SymbolTable _next;

        private int _staticIndex;
        private int _localIndex;
        private int _argIndex;
        private int _globalIndex;

        public SymbolTable(SymbolTable next)
        {
            _st = new Dictionary<string, Symbol>();

            _next = next;
            _head = null;

            _staticIndex = 0;
            _localIndex = 0;
            _argIndex = 0;
            _globalIndex = 0;
        }

        public void Reset()
        {
            _st = new Dictionary<string, Symbol>();
            _staticIndex = 0;
            _localIndex = 0;
            _argIndex = 0;
            _globalIndex = 0;
            _head = null;
        }

        public SymbolTable StartSubRoutine()
        {
            _head = new SymbolTable(this);
            return _head;
        }

        public SymbolTable GetNext()
        {
            return _next;
        }

        public void Define(string name, string type, Kind kind, bool isArray = false)
        {
            Symbol symbol = new Symbol(type, kind);
            // if passed as an argument, must save as an array type
            symbol.IsArray = isArray;

            if (SymbolExists(name))
            {
                Console.WriteLine($"Error: {name} already exists in the current scope");
                return;
            }

            switch(kind)
            {
                case Kind.STATIC:
                    symbol.Index = _staticIndex;
                    _staticIndex++;
                    break;
                case Kind.LOCAL:
                    symbol.Index = _localIndex;
                    _localIndex++;
                    break;
                case Kind.ARG:
                    symbol.Index = _argIndex;
                    _argIndex++;
                    break;
                case Kind.GLOBAL:
                    symbol.Index = _globalIndex;
                    _globalIndex++;
                    break;
            }

            _st.Add(name, symbol);
        }

        public Symbol GetSymbol(string name)
        {
            if (_next == null)
            {
                return SymbolExists(name) ? _st[name] : null;
            }

            if (SymbolExists(name))
            {
                return _st[name];
            }

            return _next.GetSymbol(name);
        }

        public void Define(string name, string type, Kind kind, LocalBuilder localBuilder)
        {
            Symbol symbol = new Symbol(type, kind);

            if (SymbolExists(name))
            {
                Console.WriteLine($"Error: {name} already exists in the current scope");
                return;
            }

            switch (kind)
            {
                case Kind.STATIC:
                    symbol.Index = _staticIndex;
                    _staticIndex++;
                    break;

                case Kind.LOCAL:
                    symbol.Index = _localIndex;
                    localBuilder.SetLocalSymInfo(name);
                    symbol.LocalBuilder = localBuilder;
                    _localIndex++;
                    break;

                case Kind.ARG:
                    symbol.Index = _argIndex;
                    _argIndex++;
                    break;

                case Kind.GLOBAL:
                    symbol.Index = _globalIndex;
                    _globalIndex++;
                    break;
            }

            _st.Add(name, symbol);
        }

        public void Define(string name, string type, Kind kind, int arrayLength, LocalBuilder localBuilder)
        {
            Symbol symbol = new Symbol(type, kind, arrayLength);

            if (SymbolExists(name))
            {
                Console.WriteLine($"Error: {name} already exists in the current scope");
                return;
            }

            switch (kind)
            {
                case Kind.STATIC:
                    symbol.Index = _staticIndex;
                    _staticIndex++;
                    break;
                case Kind.LOCAL:
                    symbol.Index = _localIndex;
                    localBuilder.SetLocalSymInfo(name);
                    symbol.LocalBuilder = localBuilder;
                    _localIndex++;
                    break;
                case Kind.ARG:
                    symbol.Index = _argIndex;
                    _argIndex++;
                    break;
                case Kind.GLOBAL:
                    symbol.Index = _globalIndex;
                    _globalIndex++;
                    break;
            }

            _st.Add(name, symbol);
        }

        public void Define(string name, string type, Kind kind, FieldBuilder fieldBuilder)
        {
            Symbol symbol = new Symbol(type, kind);

            if (SymbolExists(name))
            {
                Console.WriteLine($"Error: {name} already exists in the current scope");
                return;
            }

            switch (kind)
            {
                case Kind.STATIC:
                    symbol.Index = _staticIndex;
                    _staticIndex++;
                    break;

                case Kind.LOCAL:
                    symbol.Index = _localIndex;
                    _localIndex++;
                    break;

                case Kind.ARG:
                    symbol.Index = _argIndex;
                    _argIndex++;
                    break;

                case Kind.GLOBAL:
                    symbol.Index = _globalIndex;
                    symbol.FieldBuilder = fieldBuilder;
                    _globalIndex++;
                    break;
            }

            _st.Add(name, symbol);
        }

        public void Define(string name, string type, Kind kind, int arrayLength, FieldBuilder fieldBuilder)
        {
            Symbol symbol = new Symbol(type, kind, arrayLength);

            if (SymbolExists(name))
            {
                Console.WriteLine($"Error: {name} already exists in the current scope");
                return;
            }

            switch (kind)
            {
                case Kind.STATIC:
                    symbol.Index = _staticIndex;
                    _staticIndex++;
                    break;

                case Kind.LOCAL:
                    symbol.Index = _localIndex;
                    _localIndex++;
                    break;

                case Kind.ARG:
                    symbol.Index = _argIndex;
                    _argIndex++;
                    break;

                case Kind.GLOBAL:
                    symbol.Index = _globalIndex;
                    symbol.FieldBuilder = fieldBuilder;
                    _globalIndex++;
                    break;
            }

            _st.Add(name, symbol);
        }

        public bool SymbolExists(string name)
        {
            return _st.ContainsKey(name);
        }

        public int VarCount(Kind kind)
        {
            switch(kind)
            {
                case Kind.STATIC:
                    return _staticIndex;
                case Kind.LOCAL:
                    return _localIndex;
                case Kind.ARG:
                    return _argIndex;
                case Kind.GLOBAL:
                    return _globalIndex;
            }

            // error
            return -1;
        }
    }
}
