using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Modding;

namespace Husbandry.Capsules;

/// <summary>
/// Husbandry's animations (docs/husbandry.md "Beast Capsules", "Training", "Data file"): an ANIMATION_FILE naming its clips' files in
/// items/animations (one per body skeleton), linked to every race built on the game's human skeletons, and a disabled COMBAT_TECHNIQUE per clip,
/// played as a mod combat move (disabled, so no fight ever picks it): the capsule throw (<see cref="CapsuleThrows"/>) and the Training Post
/// trainer's drill (<see cref="Training.TrainingDrills"/>).
/// </summary>
public static class ThrowData
{
    /// <summary>
    /// The record ids: the COMBAT_TECHNIQUE and the ANIMATION_FILE.
    /// </summary>
    public const string Technique = "34-" + HusbandryData.FileName, File = "35-" + HusbandryData.FileName;

    /// <summary>
    /// The throw clip's name in both files.
    /// </summary>
    public const string Clip = "husbandry_capsule_throw";

    /// <summary>
    /// The trainer's drill: its COMBAT_TECHNIQUE and its clip's name in both files (Meshy's Cartographer as Kenshi Races' library retargeted it
    /// onto the game's skeletons, copied into Husbandry's files: Husbandry does not depend on Kenshi Races).
    /// </summary>
    public const string DrillTechnique = "40-" + HusbandryData.FileName, DrillClip = "husbandry_training_drill";

    /// <summary>
    /// The drill clip's length in seconds.
    /// </summary>
    public const float DrillSeconds = 2.967f;

    /// <summary>
    /// The clips' files for male and female bodies (male_skeleton and female_skeleton bones); the game links them by bare name.
    /// </summary>
    public const string MaleFile = @".\mods\Husbandry\items\animations\husbandry_anims_male.skeleton",
        FemaleFile = @".\mods\Husbandry\items\animations\husbandry_anims_female.skeleton";

    /// <summary>
    /// The folder of the game's human body meshes (humans, shek, hivers, skeletons, fishmen): every mesh in it uses male_skeleton or
    /// female_skeleton, except the Hive Queen's.
    /// </summary>
    const string HumanoidMeshes = "data/character/meshes/";

    /// <summary>
    /// The one mesh in <see cref="HumanoidMeshes"/> with a skeleton of its own.
    /// </summary>
    const string QueenMesh = "hive_queen.mesh";

    /// <summary>
    /// The techniques and the animation file.
    /// </summary>
    public static IEnumerable<FcsRecord> Build()
    {
        yield return Move(Technique, "Husbandry capsule throw", Clip);
        yield return Move(DrillTechnique, "Husbandry training drill", DrillClip);

        var file = HusbandryData.New(File, FcsRecordType.AnimationFile, "Husbandry animations");
        file.Filenames["male animation"] = MaleFile;
        file.Filenames["female animation"] = FemaleFile;
        yield return file;
    }

    /// <summary>
    /// A disabled COMBAT_TECHNIQUE that plays one clip.
    /// </summary>
    static FcsRecord Move(string id, string name, string clip)
    {
        var technique = HusbandryData.New(id, FcsRecordType.CombatTechnique, name);
        technique.Strings["anim name"] = clip;
        technique.Bools["disabled"] = true;
        return technique;
    }

    /// <summary>
    /// The folder fitted bodies sit in, in any mod: races fitted onto the game's human skeletons (Kenshi Races' convention, read from the data
    /// only: Husbandry does not depend on Kenshi Races).
    /// </summary>
    const string FittedBodies = "/items/races/";

    /// <summary>
    /// Adds the animation file to the <c>animation files</c> of every race on the human skeletons loaded before Husbandry (<see cref="OnHumanSkeleton"/>);
    /// returns how many. A race a mod below Husbandry in the mod list adds does not get it.
    /// </summary>
    public static int LinkRaces(ModDataContext c)
    {
        var races = c.Data.OfType(FcsRecordType.Race).Where(r => OnHumanSkeleton(r.GetPath("male mesh"))).ToList();
        foreach (var race in races) c.Edit(race.StringId).References["animation files"] = [new FcsReference(File, 0, 0, 0)];
        return races.Count;
    }

    /// <summary>
    /// Whether a race whose <c>male mesh</c> is <paramref name="maleMesh"/> is on the game's human skeletons, so the clips' bones fit it: one of the
    /// game's human meshes (not the Hive Queen's), or a fitted body in some mod's <see cref="FittedBodies"/>.
    /// </summary>
    public static bool OnHumanSkeleton(string maleMesh)
    {
        string mesh = maleMesh.Replace('\\', '/').TrimStart('.', '/').ToLowerInvariant();
        bool human = mesh.StartsWith(HumanoidMeshes, StringComparison.Ordinal) && !mesh.EndsWith(QueenMesh, StringComparison.Ordinal);
        return human || mesh.Contains(FittedBodies, StringComparison.Ordinal);
    }

    /// <summary>
    /// The string ids of the races whose <c>animation files</c> name Husbandry's file, in the merged data.
    /// </summary>
    public static IReadOnlySet<string> LinkedRaces(GameDatabase data) => data.OfType(FcsRecordType.Race)
        .Where(r => r.GetReferences("animation files").Any(f => f.TargetStringId == File))
        .Select(r => r.StringId)
        .ToHashSet(StringComparer.Ordinal);
}
