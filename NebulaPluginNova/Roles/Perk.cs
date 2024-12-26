﻿using Nebula.Compat;
using Nebula.Modules.GUIWidget;
using UnityEngine.Rendering;
using Virial;
using Virial.Compat;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Game;
using Virial.Media;
using Virial.Text;

namespace Nebula.Roles;

file static class PerkUtils
{

    static private Image IconFrameBackSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.FrameBack.png", 100f);
    static private Image IconFrameSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.Frame.png", 100f);
    static private Image IconFrameSpecialSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.FrameSpecial.png", 100f);
    static private Image IconFrameSpecialGemSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.FrameSpecialGem.png", 100f);

    static public void SetSprite(SpriteRenderer icon, SpriteRenderer back, ref Color backColor, Color perkColor, Sprite? iconSprite, Sprite? backSprite, Color? color = null)
    {
        if (icon) icon.sprite = iconSprite;
        if (back)
        {
            if (backSprite != null)
            {
                back.sprite = backSprite;
                backColor = color ?? Color.white;
                back.color = backColor * perkColor;
            }
            else
            {
                back.sprite = PerkInstance.IconMaskSprite.GetSprite();
                back.color = new(0.6f, 0.6f, 0.6f, 0.3f);
            }
        }
    }

    public static void GeneratePerkObject(PerkDefinition definition, Transform parent, out GameObject obj, out SpriteRenderer back, out SpriteRenderer gem, out SpriteRenderer icon)
    {
        obj = UnityHelper.CreateObject("PerkDisplay", parent, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());
        back = UnityHelper.CreateObject<SpriteRenderer>("Back", obj.transform, new(0f, 0f, 0f));
        back.sprite = IconFrameBackSprite.GetSprite();
        back.color = new(0.4f, 0.4f, 0.4f, 0.5f);
        var frame = UnityHelper.CreateObject<SpriteRenderer>("Frame", obj.transform, new(0f, 0f, -0.6f));
        frame.sprite = definition.specialColor.HasValue ? IconFrameSpecialSprite.GetSprite() : IconFrameSprite.GetSprite();
        if (definition.specialColor.HasValue)
        {
            gem = UnityHelper.CreateObject<SpriteRenderer>("Gem", obj.transform, new(0f, 0f, -0.8f));
            gem.sprite = IconFrameSpecialGemSprite.GetSprite();
            gem.color = definition.specialColor.Value;
        }
        else
        {
            gem = null!;
        }
        icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", obj.transform, new(0f, 0f, -0.1f));
    }
}

public class PerkFunctionalDefinition
{
    public enum Category
    {
        Standard,
        NoncrewmateOnly,
    }
    public PerkDefinition PerkDefinition { get; private set; }
    public string Id { get; private init; }
    private Func<PerkDefinition, PerkInstance, PerkFunctionalInstance> InstanceGenerator { get; set; }
    public PerkFunctionalInstance Instantiate(PerkInstance instance) => InstanceGenerator.Invoke(PerkDefinition, instance);
    public Category PerkCategory { get;private set; }
    public IOrderedSharableVariable<int> SpawnRate { get; private init; }
    public IConfiguration SpawnRateConfiguration { get; private init; }
    public PerkFunctionalDefinition(string id, Category category, PerkDefinition definition, Func<PerkDefinition, PerkInstance, PerkFunctionalInstance> generator)
    {
        this.PerkCategory = category;
        this.PerkDefinition = definition;
        this.InstanceGenerator = generator;
        this.Id = id;
        Roles.Register(this);

        SpawnRate = NebulaAPI.Configurations.SharableVariable("perk." + id + ".spawnRate", (0, 100, 10), 100);
        SpawnRateConfiguration = NebulaAPI.Configurations.Configuration(()=> PerkDefinition.DisplayName.Color(Color.Lerp(PerkDefinition.perkColor, Color.white, 0.5f)) + ": " + SpawnRate.Value + "%", () => GUI.API.HorizontalHolder(GUIAlignment.Left, 
            PerkDefinition.GetPerkImageWidget(true, overlay: ()=>PerkDefinition.GetPerkWidget()),
            new NoSGUIMargin(GUIAlignment.Center, new(0.25f, 0f)),
            GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitle), PerkDefinition.DisplayNameTranslationKey),
            ConfigurationAssets.Semicolon,
            new NoSGUIMargin(GUIAlignment.Center, new(0.1f, 0f)),
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsValue), new LazyTextComponent(() => SpawnRate.Value + "%")),
            new GUISpinButton(GUIAlignment.Center, v => { SpawnRate.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); })
            )
        , predicate: category == Category.Standard ? () => GeneralConfigurations.NumOfPlantsOption > 0 : () => GeneralConfigurations.NumOfWarpedPlantsOption > 0
        );
    }
}

public abstract class PerkFunctionalInstance : BindableGameOperator
{
    protected GamePlayer MyPlayer => NebulaGameManager.Instance!.LocalPlayerInfo!;
    public PerkDefinition PerkDefinition { get; private set; }
    public PerkInstance PerkInstance { get; private set; }
    public PerkFunctionalInstance(PerkDefinition perk, PerkInstance instance)
    {
        this.PerkDefinition = perk;
        this.PerkInstance = instance;
    }

    public virtual bool HasAction { get => false; }
    public virtual void OnClick() { }
    
}

public record PerkDefinition(string localizedName, Image? backSprite, Image? iconSprite, Color perkColor, Color? specialColor)
{
    private List<(string orig, string replaced)> allReplacement = new();
    
    public PerkDefinition(string localizedName, int backId, int iconId, Virial.Color perkColor, Virial.Color? specialColor = null) : this(localizedName, GetPerkBackIcon(backId), GetPerkIcon(iconId), perkColor.ToUnityColor(), specialColor?.ToUnityColor()) { }
    public PerkDefinition(string localizedName) : this(localizedName, null, null, Color.white, null) { }
    public PerkInstance? Instantiate(Virial.IBinder? binder = null)
    {
        var perk = NebulaAPI.CurrentGame?.GetModule<PerkHolder>()?.RegisterPerk(this);
        if (perk != null)
        {
            perk.Register(perk);
            binder?.Bind(perk);
        }
        return perk;
    }

    public PerkDefinition AppendReplacement(string orig, string replaced)
    {
        allReplacement.Add((orig, replaced));
        return this;
    }

    public PerkDefinition CooldownText(string orig, float cooldown) => AppendReplacement(orig, cooldown.ToString().Color(Color.red).Bold());
    public PerkDefinition DurationText(string orig, float cooldown) => AppendReplacement(orig, cooldown.ToString().Color(Color.yellow).Bold());
    public PerkDefinition RateText(string orig, float cooldown) => AppendReplacement(orig, cooldown.ToString().Color(Color.cyan).Bold());

    static public Image GetPerkIcon(int id) => SpriteLoader.FromResource("Nebula.Resources.Perks.Front" + id + ".png", 100f);
    static public Image GetPerkBackIcon(int id) => SpriteLoader.FromResource("Nebula.Resources.Perks.Back" + id + ".png", 100f);

    public string DisplayNameTranslationKey => "perk." + localizedName + ".name";
    public string DisplayName => Language.Translate(DisplayNameTranslationKey);
    private TextComponent DetailTextComponent => GUI.API.FunctionalTextComponent(() =>
    {
        var str = Language.Translate("perk." + localizedName + ".detail");
        foreach (var replacement in allReplacement) str = str.Replace(replacement.orig, replacement.replaced);
        return str;
    });
    public GUIWidget GetPerkWidget()
    {
        return GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
        GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), "perk." + localizedName + ".name"),
        GUI.API.Text(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), DetailTextComponent));
    }

    public GUIWidget GetPerkWidgetWithImage()
    {
        return GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
        GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Left, GetPerkImageWidget(), GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), "perk." + localizedName + ".name")),
        GUI.API.Text(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), DetailTextComponent));
    }

    public GUIWidget GetPerkImageWidget(bool masked = false, Action? action = null, GUIWidgetSupplier? overlay = null)
    {
        return new NoSGameObjectGUIWrapper(Virial.Media.GUIAlignment.Center, () =>
        {
            PerkUtils.GeneratePerkObject(this, null!, out var obj, out var back, out _, out var icon);
            Color backColor = Color.white;
            PerkUtils.SetSprite(icon, back, ref backColor, Color.white, iconSprite?.GetSprite(), backSprite?.GetSprite(), perkColor);
            obj.transform.localScale = new(0.45f, 0.45f, 1f);

            if (masked) obj.GetComponentsInChildren<SpriteRenderer>().Do(r => r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask);
            
            if(action != null || overlay != null)
            {
                var button = obj.SetUpButton(true);
                var collider = button.gameObject.AddComponent<CircleCollider2D>();
                collider.radius = 0.76f;
                collider.isTrigger = true;
                if (action != null) button.OnClick.AddListener(action);
                if(overlay != null)
                {
                    button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, overlay.Invoke()));
                    button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
                }
            }

            if (masked) obj.AddComponent<SortingGroup>();

            return (obj, new(0.8f, 0.8f));
        });
    }
}

public class PerkInstance : IGameOperator, ILifespan, IReleasable
{
    static public Image IconMaskSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.PerkMask.png", 100f);
    static private Image IconHighlightSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.Highlight.png", 100f);

    private bool isDead = false;
    bool ILifespan.IsDeadObject => isDead;
    void IReleasable.Release() => OnReleased();

    internal GameObject RelatedGameObject => obj;
    private GameObject obj;
    private SpriteRenderer back;
    private SpriteRenderer gem;
    private SpriteRenderer icon;
    private SpriteRenderer highlight;
    private SpriteRenderer coolDown;
    public PassiveButton Button { get; private set; }

    //backColorは背景色、perkColorはパーク全体にかかる色
    private Color backColor = Color.white, perkColor = Color.white;


    
    public PerkInstance(PerkDefinition definition, Transform parent)
    {
        PerkUtils.GeneratePerkObject(definition, parent, out obj, out back, out gem, out icon);
        highlight = UnityHelper.CreateObject<SpriteRenderer>("Highlight", obj.transform, new(0f, 0f, -0.5f));
        highlight.sprite = IconHighlightSprite.GetSprite();
        coolDown = UnityHelper.CreateObject<SpriteRenderer>("Mask", obj.transform, new(0f, 0f, -0.3f));
        coolDown.sprite = IconMaskSprite.GetSprite();
        coolDown.material.shader = NebulaAsset.GuageShader;

        Button = obj.SetUpButton();
        Button.OnMouseOver.AddListener(() =>
        {
            NebulaManager.Instance.SetHelpWidget(Button, definition.GetPerkWidget());
            VanillaAsset.PlayHoverSE();
            SetHighlight(true);
        });
        Button.OnMouseOut.AddListener(() =>
        {
            NebulaManager.Instance.HideHelpWidgetIf(Button);
            SetHighlight(false);
        });
        var collider = Button.gameObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.62f;

        SetCoolDownColor(Color.red);
        PerkUtils.SetSprite(icon, back, ref backColor, perkColor, definition.iconSprite?.GetSprite(), definition.backSprite?.GetSprite(), definition.perkColor);
        SetHighlight(false);
    }

    public Timer? MyTimer { get; private set; } = null!;

    public int Priority { get; set; } = 0;
    internal int RuntimeId { get; set; } = 0;

    public void OnReleased()
    {
        if (isDead) return;

        isDead = true;

        if (obj) GameObject.Destroy(obj);
        MyTimer?.ReleaseIt();
    }

    public PerkInstance BindTimer(Timer? timer)
    {
        if (MyTimer == timer) return this;

        MyTimer?.ReleaseIt();
        MyTimer = timer;

        return this;
    }

    void Update(GameUpdateEvent ev)
    {
        if (MyTimer != null) SetCoolDown(MyTimer.Percentage);
    }

    public void SetCoolDown(float rate)
    {
        if (isDead) return;

        if (rate > 0f)
        {
            coolDown.gameObject.SetActive(true);
            coolDown.material.SetFloat("_Guage", rate);
        }
        else
            coolDown.gameObject.SetActive(false);
    }

    public PerkInstance SetCoolDownColor(Color color)
    {
        coolDown.sharedMaterial.color = color.AlphaMultiplied(0.5f);

        return this;
    }

    public PerkInstance SetDisplayColor(Color color)
    {
        this.perkColor = color;

        this.back.color = this.backColor * this.perkColor;
        this.icon.color = this.perkColor;

        return this;
    }

    public PerkInstance SetHighlight(bool highlight, Color? color = null)
    {
        if (isDead) return this;

        this.highlight.gameObject.SetActive(highlight);
        if (highlight && color != null) this.highlight.material.color = color.Value;

        return this;
    }

    public void SetGemColor(Color color)
    {
        if (gem)
        {
            gem.color = color;
        }
    }

    internal void UpdateLocalPos(UnityEngine.Vector3 pos)
    {
        if (isDead) return;

        this.obj.transform.localPosition = pos;
    }
}
