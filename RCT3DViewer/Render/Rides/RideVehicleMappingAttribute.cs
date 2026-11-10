namespace RCT3D.Render.Rides;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class RideVehicleMappingAttribute(string identifier, string? legacyIdentifier = null) : Attribute
{
    public string Identifier { get; } = identifier;
    public string? LegacyIdentifier { get; } = legacyIdentifier;
}
