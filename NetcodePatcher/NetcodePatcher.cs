using System;
using System.IO;
using NetcodePatcher.CodeGen;
using Serilog;

namespace NetcodePatcher;

public static class Patcher
{
    private static readonly string[] AssemblyNameBlacklist = [
        "Unity.Netcode.Runtime",
        "UnityEngine.CoreModule",
        "Unity.Netcode.Components",
        "Unity.Networking.Transport",
        "Assembly-CSharp",
        "ClientNetworkTransform",
    ];

    public static void Patch(string assemblyPath, string outputPath, string[] references)
    {
        if (assemblyPath.ToLower().Contains("mmhook"))
        {
            Log.Warning("Skipping {FileName} as it appears to be a MonoMod hooks file", Path.GetFileName(assemblyPath));
            return;
        }

        var pdbPath = Path.Combine(Path.GetDirectoryName(assemblyPath)!, $"{Path.GetFileNameWithoutExtension(assemblyPath)}.pdb");
        if (!File.Exists(pdbPath))
        {
            Log.Error("Couldn't find debug information ({PdbFileName}) for ({AssemblyFileName}), forced to skip", Path.GetFileName(pdbPath), Path.GetFileName(assemblyPath));
            return;
        }

        Log.Information("Found debug info ({PdbFileName})", Path.GetFileName(pdbPath));
        
        // check if contains blacklisted phrases
        foreach (string blacklisted in AssemblyNameBlacklist)
        {
            if (!assemblyPath.ToLowerInvariant().Contains(blacklisted.ToLowerInvariant())) continue;
            
            Log.Warning("Skipping {FileName} as it contains a blacklisted phrase '{Phrase}'", Path.GetFileName(assemblyPath), blacklisted);
        }
        
        try
        {
            string FormatWhitespace(string input) => input.Replace("||  ", "\r\n").Replace("||", " ");
            
            void OnWarning(string warning)
            {
                Log.Warning($"Warning when patching ({Path.GetFileName(assemblyPath)}): {FormatWhitespace(warning)}");
            }

            void OnError(string error)
            {
                throw new Exception(FormatWhitespace(error));
            }

            Log.Information("Patching : {FileName}", Path.GetFileName(assemblyPath));

            ILPostProcessorFromFile.ILPostProcessFile(assemblyPath, outputPath, references, OnWarning, OnError);
            
            Log.Information("Patched successfully : {FileName} -> {OutputPath}", Path.GetFileName(assemblyPath), Path.GetFileName(outputPath));
        }
        catch (Exception exception)
        {
            Log.Error($"Failed to patch ({Path.GetFileName(assemblyPath)}): {exception}");

            // rename file from _original.dll to .dll
            File.Move(assemblyPath.Replace(".dll", "_original.dll"), assemblyPath);
            File.Move(assemblyPath.Replace(".dll", "_original.pdb"), assemblyPath.Replace(".dll", ".pdb"));
        }
    }
}