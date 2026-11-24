using System.Globalization;
using System.Numerics;
using AnimalBehaviors;
using Meitou.Game.Ui;

namespace Husbandry;

/// <summary>
/// The Hitching Post's animal list (docs/husbandry.md "Hitching Post"): an <b>Animals</b> button on a selected post of the player's opens a window
/// with each animal hitched there, its sex and age, how well it is eating and what it is doing, and a female's next litter. It reads only
/// <see cref="PostRoster"/>, published by the system, on the host's thread.
/// </summary>
public sealed class HitchingPostWindow(Func<PostRoster?> roster) : IUiExtension
{
    #region Window

    /// <summary>
    /// The window's width in pixels.
    /// </summary>
    const float Width = 820;

    /// <summary>
    /// The window's top, below the HUD's top bar.
    /// </summary>
    const float Top = 90;

    /// <summary>
    /// One line's height in pixels.
    /// </summary>
    const float Line = 28;

    /// <summary>
    /// The window's inner margin in pixels.
    /// </summary>
    const float Margin = 14;

    /// <summary>
    /// The table's columns: name, race, sex, age, hunger, doing, next litter.
    /// </summary>
    static readonly float[] Columns = [170, 120, 70, 70, 150, 140, 72];

    /// <summary>
    /// The post shown (its site id); null while the window is closed. Written and read on the host's thread.
    /// </summary>
    string? _site;

    /// <summary>
    /// The post's capacity, from the selection that opened the window.
    /// </summary>
    int _capacity;

    /// <summary>
    /// The registered style and action keys.
    /// </summary>
    Keys _keys = null!;

    public string Id => HusbandryMod.Id + ".hitching-post";

    public void Register(UiRegistration registration)
    {
        _keys = new Keys(
            registration.RegisterStyle("post-window", new UiStyle { Background = new Vector4(0.07f, 0.07f, 0.06f, 0.94f) }),
            registration.RegisterStyle("post-title", Text(new Vector4(0.95f, 0.85f, 0.55f, 1), 1.15f)),
            registration.RegisterStyle("post-head", Text(new Vector4(0.7f, 0.7f, 0.66f, 1), 0.9f)),
            registration.RegisterStyle("post-text", Text(new Vector4(0.92f, 0.92f, 0.88f, 1), 1)),
            registration.RegisterStyle("post-button", Button()),
            registration.RegisterAction("post-close", _ => _site = null),
            registration.Key("post-window"));

        registration.AddBuildingActions(Actions);
        string control = registration.RegisterControl("post-window", _ => Window());
        registration.Contribute(UiSlots.Overlay, frame => frame.CreateControl(control), order: 10);
    }

    /// <summary>
    /// The Animals button on a selected Hitching Post of the player's.
    /// </summary>
    IEnumerable<UiBuildingAction> Actions(UiBuildingContext context)
    {
        var building = context.Building;
        if (!building.Owned || building.Record != HusbandryData.Post || building.SiteId is not { } site || roster() is not { } posts) return [];

        var view = posts.For(site, HusbandryData.HitchCapacity);
        string caption = HusbandryText.Format("Animals ({1}/{2})", view.Animals.Count, view.Capacity);
        string tooltip = HusbandryText.Get("Show the animals hitched here and how well they eat.");
        return [new UiBuildingAction(caption, tooltip, () => Toggle(site, view.Capacity))];
    }

    /// <summary>
    /// Opens the window on a post, or closes it when it shows that post.
    /// </summary>
    void Toggle(string site, int capacity)
    {
        _site = _site == site ? null : site;
        _capacity = capacity;
    }

    /// <summary>
    /// The window's node this frame: hidden while closed or before the first world.
    /// </summary>
    UiNode Window()
    {
        if (_site is null || roster() is not { } posts) return new UiNode(_keys.Window) { Visible = false };

        var view = posts.For(_site, _capacity);
        var nodes = new List<UiNode>();
        float top = Margin;
        string title = HusbandryText.Format("Hitching Post: {1} of {2} places taken", view.Animals.Count, view.Capacity);
        nodes.Add(Label(nodes, title, _keys.Title, Width - 2 * Margin - 90, Margin, top));
        nodes.Add(CloseButton(nodes, top));
        top += Line + 6;

        top = Table(nodes, view, top);
        string hint = HusbandryText.Get("Hitched animals eat from a feeder or food on the ground near the post. Below 200 hunger they breed less.");
        nodes.Add(Label(nodes, hint, _keys.Head, Width - 2 * Margin, Margin, top + 4));
        return Panel(nodes, top + Line + 4 + Margin);
    }

    #endregion

    #region Table

    /// <summary>
    /// The header and a row per animal (or a line saying there are none); returns the top of the next line.
    /// </summary>
    float Table(List<UiNode> nodes, PostRosterView view, float top)
    {
        string[] head = ["Name", "Race", "Sex", "Age", "Hunger", "Doing", "Next litter"];
        Cells(nodes, [.. head.Select(HusbandryText.Get)], _keys.Head, top);
        top += Line;
        if (view.Animals.Count == 0)
        {
            nodes.Add(Label(nodes, HusbandryText.Get("No animals are hitched here."), _keys.Text, Width - 2 * Margin, Margin, top));
            return top + Line;
        }

        foreach (var animal in view.Animals)
        {
            Cells(nodes, Row(animal), _keys.Text, top);
            top += Line;
        }

        return top;
    }

    /// <summary>
    /// One animal's cells.
    /// </summary>
    static string[] Row(PostAnimal a)
    {
        string hunger = HusbandryText.Get(Hitches.Fed(a.Hunger)) + ", " + PostRoster.HungerText(a.Hunger);
        string next = a.NextLitter is { } hours ? HusbandryText.Format("{1} h", ((int)hours).ToString(CultureInfo.InvariantCulture)) : "-";
        return [a.Name, a.Race, HusbandryText.Get(a.Female ? "female" : "male"), HusbandryText.Get(a.Age), hunger, HusbandryText.Get(a.Doing), next];
    }

    /// <summary>
    /// A row of labels, one per column.
    /// </summary>
    void Cells(List<UiNode> nodes, string[] cells, string style, float top)
    {
        float left = Margin;
        for (int k = 0; k < cells.Length; k++)
        {
            nodes.Add(Label(nodes, cells[k], style, Columns[k] - 8, left, top));
            left += Columns[k];
        }
    }

    #endregion

    #region Nodes

    /// <summary>
    /// The window's styles and actions, and its node id.
    /// </summary>
    sealed record Keys(string WindowStyle, string Title, string Head, string Text, string ButtonStyle, string Close, string Window);

    /// <summary>
    /// A label style in <paramref name="colour"/> at <paramref name="scale"/>.
    /// </summary>
    static UiStyle Text(Vector4 colour, float scale) => new() { Text = colour, TextScale = scale, TextAlign = "Left VCenter" };

    /// <summary>
    /// The button style.
    /// </summary>
    static UiStyle Button() => new()
    {
        Background = new Vector4(0.24f, 0.21f, 0.16f, 1),
        Hover = new Vector4(0.36f, 0.31f, 0.22f, 1),
        Text = new Vector4(0.95f, 0.92f, 0.85f, 1),
        TextAlign = "Center",
    };

    /// <summary>
    /// A label at a spot in the window.
    /// </summary>
    UiNode Label(List<UiNode> nodes, string text, string style, float width, float left, float top) =>
        new($"{_keys.Window}.label{nodes.Count}", UiNodeKind.Label)
        {
            Placement = new UiPlacement(width, Line, Left: left, Top: top), Style = style, Text = text,
        };

    /// <summary>
    /// The Close button at the title's right.
    /// </summary>
    UiNode CloseButton(List<UiNode> nodes, float top) => new($"{_keys.Window}.button{nodes.Count}", UiNodeKind.Button)
    {
        Placement = new UiPlacement(80, Line - 4, Left: Width - Margin - 80, Top: top + 2), Style = _keys.ButtonStyle,
        Text = HusbandryText.Get("Close"), Action = _keys.Close,
    };

    /// <summary>
    /// The window panel holding the nodes, centred across the screen below the top bar.
    /// </summary>
    UiNode Panel(List<UiNode> nodes, float height)
    {
        var placement = new UiPlacement(Width, height, Left: new UiLength(-Width / 2, 0.5f), Top: Top);
        var window = new UiNode(_keys.Window) { Placement = placement, Style = _keys.WindowStyle };
        window.Children.AddRange(nodes);
        return window;
    }

    #endregion
}
