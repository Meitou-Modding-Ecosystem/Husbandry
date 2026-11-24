using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Modding;

namespace Husbandry.Capsules;

/// <summary>
/// The capsule throw's animation (docs/husbandry.md "Beast Capsules", "Data file"): an ANIMATION_FILE naming the throw clip's files in
/// items/animations (one per body skeleton), linked to every race built on the game's human skeletons, and a disabled COMBAT_TECHNIQUE that plays
/// the clip, which <see cref="CapsuleThrows"/> plays as a mod combat move (disabled, so no fight ever picks it).
/// </summary>
public static class ThrowData
{
    /// <summary>
    /// The record ids: the COMBAT_TECHNIQUE and the ANIMATION_FILE.
    /// </summary>
    public const string Technique = "34-" + HusbandryData.FileName, File = "35-" + HusbandryData.FileName;

    /// <summary>
    /// The clip's name in both files.
    /// </summary>
    public const string Clip = "husbandry_capsule_throw";

    /// <summary>
    /// The clip's files for male and female bodies (male_skeleton and female_skeleton bones); the game links them by bare name.
    /// </summary>
    const string MaleFile = @".\mods\Husbandry\items\animations\husbandry_throw_male.skeleton",
        FemaleFile = @".\mods\Husbandry\items\animations\husbandry_throw_female.skeleton";

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
    /// The technique and the animation file.
    /// </summary>
    public static IEnumerable<FcsRecord> Build()
    {
        var technique = HusbandryData.New(Technique, FcsRecordType.CombatTechnique, "Husbandry capsule throw");
        technique.Strings["anim name"] = Clip;
        technique.Bools["disabled"] = true;
        yield return technique;

        var file = HusbandryData.New(File, FcsRecordType.AnimationFile, "Husbandry animations");
        file.Filenames["male animation"] = MaleFile;
        file.Filenames["female animation"] = FemaleFile;
        yield return file;
    }

    /// <summary>
    /// Adds the animation file to the <c>animation files</c> of every humanoid race (<see cref="Humanoid"/>).
    /// </summary>
    public static void LinkRaces(ModDataContext c)
    {
        foreach (var race in c.Data.OfType(FcsRecordType.Race).Where(Humanoid))
            c.Edit(race.StringId).References["animation files"] = [new FcsReference(File, 0, 0, 0)];
    }

    /// <summary>
    /// Whether a race's body is one of the game's human meshes, so the clip's bones fit it.
    /// </summary>
    static bool Humanoid(GameRecord race)
    {
        string mesh = race.GetPath("male mesh").Replace('\\', '/').TrimStart('.', '/').ToLowerInvariant();
        return mesh.StartsWith(HumanoidMeshes, StringComparison.Ordinal) && !mesh.EndsWith(QueenMesh, StringComparison.Ordinal);
    }
}
