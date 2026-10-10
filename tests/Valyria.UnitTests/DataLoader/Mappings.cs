extern alias DataLoader;

using DataLoader::Valyria.Batch.DataLoader;

namespace Valyria.UnitTests.DataLoader;

/// <summary>
/// Mapster's config is global and can't be changed once anything has been mapped, so every test class
/// shares one call to ConfigureMappings, just like the data loader does at startup
/// </summary>
internal static class Mappings
{
    static Mappings() => MappingProfile.ConfigureMappings();

    public static void EnsureConfigured()
    {
        // Running the static constructor is all that's needed
    }
}
