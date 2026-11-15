using System.Runtime.InteropServices;

namespace ShaderCompiler
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            // New usage: ShaderCompiler <inputPathOrDirectory> <outputDirectory>
            if (args.Length < 2) {
                Console.WriteLine("Usage: ShaderCompiler <input.slang|shadersDir> <outputDir>");
                return 1;
            }

            var input = args[0];
            var outputBase = args[1];
            var anyCompiled = false;
            var hasCompilationFailures = false;

            // Ensure Slang native runtime assets are discoverable (from NuGet package runtimes/*/native)
            if (!TryEnsureSlangNativeDependencies(out var nativeMsg)) {
                Console.WriteLine($"ERROR: Could not locate Slang native runtime assets: {nativeMsg}");
                return 2;
            }

            try {
                string[] files;
                string importRoot;

                if (Directory.Exists(input)) {
                    files = Directory.GetFiles(input, "*.slang", SearchOption.AllDirectories);
                    importRoot = Path.GetFullPath(input);
                }
                else if (File.Exists(input)) {
                    files = [input];
                    importRoot = Path.GetDirectoryName(Path.GetFullPath(input))!;
                }
                else {
                    Console.WriteLine($"Input path not found: {input}");
                    return 1;
                }

                files = files.Where(file => !IsModuleSource(file)).ToArray();

                foreach (var file in files) {
                    Console.WriteLine($"[Slang BuildTime] Processing shader file: {file}");

                    var rel = Path.GetRelativePath(input, file);
                    var outDir = Path.Combine(outputBase, Path.GetDirectoryName(rel) ?? string.Empty);
                    if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                    if (IsComputeShader(file)) {
                        var computeOutPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + ".comp.spv");
                        try {
                            var computeArgs = new[] { file, "-I", importRoot, "-stage", "compute", "-entry", "main", "-target", "spirv" };
                            var computeBytes = Slangc.NET.SlangCompiler.Compile(computeArgs);
                            if (computeBytes is not { Length: > 0 })
                                throw new InvalidOperationException("compute compile produced no output");

                            File.WriteAllBytes(computeOutPath, computeBytes);
                            Console.WriteLine($" Wrote: {computeOutPath}");
                            anyCompiled = true;
                        }
                        catch (Exception ex) {
                            if (File.Exists(computeOutPath))
                                File.Delete(computeOutPath);
                            Console.WriteLine($" Error: compute compile failed for {file}: {ex.Message}");
                            Console.WriteLine($"ERROR: Shader file '{file}' failed to compile.");
                            hasCompilationFailures = true;
                        }

                        continue;
                    }

                    // Vertex
                    var vertOk = false;
                    var vertOutPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + ".vert.spv");

                    try {
                        var vertArgs = new[] { file, "-I", importRoot, "-stage", "vertex", "-entry", "vertexMain", "-target", "spirv" };
                        var vertBytes = Slangc.NET.SlangCompiler.Compile(vertArgs);

                        if (vertBytes is { Length: > 0 }) {
                            File.WriteAllBytes(vertOutPath, vertBytes);
                            Console.WriteLine($" Wrote: {vertOutPath}");
                            anyCompiled = true;
                            vertOk = true;
                        }
                        else {
                            Console.WriteLine($" Warning: vertex compile produced no output for {file}");
                        }
                    }
                    catch (Exception ex) {
                        Console.WriteLine($" Error: vertex compile failed for {file}: {ex.Message}");
                    }
                    finally {
                        if (!vertOk && File.Exists(vertOutPath)) {
                            // Remove stale output to avoid using invalid shaders later
                            try {
                                File.Delete(vertOutPath);
                            }
                            catch {
                                // TODO: log?
                            }
                        }
                    }

                    // Fragment
                    var fragOk = false;
                    var fragOutPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + ".frag.spv");

                    try {
                        var fragArgs = new[] { file, "-I", importRoot, "-stage", "fragment", "-entry", "fragmentMain", "-target", "spirv" };
                        var fragBytes = Slangc.NET.SlangCompiler.Compile(fragArgs);

                        if (fragBytes is { Length: > 0 }) {
                            File.WriteAllBytes(fragOutPath, fragBytes);
                            Console.WriteLine($" Wrote: {fragOutPath}");
                            anyCompiled = true;
                            fragOk = true;
                        }
                        else {
                            Console.WriteLine($" Warning: fragment compile produced no output for {file}");
                        }
                    }
                    catch (Exception ex) {
                        Console.WriteLine($" Error: fragment compile failed for {file}: {ex.Message}");
                    }
                    finally {
                        if (!fragOk && File.Exists(fragOutPath)) {
                            try {
                                File.Delete(fragOutPath);
                            }
                            catch {
                                // TODO: log?
                            }
                        }
                    }

                    // If either stage failed, consider overall compilation for this shader file a failure
                    if (!vertOk || !fragOk) {
                        Console.WriteLine($"ERROR: Shader file '{file}' failed to compile both stages cleanly.");
                        hasCompilationFailures = true;
                    }
                }

                if (hasCompilationFailures)
                    return 5;

                if (!anyCompiled) {
                    Console.WriteLine("ERROR: No shaders were compiled. Ensure Slang runtime native dependencies are present and shaders contain expected entry points.");
                    return 3;
                }

                return 0;
            }
            catch (Exception ex) {
                Console.WriteLine($"CRITICAL ERROR during shader compile pass: {ex.Message}");
                return 4;
            }
        }

        private static bool IsModuleSource(string file)
        {
            foreach (var line in File.ReadLines(file)) {
                var statement = line.Trim();
                if (statement.StartsWith("module ", StringComparison.Ordinal) || statement.StartsWith("public module ", StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        private static bool IsComputeShader(string file) =>
            File.ReadLines(file).Any(line => line.Contains("[shader(\"compute\")]") || line.Contains("[shader(\"compute\" )]"));

        private static bool TryEnsureSlangNativeDependencies(out string message)
        {
            message = string.Empty;

            try {
                var slangType = typeof(Slangc.NET.SlangCompiler);
                var asmPath = slangType.Assembly.Location;

                if (string.IsNullOrEmpty(asmPath) || !File.Exists(asmPath)) {
                    message = $"Could not locate Slang managed assembly location ('{asmPath}').";
                    return false;
                }

                var dir = Path.GetDirectoryName(asmPath) ?? string.Empty;
                var compilerName = OperatingSystem.IsWindows() ? "slang-compiler.dll" : "libslang-compiler.so";
                var pluginName = OperatingSystem.IsWindows() ? "slang-glslang.dll" : "libslang-glslang-2026.14.1.so";
                if (File.Exists(Path.Combine(dir, compilerName)) && File.Exists(Path.Combine(dir, pluginName))) {
                    AddNativeSearchPath(dir);
                    NativeLibrary.Load(Path.Combine(dir, pluginName));
                    message = dir;
                    return true;
                }

                // Walk up to package root (common layout: .../packages/slangc.net/<version>/lib/<tfm>/)
                var packageRoot = Directory.GetParent(dir)?.Parent?.Parent?.FullName;
                if (string.IsNullOrEmpty(packageRoot) || !Directory.Exists(packageRoot)) {
                    // Fallback: search near assembly for runtimes
                    packageRoot = Path.GetDirectoryName(dir) ?? string.Empty;
                }

                var runtimesDir = Path.Combine(packageRoot, "runtimes");
                if (!Directory.Exists(runtimesDir)) {
                    // Sometimes native assets are copied to output; check assembly dir
                    runtimesDir = dir;
                }

                // Search for candidate native dirs that contain slang-glslang or spirv-opt
                var candidates = Directory.Exists(runtimesDir)
                    ? Directory.GetDirectories(runtimesDir, "*", SearchOption.AllDirectories)
                    : Array.Empty<string>();

                foreach (var c in candidates) {
                    // look for native folder
                    if (!Path.GetFileName(c).Equals("native", StringComparison.OrdinalIgnoreCase)) continue;
                    if (Path.GetFileName(Path.GetDirectoryName(c)) != RuntimeInformation.RuntimeIdentifier) continue;
                    var files = Directory.GetFiles(c);

                    foreach (var f in files) {
                        var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                        if (!name.Contains("slang-glslang") && !name.Contains("spirv-opt") && !name.Contains("slang")) continue;

                        // Add this native dir to PATH/LD_LIBRARY_PATH/DYLD_LIBRARY_PATH
                        AddNativeSearchPath(c);
                        message = c;
                        return true;
                    }
                }

                // As a last resort, check assembly directory itself for native libs
                foreach (var f in Directory.GetFiles(dir)) {
                    var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (!name.Contains("slang-glslang") && !name.Contains("spirv-opt") && !name.Contains("slang")) continue;

                    AddNativeSearchPath(dir);
                    message = dir;
                    return true;
                }

                // Fallback: probe global NuGet package cache for Slangc.NET native runtimes
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var nugetPackages = Path.Combine(userProfile, ".nuget", "packages", "slangc.net");

                if (Directory.Exists(nugetPackages)) {
                    var versions = Directory.GetDirectories(nugetPackages);
                    // Choose latest version by name sort (semver-ish)
                    var latest = versions.OrderByDescending(p => p).FirstOrDefault();

                    if (!string.IsNullOrEmpty(latest) && Directory.Exists(latest)) {
                        var runtimesRoot = Path.Combine(latest, "runtimes");

                        if (Directory.Exists(runtimesRoot)) {
                            var nativeDirs = Directory.GetDirectories(runtimesRoot, "native", SearchOption.AllDirectories);

                            foreach (var nd in nativeDirs) {
                                if (Path.GetFileName(Path.GetDirectoryName(nd)) != RuntimeInformation.RuntimeIdentifier) continue;
                                var nativeFiles = Directory.GetFiles(nd);

                                foreach (var nf in nativeFiles) {
                                    var nname = Path.GetFileNameWithoutExtension(nf).ToLowerInvariant();
                                    if (!nname.Contains("slang-glslang") && !nname.Contains("spirv-opt") && !nname.Contains("slang")) continue;

                                    AddNativeSearchPath(nd);
                                    message = nd;
                                    return true;
                                }
                            }
                        }
                    }
                }

                message = "Could not find native runtimes under NuGet package 'runtimes/*/native', assembly output directory, or global NuGet cache.";
                return false;
            }
            catch (Exception ex) {
                message = ex.Message;
                return false;
            }
        }

        private static void AddNativeSearchPath(string nativeDir)
        {
            if (!Directory.Exists(nativeDir)) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

                if (!path.Split(Path.PathSeparator).Contains(nativeDir, StringComparer.OrdinalIgnoreCase)) {
                    path = nativeDir + Path.PathSeparator + path;
                    Environment.SetEnvironmentVariable("PATH", path);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                var ld = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? string.Empty;

                if (!ld.Split(Path.PathSeparator).Contains(nativeDir, StringComparer.Ordinal)) {
                    ld = nativeDir + Path.PathSeparator + ld;
                    Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", ld);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                var dyld = Environment.GetEnvironmentVariable("DYLD_LIBRARY_PATH") ?? string.Empty;

                if (!dyld.Split(Path.PathSeparator).Contains(nativeDir, StringComparer.Ordinal)) {
                    dyld = nativeDir + Path.PathSeparator + dyld;
                    Environment.SetEnvironmentVariable("DYLD_LIBRARY_PATH", dyld);
                }
            }
        }
    }
}
