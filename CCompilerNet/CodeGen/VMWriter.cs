using CCompilerNet.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using CCompilerNet.Lex;

namespace CCompilerNet.CodeGen
{
    public class VMWriter
    {
        private AppDomain _domain;
        private AssemblyBuilder _asmBuilder;
        private ModuleBuilder _moduleBuilder;
        private TypeBuilder _typeBuilder;

        //private ConstructorBuilder _ctorBuilder;
        private MethodBuilder _cctorBuilder;
        private ILGenerator _cctorIL;

        private MethodBuilder _methodBuilder;
        private ILGenerator _currILGen;

        // global scope table
        public SymbolTable SymbolTable { get; set; }
        // function table
        public FunctionTable FunctionTable { get; }

        public VMWriter(string output)
        {
            _domain = AppDomain.CurrentDomain;
            _asmBuilder = _domain.DefineDynamicAssembly(
                new AssemblyName("MyASM"), AssemblyBuilderAccess.Save);
            _moduleBuilder = _asmBuilder.DefineDynamicModule(
                "MyASM", output, true);
            _typeBuilder = _moduleBuilder.DefineType("Program",
                TypeAttributes.Class | TypeAttributes.Public);

            _cctorBuilder = _typeBuilder.DefineMethod(
                ".cctor",
                MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static
                , typeof(void),
                new Type[0]
                );

            _cctorIL = _cctorBuilder.GetILGenerator();

            SymbolTable = new SymbolTable(null);
            FunctionTable = new FunctionTable(_typeBuilder);
        }

        public ILGenerator GetCurrentILGenerator()
        {
            return _currILGen;
        }

        private void Push(char value, bool isGlobal = false)
        {
            (isGlobal ? _cctorIL : _currILGen).Emit(OpCodes.Ldc_I4, value);
        }

        private void Push(int value, bool isGlobal = false)
        {
            (isGlobal ? _cctorIL : _currILGen).Emit(OpCodes.Ldc_I4, value);
        }

        private void Push(bool value, bool isGlobal = false)
        {
            (isGlobal ? _cctorIL : _currILGen).Emit(OpCodes.Ldc_I4, value ? 1 : 0);
        }

        private void Push(string id, bool isGlobal)
        {
            Symbol symbol = SymbolTable.GetSymbol(id);
            OpCode opCode = GetLdCode(symbol.Kind);

            if (symbol == null)
            {
                Console.Error.WriteLine($"Error: {id} is not declared.");
                Environment.Exit(-1);
            }

            if (isGlobal)
            {
                _cctorIL.Emit(OpCodes.Ldsfld, symbol.FieldBuilder);
                return;
            }

            switch(symbol.Kind)
            {
                case Kind.LOCAL:
                    _currILGen.Emit(opCode, symbol.LocalBuilder);
                    break;
                case Kind.GLOBAL:
                    _currILGen.Emit(opCode, symbol.FieldBuilder);
                    break;
                case Kind.ARG:
                    _currILGen.Emit(opCode, symbol.Index);
                    break;
            }
        }

        private void PushString(string str, bool isGlobal = false)
        {
            (isGlobal ? _cctorIL : _currILGen).Emit(OpCodes.Ldstr, str);
        }

        private OpCode GetLdCode(Kind kind)
        {
            switch(kind)
            {
                case Kind.LOCAL:
                    return OpCodes.Ldloc;
                case Kind.ARG:
                    return OpCodes.Ldarg;
                case Kind.GLOBAL:
                    return OpCodes.Ldsfld;
                default:
                    return OpCodes.Ldloc;
            }
        }

        private OpCode GetStCode(Kind kind)
        {
            switch (kind)
            {
                case Kind.LOCAL:
                    return OpCodes.Stloc;
                case Kind.ARG:
                    return OpCodes.Starg;
                case Kind.GLOBAL:
                    return OpCodes.Stsfld;
                default:
                    return OpCodes.Stloc;
            }
        }

        public LocalBuilder GetLocalBuilder(string type)
        {
            return _currILGen.DeclareLocal(ConvertToType(type, false)); //literally made for one line (check compileiterstmt)
        }

        public void CodeWriteFunction(ASTNode root)
        {
            string id = SemanticHelper.GetFunctionId(root);
            FunctionSymbol symbol = FunctionTable.GetFunctionSymbol(id);

            if (_currILGen != null)
            {
                // returning default value if no return specified
                switch(FunctionTable.GetFunctionSymbol(FunctionTable.GetLastId()).Type)
                {
                    case "int":
                        Push(0);
                        break;
                    case "char":
                        Push('.');
                        break;
                    case "bool":
                        Push(0);
                        break;
                }

                _currILGen.Emit(OpCodes.Ret);
            }

            _methodBuilder = FunctionTable.GetFunctionSymbol(id).MethodBuilder;
            _currILGen = _methodBuilder.GetILGenerator();
        }

        public void Save(string path)
        {
            if (FunctionTable.FunctionSymbolExists("main"))
            {
                // returning default value if no return specified
                switch (FunctionTable.GetFunctionSymbol("main").Type)
                {
                    case "int":
                        Push(0);
                        break;
                    case "char":
                        Push('.');
                        break;
                    case "bool":
                        Push(0);
                        break;
                }

                _currILGen.Emit(OpCodes.Ret);
                _asmBuilder.SetEntryPoint(FunctionTable.GetFunctionSymbol("main").MethodBuilder, PEFileKinds.ConsoleApplication);
            }
            else
            {
                // creating an empty method to set an entry point
                // to make the exe a valid program
                _methodBuilder = _typeBuilder.DefineMethod(
                "Main", MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Static,
                typeof(void), new Type[] { typeof(string[]) });
                _methodBuilder.GetILGenerator().Emit(OpCodes.Ret);
                _asmBuilder.SetEntryPoint(_methodBuilder, PEFileKinds.ConsoleApplication);
            }

            _cctorIL.Emit(OpCodes.Ret);

            _typeBuilder.CreateType();

            File.Delete(path);
            _asmBuilder.Save(path);
        }

        public void CodeWriteReturnStmt(ASTNode root)
        {
            if (root.Children.Count > 1)
            {
                CodeWriteExp(root.Children[1]);   //sends the expression after the return
            }
            _currILGen.Emit(OpCodes.Ret);
        }

        public void CodeWriteStmtList(ASTNode root)
        {
            // iterating through the statements
            foreach(ASTNode child in root.Children)
            {
                CodeWriteStmt(child);
            }
        }

        public void CodeWriteStmt(ASTNode root)
        {
            switch(root.Children[0].Tag)
            {
                case "returnStmt":
                    CodeWriteReturnStmt(root.Children[0]);
                    break;
                case "selectStmt":
                    CodeWriteSelectStmt(root.Children[0]);
                    break;
                case "expStmt":
                    string id = GetID(root.Children[0]);
                    FunctionSymbol symbol = FunctionTable.GetFunctionSymbol(id);

                    if (root.Children[0].Children[0].Children.Count > 1 || symbol != null)
                    {
                        string type = CodeWriteExp(root.Children[0].Children[0]);

                        if (!type.Contains("expression") && symbol.Type != "void")
                        {
                            GetCurrentILGenerator().Emit(OpCodes.Pop);
                        }
                    }
                    break;
                case "compoundStmt":
                    CodeWriteCompoundStmt(root.Children[0]);
                    break;
                case "iterStmt":
                    CodeWriteIterStmt(root.Children[0]);
                    break;
            }
        }
        public void CodeWriteIterStmt(ASTNode root)
        {
            if (root.Children[0].Token.Value == "for")
            {
                SymbolTable = SymbolTable.StartSubRoutine();
                SymbolTable.Define(root.Children[1].Token.Value, "int", Kind.LOCAL, GetLocalBuilder("int"));
                CodeWriteForLoop(root);
                SymbolTable = SymbolTable.GetNext();

            }
            else
            {
                CodeWriteWhileLoop(root);
            }
        }
        public void CodeWriteCompoundStmt(ASTNode root)
        {
            SymbolTable = SymbolTable.StartSubRoutine();
            // if localDecls exists then stmt is the second child,
            // otherwise stmts are the only children
            int index = 0;

            if (root.Children.Count == 0)
            {
                return;
            }

            if (root.Children[0].Tag == "localDecls")
            {
                foreach (ASTNode child in root.Children[0].Children)
                {
                    AddSymbolsFromScopedVarDecl(child);
                }
                index = 1;
            }
            
            foreach (ASTNode child in root.Children[index].Children)
            {
                CodeWriteStmt(child);
            }

            SymbolTable = SymbolTable.GetNext();
        }

        public void CodeWriteSelectStmt(ASTNode root)
        {
            
            // checking if the root is just a single if
            // with else
            if (root.Children.Count > 2)
            {
                CodeWriteSimpleExp(root.Children[0]);
                Label toEnd = _currILGen.DefineLabel();
                Label toElse = _currILGen.DefineLabel();

                // branching to else statements if the condition is false
                _currILGen.Emit(OpCodes.Brfalse, toElse);

                // translating the statements inside the if
                CodeWriteStmt(root.Children[1]);

                // finishing the if statement
                _currILGen.Emit(OpCodes.Br, toEnd);

                _currILGen.MarkLabel(toElse);
                // translating the statements inside else
                CodeWriteStmt(root.Children[2]);
                _currILGen.MarkLabel(toEnd);
            }
            // without else
            else
            {
                CodeWriteSimpleExp(root.Children[0]);
                Label toEnd = _currILGen.DefineLabel();
                _currILGen.Emit(OpCodes.Brfalse, toEnd);
                CodeWriteStmt(root.Children[1]);
                _currILGen.MarkLabel(toEnd);
            }
        }

        public void CodeWriteForLoop(ASTNode root)
        {
            Label toLoopTop = _currILGen.DefineLabel();
            Label toCondition = _currILGen.DefineLabel();
            Symbol symbol = SymbolTable.GetSymbol(root.Children[1].Token.Value);

            CodeWriteSimpleExp(root.Children[2].Children[0]); //Pushes first value of iter range into stack
            _currILGen.Emit(OpCodes.Stloc, symbol.LocalBuilder.LocalIndex); //pop into "i", which will always be the first local variable registered;

            _currILGen.Emit(OpCodes.Br, toCondition);

            // translating the statements
            _currILGen.MarkLabel(toLoopTop);

            CodeWriteStmt(root.Children[4]);
            
            _currILGen.Emit(OpCodes.Ldloc, symbol.LocalBuilder.LocalIndex);

            if (root.Children[2].Children.Count > 2)   //checks if theres a "by"
            {
                CodeWriteSimpleExp(root.Children[2].Children[2]);  //push the size of each jump
            }
            else
            {
                Push(1); //push the size of each jump
            }

            _currILGen.Emit(OpCodes.Add);
            _currILGen.Emit(OpCodes.Stloc, symbol.LocalBuilder.LocalIndex);  //pop value into "i"

            //translate condition
            _currILGen.MarkLabel(toCondition);
            _currILGen.Emit(OpCodes.Ldloc, symbol.LocalBuilder.LocalIndex); //push "i"
            // i <= range
            CodeWriteSimpleExp(root.Children[2].Children[1]); //push range
            _currILGen.Emit(OpCodes.Cgt);
            _currILGen.Emit(OpCodes.Ldc_I4_0);
            _currILGen.Emit(OpCodes.Ceq);

            _currILGen.Emit(OpCodes.Brtrue, toLoopTop);


        }
        public void CodeWriteWhileLoop(ASTNode root)
        {
            Label toLoopTop = _currILGen.DefineLabel();
            Label toCondition = _currILGen.DefineLabel();

            _currILGen.Emit(OpCodes.Br, toCondition);
            
            _currILGen.MarkLabel(toLoopTop);
            // translating the statements
            CodeWriteStmt(root.Children[3]);
            _currILGen.MarkLabel(toCondition);
            // translating the condition
            CodeWriteSimpleExp(root.Children[1]);
            _currILGen.Emit(OpCodes.Brtrue, toLoopTop);
        }

        public string CodeWriteArray(Symbol arr2, Symbol arr1)
        {
            OpCode op1 = GetLdCode(arr1.Kind);   //random assignment
            OpCode op2 = GetLdCode(arr2.Kind);

            if (arr2.Type != arr1.Type)
            {
                Console.Error.WriteLine("Types mismatch");
                Environment.Exit(-1);
            }

            for (int i = 0; i < arr1.ArrayLength && i < arr2.ArrayLength; i++)
            {
                if (arr1.Kind == Kind.GLOBAL)
                {
                    _currILGen.Emit(op1, arr1.FieldBuilder);
                }
                else
                {
                    _currILGen.Emit(op1, arr1.Index);
                }
                Push(i);
                if (arr2.Kind == Kind.GLOBAL)
                {
                    _currILGen.Emit(op1, arr2.FieldBuilder);
                }
                else
                {
                    _currILGen.Emit(op1, arr2.Index);
                }
                Push(i);
                _currILGen.Emit(OpCodes.Ldelem, ConvertToType(arr1.Type, false)); //push 2nd array value at index i
                _currILGen.Emit(OpCodes.Stelem, ConvertToType(arr1.Type, false)); //pop into 1st array at index i
            }

            //return arr1.Type;
            return "expression";
        }

        public string CodeWriteExp(ASTNode exp)
        {
            if (exp.Children.Count == 3 && exp.Children[1].Token != null)
            {

                if (exp.Children[2].Children.Count < 2)
                {
                    Symbol symbol = SymbolTable.GetSymbol(GetID(exp.Children[0]));
                    string type;

                    // possibly will be removed
                    // the condition is checked in the parser
                    if (symbol == null)
                    {
                        ErrorHandler.VariableIsNotDeclaredError(exp.Children[0].Token.Value);
                    }

                    if (symbol.IsArray)
                    {
                        Push(GetID(exp.Children[0]), false);

                        if (PushIndex(exp.Children[0]) == "no index")   //trying to reset/copy an array
                        {
                            if (exp.Children[1].Token.Value != "=")
                            {
                                ErrorHandler.Error("Wrong operator, must be = when working with the array itself");
                            }
                            _currILGen.Emit(OpCodes.Pop);   //get rid of earlier push, since we wont be working with index anymore
                            return CodeWriteArray(SymbolTable.GetSymbol(GetID(exp.Children[2])), symbol);
                        }
                    }

                    type = CodeWriteExp(exp.Children[2]);
                    //Pop the value since the order of numbers is important
                    if (exp.Children[1].Token.Value == "-=" || exp.Children[1].Token.Value == "/=")
                    {
                        _currILGen.Emit(OpCodes.Pop);
                    }

                    if (!type.Contains(symbol.Type))
                    {
                        ErrorHandler.Error("Error: type missmatch");
                    }

                    OpCode op = GetStCode(symbol.Kind);
                    
                    switch (exp.Children[1].Token.Value)
                    {
                        case "+=":

                            Push(GetID(exp.Children[0]), false);
                            if (symbol.IsArray)
                            {
                                PushIndex(exp.Children[0]);
                                _currILGen.Emit(OpCodes.Ldelem, ConvertToType(symbol.Type, false));
                            }

                            _currILGen.Emit(OpCodes.Add);
                            break;

                        case "-=":
                            //order of push is important so pushes the 2nd part again and pops the remaining one 
                            Push(GetID(exp.Children[0]), false);

                            if (symbol.IsArray)
                            {
                                PushIndex(exp.Children[0]);
                                _currILGen.Emit(OpCodes.Ldelem, ConvertToType(symbol.Type, false));
                            }

                            CodeWriteExp(exp.Children[2]);
                            _currILGen.Emit(OpCodes.Sub);
                            break;

                        case "*=":
                            Push(GetID(exp.Children[0]), false);

                            if (symbol.IsArray)
                            {
                                PushIndex(exp.Children[0]);
                                _currILGen.Emit(OpCodes.Ldelem, ConvertToType(symbol.Type, false));
                            }

                            _currILGen.Emit(OpCodes.Mul);
                            break;

                        case "/=":
                            //order of push is important so pushes the 2nd part again and pops the remaining one 

                            Push(GetID(exp.Children[0]), false);

                            if (symbol.IsArray)
                            {
                                PushIndex(exp.Children[0]);
                                _currILGen.Emit(OpCodes.Ldelem, ConvertToType(symbol.Type, false));
                            }

                            CodeWriteExp(exp.Children[2]);
                            _currILGen.Emit(OpCodes.Div);
                            break;
                    }

                    if (symbol.IsArray)
                    {
                        _currILGen.Emit(OpCodes.Stelem, ConvertToType(symbol.Type, false));
                    }
                    else
                    {
                        if (symbol.Kind == Kind.GLOBAL)
                        {
                            _currILGen.Emit(op, symbol.FieldBuilder);
                        }
                        else
                        {
                            _currILGen.Emit(op, symbol.Index);
                        }
                    }

                    return "expression";
                }
                else
                {
                    ErrorHandler.Error("Can only use 1 operator in an expression");
                }
            }

            if (exp.Children.Count == 2 && exp.Children[1].Token != null)
            {
                string op = exp.Children[1].Token.Value;
                if (op == "++" || op == "--")
                {
                    Symbol symbol = SymbolTable.GetSymbol(GetID(exp.Children[0]));
                    OpCode opCode = symbol.Kind == Kind.LOCAL ? OpCodes.Stloc : OpCodes.Starg;

                    if (symbol.Type != "int")
                    {
                        ErrorHandler.Error("type missmatch");
                    }

                    Push(GetID(exp.Children[0]), false);

                    if (symbol.IsArray)
                    {
                        PushIndex(exp.Children[0]);
                        Push(GetID(exp.Children[0]), false);
                        PushIndex(exp.Children[0]);
                        _currILGen.Emit(OpCodes.Ldelem, ConvertToType(symbol.Type, false));
                    }

                    Push(1);

                    if (op == "++")
                    {
                        _currILGen.Emit(OpCodes.Add);
                    }
                    else
                    {
                        _currILGen.Emit(OpCodes.Sub);
                    }

                    if (symbol.IsArray)
                    {
                        _currILGen.Emit(OpCodes.Stelem, ConvertToType(symbol.Type, false));
                    }
                    else
                    {
                        _currILGen.Emit(opCode, symbol.Index);
                    }

                    return "expression";
                }
            }

            return CodeWriteSimpleExp(exp);
        }

        public string CodeWriteSimpleExp(ASTNode exp, bool isGlobal = false)
        {
            ILGenerator il = isGlobal ? _cctorIL : _currILGen;

            if (exp.Tag == "constant" || exp.Tag == "mutable")
            {
                string value = exp.Children[0].Token.Value;

                if (exp.Tag == "constant")
                {
                    if (value.Contains('"'))
                    {
                        // removing " from token value (string literals come with ")
                        value = value.Replace("\"", "");
                        PushString(value, isGlobal);
                        return "string";
                    }
                    if (value == "false" || value == "true")
                    {
                        Push(bool.Parse(value), isGlobal);
                        return "bool";
                    }

                    if (value.Contains("'"))
                    {
                        Push(value.ElementAt(1), isGlobal);
                        return "char";
                    }

                    Push(int.Parse(value), isGlobal);
                    return "int";
                }
                else
                {
                    // pushing the id
                    Push(value, isGlobal);

                    if (exp.Children.Count == 4)  //if variable is an array with index
                    {
                        CodeWriteSimpleExp(exp.Children[2], isGlobal);

                        il.Emit(OpCodes.Ldelem, ConvertToType(SymbolTable.GetSymbol(value).Type, false));

                        return SymbolTable.GetSymbol(value).Type;
                    }

                    return SymbolTable.GetSymbol(value).Type;
                }
            }

            if (exp.Tag == "call")
            {
                FunctionSymbol func = FunctionTable.GetFunctionSymbol(exp.Children[0].Token.Value);
                int num = 0;

                if (func == null)
                {
                    ErrorHandler.FunctionIsNotDeclaredError(exp.Children[0].Token.Value);
                }
                
                if (exp.Children[0].Token.Value == "print")
                {
                    if (exp.Children.Count < 2)
                    {
                        ErrorHandler.Error("print function requires arguments");
                    }
                    CodeWritePrint(exp.Children[1].Children[0]);
                    return "expression";
                }

                if (exp.Children[0].Token.Value == "put")
                {
                    if (exp.Children.Count < 2)
                    {
                        ErrorHandler.Error("put function requires arguments");
                    }

                    CodeWritePut(exp.Children[1].Children[0]);
                    return "expression";
                }

                if (func.ParmTypeList != null && func.ParmTypeList.Count > 0) //checks if argument list is empty
                {
                    num = exp.Children[1].Children[0].Children.Count;

                    if (num != func.ParmTypeList.Count)
                    {
                        Console.Error.WriteLine($"Error: wrong number of parameters when calling a function.");
                        Environment.Exit(-1);
                    }

                    for (int i = 0; i < num; i++)
                    {
                        if (!CodeWriteSimpleExp(exp.Children[1].Children[0].Children[i], isGlobal).Contains(func.ParmTypeList[i]))
                        {
                            ErrorHandler.Error("Error: parameter type missmatch");
                        }
                    }
                }

                il.Emit(OpCodes.Call, func.MethodBuilder);  //call func

                return func.Type;
            }

            if (exp.Tag == "operator")
            {
                switch (exp.Children[0].Token.Value)
                {
                    case "+":
                        il.Emit(OpCodes.Add);
                        break;
                    case "-":
                        il.Emit(OpCodes.Sub);
                        break;
                    case "*":
                        il.Emit(OpCodes.Mul);
                        break;
                    case "/":
                        il.Emit(OpCodes.Div);
                        break;
                }

                return "operator";
            }

            if (exp.Tag == "unaryOperator")
            {
                switch (exp.Children[0].Token.Value)
                {
                    case "-":
                        il.Emit(OpCodes.Neg);
                        break;
                    case "not":
                        il.Emit(OpCodes.Ldc_I4_0);
                        il.Emit(OpCodes.Ceq);
                        break;
                    case "*":
                        il.Emit(OpCodes.Ldlen);
                        break;
                    case "?":
                        Label toPositiveCase = il.DefineLabel();
                        Label toEnd = il.DefineLabel();

                        il.Emit(OpCodes.Ldc_I4, 0);
                        il.Emit(OpCodes.Clt);
                        // if value is greater than zero, branch to the end
                        il.Emit(OpCodes.Brfalse, toPositiveCase);
                        il.Emit(OpCodes.Neg);
                        il.Emit(OpCodes.Callvirt, typeof(Random).GetMethod("Next", new Type[] { typeof(Int32) }));
                        il.Emit(OpCodes.Neg);
                        il.Emit(OpCodes.Br, toEnd);

                        il.MarkLabel(toPositiveCase);
                        il.Emit(OpCodes.Callvirt, typeof(Random).GetMethod("Next", new Type[] { typeof(Int32) }));
                        il.MarkLabel(toEnd);
                        break;
                }
            }

            if (exp.Children.Count > 1)
            {
                if (exp.Tag == "andExpression")
                {
                    string type = CodeWriteSimpleExp(exp.Children[0], isGlobal);

                    for (int i = 1; i < exp.Children.Count; i++)
                    {
                        if (!CodeWriteSimpleExp(exp.Children[i], isGlobal).Contains(type))
                        {
                            ErrorHandler.Error("type missmatch");
                        }
                        il.Emit(OpCodes.And);
                    }

                    return type;
                }

                if (exp.Tag == "simpleExpression")
                {
                    string type = CodeWriteSimpleExp(exp.Children[0], isGlobal);

                    for (int i = 1; i < exp.Children.Count; i++)
                    {
                        if (!CodeWriteSimpleExp(exp.Children[i], isGlobal).Contains(type))
                        {
                            ErrorHandler.Error("type missmatch");
                        }
                        il.Emit(OpCodes.Or);
                    }

                    return type;
                }

                if (exp.Tag == "relExpression")
                {
                    //push both members of the rel expression
                    CodeWriteSimpleExp(exp.Children[0], isGlobal);
                    CodeWriteSimpleExp(exp.Children[2], isGlobal);

                    switch (exp.Children[1].Children[0].Token.Value) // switch case of rel expression operator
                    {
                        case "==":
                            il.Emit(OpCodes.Ceq);
                            break;

                        case "!=":
                            il.Emit(OpCodes.Ceq);
                            il.Emit(OpCodes.Ldc_I4_0);
                            il.Emit(OpCodes.Ceq);
                            break;

                        case ">":
                            il.Emit(OpCodes.Cgt);
                            break;

                        case "<":
                            il.Emit(OpCodes.Clt);
                            break;

                        case ">=":
                            il.Emit(OpCodes.Clt);
                            il.Emit(OpCodes.Ldc_I4_0);
                            il.Emit(OpCodes.Ceq);
                            break;

                        case "<=":
                            il.Emit(OpCodes.Cgt);
                            il.Emit(OpCodes.Ldc_I4_0);
                            il.Emit(OpCodes.Ceq);
                            break;
                    }

                    return "bool";
                }

                if (exp.Tag == "mulExpression" || exp.Tag == "sumExpression")
                {
                    string type = CodeWriteSimpleExp(exp.Children[0], isGlobal);    //push 1st exp

                    if (!CodeWriteTag(exp.Children[1], type.Replace("call ", ""), isGlobal))  //code generation of tag
                    {
                        ErrorHandler.Error("type missmatch");
                    }

                    return type;
                }

                if (exp.Tag == "unaryExp")
                {
                    if (exp.Children[1].Children[0].Token.Value == "?")
                    {
                        il.Emit(OpCodes.Newobj, typeof(Random).GetConstructor(new Type[] { }));
                        // ? only works with ints
                        string check = CodeWriteSimpleExp(exp.Children[0], isGlobal);

                        if (!check.Contains("int"))
                        {
                            ErrorHandler.Error("type missmatch");
                        }
                    }

                    string type = CodeWriteSimpleExp(exp.Children[0], isGlobal); //push exp

                    CodeWriteSimpleExp(exp.Children[1], isGlobal); // pushing the operator

                    return type;
                }

                if (exp.Tag == "unaryRelExpression")
                {
                    string type = CodeWriteSimpleExp(exp.Children[1], isGlobal); //push exp

                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ceq);

                    return type;
                }
            }

            if (exp.Children.Count == 1)
            {
                return CodeWriteSimpleExp(exp.Children[0], isGlobal);     //move over to next member
            }

            return null;
        }

        public void CodeWritePut(ASTNode args)
        {
            Symbol symbol = null;
            foreach (ASTNode child in args.Children)
            {
                symbol = SymbolTable.GetSymbol(GetID(child));

                if (symbol == null)
                {
                    ErrorHandler.VariableIsNotDeclaredError(GetID(child));
                }

                _currILGen.Emit(OpCodes.Call, typeof(Console).GetMethod("ReadLine"));

                switch (symbol.Type)
                {
                    case "int":
                        _currILGen.Emit(OpCodes.Call, typeof(Int32).GetMethod("Parse", new Type[] { typeof(string) }) );
                        break;

                    case "char":
                        _currILGen.Emit(OpCodes.Call, typeof(Char).GetMethod("Parse", new Type[] { typeof(string) }));
                        break;

                    case "bool":
                        _currILGen.Emit(OpCodes.Call, typeof(Boolean).GetMethod("Parse", new Type[] { typeof(string) }));
                        break;
                }

                if (symbol.IsArray)
                {
                    switch (symbol.Kind)
                    {
                        case Kind.LOCAL:
                            _currILGen.Emit(OpCodes.Ldloc, symbol.Index);
                            break;

                        case Kind.ARG:
                            _currILGen.Emit(OpCodes.Ldarg, symbol.Index);
                            break;

                        case Kind.GLOBAL:
                            _currILGen.Emit(OpCodes.Ldsfld, symbol.FieldBuilder);
                            break;
                    }

                }
                else
                {
                    switch (symbol.Kind)
                    {
                        case Kind.LOCAL:
                            _currILGen.Emit(OpCodes.Stloc, symbol.Index);
                            break;

                        case Kind.ARG:
                            _currILGen.Emit(OpCodes.Starg, symbol.Index);
                            break;

                        case Kind.GLOBAL:
                            _currILGen.Emit(OpCodes.Stsfld, symbol.FieldBuilder);
                            break;
                    }
                }
            }
        }

        public void CodeWritePrint(ASTNode args)
        {
            string type = CodeWriteExp(args.Children[0]);
            List<Type> types = new List<Type> { ConvertToType(type, false) };
            if (type != "string" && args.Children.Count > 1)
            {
                ErrorHandler.Error("incorrect use of print");
            }
            else if (args.Children.Count > 1 )
            {
                for (int i = 1; i < args.Children.Count; i++)
                {
                    type = CodeWriteExp(args.Children[i]);

                    types.Add(ConvertToType(type, false));

                    _currILGen.Emit(OpCodes.Box, types[i]);
                }
            }

            _currILGen.Emit(OpCodes.Call, typeof(Console).GetMethod("Write", types.ToArray()));
        }

        public bool CodeWriteTag(ASTNode exp, string type, bool isGlobal)
        {
            string currType;
            foreach (ASTNode child in exp.Children)
            {
                if (child.Tag == "mulExpressionTag" || child.Tag == "sumExpressionTag")
                {
                    CodeWriteTag(child, type, isGlobal);
                }
                else
                {
                    currType = CodeWriteExp(child);

                    if (currType != "operator" && !currType.Contains(type))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void CodeWriteScopedVarDecl(ASTNode root, ASTNode exp, string name)
        {
            Symbol symbol = SymbolTable.GetSymbol(name);

            if (symbol == null)
            {
                ErrorHandler.VariableIsNotDeclaredError(name);
            }

            if (symbol.IsArray)
            {
                for (int i = 0; i < symbol.ArrayLength; i++)
                {
                    _currILGen.Emit(OpCodes.Ldloc, symbol.LocalBuilder.LocalIndex);
                    _currILGen.Emit(OpCodes.Ldc_I4, i);
                    string type = CodeWriteSimpleExp(exp);
                    if (!type.Contains(symbol.Type))
                    {
                        ErrorHandler.Error("type missmatch");
                    }
                    _currILGen.Emit(OpCodes.Stelem, ConvertToType(type, false));
                }
            }
            else
            {
                string type = CodeWriteSimpleExp(exp);

                if (!type.Contains(symbol.Type))
                {
                    ErrorHandler.Error("type missmatch");
                }

                _currILGen.Emit(OpCodes.Stloc, symbol.LocalBuilder.LocalIndex);
            }
        }

        public void CodeWriteVarDecl(ASTNode root, ASTNode exp, string name)
        {
            Symbol symbol = SymbolTable.GetSymbol(name);

            if (symbol == null)
            {
                ErrorHandler.VariableIsNotDeclaredError(name);
            }

            if (symbol.IsArray)
            {
                for (int i = 0; i < symbol.ArrayLength; i++)
                {
                    _cctorIL.Emit(OpCodes.Ldsfld, symbol.FieldBuilder);
                    _cctorIL.Emit(OpCodes.Ldc_I4, i);
                    string type = CodeWriteSimpleExp(exp, true);
                    if (!type.Contains(symbol.Type))
                    {
                        ErrorHandler.Error("type missmatch");
                    }
                    _cctorIL.Emit(OpCodes.Stelem, ConvertToType(type, false));
                }
            }
            else
            {
                string type = CodeWriteSimpleExp(exp, true);

                if (!type.Contains(symbol.Type))
                {
                    ErrorHandler.Error("type missmatch");
                }

                _cctorIL.Emit(OpCodes.Stsfld, symbol.FieldBuilder);
            }
        }

        public void AddSymbolsFromVarDecl(ASTNode root)
        {
            // getting typeSpec token value
            string type = root.Children[0].Token.Value;

            foreach (ASTNode child in root.Children[1].Children)
            {
                if (child.Children[0].Children.Count > 1)
                {
                    int arrayLength = int.Parse(child.Children[0].Children[1].Token.Value);
                    string name = child.Children[0].Children[0].Token.Value;

                    SymbolTable.Define(name, type, Kind.GLOBAL, arrayLength, _typeBuilder.DefineField(
                        name, 
                        ConvertToType(type, true),
                        FieldAttributes.Public | FieldAttributes.Static
                        ));

                    // init the array
                    _cctorIL.Emit(OpCodes.Ldc_I4, arrayLength);
                    _cctorIL.Emit(OpCodes.Newarr, ConvertToType(type, false));
                    _cctorIL.Emit(OpCodes.Stsfld, SymbolTable.GetSymbol(name).FieldBuilder);
                }
                else
                {
                    SymbolTable.Define(
                        child.Children[0].Children[0].Token.Value, 
                        type,
                        Kind.GLOBAL, 
                        _typeBuilder.DefineField(child.Children[0].Children[0].Token.Value, 
                        ConvertToType(type, false),
                        FieldAttributes.Public | FieldAttributes.Static)
                        );
                }

                // check if initialization is needed
                // the second child of varDeclInit is always the value
                if (child.Children.Count > 1)
                {
                    string name = child.Children[0].Children[0].Token.Value;

                    CodeWriteVarDecl(child.Children[0], child.Children[1], name);
                }
            }
        }

        public void AddSymbolsFromScopedVarDecl(ASTNode root)
        {
            string type = "";
            // starting as local kind
            Kind attribute = Kind.LOCAL;
            // points to the varDeclInit in children
            // depends on the number of children, assuming the number is 2 at the beginning
            int startIndex = 1;

            // if there is an attribute, the count of children will be 3 (static, typeSpec varDeclList)
            if (root.Children.Count > 2)
            {
                // changing the start index to 2 because the number of children is 3
                startIndex = 2;
                // changing the attribute to be static
                attribute = Kind.STATIC;
            }

            type = root.Children[startIndex - 1].Token.Value;

            // iterating through the IDs and adding them to the symbol table
            foreach (ASTNode child in root.Children[startIndex].Children)
            {
                if (child.Children[0].Children.Count > 1)
                {
                    int arrayLength = int.Parse(child.Children[0].Children[1].Token.Value);
                    string name = child.Children[0].Children[0].Token.Value;

                    SymbolTable.Define(name, type, attribute, arrayLength, _currILGen.DeclareLocal(ConvertToType(type, true)));
                    
                    // init the array
                    _currILGen.Emit(OpCodes.Ldc_I4, arrayLength);
                    _currILGen.Emit(OpCodes.Newarr, ConvertToType(type, false));
                    _currILGen.Emit(OpCodes.Stloc, SymbolTable.GetSymbol(name).Index);
                }
                else
                {
                    SymbolTable.Define(child.Children[0].Children[0].Token.Value, type, attribute, _currILGen.DeclareLocal(ConvertToType(type, false)));
                }

                // check if initialization is needed
                // the second child of varDeclInit is always the value
                if (child.Children.Count > 1)
                {
                    string name = child.Children[0].Children[0].Token.Value;

                    CodeWriteScopedVarDecl(child.Children[0], child.Children[1], name);
                }
            }
        }

        public static Type ConvertToType(string type, bool isArrayType)
        {
            if (!isArrayType)
            {
                switch (type)
                {
                    case "int":
                        return typeof(Int32);
                    case "bool":
                        return typeof(Boolean);
                    case "char":
                        return typeof(Char);
                    case "string":
                        return typeof(string);
                    default:
                        return null;
                }
            }
            else
            {
                switch (type)
                {
                    case "int":
                        return typeof(Int32[]);
                    case "bool":
                        return typeof(Boolean[]);
                    case "char":
                        return typeof(Char[]);
                    default:
                        return null;
                }
            }
        }
        public static Type[] ConvertToType(string[] types)
        {
            if (types == null || types.Length == 0)
            {
                // no types to convert
                return null;
            }
            Type[] result = new Type[types.Length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = ConvertToType(types[i].Replace(" arr", ""), types[i].Contains("arr"));
            }

            return result;
        }

        public string GetID(ASTNode root)
        {
            if (root.Tag == "mutable")
            {
                return root.Children[0].Token.Value;
            }

            if (root.Tag == "call")
            {
                return root.Children[0].Token.Value;
            }

            if (root.Tag == "constant")
            {
                return null;
            }

            return GetID(root.Children[0]);
        }

        public string PushIndex(ASTNode root)
        {
            if (root.Tag == "mutable")
            {
                if (root.Children.Count > 2)
                {
                    return CodeWriteSimpleExp(root.Children[2]);
                }
                else return "no index";
            }

            if (root.Tag == "constant")
            {
                return null;
            }

            return PushIndex(root.Children[0]);
        }
    }
}
