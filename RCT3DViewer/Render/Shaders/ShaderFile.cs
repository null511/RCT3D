namespace RCT3D.Render.Shaders;

internal class ShaderFile
{
    public static string Get(string shaderName, string extension)
    {
        var filePath = Path.GetDirectoryName(shaderName)?.Split('/');
        var fileName = Path.GetFileName(shaderName);

        var shaderPath = $"{fileName}.{extension}.spv";

        if (filePath is not null && filePath.Length > 0)
            shaderPath = Path.Combine(Path.Combine(filePath), shaderPath);

        shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", shaderPath);

        return shaderPath;
    }
}
