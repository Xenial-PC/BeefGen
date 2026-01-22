using BeefGen.Classes.Extensions;
using BeefGen.Classes.Interfaces;
using CppSharp.AST;
using CppSharp.Parser;
using CppSharp.AST.Extensions;
using Type = CppSharp.AST.Type;
using System.Text;
using Parameter = CppSharp.AST.Parameter;

namespace BeefGen.Classes.Beef;

/// <summary>
/// Converts a simple C header to beef
/// </summary>
public class GenBeefCBindings : IGenerator
{
    /// <summary>
    /// Defines and holds all function aliases
    /// </summary>
    private readonly Dictionary<string, FunctionType> _functionAliases = new();

    /// <summary>
    /// Defines and holds all type aliases
    /// </summary>
    private readonly Dictionary<string, string> _typeAliases = new();

    /// <summary>
    /// Cache system to keep track of resolved types
    /// </summary>
    private readonly Dictionary<Type, string> _resolvedTypeCache = new();

    /// <summary>
    /// Reserved words we don't want to use for param or function names
    /// </summary>
    private static readonly HashSet<string> BeefKeywords = new()
    {
        "in", "out", "ref", "mut", "this", "params", "delegate"
    };

    /// <summary>
    /// The import dll name
    /// </summary>
    private readonly string _dllName;

    /// <summary>
    /// The current namespace for the file
    /// </summary>
    private readonly string _namespace;

    /// <summary>
    /// Contains the parsing context information
    /// </summary>
    public BindingContext Context = new();

    /// <summary>
    /// Initializes and parses the file
    /// </summary>
    /// <param name="hPath"></param>
    /// <param name="oPath"></param>
    /// <param name="dllName"></param>
    /// <param name="namespace"></param>
    public GenBeefCBindings(string hPath, string oPath, string dllName, string @namespace)
    {
        _namespace = @namespace;
        _dllName = dllName;
        GenerateBindings(hPath, oPath);
    }

    /// <summary>
    /// Generates the bindings
    /// </summary>
    /// <param name="hPath"></param>
    /// <param name="oPath"></param>
    public bool GenerateBindings(string hPath, string oPath)
    {
        Context.AST = Context.Parser.Parse(hPath, LanguageVersion.CPP20_GNU);

        CollectAliasesAndFunctions();
        
        if (!GenerateUsings()) return false;
        Context.AppendString($"public class {_namespace}API");
        Context.AppendString("{");
        IncreaseTab();

        if (!GenerateFunctions()) return false;
        if (!GenerateEnums()) return false;
        if (!GenerateStructs()) return false;

        GenerateAliases();

        DecreaseTab();
        Context.AppendString("}");

        File.WriteAllText(oPath, Context.OutputFile.ToString());
        Console.WriteLine("Completed successfully!");

        return true;
    }

    /// <summary>
    /// Generates a usings header
    /// </summary>
    /// <returns></returns>
    public bool GenerateUsings()
    {
        Context.AppendString("using System;");
        Context.AppendString("using System.Interop;");
        Context.AppendString();
        Context.AppendString($"namespace {_namespace};");
        Context.AppendString();
        return true;
    }

    /// <summary>
    /// Generates all the enums in the C header
    /// </summary>
    /// <returns></returns>
    public bool GenerateEnums()
    {
        foreach (var @enum in Context.Units.SelectMany(unit => unit.Enums))
        {
            if (@enum.Comment is { Text: not null }) Context.AppendString($"{@enum.Comment.Text}");
            Context.AppendString("[AllowDuplicates]");
            Context.AppendString($"public enum {@enum.Name} : c_int");
            Context.AppendString(@"{");
            IncreaseTab();

            foreach (var item in @enum.Items)
            {
                if (item.Comment is { Text: not null }) Context.AppendString($"// {item.Comment.Text}");
                Context.AppendString($"case {item.Name} = {item.Value};");
            }

            DecreaseTab();
            Context.AppendString(@"}");
            Context.AppendString();
        }

        return true;
    }

    /// <summary>
    /// Generates all the structs in the C header
    /// </summary>
    /// <returns></returns>
    public bool GenerateStructs()
    {
        Context.TabIndex = 0;
        foreach (var structs in Context.Units.SelectMany(contextUnit => contextUnit.Declarations))
        {
            if (structs is not CppSharp.AST.Class cls) continue;
            var ast = cls.OriginalClass ?? cls;

            if (ast.TagKind != TagKind.Struct) continue;

            Context.AppendString("[CRepr]");
            Context.AppendString($"public struct {structs.Name}");
            Context.AppendString("{");
            IncreaseTab();

            var constructorLine = "public this(";

            var fieldsDict = new Dictionary<string, string>();

            foreach (var field in ast.Fields)
            {
                var fieldType = field.Type;
                var baseType = GetCanonicalType(fieldType);

                var finalType = string.Empty;
                if (baseType is BuiltinType type)
                    finalType = type.Type.ToString();

                if (baseType is TypedefType typedef)
                    finalType = typedef.Declaration.Name;

                if (baseType is TagType tagType)
                {
                    finalType = tagType.Declaration switch
                    {
                        Class clss => clss.Name,
                        Enumeration en => en.Name,
                        _ => finalType
                    };
                }

                if (fieldType is PointerType ptr)
                {
                    var pointee = ptr.Pointee;
                    var desugar = pointee.Desugar();

                    if (desugar is FunctionType)
                        finalType = $"{ConvertName(field.Name)}Fn";
                }

                Context.AppendString($"public {ConvertTypes(finalType)} {ConvertName(field.Name)};");
                Context.AppendString();

                constructorLine += $"{ConvertTypes(finalType)} {ConvertName(field.Name, true)}";
                fieldsDict.TryAdd(ConvertName(field.Name), ConvertName(field.Name, true));
                if (field != ast.Fields.Last()) constructorLine += ", ";
            }

            constructorLine += ")";
            Context.AppendString(constructorLine);
            Context.AppendString("{");
            IncreaseTab();

            foreach (var fields in fieldsDict)
                Context.AppendString($"this.{fields.Key} = {fields.Value};");

            DecreaseTab();
            Context.AppendString("}");

            DecreaseTab();
            Context.AppendString("}");

            Context.AppendString();
        }

        return true;
    }

    /// <summary>
    /// Resolves the AST type to a base type
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private string ResolveType(Type? type)
    {
        if (_resolvedTypeCache.TryGetValue(type, out var cached))
            return cached;

        if (TryGetFunctionType(type, out var fn))
        {
            var alias = _functionAliases
                .FirstOrDefault(kv => Equals(kv.Value, fn)).Key;

            return alias != null ? ConvertTypes(alias) : "void*";
        }

        var baseType = GetCanonicalType(type);

        var result = baseType switch
        {
            BuiltinType b => ConvertTypes(b.Type.ToString()),
            TypedefType t => ConvertTypes(t.Declaration.Name),
            TagType tag => ConvertTypes(tag.Declaration.Name),
            _ => ConvertTypes(baseType.ToString())
        };

        _resolvedTypeCache[type] = result;
        return result;
    }

    /// <summary>
    /// Collects all aliases and functions into a list for later use
    /// </summary>
    public void CollectAliasesAndFunctions()
    {
        foreach (var decl in Context.Units.SelectMany(unit => unit.Declarations))
        {
            switch (decl)
            {
                case TypedefDecl td:
                {
                    if (TryGetFunctionType(td.Type, out var fn))
                    {
                        _functionAliases[td.Name] = fn;
                        break;
                    }

                    _typeAliases[td.Name] = ConvertTypes(td.Type.ToString());
                    break;
                }
                case Class { TagKind: TagKind.Struct } cls:
                {
                    foreach (var field in cls.Fields)
                    {
                        if (!TryGetFunctionType(field.Type, out var fn))
                            continue;

                        var fnName = $"{ConvertName(field.Name)}Fn";

                        _functionAliases.TryAdd(fnName, fn);
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Generates the aliases for functions
    /// </summary>
    /// <returns></returns>
    public bool GenerateAliases()
    {
        foreach (var (key, fn) in _functionAliases)
        {
            var name = ConvertName(key);

            var returnType = ResolveType(fn.ReturnType.Type);

            var parameters = fn.Parameters
                .Select(EmitParameter)
                .ToList();

            var sb = new StringBuilder(128);

            sb.Append("public function ");
            sb.Append(returnType);
            sb.Append(PointerDepth(GetPointerDepth(fn.ReturnType.Type)));
            sb.Append(' ');
            sb.Append(name);
            sb.Append('(');

            for (var i = 0; i < parameters.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(parameters[i]);
            }

            sb.Append(");");

            var input = sb.ToString();
            Context.AppendString(input);
            Context.AppendString();
        }

        return true;
    }

    /// <summary>
    /// Gets the base type / actual type
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static Type? GetCanonicalType(Type? type)
    {
        var visited = new HashSet<Type>();

        while (type != null)
        {
            if (!visited.Add(type)) return type;

            switch (type)
            {
                case TypedefType td:
                    type = td.Declaration.Type;
                    continue;

                case PointerType ptr:
                    type = ptr.Pointee;
                    continue;

                case ArrayType arr:
                    type = arr.Type;
                    continue;

                default:
                    return type;
            }
        }

        return type;
    }

    /// <summary>
    /// Fixes the param name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static string FixParamName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "value";

        if (name == "this")
            return "allocator";

        if (BeefKeywords.Contains(name))
            return name + "Value";

        return name;
    }

    /// <summary>
    /// Recursively desugars and gets the base function types
    /// </summary>
    /// <param name="type"></param>
    /// <param name="fn"></param>
    /// <returns></returns>
    private static bool TryGetFunctionType(Type? type, out FunctionType fn)
    {
        fn = null!;
        var visited = new HashSet<Type>();

        while (type != null)
        {
            if (!visited.Add(type))
                return false;

            type = type.Desugar();

            switch (type)
            {
                case PointerType ptr:
                    type = ptr.Pointee;
                    continue;

                case TypedefType td:
                    type = td.Declaration.Type;
                    continue;

                case FunctionType f:
                    fn = f;
                    return true;

                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Generates the parameter
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    private string EmitParameter(Parameter p)
    {
        var ptrDepth = GetPointerDepth(p.Type);
        var baseType = ResolveType(p.Type);
        var name = FixParamName(ConvertName(p.Name, true));

        return $"{baseType}{PointerDepth(ptrDepth)} {name}";
    }

    /// <summary>
    /// Generates the pointer depth
    /// </summary>
    /// <param name="pointerDepth"></param>
    /// <returns></returns>
    private string PointerDepth(int pointerDepth)
    {
        var pointers = string.Empty;
        for (var i = 0; i < pointerDepth; i++)
            pointers += "*";
        return pointers;
    }

    /// <summary>
    /// Gets the pointer depth of a type
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private int GetPointerDepth(Type type)
    {
        var depth = 0;

        while (type is PointerType ptr)
        {
            depth++;
            type = ptr.Pointee;
        }

        return depth;
    }

    /// <summary>
    /// Generates the functions from the C header
    /// </summary>
    /// <returns></returns>
    public bool GenerateFunctions()
    {
        foreach (var fn in Context.Units.SelectMany(u => u.Functions))
        {
            if (fn.IsInline || fn.IsOperator)
                continue;

            var name = ConvertName(fn.Name);

            var returnType = ResolveType(fn.ReturnType.Type);

            var parameters = fn.Parameters
                .Select(EmitParameter)
                .ToList();

            Context.AppendString($"[CLink, Import(\"{_dllName}\")]");

            var sb = new StringBuilder(128);
            sb.Append("public static extern ");
            sb.Append(returnType);
            sb.Append(' ');
            sb.Append(name);
            sb.Append('(');

            for (var i = 0; i < parameters.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                sb.Append(parameters[i]);
            }

            sb.Append(");");

            Context.AppendString(sb.ToString());
            Context.AppendString();
        }

        return true;
    }

    /// <summary>
    /// Convert types to Beef 
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    /// TODO: Create a dictionary to hold these values
    public string ConvertTypes(string input)
    {
        input = input.ReplaceWord("unsigned char", "uint8");
        input = input.ReplaceWord("uint32_t", "uint32");
        input = input.ReplaceWord("int32_t", "int32");
        input = input.ReplaceWord("UChar", "uint8");
        input = input.ReplaceWord("ULong", "uint64");
        input = input.ReplaceWord("UShort", "uint16");
        input = input.ReplaceWord("Short", "int16");
        input = input.ReplaceWord("Long", "int64");
        input = input.ReplaceWord("unsigned int", "uint32");
        input = input.ReplaceWord("char", "char8");
        input = input.ReplaceWord("Char", "char8");
        input = input.ReplaceWord("long", "int32");
        input = input.ReplaceWord("va_list", "void*");
        input = input.ReplaceWord("short", "uint16");
        input = input.ReplaceWord("Bool", "bool");
        input = input.ReplaceWord("int", "int32");
        input = input.ReplaceWord("INT", "int32");
        input = input.ReplaceWord("UInt", "uint32");
        input = input.ReplaceWord("Int", "int32");
        input = input.ReplaceWord("STRING", "char8*");
        input = input.ReplaceWord("FLOAT", "float");
        input = input.ReplaceWord("DOUBLE", "double");
        input = input.ReplaceWord("Void", "void");
        input = input.ReplaceWord("ULongLong", "uint64");
        input = input.ReplaceWord("LongLong", "int64");
        input = input.ReplaceWord("WideChar", "char16");
        
        if (input.StartsWith("const"))
            input = input.Remove(0, 6);

        if (input.StartsWith("unsigned") && !input.EndsWith("int"))
            input = input.Remove(0, 9);

        return input;
    }

    /// <summary>
    /// Converts the type, param, and or function name to the desired Beef style syntax
    /// </summary>
    /// <param name="input"></param>
    /// <param name="isParam"></param>
    /// <returns></returns>
    public string ConvertName(string input, bool isParam = false)
    {
        var splitInput = input.Split('_');
        var output = string.Empty;
        if (isParam)
        {
            for (var i = 0; i < splitInput.Length; i++)
            {
                var paramName = splitInput[i];
                if (i == 0)
                {
                    output += paramName;
                    continue;
                }

                if (paramName.Length > 0)
                {
                    var toUpper = paramName[0].ToString().ToUpper();
                    paramName = $"{toUpper + paramName.Remove(0, 1)}";
                }

                output += paramName;
            }

            return output;
        }

        foreach (var name in splitInput)
        {
            var paramName = name;
            if (name.Length > 0)
            {
                var toUpper = paramName[0].ToString().ToUpper();
                paramName = $"{toUpper + paramName.Remove(0, 1)}";
            }
            output += paramName;
        }

        return output;
    }

    /// <summary>
    /// Increases the tab for proper styling
    /// </summary>
    private void IncreaseTab() => Context.TabIndex++;

    /// <summary>
    /// Decreases the tab for proper styling
    /// </summary>
    private void DecreaseTab() => Context.TabIndex--;
}
