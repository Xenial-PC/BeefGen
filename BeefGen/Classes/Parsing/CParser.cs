using BeefGen.Classes.Interfaces;
using CppSharp;
using CppSharp.AST;
using CppSharp.Parser;
using ClangParser = CppSharp.ClangParser;

namespace BeefGen.Classes.Parsing;

public class CParser : IParser
{
    /// <summary>
    /// Parses the C header
    /// </summary>
    /// <param name="input"></param>
    /// <param name="langVersion"></param>
    /// <returns></returns>
    public ASTContext? Parse(string input, LanguageVersion langVersion)
    {
        var parserOptions = new ParserOptions()
        {
            LanguageVersion = langVersion,
            Verbose = true,
        };

        parserOptions.Setup(Platform.Host);

        var parserResult = ClangParser.ParseSourceFile(input, parserOptions);

        if (parserResult.Kind != ParserResultKind.Success)
        {
            if (parserResult.Kind == ParserResultKind.FileNotFound)
                Console.WriteLine($"Error File: {input} not found!");

            for (uint i = 0; i < parserResult.DiagnosticsCount; i++)
            {
                var diag = parserResult.GetDiagnostics(i);

                Console.WriteLine(
                    $"{diag.FileName}:({diag.LineNumber}, {diag.ColumnNumber}) {diag.Level.ToString()}:\n{diag.Message}");
            }

            parserResult.Dispose();
            return null;
        }

        var astContext = ClangParser.ConvertASTContext(parserOptions.ASTContext);
        
        parserResult.Dispose();
        return astContext;
    }
}
