using CppSharp.AST;
using CppSharp.Parser;

namespace BeefGen.Classes.Interfaces;

public interface IParser
{
    /// <summary>
    /// Interface for parsing C headers
    /// </summary>
    /// <param name="input"></param>
    /// <param name="langVersion"></param>
    /// <returns></returns>
    public ASTContext? Parse(string input, LanguageVersion langVersion);
}
