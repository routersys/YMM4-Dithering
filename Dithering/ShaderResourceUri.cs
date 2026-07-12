namespace Dithering;

internal static class ShaderResourceUri
{
    public static Uri Get(string shaderName) => new($"pack://application:,,,/Dithering;component/Shaders/{shaderName}.cso", UriKind.Absolute);
}
