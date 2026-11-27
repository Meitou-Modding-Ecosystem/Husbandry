using System.Numerics;
using Meitou.Game.Ui;

namespace Husbandry.Capsules;

/// <summary>
/// The capsules' interface (docs/husbandry.md "Beast Capsules"): the right-click row Throw Beast Capsule on a knocked-out beast a capsule may
/// catch, the pink mist where a thrown capsule lands and a filled capsule's tooltip lines (<see cref="CapsuleTooltip"/>). All run on the host's
/// thread and read only the systems' published views; choosing the row queues a <see cref="ThrowCapsuleCommand"/> for the selection.
/// </summary>
public sealed class CapsuleUi(Func<CapsuleThrows?> throws, Func<CapsuleSystem?> capsules, Action<Vector3> mist) : IUiExtension
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
        registration.AddItemTooltipLines(Tooltip);
    }

    /// <summary>
    /// A filled capsule's lines (an older save's full capsule too); none for any other item.
    /// </summary>
    IEnumerable<UiItemTooltipLine> Tooltip(UiItemTooltipContext item)
    {
        bool filled = item.Record == HusbandryData.FilledCapsule
            || item.Record == HusbandryData.Capsule && CapsuleSystem.Key(item.ModTag) is not null;
        return filled ? CapsuleTooltip.Lines(capsules()?.View(item.ModTag)) : [];
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
