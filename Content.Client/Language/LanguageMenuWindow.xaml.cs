using Content.Client.Language.Systems;
using Content.Shared.Language;
using Content.Shared.Language.Systems;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Console;
using Robust.Shared.Utility;
using Serilog;
using static Content.Shared.Language.Systems.SharedLanguageSystem;

namespace Content.Client.Language;

[GenerateTypedNameReferences]
public sealed partial class LanguageMenuWindow : DefaultWindow
{
    private readonly LanguageSystem _clientLanguageSystem;
    private readonly List<EntryState> _entries = new();

    public LanguageMenuWindow()
    {
        RobustXamlLoader.Load(this);
        _clientLanguageSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<LanguageSystem>();
    }

    protected override void Opened()
    {
        // Refresh the window when it gets opened.
        // This actually causes two refreshes: one immediately, and one after the server sends a state message.
        UpdateState(_clientLanguageSystem.CurrentLanguage, _clientLanguageSystem.SpokenLanguages);
        _clientLanguageSystem.RequestStateUpdate();
    }


    public void UpdateState(string currentLanguage, List<string> spokenLanguages)
    {
        var langName = Loc.GetString($"language-{currentLanguage}-name");
        CurrentLanguageLabel.Text = Loc.GetString("language-menu-current-language", ("language", langName));

        OptionsList.RemoveAllChildren();
        _entries.Clear();

        foreach (var language in spokenLanguages)
        {
            AddLanguageEntry(language);
        }

        // Disable the button for the currently chosen language
        foreach (var entry in _entries)
        {
            if (entry.button != null)
                entry.button.Disabled = entry.language == currentLanguage;
        }
    }

    private void AddLanguageEntry(string language)
    {
        var proto = _clientLanguageSystem.GetLanguagePrototype(language);
        var state = new EntryState { language = language };

        var container = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };

        #region Header
        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 2
        };

        var name = new Label
        {
            Text = proto?.Name ?? Loc.GetString("generic-error"),
            MinWidth = 50,
            HorizontalExpand = true
        };

        var button = new Button { Text = "Choose" };
        button.OnPressed += _ => OnLanguageChosen(language);
        state.button = button;

        header.AddChild(name);
        header.AddChild(button);

        container.AddChild(header);
        #endregion

        #region Collapsible description
        var body = new CollapsibleBody
        {
            HorizontalExpand = true,
            Margin = new Thickness(4f, 4f)
        };

        var description = new RichTextLabel { HorizontalExpand = true };
        description.SetMessage(proto?.Description ?? Loc.GetString("generic-error"));
        body.AddChild(description);

        var collapser = new Collapsible(Loc.GetString("language-menu-description-header"), body)
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };

        container.AddChild(collapser);
        #endregion

        // Before adding, wrap the new container in a PanelContainer to give it a distinct look
        var wrapper = new PanelContainer();
        wrapper.StyleClasses.Add("PdaBorderRect");

        wrapper.AddChild(container);
        OptionsList.AddChild(wrapper);

        _entries.Add(state);
    }


    private void OnLanguageChosen(string id)
    {
        var proto = _clientLanguageSystem.GetLanguagePrototype(id);
        if (proto != null)
            _clientLanguageSystem.RequestSetLanguage(proto);
    }

    private struct EntryState
    {
        public string language;
        public Button? button;
    }
}
