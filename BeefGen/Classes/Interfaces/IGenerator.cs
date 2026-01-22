namespace BeefGen.Classes.Interfaces;

public interface IGenerator
{
    /// <summary>
    /// Generates the beef bindings with a header in file, and out beef path
    /// </summary>
    /// <param name="hPath"></param>
    /// <param name="oPath"></param>
    public bool GenerateBindings(string hPath, string oPath);

    /// <summary>
    /// Generates base using statements for the current file
    /// </summary>
    public bool GenerateUsings();

    /// <summary>
    /// Converts all enums to beef style
    /// </summary>
    public bool GenerateEnums();

    /// <summary>
    /// Converts all structs to beef style
    /// </summary>
    public bool GenerateStructs();

    /// <summary>
    /// Converts all functions to CRepr calls
    /// </summary>
    public bool GenerateFunctions();

    /// <summary>
    /// Converts types to beef style
    /// </summary>
    /// <param name="input"></param>
    public string ConvertTypes(string input);

    /// <summary>
    /// Converts names to beef style
    /// </summary>
    /// <param name="input"></param>
    /// <param name="isParam"></param>
    public string ConvertName(string input, bool isParam = false);
}
