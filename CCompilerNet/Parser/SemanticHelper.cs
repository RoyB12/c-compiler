using CCompilerNet.CodeGen;
using CCompilerNet.Lex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCompilerNet.Parser
{
    public static class SemanticHelper
    {
        public static string GetFunctionType(ASTNode root)
        {
            Token type = root.Children[0].Token;

            if (type.Value != "int" && type.Value != "bool" && type.Value != "char")
            {
                return "void";
            }

            return type.Value;
        }

        public static Dictionary<string, bool> GetParmIds(ASTNode root)
        {
            // holds the id as the key and bool to identify if the id is an array type
            Dictionary<string, bool> result = new Dictionary<string, bool>();
            
            foreach(ASTNode child in root.Children[1].Children)
            {
                result.Add(child.Token.Value, child.Children.Count > 0);
            }

            return result;
        }

        public static string GetParmType(ASTNode root)
        {
            return root.Children[0].Token.Value;
        }

        public static string GetFunctionId(ASTNode root)
        {
            if (GetFunctionType(root) != "void")
            {
                return root.Children[1].Token.Value;
            }

            return root.Children[0].Token.Value;
        }

        public static List<string> GetFunctionParmTypes(ASTNode root)
        {
            ASTNode parmList = null;
            List<string> result = new List<string>();

            if (GetFunctionType(root) != "void")
            {
                if (root.Children.Count == 3)
                {
                    parmList = root.Children[2];
                }
                else
                {
                    return result;
                }
            }
            else
            {
                if (root.Children.Count == 2)
                {
                    parmList = root.Children[1];
                }
                else
                {
                    return result;
                }
            }

            // iterating through parmTypeLists
            foreach (ASTNode child in parmList.Children[0].Children)
            {
                /*for (int i = 0; i < child.Children[1].Children.Count; i++)
                {
                    result.Add(child.Children[0].Token.Value + " arr");
                }*/
                string type = child.Children[0].Token.Value;

                foreach(ASTNode parmId in child.Children[1].Children)
                {
                    if (parmId.Children.Count > 0)
                    {
                        result.Add(child.Children[0].Token.Value + " arr");
                    }
                    else
                    {
                        result.Add(child.Children[0].Token.Value);
                    }
                }
            }

            return result;
        }
    }
}
