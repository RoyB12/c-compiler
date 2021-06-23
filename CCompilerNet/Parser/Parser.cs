using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using CCompilerNet.CodeGen;
using CCompilerNet.Lex;

namespace CCompilerNet.Parser
{
    public class Parser
    {
        private Lexer _lexer;
        private Token _currentToken;
        public AST _ast { get; private set; }
        public VMWriter VM { get; }

        public Parser(string filePath, string fileName)
        {
            _ast = null;
            _lexer = new Lexer(filePath);
            _currentToken = _lexer.GetNextToken();
            VM = new VMWriter(fileName);
        }

        private void EatToken()
        {
            _currentToken = _lexer.GetNextToken();
        }

        private bool IsTokenTypeEquals(TokenType tokenType)
        {
            return _currentToken != null && _currentToken.Type == tokenType;
        }

        private bool IsValueEquals(string value)
        {
            return _currentToken != null && _currentToken.Value == value;
        }

        #region Declarations

        // program -> declList
        public bool CompileProgram()
        {
            var root = new ASTNode("program");

            if (CompileDeclList(root))
            {
                _ast = new AST(root);
                return true;
            }

            return false;
        }

        // declList -> declList decl | decl   =>   decList → decl decList'   decList' → decl decList' | epsilon
        private bool CompileDeclList(ASTNode parent)
        {
            ASTNode compileDecList = new ASTNode("declList");

            if (CompileDecl(compileDecList))
            {
                if (CompileDeclListTag(compileDecList))
                {
                    if (_currentToken != null)
                    {
                        ErrorHandler.Error("No declarations found");
                    }
                    parent.Add(compileDecList);
                    return true;
                }
            }

            return false;
        }

        // decList' -> decl decList' | epsilon
        private bool CompileDeclListTag(ASTNode parent)
        {
            ASTNode declListTag = new ASTNode("declListTag");

            // epsilon
            if (!CompileDecl(declListTag))
            {
                return true;
            }

            if (!CompileDeclListTag(declListTag))
            {
                return false;
            }

            parent.Add(declListTag);
            return true;
        }

        // decl -> varDecl | funDecl
        private bool CompileDecl(ASTNode parent)
        {
            ASTNode decl = new ASTNode("decl");

            if (!CompileFunDecl(decl) && !CompileVarDecl(decl))
            {
                return false;
            }

            parent.Add(decl);
            return true;
        }
        #endregion

        #region Variable Declarations

        // varDecl -> typeSpec varDeclList ;
        private bool CompileVarDecl(ASTNode parent)
        {
            ASTNode varDecl = new ASTNode("varDecl");

            if (!CompileTypeSpec(varDecl))
            {
                return false;
            }

            EatToken();

            if (!CompileVarDeclList(varDecl))
            {
                return false;
            }

            if (!IsValueEquals(";"))
            {
                ErrorHandler.UnexpectedTokenError(";", _currentToken);
                return false;
            }

            VM.AddSymbolsFromVarDecl(varDecl);

            EatToken();
            parent.Add(varDecl);
            return true;
        }

        // scopedVarDecl -> static typeSpec varDeclList ; | typeSpec varDeclList ;
        private bool CompileScopedVarDecl(ASTNode parent)
        {
            ASTNode scopedVarDecl = new ASTNode("scopedVarDecl");

            if (IsValueEquals("static"))
            {
                scopedVarDecl.Add(new ASTNode(_currentToken));
                EatToken();
            }

            if (!CompileTypeSpec(scopedVarDecl))
            {
                return false;
            }

            EatToken();

            if (!CompileVarDeclList(scopedVarDecl))
            {
                return false;
            }

            if (!IsValueEquals(";"))
            {
                ErrorHandler.UnexpectedTokenError(";", _currentToken);
                return false;
            }

            VM.AddSymbolsFromScopedVarDecl(scopedVarDecl);

            EatToken();
            parent.Add(scopedVarDecl);
            return true;
        }

        // varDeclList -> varDeclList , varDeclInit | varDeclInit
        // varDeclList -> varDeclInit varDeclList`
        // varDeclList` -> , varDeclInit varDeclList` | epsilon
        private bool CompileVarDeclList(ASTNode parent)
        {
            ASTNode varDeclList = new ASTNode("varDeclList");

            if (CompileVarDeclInit(varDeclList))
            {
                if (CompileVarDeclListTag(varDeclList))
                {
                    parent.Add(varDeclList);
                    return true;
                }
            }

            return false;
        }

        // varDeclList` -> , varDeclInit varDeclList` | epsilon
        private bool CompileVarDeclListTag(ASTNode parent)
        {
            ASTNode varDeclListTag = new ASTNode("varDeclListTag");
            //parent.Add(varDeclListTag);

            if (!IsValueEquals(","))
            {
                // epsilon - empty, assuming that the declaration has ended
                return true;
            }

            EatToken();

            if (CompileVarDeclInit(varDeclListTag))
            {
                if (CompileVarDeclListTag(varDeclListTag))
                {
                    //parent.Add(varDeclListTag);
                    parent += varDeclListTag;
                    return true;
                }
            }

            return false;
        }

        // varDeclInit -> varDeclId | varDeclId : simpleExp
        private bool CompileVarDeclInit(ASTNode parent)
        {
            ASTNode varDeclInit = new ASTNode("varDeclInit");

            if (!CompileVarDeclId(varDeclInit))
            {
                return false;
            }

            // if didn't encounter :, then it's -> varDeclId
            if (!IsValueEquals(":"))
            {
                parent.Add(varDeclInit);
                return true;
            }

            EatToken();

            if (!CompileSimpleExp(varDeclInit))
            {
                return false;
            }

            parent.Add(varDeclInit);
            // else it's -> varDeclId : simpleExp
            return true;
        }


        // varDeclId -> ID | ID [ NUMCONST ]
        private bool CompileVarDeclId(ASTNode parent)
        {
            ASTNode varDeclId = new ASTNode("varDeclId");

            if (!IsTokenTypeEquals(TokenType.ID))
            {
                ErrorHandler.UnexpectedTokenTypeError(TokenType.ID, _currentToken);
                return false;
            }

            // saving the ID
            varDeclId.Add(new ASTNode(_currentToken));
            EatToken();

            // ID
            if (!IsValueEquals("["))
            {
                parent.Add(varDeclId);
                return true;
            }

            EatToken();

            // must be checked further if const is a numconst
            if (!IsTokenTypeEquals(TokenType.Const))
            {
                ErrorHandler.UnexpectedTokenTypeError(TokenType.Const, _currentToken);
                return false;
            }

            varDeclId.Add(new ASTNode(_currentToken));
            EatToken();

            if (!IsValueEquals("]"))
            {
                ErrorHandler.UnexpectedTokenError("]", _currentToken);
                return false;
            }

            EatToken();
            parent.Add(varDeclId);
            return true;
        }

        // typeSpec -> int | bool | char
        private bool CompileTypeSpec(ASTNode parent)
        {
            ASTNode typeSpec = null;

            if (IsValueEquals("int") || IsValueEquals("bool") || IsValueEquals("char"))
            {
                typeSpec = new ASTNode("typeSpec", _currentToken);
                parent.Add(typeSpec);

                return true;
            }
            return false;
        }

        // funDecl -> typeSpec ID ( parms ) stmt | ID ( parms ) stmt
        private bool CompileFunDecl(ASTNode parent)
        {
            ASTNode funDecl = new ASTNode("funDecl");

            if (IsValueEquals("int") || IsValueEquals("bool") || IsValueEquals("char"))
            {
                if (_lexer.Peek(2) == null || _lexer.Peek(2).Value != "(")
                {
                    return false;
                }
            }
            else
            {
                if (_lexer.Peek(1) == null || _lexer.Peek(1).Value != "(")
                {
                    return false;
                }
            }

            if (CompileTypeSpec(funDecl))
            {
                // typespec doesn't eat tokens, so here we do it manually
                EatToken();
            }

            if (!IsTokenTypeEquals(TokenType.ID))
            {
                ErrorHandler.UnexpectedTokenTypeError(TokenType.ID, _currentToken);
                return false;
            }

            funDecl.Add(new ASTNode(_currentToken));
            EatToken();

            if (!IsValueEquals("("))
            {
                ErrorHandler.UnexpectedTokenError("(", _currentToken);
                return false;
            }

            EatToken();

            VM.SymbolTable = VM.SymbolTable.StartSubRoutine();

            if (!CompileParms(funDecl))
            {
                return false;
            }

            if (!IsValueEquals(")"))
            {
                ErrorHandler.UnexpectedTokenError(")", _currentToken);
                return false;
            }

            EatToken();

            // vm translation
            VM.FunctionTable.Define(funDecl);
            VM.CodeWriteFunction(funDecl);

            if (!CompileStmt(funDecl, true))
            {
                return false;
            }

            VM.SymbolTable = VM.SymbolTable.GetNext();

            parent.Add(funDecl);
            return true;
        }

        // parms -> parmList | epsilon
        private bool CompileParms(ASTNode parent)
        {
            ASTNode parms = new ASTNode("parms");

            if (!CompileParmList(parms))
            {
                return true;
            }

            parent.Add(parms);
            return true;
        }

        // parmList -> parmList ; parmTypeList | parmTypeList
        // parmList -> parmTypeList parmList`
        // parmList` -> ; parmTypeList parmList` | epsilon
        private bool CompileParmList(ASTNode parent)
        {
            ASTNode parmList = new ASTNode("parmList");

            if (!CompileParmTypeList(parmList))
            {
                return false;
            }

            if (!CompileParmListTag(parmList))
            {
                return false;
            }

            parent.Add(parmList);
            return true;
        }

        // parmList` -> ; parmTypeList parmList` | epsilon
        private bool CompileParmListTag(ASTNode parent)
        {
            ASTNode parmListTag = new ASTNode("parmListTag");

            // epsilon
            if (!IsValueEquals(";"))
            {
                return true;
            }

            EatToken();

            if (!CompileParmTypeList(parmListTag))
            {
                return false;
            }

            if (!CompileParmListTag(parmListTag))
            {
                return false;
            }

            parent += parmListTag;
            return true;
        }

        // typeSpec parmIdList
        private bool CompileParmTypeList(ASTNode parent)
        {
            ASTNode parmTypeList = new ASTNode("parmTypeList");

            if (!CompileTypeSpec(parmTypeList))
            {
                return false;
            }

            // if typespec returned true, type token must be eaten
            // typespec doesn't eat tokens
            EatToken();

            if (!CompileParmIdList(parmTypeList))
            {
                return false;
            }

            string type = SemanticHelper.GetParmType(parmTypeList);
            Dictionary<string, bool> ids = SemanticHelper.GetParmIds(parmTypeList);

            foreach (string id in ids.Keys)
            {
                VM.SymbolTable.Define(id, type, Kind.ARG, ids[id]);
            }

            parent.Add(parmTypeList);
            return true;
        }

        // parmIdList -> parmIdList , parmId | parmId
        // parmIdList -> parmId parmIdList`
        // parmIdList` -> , parmId parmIdList` | epsilon
        private bool CompileParmIdList(ASTNode parent)
        {
            ASTNode parmIdList = new ASTNode("parmIdList");

            if (!CompileParmId(parmIdList))
            {
                return false;
            }

            if (!CompileParmIdListTag(parmIdList))
            {
                return false;
            }

            parent.Add(parmIdList);
            return true;
        }

        // parmIdList` -> , parmId parmIdList` | epsilon
        private bool CompileParmIdListTag(ASTNode parent)
        {
            ASTNode parmIdListTag = new ASTNode("parmIdListTag");

            // epsilon
            if (!IsValueEquals(","))
            {
                return true;
            }

            EatToken();

            if (!CompileParmId(parmIdListTag))
            {
                return false;
            }

            if (!CompileParmIdListTag(parmIdListTag))
            {
                return false;
            }

            parent += parmIdListTag;
            return true;
        }

        // parmId -> ID | ID [ ]
        private bool CompileParmId(ASTNode parent)
        {
            ASTNode parmId = null;

            if (!IsTokenTypeEquals(TokenType.ID))
            {
                return false;
            }

            parmId = new ASTNode("parmId", _currentToken);
            EatToken();

            // ID
            if (!IsValueEquals("["))
            {
                parent.Add(parmId);
                return true;
            }

            parmId.Add(new ASTNode(_currentToken));
            EatToken();

            if (!IsValueEquals("]"))
            {
                return false;
            }

            parmId.Add(new ASTNode(_currentToken));
            EatToken();

            parent.Add(parmId);
            return true;
        }
        #endregion

        #region Expression

        // exp -> mutable = exp | mutable += exp | mutable -= exp | mutable *= exp | mutable /= exp | mutable++ | mutable-- | simpleExp
        private bool CompileExp(ASTNode parent)
        {
            ASTNode expression = new ASTNode("expression");
            List<string> mutables = new List<string> { "=", "+=", "-=", "*=", "/=" };   //list of operators that lead to another expression
            List<string> ops = new List<string> { "++", "--" };

            if (CompileSimpleExp(expression))
            {
                if (GetTag(expression) == "mutable")
                {
                    if (_currentToken == null)
                    {
                        ErrorHandler.UnexpectedTokenError("=", _currentToken);                 
                    }

                    string id = VM.GetID(expression);
                    // check if the id is declared
                    if (VM.SymbolTable.GetSymbol(id) == null)
                    {
                        ErrorHandler.VariableIsNotDeclaredError(id);
                    }

                    if (mutables.Contains(_currentToken.Value)) // checks if mutable -> exp
                    {
                        expression.Add(new ASTNode(_currentToken)); //add operator to node of the expression
                        EatToken();

                        if (CompileExp(expression))
                        {
                            parent.Add(expression);
                            return true;
                        }

                        ErrorHandler.UnexpectedTokenError("expression", _currentToken);
                    }

                    if (ops.Contains(_currentToken.Value))
                    {
                        expression.Add(new ASTNode(_currentToken));   //add operator to node of the expression

                        parent.Add(expression);
                        EatToken();
                        return true;
                    }
                }

                parent.Add(expression);
                return true;
            }

            return false;
        }

        static private string GetTag(ASTNode exp)
        {
            if (exp.Children.Count == 1 && exp.Tag != "mutable")
            {
                return GetTag(exp.Children[0]);
            }

            return exp.Tag;
        }

        // simpleExp -> simpleExp or andExp | andExp
        // simpleExp -> andExp SimpleExp'
        // simpleExp' -> or andExp simpleExp' | epsilon
        private bool CompileSimpleExp(ASTNode parent)
        {
            ASTNode simpleExp = new ASTNode("simpleExpression");

            if (!CompileAndExp(simpleExp))
            {
                return false;
            }

            if (!CompileSimpleExpTag(simpleExp))
            {
                return false;
            }

            parent.Add(simpleExp);
            return true;
        }

        private bool CompileSimpleExpTag(ASTNode parent)
        {
            ASTNode simpleExpTag = new ASTNode("simpleExpressionTag");

            if (!IsValueEquals("or"))
            {
                return true;
            }

            EatToken();

            if (!CompileAndExp(simpleExpTag))
            {
                return false;
            }

            if (!CompileSimpleExpTag(simpleExpTag))
            {
                return false;
            }

            parent += simpleExpTag;
            return true;
        }

        // AndExp -> andExp and unaryRelExp | unaryRelExp
        // AndExp -> unaryRelExp AndExp'
        // AndExp' -> and unaryRelExp AndExp' | epsilon
        private bool CompileAndExp(ASTNode parent)
        {
            ASTNode andExp = new ASTNode("andExpression");

            if (!CompileUnaryRelExp(andExp))
            {
                return false;
            }

            if (!CompileAndExpTag(andExp))
            {
                return false;
            }

            parent.Add(andExp);
            return true;
        }

        private bool CompileAndExpTag(ASTNode parent)
        {
            ASTNode andExpTag = new ASTNode("andExpressionTag");

            if (!IsValueEquals("and"))
            {
                return true;
            }

            EatToken();

            if (!CompileUnaryRelExp(andExpTag))
            {
                return false;
            }

            if (!CompileAndExpTag(andExpTag))
            {
                return false;
            }

            parent += andExpTag;
            return true;
        }

        //unaryRelExp -> not unaryRelExp | relExp
        private bool CompileUnaryRelExp(ASTNode parent)
        {
            ASTNode unaryRelExp = new ASTNode("unaryRelExpression");

            if (IsValueEquals("not"))
            {
                unaryRelExp.Add(new ASTNode(_currentToken));
                EatToken();

                if (!CompileUnaryRelExp(unaryRelExp))
                {
                    ErrorHandler.UnexpectedTokenError("expression", _currentToken);
                    return false;
                }

                parent.Add(unaryRelExp);
                return true;
            }

            if (CompileRelExp(unaryRelExp))
            {
                parent.Add(unaryRelExp);
                return true;
            }

            return false;
        }

        //relExp -> MinMaxExp relop MinMaxExp | MinMaxExp
        private bool CompileRelExp(ASTNode parent)
        {
            ASTNode relExp = new ASTNode("relExpression");

            if (!CompileMinMaxExp(relExp))
            {
                return false;
            }

            if (!CompileRelop(relExp))  //if no relop after MinMaxExp then its the 2nd variation
            {
                parent.Add(relExp);
                return true;
            }

            if (!CompileMinMaxExp(relExp))
            {
                return false;
            }

            parent.Add(relExp);
            return true;
        }

        //relop -> <= | < | > | >= | == | !=
        private bool CompileRelop(ASTNode parent)
        {
            List<string> operators = new List<string> { "<=", "<", ">", ">=", "==", "!=" }; //list of relop operators
            ASTNode relop = new ASTNode("relop");
            if (!operators.Contains(_currentToken.Value))
            {
                return false;
            }

            relop.Add(new ASTNode(_currentToken));    //add the operator to the relop node
            EatToken();  //move on to the next token after adding it

            parent.Add(relop);  //add to parent node

            return true;

        }

        // minmaxExp -> minmaxExp minmaxOp sumExp | sumExp
        // minmaxExp -> sumExp minmaxExp'
        // minmaxExp' -> minmaxOp sumExp minmaxExp'  | epsilon
        private bool CompileMinMaxExp(ASTNode parent)
        {
            ASTNode minMaxExp = new ASTNode("minMaxExpression");

            if (!CompileSumExp(minMaxExp))
            {
                return false;
            }

            if (!CompileMinMaxExpTag(minMaxExp))
            {
                return false;
            }

            parent.Add(minMaxExp);
            return true;
        }

        private bool CompileMinMaxExpTag(ASTNode parent)
        {
            ASTNode minMaxExpTag = new ASTNode("minMaxExpressionTag");

            if (!CompileMinMaxOp(minMaxExpTag))
            {
                return true;
            }

            if (!CompileSumExp(minMaxExpTag))
            {
                return false;
            }

            if (!CompileMinMaxExpTag(minMaxExpTag))
            {
                return false;
            }

            parent.Add(minMaxExpTag);
            return true;
        }

        //MinMaxOp -> :>: | :<:
        private bool CompileMinMaxOp(ASTNode parent)
        {
            ASTNode minMaxOp = new ASTNode("minMaxOp");

            if (!IsValueEquals(":>:") && !IsValueEquals(":<:"))
            {
                return false;
            }

            minMaxOp.Add(new ASTNode(_currentToken)); //add operator to node
            EatToken(); //move over to next token 

            parent.Add(minMaxOp); //add node to parent
            return true;
        }

        //SumExp -> SumExp sumOp mulExp | mulExp
        //SumExp -> mulExp SumExp'
        //SumExp' -> sumOp mulExp SumExp' | Epsilon
        private bool CompileSumExp(ASTNode parent)
        {
            ASTNode sumExp = new ASTNode("sumExpression");

            if (!CompileMulExp(sumExp))
            {
                return false;
            }

            if (!CompileSumExpTag(sumExp))
            {
                return false;
            }

            parent.Add(sumExp);
            return true;
        }

        private bool CompileSumExpTag(ASTNode parent)
        {
            ASTNode sumExpTag = new ASTNode("sumExpressionTag");

            if (!CompileSumOp(sumExpTag))
            {
                return true;
            }

            if (!CompileMulExp(sumExpTag))
            {
                ErrorHandler.UnexpectedTokenError("expression", _currentToken);
                return false;
            }

            if (!CompileSumExpTag(sumExpTag))
            {
                return false;
            }

            sumExpTag.Children.Reverse();
            parent.Add(sumExpTag);
            return true;
        }

        //sumOp -> + | -
        private bool CompileSumOp(ASTNode parent)
        {
            ASTNode sumOp = new ASTNode("operator");

            if (!IsValueEquals("+") && !IsValueEquals("-"))
            {
                return false;
            }

            sumOp.Add(new ASTNode(_currentToken));

            EatToken();

            parent.Add(sumOp);
            return true;
        }

        //mulExp -> mulExp mulOp unaryExp | unaryExp
        //mulExp -> unaryExp mulExp'
        //mulExp' -> mulOp unaryExp mulExp' | epsilon 
        private bool CompileMulExp(ASTNode parent)
        {
            ASTNode mulExp = new ASTNode("mulExpression");

            if (!CompileUnaryExp(mulExp))
            {
                return false;
            }

            if (!CompileMulExpTag(mulExp))
            {
                return false;
            }

            parent.Add(mulExp);
            return true;
        }

        private bool CompileMulExpTag(ASTNode parent)
        {
            ASTNode mulExpTag = new ASTNode("mulExpressionTag");

            if (!CompileMulOp(mulExpTag))
            {
                return true;
            }

            if (!CompileUnaryExp(mulExpTag))
            {
                ErrorHandler.UnexpectedTokenError("expression", _currentToken);
                return false;
            }

            if (!CompileMulExpTag(mulExpTag))
            {
                return false;
            }

            mulExpTag.Children.Reverse();
            parent.Add(mulExpTag);
            return true;
        }

        //mulOp -> * | / | %
        private bool CompileMulOp(ASTNode parent)
        {
            ASTNode mulOp = new ASTNode("operator");

            if (!IsValueEquals("*") && !IsValueEquals("/") && !IsValueEquals("%"))
            {
                return false;
            }

            mulOp.Add(new ASTNode(_currentToken));

            EatToken();

            parent.Add(mulOp);
            return true;
        }

        //unaryExp -> unaryOp unaryExp | factor
        private bool CompileUnaryExp(ASTNode parent)
        {
            ASTNode unaryExp = new ASTNode("unaryExp");

            if (CompileUnaryOperator(unaryExp))
            {
                if (!CompileUnaryExp(unaryExp))
                {
                    ErrorHandler.UnexpectedTokenError("expression", _currentToken);
                    return false;
                }

                unaryExp.Children.Reverse();
                parent.Add(unaryExp);
                return true;
            }
            if (CompileFactor(unaryExp))
            {
                parent.Add(unaryExp);
                return true;
            }

            return false;
        }

        //unaryOp -> - | * | ?
        private bool CompileUnaryOperator(ASTNode parent)
        {
            ASTNode unaryOp = new ASTNode("unaryOperator");

            if (!IsValueEquals("-") && !IsValueEquals("*") && !IsValueEquals("?"))
            {
                return false;
            }

            unaryOp.Add(new ASTNode(_currentToken));

            EatToken();

            parent.Add(unaryOp);

            return true;
        }

        //factor -> immutable | mutable
        private bool CompileFactor(ASTNode parent)
        {
            ASTNode factor = new ASTNode("factor");

            if (CompileImmutable(factor))
            {
                parent.Add(factor);
                return true;
            }

            if (CompileMutable(factor))
            {
                EatToken();
                parent.Add(factor);
                return true;
            }
            
            return false;
        }

        //immutable ->  ( exp ) | call | constant
        private bool CompileImmutable(ASTNode parent)
        {
            ASTNode immutable = new ASTNode("immutable");

            if (IsValueEquals("("))
            {
                EatToken();

                if (!CompileExp(immutable))
                {
                    ErrorHandler.UnexpectedTokenError("immutable", _currentToken);
                    return false;
                }

                if (!IsValueEquals(")"))
                {
                    return false;
                }

                EatToken();

                parent.Add(immutable);
                return true;
            }

            if (CompileCall(immutable) || CompileConst(immutable))
            {
                parent.Add(immutable);
                return true;
            }

            return false;
        }

        //mutable -> ID | ID [exp]
        private bool CompileMutable(ASTNode parent)
        {
            ASTNode mutable = new ASTNode("mutable");

            if (!IsTokenTypeEquals(TokenType.ID))
            {
                return false;
            }

            mutable.Add(new ASTNode(_currentToken));

            if (_lexer.Peek(1).Value != "[")
            {
                parent.Add(mutable);
                return true;            //if no [ after id then its not an array
            }

            EatToken();
            EatToken();

            mutable.Add(new ASTNode(new Token(TokenType.SpecialSymbol, "[")));

            if (!CompileExp(mutable))  //must be an expression between the []
            {
                return false;
            }

            if (!IsValueEquals("]"))
            {
                return false;
            }

            mutable.Add(new ASTNode(new Token(TokenType.SpecialSymbol, "]")));

            parent.Add(mutable);
            return true;
        }

        //call -> ID ( args )
        private bool CompileCall(ASTNode parent)
        {
            ASTNode call = new ASTNode("call");

            if (!IsTokenTypeEquals(TokenType.ID))
            {
                return false;
            }

            if (_lexer.Peek(1).Value != "(")
            {
                return false;
            }

            call.Add(new ASTNode(_currentToken));

            EatToken();
            EatToken();

            if (!CompileArgs(call))
            {
                return false;
            }

            if (!IsValueEquals(")"))
            {
                return false;
            }

            EatToken();

            parent.Add(call);
            return true;
        }

        //args -> argList | epsilon
        private bool CompileArgs(ASTNode parent)
        {
            ASTNode args = new ASTNode("args");

            if (!CompileArgList(args))
            {
                return true;
            }

            parent.Add(args);
            return true;
        }

        //argList -> argList, exp | exp
        //argList -> exp argList'
        //argList -> , exp argList' | epsilon
        private bool CompileArgList(ASTNode parent)
        {
            ASTNode argList = new ASTNode("argList");

            if (!CompileExp(argList))
            {
                return false;
            }

            if (!CompileArgListTag(argList))
            {
                return false;
            }

            parent.Add(argList);
            return true;
        }

        private bool CompileArgListTag(ASTNode parent)
        {
            ASTNode argListTag = new ASTNode("argListTag");

            if (!IsValueEquals(","))
            {
                return true;
            }

            EatToken();

            if (!CompileExp(argListTag))
            {
                ErrorHandler.UnexpectedTokenError("expression", _currentToken);
                return false;
            }

            if (!CompileArgListTag(argListTag))
            {
                return false;
            }

            parent += (argListTag);
            return true;
        }

        //const -> NUMCONST | CHARCONST | STRINGCONST | true | false
        private bool CompileConst(ASTNode parent)
        {
            ASTNode constant = new ASTNode("constant");

            if (!IsTokenTypeEquals(TokenType.Const) && !IsTokenTypeEquals(TokenType.StringLiteral))
            {
                return false;
            }

            constant.Add(new ASTNode(_currentToken));

            EatToken();

            parent.Add(constant);
            return true;
        }

        #endregion

        #region Statements

        // stmt -> expStmt | compoundStmt | selectStmt | iterStmt | returnStmt | breakStmt
        private bool CompileStmt(ASTNode parent, bool write)
        {
            ASTNode stmt = new ASTNode("stmt");

            if (!(
                CompileExpStmt(stmt, write)
                || CompileCompoundStmt(stmt, write)
                || CompileSelectStmt(stmt, write)
                || CompileIterStmt(stmt, write)
                || CompileReturnStmt(stmt, write)
                || CompileBreakStmt(stmt, write)
                ))
            {
                return false;
            }

            parent.Add(stmt);
            return true;
        }

        // expStmt -> exp ; | ;
        private bool CompileExpStmt(ASTNode parent, bool write)
        {
            ASTNode expStmt = new ASTNode("expStmt");

            if (IsValueEquals(";"))
            {
                EatToken();
                return true;
            }

            if (!CompileExp(expStmt) && !IsValueEquals(";"))
            {
                return false;
            }

            if (!IsValueEquals(";"))
            {
                ErrorHandler.UnexpectedTokenError(";", _currentToken);
                return false;
            }

            string id = VM.GetID(expStmt.Children[0]);
            FunctionSymbol symbol = VM.FunctionTable.GetFunctionSymbol(id);

            if (write && (expStmt.Children[0].Children.Count > 1 || symbol != null))
            {
                string type = VM.CodeWriteExp(expStmt.Children[0]);
                
                if (!type.Contains("expression") && symbol.Type != "void")
                {
                    VM.GetCurrentILGenerator().Emit(OpCodes.Pop);
                }
            }

            EatToken();
            parent.Add(expStmt);
            return true;
        }

        // compoundStmt -> { localDecls stmtList }
        private bool CompileCompoundStmt(ASTNode parent, bool write)
        {
            ASTNode compoundStmt = new ASTNode("compoundStmt");

            if (!IsValueEquals("{"))
            {
                return false;
            }

            EatToken();

            if (write)
            {
                VM.SymbolTable = VM.SymbolTable.StartSubRoutine();
            }

            if (!CompileLocalDecls(compoundStmt))
            {
                return false;
            }

            if (!CompileStmtList(compoundStmt, write))
            {
                return false;
            }

            if (!IsValueEquals("}"))
            {
                ErrorHandler.UnexpectedTokenError("}", _currentToken);
                return false;
            }

            if (write)
            {
                VM.SymbolTable = VM.SymbolTable.GetNext();
            }

            EatToken();
            parent.Add(compoundStmt);
            return true;
        }

        // selectStmt -> if simpleExp then stmt | if simpleExp then stmt else stmt
        private bool CompileSelectStmt(ASTNode parent, bool write)
        {
            ASTNode selectStmt = new ASTNode("selectStmt");

            if (!IsValueEquals("if"))
            {
                return false;
            }

            EatToken();

            if (!CompileSimpleExp(selectStmt))
            {
                return false;
            }

            if (!IsValueEquals("then"))
            {
                ErrorHandler.UnexpectedTokenError("then", _currentToken);
                return false;
            }

            EatToken();

            if (!CompileStmt(selectStmt, false))
            {
                return false;
            }

            if (!IsValueEquals("else"))
            {
                VM.CodeWriteSelectStmt(selectStmt);
                parent.Add(selectStmt);
                return true;
            }

            EatToken();

            if (!CompileStmt(selectStmt, false))
            {
                return false;
            }

            VM.CodeWriteSelectStmt(selectStmt);

            parent.Add(selectStmt);
            return true;
        }

        // iterStmt -> while simpleExp do stmt | for ID = iterRange do stmt
        private bool CompileIterStmt(ASTNode parent, bool write)
        {
            ASTNode iterStmt = new ASTNode("iterStmt");

            // while simpleExp do stmt
            if (IsValueEquals("while"))
            {
                iterStmt.Add(new ASTNode(_currentToken));
                EatToken();

                if (!CompileSimpleExp(iterStmt))
                {
                    return false;
                }

                if (!IsValueEquals("do"))
                {
                    ErrorHandler.UnexpectedTokenError("do", _currentToken);
                    return false;
                }

                iterStmt.Add(new ASTNode(_currentToken));
                EatToken();

                if (!CompileStmt(iterStmt, false))
                {
                    return false;
                }

                if (write)
                {
                    VM.CodeWriteWhileLoop(iterStmt);
                }

                parent.Add(iterStmt);
                return true;
            }

            // for ID = iterRange do stmt
            else if (IsValueEquals("for"))
            {
                iterStmt.Add(new ASTNode(_currentToken));
                EatToken();

                if (!IsTokenTypeEquals(TokenType.ID))
                {
                    ErrorHandler.UnexpectedTokenTypeError(TokenType.ID, _currentToken);
                    return false;
                }

                // storing the id
                iterStmt.Add(new ASTNode(_currentToken));
                EatToken();

                if (!IsValueEquals("="))
                {
                    ErrorHandler.UnexpectedTokenError("=", _currentToken);
                    return false;
                }

                EatToken();

                if (!CompileIterRange(iterStmt))
                {
                    return false;
                }

                if (!IsValueEquals("do"))
                {
                    ErrorHandler.UnexpectedTokenError("do", _currentToken);
                    return false;
                }

                iterStmt.Add(new ASTNode(_currentToken));
                EatToken();

                VM.SymbolTable = VM.SymbolTable.StartSubRoutine();
                VM.SymbolTable.Define(iterStmt.Children[1].Token.Value, "int", Kind.LOCAL, VM.GetLocalBuilder("int"));

                if (!CompileStmt(iterStmt, false))
                {
                    return false;
                }

                if (write)
                {
                    VM.CodeWriteForLoop(iterStmt);
                }

                VM.SymbolTable = VM.SymbolTable.GetNext();

                parent.Add(iterStmt);
                return true;
            }

            // didn't much any pattern in the production rule
            return false;
        }

        // iterRange -> simpleExp | simpleExp to simpleExp | simpleExp to simpleExp by simpleExp
        private bool CompileIterRange(ASTNode parent)
        {
            ASTNode iterRange = new ASTNode("iterRange");

            if (!CompileSimpleExp(iterRange))
            {
                return false;
            }

            if (!IsValueEquals("to"))
            {
                parent.Add(iterRange);
                return true;
            }

            EatToken();

            if (!CompileSimpleExp(iterRange))
            {
                return false;
            }

            if (!IsValueEquals("by"))
            {
                parent.Add(iterRange);
                return true;
            }

            EatToken();

            if (!CompileSimpleExp(iterRange))
            {
                return false;
            }

            parent.Add(iterRange);
            return true;
        }

        // returnStmt -> return ; | return exp ;
        private bool CompileReturnStmt(ASTNode parent, bool write)
        {
            ASTNode returnStmt = new ASTNode("returnStmt");

            if (!IsValueEquals("return"))
            {
                return false;
            }

            returnStmt.Add(new ASTNode(_currentToken));
            EatToken();

            if (!IsValueEquals(";"))
            {
                if (!CompileExp(returnStmt))
                {
                    return false;
                }

                if (!IsValueEquals(";"))
                {
                    ErrorHandler.UnexpectedTokenError(";", _currentToken);
                    return false;
                }

                EatToken();

                if (write)
                {
                    VM.CodeWriteReturnStmt(returnStmt);
                }
                parent.Add(returnStmt);
                return true;
            }

            EatToken();
            VM.CodeWriteReturnStmt(returnStmt);
            parent.Add(returnStmt);
            return true;
        }

        private bool CompileBreakStmt(ASTNode parent, bool write)
        {
            ASTNode breakStmt = null;

            if (!IsValueEquals("break"))
            {
                return false;
            }

            breakStmt = new ASTNode("breakStmt", _currentToken);
            EatToken();

            if (!IsValueEquals(";"))
            {
                ErrorHandler.UnexpectedTokenError(";", _currentToken);
                return false;
            }
            EatToken();

            parent.Add(breakStmt);
            return true;
        }

        // stmtList -> stmtList stmt | epsilon
        private bool CompileStmtList(ASTNode parent, bool write)
        {
            ASTNode stmtList = new ASTNode("stmtList");

            while (CompileStmt(stmtList, write)) ;

            if (stmtList.Children.Count() > 0)
            {
                parent.Add(stmtList);
            }

            return true;
        }

        // localDecls -> localDecls scopedVarDecl | epsilon
        private bool CompileLocalDecls(ASTNode parent)
        {
            ASTNode localDecls = new ASTNode("localDecls");

            while (CompileScopedVarDecl(localDecls)) ;

            if (localDecls.Children.Count() > 0)
            {
                parent.Add(localDecls);
            }

            return true;
        }

        #endregion
    }
}
