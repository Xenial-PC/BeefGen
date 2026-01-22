using System.Text;
using BeefGen.Classes.Parsing;
using CppSharp.AST;

namespace BeefGen.Classes.Beef;

public class BindingContext
{
    /// <summary>
    /// CPPSharp AST Context
    /// </summary>
    public ASTContext? AST { get; set; }

    /// <summary>
    /// CParser interface
    /// </summary>
    public CParser Parser { get; set; } = new();

    /// <summary>
    /// Quick list for translation units from AST
    /// </summary>
    public List<TranslationUnit> Units => AST!.TranslationUnits;

    /// <summary>
    /// Tab spacing index
    /// </summary>
    public int TabIndex;

    /// <summary>
    /// String output for the file
    /// </summary>
    public StringBuilder OutputFile = new StringBuilder();

    /// <summary>
    /// Quick function to append a new string to the output
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ignoreTab"></param>
    public void AppendString(string input = "", bool ignoreTab = false)
    {
        var outPut = string.Empty;
        if (!ignoreTab)
        {
            for (var i = 0; i < TabIndex; i++)
                outPut += "\t";
        }
        outPut += input;
        OutputFile.Append($"{outPut}\n");
    }
}
