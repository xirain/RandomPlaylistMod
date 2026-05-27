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
            var bsmlPath = System.IO.Path.Combine(basePath, "..", "..", "Plugins", "BSML.dll");
            output.AppendLine($"Loading BSML from: {System.IO.Path.GetFullPath(bsmlPath)}");
            if (!System.IO.File.Exists(System.IO.Path.GetFullPath(bsmlPath)))
            {
                output.AppendLine("BSML.dll not found!");
            }
            else
            {
                var bsml = Assembly.LoadFrom(System.IO.Path.GetFullPath(bsmlPath));
                foreach (var t in bsml.GetTypes())
                {
                    if (t.Name.Contains("IncrementSetting") || t.Name == "StringSetting")
                    {
                        output.AppendLine($"\n=== {t.FullName} ===");
                        output.AppendLine($"BaseType: {t.BaseType?.FullName}");
                        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                            output.AppendLine($"  Field: {f.FieldType.FullName} {f.Name}");
                        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                            output.AppendLine($"  Property: {p.PropertyType.FullName} {p.Name}");
                        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                            output.AppendLine($"  Method: {m.ReturnType.FullName} {m.Name}({string.Join(", ", m.GetParameters().Select(pp => pp.ParameterType.Name))})");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            output.AppendLine($"Error: {ex.Message}");
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
