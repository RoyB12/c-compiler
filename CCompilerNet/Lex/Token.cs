using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCompilerNet.Lex
{
    public enum TokenType
    {
        Keyword,
        ID,
        Const,
        StringLiteral,
        SpecialSymbol,
        Operator,
        BadToken,
    }

    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }
        public int Line { get; set; }

        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }

        public override string ToString()
        {
            return String.Format("<{0}>{1}</{2}>", Type, Value, Type);
        }
    }
}
