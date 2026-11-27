using System.Text.Json;
using Meitou.Content;
using Husbandry.Training;
using Meitou.Modding;

namespace Husbandry.Tests;

/// <summary>
/// The manifest and the code agree, and the entry is the framework dependents ask for (<c>c.Dependency&lt;IHusbandryApi&gt;("husbandry")</c>).
/// </summary>
public sealed class ManifestTests
{
    [Fact]
    public void The_manifest_names_the_entry_its_id_and_the_framework_surface()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "meitou.json")));
        var root = manifest.RootElement;

        var entry = typeof(HusbandryMod).Assembly.GetType(root.GetProperty("entry").GetString()!);

        Assert.NotNull(entry);
        Assert.True(typeof(IMod).IsAssignableFrom(entry));
        Assert.True(typeof(IHusbandryApi).IsAssignableFrom(entry));
        Assert.True(typeof(IModDependents).IsAssignableFrom(entry));
        Assert.Equal(HusbandryMod.Id, root.GetProperty("id").GetString());
    }

    /// <summary>
    /// The engine's own reader accepts the manifest (a malformed one refuses the mod in game): it needs Animal Behaviors and may use Kenshi Races
    /// (the trainers' drill clip) under the id the code tests for.
    /// </summary>
    [Fact]
    public void The_engine_reads_the_manifest_and_its_dependencies()
    {
        var manifest = CodeModManifest.Read(Path.Combine(AppContext.BaseDirectory, "meitou.json"));

        Assert.Equal(2, manifest.DependsOn.Count);
        var animals = manifest.DependsOn.Single(d => d.Id == AnimalBehaviors.AnimalBehaviorsMod.Id);
        Assert.False(animals.Optional);
        Assert.NotNull(animals.Range);
        var races = manifest.DependsOn.Single(d => d.Id == KenshiRaces.KenshiRacesMod.Id);
        Assert.True(races.Optional);
        Assert.NotNull(races.Range);
    }

    [Fact]
    public void The_own_skills_keep_their_training_job_types()
    {
        var types = AnimalSkills.All.Index().Select(s => (int)TrainingPost.TypeOf(s.Index)).ToList();

        Assert.Equal([4510, 4511, 4512, 4513, 4514, 4515], types);
    }
}
