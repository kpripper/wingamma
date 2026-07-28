using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Vortice.D3DCompiler;
using static Vortice.D3DCompiler.Compiler;

namespace WinGamma
{
    internal static class ShaderCache
    {
        public static ReadOnlyMemory<byte> LoadOrCompile(string shaderPath,
            string entryPoint, string profile)
        {
            byte[] source = File.ReadAllBytes(shaderPath);
            byte[] identity = Encoding.UTF8.GetBytes(
                entryPoint + "\0" + profile + "\0WinGamma-HSL-v1");
            byte[] combined = new byte[source.Length + identity.Length];
            Buffer.BlockCopy(source, 0, combined, 0, source.Length);
            Buffer.BlockCopy(identity, 0, combined, source.Length,
                identity.Length);
            string hash = Convert.ToHexString(SHA256.HashData(combined))
                .Substring(0, 24);
            string directory = Path.Combine(SettingsStore.DataDirectory,
                "ShaderCache");
            string cachePath = Path.Combine(directory,
                hash + "-" + entryPoint + ".cso");
            try
            {
                if (File.Exists(cachePath))
                    return File.ReadAllBytes(cachePath);
            }
            catch (Exception exception)
            {
                SettingsStore.Log("Shader cache read failed: " + exception);
            }

            ReadOnlyMemory<byte> bytecode = CompileFromFile(shaderPath,
                entryPoint, profile, ShaderFlags.OptimizationLevel3);
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(cachePath, bytecode.ToArray());
            }
            catch (Exception exception)
            {
                SettingsStore.Log("Shader cache write failed: " + exception);
            }
            return bytecode;
        }
    }
}
