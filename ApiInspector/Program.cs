using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        var basePath = @"F:\paly\BSManager\BSInstances\1.40.8\Beat Saber_Data\Managed\";
        var output = new System.Text.StringBuilder();
        
        try
        {
            var dm = Assembly.LoadFrom(basePath + "DataModels.dll");
            foreach (var t in dm.GetTypes())
            {
                if (t.Name == "BeatmapLevel" || t.Name == "BeatmapLevelSO" || t.Name == "IBeatmapLevelData" || t.Name == "BeatmapLevelExtensions")
                {
                    output.AppendLine($"\n=== {t.FullName} (base: {t.BaseType?.FullName}) ===");
                    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        output.AppendLine($"  Property: {p.PropertyType.Name} {p.Name} ({p.PropertyType.FullName})");
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        output.AppendLine($"  Method: {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(pp => pp.ParameterType.Name))})");
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var t in ex.Types.Where(x => x != null))
            {
                if (t.Name == "BeatmapLevel" || t.Name == "BeatmapLevelSO" || t.Name == "IBeatmapLevelData" || t.Name == "BeatmapLevelExtensions")
                {
                    output.AppendLine($"\n=== {t.FullName} (base: {t.BaseType?.FullName}) ===");
                    try
                    {
                        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                            output.AppendLine($"  Property: {p.PropertyType.Name} {p.Name} ({p.PropertyType.FullName})");
                        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                            output.AppendLine($"  Method: {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(pp => pp.ParameterType.Name))})");
                    }
                    catch (Exception e) { output.AppendLine($"  Error: {e.Message}"); }
                }
            }
        }
        
        System.IO.File.WriteAllText(@"D:\code\aidemo\bsmodrandom\api_output7.txt", output.ToString());
    }
}
