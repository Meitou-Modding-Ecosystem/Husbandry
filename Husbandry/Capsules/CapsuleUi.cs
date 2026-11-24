using System.Numerics;
using Meitou.Game.Ui;

namespace Husbandry.Capsules;

/// <summary>
/// The capsule throw's interface (docs/husbandry.md "Beast Capsules"): the right-click row Throw Beast Capsule on a knocked-out beast a capsule may
/// catch, and the pink mist where a thrown capsule lands. Both run on the host's thread and read only <see cref="CapsuleThrows"/>' published
/// views; choosing the row queues a <see cref="ThrowCapsuleCommand"/> for the selection.
/// </summary>
public sealed class CapsuleUi(Func<CapsuleThrows?> throws, Action<Vector3> mist) : IUiExtension
{
    /// <summary>
    /// The row's caption.
    /// </summary>
    public const string Caption = "Throw Beast Capsule";

    /// <summary>
    /// The hidden overlay node whose frame drains the landings (<see cref="Mist"/>).
    /// </summary>
    string _mistKey = "";

    public string Id => HusbandryMod.Id;

    public void Register(UiRegistration registration)
    {
        registration.AddCharacterMenuVerbs(Rows);
        _mistKey = registration.Key("throw-mist");
        registration.Contribute(UiSlots.Overlay, _ => Mist());
    }

    /// <summary>
    /// The row, when the menu is for a catchable beast; none otherwise.
    /// </summary>
    IEnumerable<UiMenuVerb> Rows(UiMenuContext context)
    {
        if (throws() is not { } system || !system.Catchable(context.Target.Id)) return [];

        var selected = context.Selected.ToArray();
        var target = context.Target.Id;
        return [new UiMenuVerb(HusbandryText.Get(Caption), () => context.Enqueue(new ThrowCapsuleCommand(selected, target) { Tick = context.Tick }))];
    }

    /// <summary>
    /// Once a frame (the overlay is arranged each frame): plays the mist at each capsule landed since the last; the node itself shows nothing.
    /// </summary>
    UiNode Mist()
    {
        if (throws() is { } system)
            while (system.Landed.TryDequeue(out var at)) mist(at);

        return new UiNode(_mistKey) { Visible = false };
    }
}
