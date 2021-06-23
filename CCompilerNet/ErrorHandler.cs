using CCompilerNet.Lex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCompilerNet
{
    public static class ErrorHandler
    {
        public static void Error(string errorMessage)
        {
            Console.Error.WriteLine($"Error: {errorMessage}");
            Environment.Exit(-1);
        }

        #region Syntax Errors

        /// <summary>
        /// Shows an unexpected token error and quits the compiling process
        /// </summary>
        /// <param name="expected">The token compiler expected to get</param>
        /// <param name="unexpceted">The token compiler got from the input</param>
        /// <param name="line">The line where the error took place</param>
        public static void UnexpectedTokenError(string expected, Token unexpected)
        {
            if (unexpected == null)
            {
                Console.Error.WriteLine($"Syntax error: {expected} expected");
            }
            else
            {
                Console.Error.WriteLine($"Syntax error at line {unexpected.Line}: {expected} expected, but {unexpected.Value} was passed");
            }
            Environment.Exit(-1);
        }

        public static void UnexpectedTokenTypeError(TokenType expected, Token unexpected)
        {
            if (unexpected == null)
            {
                Console.Error.WriteLine($"Syntax error: {expected} expected");
            }
            else
            {
                Console.Error.WriteLine($"Syntax error at line {unexpected.Line}: {expected} expected, but {unexpected.Type} was passed");
            }
            Environment.Exit(-1);
        }

        #endregion

        #region Semantic Errors

        public static void VariableIsNotDeclaredError(string id)
        {
            Console.Error.WriteLine($"Error: {id} is not declared");
            Environment.Exit(-1);
        }

        public static void FunctionIsNotDeclaredError(string id)
        {
            Console.Error.WriteLine($"Error: {id} is not declared");
            Environment.Exit(-1);
        }

        #endregion
    }
}
