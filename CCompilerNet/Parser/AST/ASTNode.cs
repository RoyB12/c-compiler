using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CCompilerNet.Lex;

namespace CCompilerNet.Parser
{
    public class ASTNode
    {
        /* Public Properties */
        public string Tag { get; set; }
        public List<ASTNode> Children { get; private set; }
        public Token Token { get; set; }
                                                                                    
        /* Constructors */
        public ASTNode(string tag, Token token = null)
        {
            Children = new List<ASTNode>();
            Tag = tag;
            Token = token;
        }

        public ASTNode(Token token)
        {
            Children = new List<ASTNode>();
            Tag = "";
            Token = token;
        }

        /* Public Methods */
        /// <summary>
        /// Adds a child node to the current node
        /// </summary>
        /// <param name="node">Node to be added</param>
        public void Add(ASTNode node)
        {
            Children.Add(node);
        }

        public static string Print(ASTNode node, int level, bool print)
        {
            string tabs = new string('\t', level);

            string result = "";

            if (node.Tag == "")
            {
                return tabs + node.Token + '\n';
            }

            if (print || node.Token != null)

            {
                result += tabs + "<" + node.Tag + ">\n";

                if (node.Token != null)
                {
                    result += tabs + '\t' + node.Token.ToString() + '\n';
                }

            }

            foreach (ASTNode child in node.Children)
            {
                if (child.Children.Count > 1 || child.Token != null)
                {
                    result += ASTNode.Print(child, level + 1, true);
                }
                else
                {
                    result += ASTNode.Print(child, level, false);
                }
            }

            if (print || node.Token != null)
            {
                result += tabs + "</" + node.Tag + ">\n";
            }

            return result;
        }

        public static ASTNode operator +(ASTNode a, ASTNode b)
        {
            foreach(ASTNode child in b.Children)
            {
                a.Add(child);
            }

            return a;
        }
    }
}
