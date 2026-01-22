using BeefGen.Classes.Beef;

namespace BeefGen;

public class Program
{
    public static void Main()
    {
        // Example use case
        var genBeefBindings = new GenBeefCBindings(
            $"{Directory.GetCurrentDirectory()}/onnxruntime/onnxruntime_c_api.h", 
            $"{Directory.GetCurrentDirectory()}/beef/onnxruntime.bf", 
            "onnxruntime.dll", 
            "OnnxRuntime");
    }
}