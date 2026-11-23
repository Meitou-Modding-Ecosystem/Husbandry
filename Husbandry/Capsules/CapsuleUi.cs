using Meitou.Game.Ui;

namespace Husbandry.Capsules;

/// <summary>
/// The right-click row Throw Beast Capsule on a knocked-out beast a capsule may catch (docs/husbandry.md "Beast Capsules"). It runs on the host's
/// thread and reads only <see cref="CapsuleThrows"/>' published views; choosing it queues a <see cref="ThrowCapsuleCommand"/> for the selection.
/// </summary>
public sealed class CapsuleUi(Func<CapsuleThrows?> throws) : IUiExtension
{
    /// <summary>
    /// The row's caption.
    /// </summary>
    public const string Caption = "Throw Beast Capsule";

    public string Id => HusbandryMod.Id;

    public void Register(UiRegistration registration) => registration.AddCharacterMenuVerbs(Rows);

    /// <summary>
    /// The row, when the menu is for a catchable beast; none otherwise.
    /// </summary>
    IEnumerable<UiMenuVerb> Rows(UiMenuContext context)
    {
        if (throws() is not { } system || !system.Catchable(context.Target.Id)) return [];

        var selected = context.Selected.ToArray();
        var target = context.Target.Id;
        return [new UiMenuVerb(Caption, () => context.Enqueue(new ThrowCapsuleCommand(selected, target) { Tick = context.Tick }))];
    }
}
