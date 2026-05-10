using System;
using System.Linq;
using System.Collections.Generic;
using UAlbion.Api.Settings;
using UAlbion.Formats;
using UAlbion.Formats.Assets;
using UAlbion.Formats.Ids;
using UAlbion.Game.Events;
using UAlbion.Game.Gui.Controls;
using UAlbion.Game.Gui.Text;
using UAlbion.Game.Text;

namespace UAlbion.Game.Gui.Menus;

public class OptionsMenu : ModalDialog
{
    public event EventHandler Closed;

    int _musicVolume;
    int _fxVolume;
    int _combatDelay;
    int _hoverExamine;
    int _merchantBuy;
    int _ctrlSell;
    int _altMultiSell;
    int _qolVersion;

    bool HasLanguageFiles(string language)
        => Assets.IsStringDefined(Base.SystemText.MainMenu_MainMenu, language);

    public OptionsMenu() : base(DialogPositioning.Center) { }

    HorizontalStacker QolRow(TextId label, Func<int> get, Action<int> set)
    {
        // "X"/" " are visual checkmark indicators, not localised text.
        var text = new DynamicText(() => [new TextBlock(get() != 0 ? "X" : " ")], _ => _qolVersion);
        var btn = new Button(new UiText(text));
        btn.OnClick(() => { set(get() != 0 ? 0 : 1); _qolVersion++; });
        var fixedBtn = new FixedWidth(18, btn) { Position = DialogPositioning.Left };
        return new HorizontalStacker(new Label(label), new Greedy(new Spacing(1, 0)), fixedBtn);
    }

    protected override void Subscribed()
    {
        _musicVolume  = ReadVar(V.User.Audio.MusicVolume);
        _fxVolume     = ReadVar(V.User.Audio.FxVolume);
        _combatDelay  = ReadVar(V.User.Gameplay.CombatDelay);
        _hoverExamine = ReadVar(V.User.Qol.HoverExamine);
        _merchantBuy  = ReadVar(V.User.Qol.MerchantDirectBuy);
        _ctrlSell     = ReadVar(V.User.Qol.MerchantCtrlSell);
        _altMultiSell = ReadVar(V.User.Qol.MerchantAltMultiSell);

        var languageButtons = new List<IUiElement>();
        void SetLanguage(string language) => Raise(new SetLanguageEvent(language));

        var languages = new List<(string, string)>();
        var modApplier = Resolve<IModApplier>();
        foreach (var kvp in modApplier.Languages.OrderBy(x => x.Value.ShortName))
            if (HasLanguageFiles(kvp.Key))
                languages.Add((kvp.Key, kvp.Value.ShortName));

        foreach (var (language, shortName) in languages)
            languageButtons.Add(new Button(shortName).OnClick(() => SetLanguage(language)));

        var elements = new List<IUiElement>
        {
            new Spacing(200, 2),
            new Label(Base.UAlbionString.LanguageLabel),
            new HorizontalStacker(languageButtons),
            new Spacing(0, 2),
            new Label(Base.SystemText.Options_MusicVolume),
            new Slider(() => _musicVolume, x => _musicVolume = x, 0, 127),
            new Spacing(0, 2),
            new Label(Base.SystemText.Options_FXVolume),
            new Slider(() => _fxVolume, x => _fxVolume = x, 0, 127),
            new Spacing(0, 2),
            new Label(Base.SystemText.Options_CombatTextDelay),
            new Slider(() => _combatDelay, x => _combatDelay = x, 1, 50),
            new Spacing(0, 2),
            new Divider(CommonColor.Yellow3),
            new Spacing(0, 2),
            new Label(Base.UAlbionString.QolSection),
            new Spacing(0, 1),
            QolRow(Base.UAlbionString.QolHoverExamine,     () => _hoverExamine, v => _hoverExamine = v),
            QolRow(Base.UAlbionString.QolQuickMerchantBuy, () => _merchantBuy,  v => _merchantBuy  = v),
            QolRow(Base.UAlbionString.QolCtrlSell,         () => _ctrlSell,     v => _ctrlSell     = v),
            QolRow(Base.UAlbionString.QolAltMultiSell,     () => _altMultiSell, v => _altMultiSell = v),
            new Spacing(0, 2),
            new Button(Base.SystemText.MsgBox_OK).OnClick(SaveAndClose),
            new Spacing(0, 2),
        };
        var stack = new VerticalStacker(elements);
        AttachChild(new DialogFrame(stack));
    }

    void SaveAndClose()
    {
        var settings = Resolve<ISettings>();
        V.User.Audio.MusicVolume.Write(settings, _musicVolume);
        V.User.Audio.FxVolume.Write(settings, _fxVolume);
        V.User.Gameplay.CombatDelay.Write(settings, _combatDelay);
        V.User.Qol.HoverExamine.Write(settings, _hoverExamine);
        V.User.Qol.MerchantDirectBuy.Write(settings, _merchantBuy);
        V.User.Qol.MerchantCtrlSell.Write(settings, _ctrlSell);
        V.User.Qol.MerchantAltMultiSell.Write(settings, _altMultiSell);
        settings.Save();

        Closed?.Invoke(this, EventArgs.Empty);
        Remove();
    }
}
