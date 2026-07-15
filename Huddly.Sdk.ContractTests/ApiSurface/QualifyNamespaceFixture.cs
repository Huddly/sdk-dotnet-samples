namespace SystemDiagnosticsExtensions;

/// <summary>
/// Fixture only: lives in a namespace that starts with the literal text "System" but isn't
/// System.* itself, to verify ApiSurfaceGenerator.Qualify checks for a real dot-boundary rather
/// than a bare string prefix.
/// </summary>
public interface IQualifyNamespaceFixtureMarker;

/// <summary>
/// Fixture only: shares its simple name ("Collision") with Huddly.Sdk.ContractTests.ApiSurface's
/// own CollisionAttribute, to verify attribute names get the same namespace-qualification as
/// regular types.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CollisionAttribute : Attribute;
