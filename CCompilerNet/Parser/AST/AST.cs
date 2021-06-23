using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCompilerNet.Parser
{
    public class AST
    {
        /* public Properties */
        public ASTNode Root { get; set; }

        /* Constructors */
        public AST(ASTNode root)
        {
            Root = root;
        }

        public override string ToString()
        {
            return ASTNode.Print(Root, 0, true);
        }
    }
}
