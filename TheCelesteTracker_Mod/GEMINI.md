Celeste modding documentation

## Editing Settings, SaveData and Session

Table of Contents

- [Table of Contents](#table-of-contents)
- [Introduction](#introduction)
- [Setup](#setup)
- [Usage](#usage)
- [Custom Settings](#custom-settings)
  - [Beginner Settings](#beginner-settings)
    - [`Boolean`](#boolean)
    - [`Int32` (`int`)](#int32-int)
    - [`Enum`](#enum)
    - [`Single` (`float`)](#single-float)
    - [`String`](#string)
    - [`ButtonBinding`](#buttonbinding)
    - [Submenus](#submenus)
    - [Generic attributes](#generic-attributes)
      - [`[SettingName]`](#settingname)
      - [`[SettingSubText]`](#settingsubtext)
      - [`[SettingSubHeader]`](#settingsubheader)
      - [`[SettingInGame]`](#settingingame)
      - [`[SettingNeedsRelaunch]`](#settingneedsrelaunch)
  - [Advanced settings](#advanced-settings)
    - [Create menu items manually](#create-menu-items-manually)
    - [Settings created at runtime](#settings-created-at-runtime)
    - [Full-screen submenu (Main menu only)](#full-screen-submenu-main-menu-only)
    - [Custom setting items](#custom-setting-items)
  - [Grandmaster Settings](#grandmaster-settings)

# Introduction

Celeste stores persistent data in three ways.

- `Session`:
  Contains data relevant to the current playthrough, like the death count, time spent, currently collected strawberries, and the current room.  
  Session data is persistent across _Save & Quit_, and is reset during _Restart Chapter_ or _Return to Map_.

- `SaveData`:
  Contains data relevant to the entire save file, like the total number of dashes, least deaths, all-time collected strawberries, and whether the _Crystal Heart_ has been collected.  
  Save data is persistent across an entire save file, and is only reset when deleting the save file it's tied to.

- `Settings`:
  Contains data global to the entirety of Celeste, like current language, music and SFX volume, the default save file name, and whether _Variant Mode_ has been unlocked.  
  Settings are never reset in-game. The only way to reset settings is by deleting the settings file outside of Celeste.

Everest allows mods to save persistent data in the form of `EverestModuleSession`, `EverestModuleSaveData` and `EverestModuleSettings` classes.

Vanilla information is [serialized :link:](https://en.wikipedia.org/wiki/Serialization) to [XML :link:](https://en.wikipedia.org/wiki/XML) when writing to disk, while Everest defaults to [YAML :link:](https://en.wikipedia.org/wiki/YAML).

If for any reason you need to define your own format for persistent data, you can extend from `EverestModuleBinarySession`, `EverestModuleBinarySaveData` and `EverestModuleBinarySettings`.

# Setup

> [!NOTE]
> If you've used the Celeste Code Mod Template, all of the setup has been done for you.  
> See the [Code Mod Setup](Code-Mod-Setup) page to learn how to use the template.

To be able to store persistent data, you need to create a type which extends from one of the aforementioned Everest types.

Let's make custom save data for example. Create a class which extends from `EverestModuleSaveData`.

```cs
// ExampleModSaveData.cs

namespace Celeste.Mod.ExampleMod;

public class ExampleModSaveData : EverestModuleSaveData
{
}
```

Then, you need to tell Everest the type in which save data is stored. In your module class, override the `SaveDataType` property and set its value to the `ExampleModSaveData` type.

```cs
// ExampleModModule.cs

namespace Celeste.Mod.ExampleMod;

public class ExampleModModule : EverestModule
{
    public static ExampleModModule Instance;

    public override Type SaveDataType => typeof(ExampleModSaveData);

    public ExampleModModule()
    {
        Instance = this;
    }

    // ...
}
```

`EverestModule` exposes your save data in the instance `_SaveData` property. It is recommended to also create a static `SaveData` property, which casts the instance property into the `SaveData` type.

```cs
// ExampleModModule.cs

namespace Celeste.Mod.ExampleMod;

public class ExampleModModule : EverestModule
{
    public static ExampleModModule Instance;

    public override Type SaveDataType => typeof(ExampleModSaveData);
    public static ExampleModSaveData SaveData => (ExampleModSaveData)Instance._SaveData;

    public ExampleModModule()
    {
        Instance = this;
    }
}
```

# Usage

Storing data is simple - just define a **public instance [property :link:](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/properties)** on the class, and assign to it when needed. Note that both accessors have to be public.  
**Anything else will not be serialized.**

> [!TIP]
> If you want to omit a property from serialization, give it the `[YamlIgnore]` attribute.  
> This requires adding the `YamlDotNet` NuGet package.

Let's define some custom save data properties:

```cs
// ExampleModSaveData.cs

namespace Celeste.Mod.ExampleMod;

public class ExampleModSaveData : EverestModuleSaveData
{
    // Public instance properties work
    public bool MyBool { get; set; }

    // Default to 10 if there is no saved value
    public int MyNumber { get; set; } = 10;

    // Don't save the property value
    [YamlIgnore]
    public float MyFloat { get; set; }
}
```

Then, to access the property value, reference the `Session`, `SaveData` or `Settings` properties from your module class and access what you need.  
In this example, `ExampleModModule.SaveData.MyNumber` accesses the `MyNumber` property from our custom save data.

# Custom Settings

The `EverestModuleSettings` type is special. Everest inspects your settings class and generates your mod's settings section in the Mod Options menu.

Its behavior is very customizable.  
For [beginner settings](#beginner-settings), there are a number of attributes you can use to control how your settings behave.  
For [advanced control](#advanced-settings), you can specify how the menu item for your setting property is instantiated and manually add it to the menu.  
If you feel like a [settings grandmaster](#grandmaster-settings), you can override how the entire mod section is generated in your mod module.

> [!TIP]
> If you want a property to not show up in the settings menu, give it the `[SettingIgnore]` attribute.

## Beginner Settings

If you want simple settings, Everest does a lot of the heavy lifting for you. It guesses what menu item to pick based on the property type.  
You may also [add attributes](#generic-attributes) to your properties to tell Everest how you want your settings to behave.

Supported property types include:

- [`Boolean`](#boolean)
- [`Int32` (`int`)](#int32-int)
- [`Enum`](#enum)
- [`Single` (`float`)](#single-float)
- [`String`](#string)
- [`ButtonBinding`](#buttonbinding)

In addition, you can create [submenus](#submenus).

---

### `Boolean`

A `bool` property becomes an On/Off slider.

```cs
// Define an on/off toggle
public bool MyToggle { get; set; }
```

---

### `Int32` (`int`)

An `int` property can either be a slider, or a button which takes the user to a number input menu.

To make the property a slider, you must give it the `[SettingRange]` attribute to define the allowable range for your setting.  
You can also specify whether to optimize the slider for large ranges.

To make the property a slider, you must give it the `[SettingNumberInput]` attribute to define the max amount of digits, and whether negatives are valid.  
By default, negatives are allowed, and the setting can have max `6` digits.

> [!IMPORTANT]
> Settings using `[SettingNumberInput]` will be disabled in-game, as attempting to open the number input menu in-game crashes Celeste.

```cs
// Set the range of MySlider to [-10, 10]
[SettingRange(min: -10, max: 10)]
public int MySlider { get; set; }

// Set the range of MyLargeSlider to [-1000, 1000],
// and optimize for large ranges
[SettingRange(min: -1000, max: 1000, largeRange: true)]
public int MyLargeSlider { get; set; }

// Allow the user to type an up to 6 digits long number
// Note: The setting will be disabled in-game
[SettingNumberInput]
public int MyNumberInput { get; set; }

// Allow the user to type an up to 5 digits long number, without allowing negatives
// Note: The setting will be disabled in-game
[SettingNumberInput(allowNegatives: false, maxLength: 5)]
public int MyOtherNumberInput { get; set; }
```

---

### `Enum`

An `Enum` property becomes a slider, scrolling through all its values.  
The order is determined by the value assigned to said enum.

```cs
public enum MyEnumeration
{
    One,
    Two,
    Three,
    Four,
    Five
}

// Define a slider which scrolls through the options in MyEnumeration
public MyEnumeration MyEnumSlider { get; set; }

// Scroll order is determined by the enum's integer value
// This means that the slider will scroll right in the order of World, There, Hello
public enum MyOtherEnumeration
{
    Hello = 5,
    There = 0,
    World = -5
}

// Define a slider which scrolls through the options in MyOtherEnumeration
public MyOtherEnumeration MyOtherEnumSlider { get; set; } = MyOtherEnumeration.World;
```

---

### `Single` (`float`)

A `float` property becomes a button which takes the user to a number input menu.

You must give it the `[SettingNumberInput]` attribute to define the max amount of digits, and whether negatives are valid.  
By default, negatives are allowed, and the setting can have max `6` digits.

> [!IMPORTANT]
> Settings using `[SettingNumberInput]` will be disabled in-game, as attempting to open the number input menu in-game crashes Celeste.

```cs
// Allow the user to type an up to 6 digits long number
[SettingNumberInput]
public int MyNumberInput { get; set; }

// Allow the user to type an up to 5 digits long number, without allowing negatives
[SettingNumberInput(allowNegatives: false, maxLength: 5)]
public float MyOtherNumberInput { get; set; }
```

---

### `String`

A `string` property becomes a button which takes the user to a text input menu.

You can control the string length range by using `[SettingMinLength]` and `[SettingMaxLength]` attributes.  
By default, the string can be between `1` and `12` characters.

> [!IMPORTANT]
> String settings will be disabled in-game, as attempting to open the text input menu in-game crashes Celeste.

```cs
// Allow the user to type a string between 1 and 12 characters
public string MyTextInput { get; set; }

// Allow the user to type a string exactly 6 characters long
[SettingMinLength(6)]
[SettingMaxLength(6)]
public string MyTextInput { get; set; }
```

---

### `ButtonBinding`

A `ButtonBinding` property allows mods to define custom button bindings, separate from vanilla ones.  
They can be rebound in the mod's Mod Options section.

You can specify the default binding by adding the `[DefaultButtonBinding]` attribute.  
The `Buttons` enum is using the Xbox layout. This means that the A button on Xbox is the B button on Switch, and the X button on PlayStation, to name a few examples.

```cs
// Define a custom button binding
// Defaults to the A button on controller, and the C key on keyboard
[DefaultButtonBinding(button: Buttons.A, key: Keys.C)]
public ButtonBinding MyCustomBinding { get; set; }

// Define another custom button binding
// Defaults to the A, B, X and Y buttons on controller,
// and the Z, X and C keys on keyboard
[DefaultButtonBinding(
    buttons: new[] {
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y
    },
    keys: new[] {
        Keys.Z, Keys.X, Keys.C
    }
)]
public ButtonBinding MyOtherCustomBinding { get; set; }
```

Then, when interacting with the binding, you use `ButtonBinding`'s various members:

- `MyCustomBinding.Pressed` - whether the binding has just been pressed or is being buffered
- `MyCustomBinding.Check` - whether the binding is currently being held
- `MyCustomBinding.Repeating` - whether the binding is being held long enough to repeat
- `MyCustomBinding.Released` - whether the binding has just been released
- `MyCustomBinding.ConsumePress()` - consumes the press and buffer, making the binding no longer report a press from the rest of the current frame and onwards
- `MyCustomBinding.ConsumeBuffer()` - consumes the buffer only, making the binding no longer report a press from the rest of the current frame (**only if the input was buffered**) and onwards

The difference between `ButtonBinding.ConsumePress()` and `ButtonBinding.ConsumeBuffer()` is only evident when called on the same frame that the button press was registered.  
`ButtonBinding.ConsumePress()` will make the binding not report a press from the rest of the current frame onwards, while `ButtonBinding.ConsumeBuffer()` will **continue to report a press** until the next frame.

> [!NOTE]
> By default, `ButtonBinding`s have `0.08` seconds _(`5` frames)_ of buffer time.
>
> If you'd like to change that _(as well as other properties of the binding)_, override `OnInputInitialize` in your module class and modify the binding's properties there.
>
> ```cs
> // ExampleModModule.cs
>
> // Called by Everest in Input.Initialize()
> public override void OnInputInitialize()
> {
>     // Remember to call base.OnInputInitialize(),
>     // so that Everest creates your bindings properly
>     base.OnInputInitialize();
>
>     // Set BufferTime to 0 seconds
>     Settings.MyCustomBinding.BufferTime = 0;
> }
> ```

---

### Submenus

To create a submenu, create a class with a `[SettingSubMenu]` attribute.

Then, make a property typed after the class you just defined.

> [!NOTE]
> All the restrictions which apply to the setting class also apply to the submenu classes.
>
> Also note that not all attributes may be supported or work as expected.  
> If you have an idea on how to fix that, Everest is open to pull requests.

```cs
[SettingSubMenu]
public class ExampleSubMenu
{
    public bool Toggle { get; set; }

    [SettingRange(min: -10, max: 10)]
    public int Slider { get; set; }
}

// Create a submenu
// Remember to initialize it to set its default values
public ExampleSubMenu SubMenu { get; set; } = new();
```

---

### Generic attributes

Among the aforementioned attributes, there are other ones which aren't specific to the property type.

These attributes include:

- [`[SettingName]`](#settingname)
- [`[SettingSubText]`](#settingsubtext)
- [`[SettingSubHeader]`](#settingsubheader)
- [`[SettingInGame]`](#settingingame)
- [`[SettingNeedsRelaunch]`](#settingneedsrelaunch)

---

#### `[SettingName]`

Allows you to define a custom dialog ID for your setting.

If unspecified, the setting dialog ID will be `$"modoptions_{typeName}_{propertyName}`, where `typeName` is the settings type name _(stripping the ending `Settings` part, if any)_, and `propertyName` is the name of your property.

If the dialog ID is not defined, the setting name will be displayed in spaced pascal case.

> [!TIP]
> Dialog IDs are case-insensitive.  
> This means that `"MODOPTIONS_EXAMPLEMOD_ABC"` is the same as `"modoptions_examplemod_abc"`.

```cs
namespace Celeste.Mod.ExampleMod;

// The default dialog keys will begin with "modoptions_ExampleMod_",
// because the "Settings" part is stripped
public class ExampleModSettings : EverestModuleSettings
{
    // Use the default dialog key for this setting,
    // which is "modoptions_ExampleMod_UnnamedSetting"
    // If there is such a dialog ID defined, use its translation,
    // else default to spaced pascal case, which is "Unnamed Setting"
    public bool UnnamedSetting { get; set; }

    // Use a custom dialog key for this setting
    // If there is such a dialog ID defined, use its translation,
    // else default to spaced pascal case, which is "Named Setting"
    [SettingName("EXAMPLEMOD_SETTINGS_NAMEDSETTING")]
    public bool NamedSetting { get; set; }
}
```

This attribute can also be applied to the settings type to define the dialog ID used for the settings title header.

If unspecified, the title dialog ID will be `$"modoptions_{typeName}_title"`, where `typeName` is the settings type name _(stripping the ending `Settings` part, if any)_.

If the dialog ID is not defined, the title header will be displayed in spaced pascal case.

```cs
namespace Celeste.Mod.ExampleMod;

// Use a custom dialog key for the Mod Options title header
// If there is such a dialog ID defined, use its translation,
// else default to spaced pascal case, which is "Example Mod",
// because the "Settings" part is stripped
[SettingName("EXAMPLEMOD_SETTINGS_TITLE")]
public class ExampleModSettings : EverestModuleSettings
{
}
```

---

#### `[SettingSubText]`

Allows you to define a description for a setting, which shows when it is selected.

Everest tries to interpret the attribute contents as a Dialog ID, and if it can't, it displays the contents unchanged.

> [!TIP]
> While theoretically you can use any text, you should be using dialog IDs. This ensures that your descriptions are translatable.

```cs
// Add a description with the given dialog ID
// If there is such a dialog ID defined, use its translation,
// else display it unchanged
[SettingSubText("MODOPTIONS_EXAMPLEMOD_EXAMPLE_HINT")]
public bool ToggleWithDescription { get; set; }
```

---

#### `[SettingSubHeader]`

Allows you to separate your settings with a subheader.

Everest tries to interpret the attribute contents as a Dialog ID, and if it can't, it displays the contents unchanged.

> [!TIP]
> While theoretically you can use any text, you should be using dialog IDs. This ensures that your subheaders are translatable.

```cs
// Add a subheader before the setting with the given dialog ID
// If there is such a dialog ID defined, use its translation,
// else display it unchanged
[SettingSubHeader("MODOPTIONS_EXAMPLEMOD_SUBHEADER")]
public bool ToggleWithSubHeader { get; set; }
```

---

#### `[SettingInGame]`

Allows you to specify whether the setting should be visible only in-game, or only in the main menu.

```cs
// Show the setting in-game only
[SettingInGame(true)]
public bool InGameOnlyToggle { get; set; }

// Show the setting in the main menu only
[SettingInGame(false)]
public bool MainMenuOnlyToggle { get; set; }
```

---

#### `[SettingNeedsRelaunch]`

Allows you to warn the user when the setting is changed that a restart of Celeste will be required for changes to be applied.

```cs
// Warn when the setting is changed that a restart of Celeste is required
[SettingNeedsRelaunch]
public bool RestartRequiredToggle { get; set; }
```

## Advanced settings

Sometimes you need more control over your settings than what Everest provides by default. Fortunately, Everest allows you to specify how the menu item corresponding to your settings property is created, and hand everything else off to Everest.

For example:

- What if you want to disable a setting based on some other setting?
- What if you want to dynamically create menu items?
- What if you want to create a completely custom menu item?
- What if you want to create a button which takes you to a full-screen submenu?

All of that is possible, and is explained here.

---

### Create menu items manually

To specify how your setting menu item is created, create the **public instance** `CreateXYZEntry` method, where `XYZ` is the name of your property.  
The method expects two parameters:

- `TextMenu menu`: the menu to which the menu item should be added
- `bool inGame`: whether the menu was opened in-game

Everest converts the following property types into the following menu items:

- `Boolean`
  - `TextMenu.OnOff`
- `Int32` (`int`)
  - `[SettingRange(largeRange: false)]`: `TextMenu.Slider`
  - `[SettingRange(largeRange: true)]`: `TextMenuExt.IntSlider`
  - `[SettingNumberInput]`: `TextMenu.Button`, which goes to `OuiNumberInput`
- `Enum`
  - `TextMenu.Slider`
- `Single` (`float`)
  - `TextMenu.Button`, which when pressed, goes to `OuiNumberInput`
- `String`
  - `TextMenu.Button`, which when pressed, goes to `OuiModOptionString`
- `ButtonBinding`
  - `TextMenu.Button` which on press will...
    - For controller, add a new `ModuleSettingsButtonConfigUI` entity to the scene
    - For keyboard, add a new `ModuleSettingsKeyboardConfigUI` entity to the scene
- Submenus
  - `TextMenuExt.SubMenu`

> [!TIP]
> If you want to see exactly how Everest constructs your settings based on the structure of your code, check out this _behemoth of a method_ over at [`EverestModule.CreateModMenuSection` :link:](https://github.com/EverestAPI/Everest/blob/a178cc3b863807a72aed1a40706dc1db3b2d3df5/Celeste.Mod.mm/Mod/Module/EverestModule.cs#L639).
>
> Everest heavily utilizes [reflection :link:](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/) to construct mod settings for you.
> The code may be hard to read if you're not very comfortable with C#.
>
> Here are relevant code snippets for the given property types:
>
> - `Boolean`
>   - [link :link:](https://github.com/EverestAPI/Everest/blob/a178cc3b863807a72aed1a40706dc1db3b2d3df5/Celeste.Mod.mm/Mod/Module/EverestModule.cs#L701-L706)
> - `Int32` (`int`) with `[SettingRange]`
>   - [link :link:](https://github.com/EverestAPI/Everest/blob/a178cc3b863807a72aed1a40706dc1db3b2d3df5/Celeste.Mod.mm/Mod/Module/EverestModule.cs#L707-L719)
> - `Int32` (`int`) or `Single` (`float`) with `[SettingNumberInput]`
>   - [link :link:](https://github.com/EverestAPI/Everest/blob/a178cc3b863807a72aed1a40706dc1db3b2d3df5/Celeste.Mod.mm/Mod/Module/EverestModule.cs#L720-L748)
> - `Enum`
>   - [link :link:](https://github.com/EverestAPI/Everest/blob/a178cc3b863807a72aed1a40706dc1db3b2d3df5/Celeste.Mod.mm/Mod/Module/EverestModule.cs#L749-L763)
> - `String`
>   - [link :link:](https://github.com/EverestAPI/Everest/blob/a178cc3b863807a72aed1a40706dc1db3b2d3df5/Celeste.Mod.mm/Mod/Module/EverestModule.cs#L764-L781)
> - `ButtonBinding`
>   - [link :link:](https://github.com/EverestAPI/Everest/blob/a178cc3b863807a72aed1a40706dc1db3b2d3df5/Celeste.Mod.mm/Mod/Module/EverestModule.cs#L531-L532)
> - Submenus
>   - [link :link:](https://github.com/EverestAPI/Everest/blob/a178cc3b863807a72aed1a40706dc1db3b2d3df5/Celeste.Mod.mm/Mod/Module/EverestModule.cs#L813-L847)

Here's an example of making an `int` property which:

- switches between `40` and `50`
- whose name is sourced from the `MODOPTIONS_EXAMPLEMOD_INTEGERTOGGLE` dialog ID
- is only enabled in-game

```cs
// Create the property; then Everest will look for a CreateIntegerToggleEntry and invoke it
public int IntegerToggle { get; set; } = 40;

// If you need to, you can store the menu entry to edit later.
private TextMenu.OnOff IntegerToggleEntry;

// Specify how to create the menu item
public void CreateIntegerToggleEntry(TextMenu menu, bool inGame)
{
    // Create a new TextMenu.OnOff item
    // with the MODOPTIONS_EXAMPLEMOD_INTEGERTOGGLE dialog ID
    // and make it on if IntegerToggle is 50
    menu.Add(IntegerToggleEntry = new TextMenu.OnOff(
        label: Dialog.Clean("MODOPTIONS_EXAMPLEMOD_INTEGERTOGGLE"),
        on: IntegerToggle == 50
    ));

    // Disable it if not in-game
    IntegerToggleEntry.Disabled = !inGame;

    // On change, set IntegerToggle to 50 if on, and 40 if off
    IntegerToggleEntry.Change(newValue => IntegerToggle = newValue ? 50 : 40);
}
```

> [!NOTE]
> `CreateXYZEntry` methods also work in submenus.  
> **However, the first parameter changes from `TextMenu` to `TextMenuExt.SubMenu`!**
>
> ```cs
> public ExampleMenu Menu { get; set; } = new();
>
> [SettingSubMenu]
> public class ExampleMenu
> {
>     public int IntegerToggle { get; set; } = 40;
>
>     private TextMenu.OnOff IntegerToggleEntry;
>
>     // Note that the first parameter changes to a TextMenuExt.SubMenu
>     public void CreateIntegerToggleEntry(TextMenuExt.SubMenu subMenu, bool inGame)
>     {
>         subMenu.Add(IntegerToggleEntry = new TextMenu.OnOff(
>             label: Dialog.Clean("MODOPTIONS_EXAMPLEMOD_EXAMPLEMENU_INTEGERTOGGLE"),
>             on: IntegerToggle == 50
>         ));
>
>         IntegerToggleEntry.Disabled = !inGame;
>
>         IntegerToggleEntry.Change(newValue => IntegerToggle = newValue ? 50 : 40);
>     }
> }
> ```

---

### Settings created at runtime

The `CreateXYZEntry` doesn't _have_ to be tied to the property it's named after. In fact, you're not restricted to one menu item per `CreateXYZEntry` method.  
This can be utilized to create a dynamic settings menu.

Here's an example: a submenu which contains a bunch of dynamic on/off settings.

```cs
// The dictionary stores the actual dynamic settings
// Remember to make it a public instance property so that it gets serialized
public Dictionary<string, bool> DynamicSettings { get; set; } = new();

// Don't serialize the dynamic menu - there's nothing there anyway
[YamlIgnore]
public DynamicSettingsMenu SettingsMenu { get; set; } = new();

// Create the actual submenu class
[SettingSubMenu]
public class DynamicSettingsMenu
{
    // Create a dummy property so that we can make use of the CreateDummyEntry method
    // We won't be actually using this - we just want the method
    [YamlIgnore]
    public bool Dummy { get; set; }

    // If you need to access the setting items in the future, they'll be stored here
    public Dictionary<string, TextMenu.OnOff> DynamicSettingItems = new();

    // Remember that the first argument becomes a TextMenuExt.SubMenu
    public void CreateDummyEntry(TextMenuExt.SubMenu menu, bool inGame)
    {
        Dictionary<string, bool> dynamicSettings = ExampleModModule.Settings.DynamicSettings;

        foreach ((string settingName, bool settingValue) in dynamicSettings)
        {
            // Note that the setting name won't be translatable
            TextMenu.OnOff settingEntry = new(
                label: settingName,
                on: settingValue
            );

            settingEntry.Change(newValue => dynamicSettings[settingName] = newValue);

            DynamicSettingItems[settingName] = settingEntry;
            menu.Add(settingEntry);
        }
    }
}
```

Now you can add, remove, read from and write to dynamic settings by accessing the `DynamicSettings` dictionary.

> [!NOTE]
> The `CreateXYZEntry` method is only called once, when the Mod Options menu is about to be opened.
>
> This means that when a dynamic setting is added while the Mod Options menu is already open, it needs to be closed and opened again for it to be interactible.

---

### Full-screen submenu (Main menu only)

To create a full-screen submenu, create a class which extends from `OuiGenericMenu` and implements the `OuiModOptions.ISubmenu` interface.  
_(the **Oui** stands for **O**verworld **U**ser **I**nterface)_

Then, to access the menu, call `OuiGenericMenu.Goto<T>`.  
It has one required parameter, an `Action<Overworld>` which will be called when returning from the current submenu to the parent menu. If necessary, it can be accessed in the `backToParentMenu` field.  
Any other parameters passed to the method are accessible from the Oui's `parameters` field.

```cs
// OuiExampleSubmenu.cs

namespace Celeste.Mod.ExampleMod;

public class OuiExampleSubmenu : OuiGenericMenu, OuiModOptions.ISubmenu
{
    // Set the submenu title
    // Titles are generally in all uppercase
    public override string MenuName => "EXAMPLE SUBMENU";

    // Add menu items
    // Note the casing - this method is in camelCase
    protected override void addOptionsToMenu(TextMenu menu)
    {
        // The "return to parent Oui" Action<Overworld> is found in the
        // "backToParentMenu" field

        // Any remaining parameters passed to "OuiGenericMenu.Goto<T>(...)"
        // are present in the "parameters" field

        TextMenu.OnOff exampleToggle = new(
            label: "Example Toggle",
            on: false
        );

        exampleToggle.Change(newValue =>
            Logger.Debug(nameof(OuiExampleSubmenu), $"Example Toggle set to {newValue}.")
        );

        menu.Add(exampleToggle);
    }
}
```

Then, in the settings you can create a button which accesses the submenu when pressed.

```cs
// Create a dummy property so that we can make use of the CreateSubmenuExampleEntry method
// We won't be actually using this - we just want the method
[YamlIgnore]
public bool SubmenuExample { get; set; }

public void CreateSubmenuExampleEntry(TextMenu menu, bool inGame)
{
    // Only add the button if in the main menu
    if (inGame)
        return;

    TextMenu.Button submenuButton = new("Submenu Example");

    // Go to our custom menu
    // When exiting, return to the Mod Options menu
    submenuButton.Pressed(() =>
        OuiGenericMenu.Goto<OuiExampleSubmenu>(
            backToParentMenu: overworld => overworld.Goto<OuiModOptions>()
        )
    );

    menu.Add(submenuButton);
}
```

---

### Custom setting items

If any of the built-in menu items don't suit your needs, you can always create your own.

To do that, create a class which extends from `TextMenu.Item`, and override its properties.  
After your implementation is complete, simply add the item to the `TextMenu`.

> [!TIP]
> You may use existing classes for reference, like `TextMenu.Button` or `TextMenu.Slider<T>`.

Here is an example menu item which plays a sound when any of the _Confirm_, _Left_ or _Right_ binds are pressed.

```cs
// ExampleMenuItem.cs

public class ExampleMenuItem : TextMenu.Item
{
    public string Label;

    public ExampleMenuItem(string label)
        => Label = label;

    // Menu item properties

    public override float LeftWidth()
        => ActiveFont.Measure(Label).X;

    public override float Height()
        => ActiveFont.LineHeight;

    // Mod Search support

    public override string SearchLabel()
        => Label;

    // Interactions

    public override void ConfirmPressed()
        => PlaySound();

    public override void LeftPressed()
        => PlaySound();

    public override void RightPressed()
        => PlaySound();

    private static void PlaySound()
        => Audio.Play(SFX.ui_game_increment_strawberry);

    // Rendering

    public override void Render(Vector2 position, bool highlighted)
    {
        float alpha = Container.Alpha;
        bool isTwoColumn = Container.InnerContent == TextMenu.InnerContentMode.TwoColumn;

        ActiveFont.DrawOutline(
            Label,
            position: position + (isTwoColumn
                ? Vector2.Zero
                : Vector2.UnitX * (Container.Width / 2f)),
            justify: isTwoColumn
                ? Vector2.UnitY / 2f
                : Vector2.One / 2f,
            scale: Vector2.One,
            color: Disabled
                ? Color.DarkSlateGray
                : (highlighted ? Container.HighlightColor : Color.White) * alpha,
            stroke: 2f,
            strokeColor: Color.Black * (alpha * alpha * alpha)
        );
    }
}
```

## Grandmaster Settings

If you feel independent and want to handle mod options all by yourself, you can define a `CreateModMenuSection` method in your mod module.

> [!IMPORTANT]
> Because you're overriding the method, **Everest will no longer handle the menu creation**.  
> This means that none of the aforementioned attributes, nor the `CreateXYZEntry` methods will work.

```cs
// ExampleModModule.cs

namespace Celeste.Mod.ExampleMod;

public class ExampleModModule : EverestModule
{
    public static ExampleModModule Instance;

    public override Type SessionType => typeof(ExampleModSession);
    public static ExampleModSession Session => (ExampleModSession)Instance._Session;

    public override Type SaveDataType => typeof(ExampleModSaveData);
    public static ExampleModSaveData SaveData => (ExampleModSaveData)Instance._SaveData;

    public override Type SettingsType => typeof(ExampleModSettings);
    public static ExampleModSettings Settings => (ExampleModSettings)Instance._Settings;

    public ExampleModModule()
    {
        Instance = this;
    }

    // Override how the mod menu section is created
    // The pauseSnapshot argument represents the Level.PauseSnapshot,
    // which lets you change how the sound is muffled
    // (for example, when hovering over the Music/SFX sliders in vanilla)
    protected override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance pauseSnapshot)
    {
        // Remember to add the section header, else your settings won't be visible
        CreateModMenuSectionHeader(menu, inGame, pauseSnapshot);

        // Now, add your own stuff!

        // Add your keyboard/controller binding buttons, if necessary
        CreateModMenuSectionKeyBindings(menu, inGame, pauseSnapshot);
    }
}
```

> [!NOTE]
> If you also need control over how your Mod Options section, or how the buttons which open the keybindings menu are created, you can override the `CreateModMenuSectionHeader` and `CreateModMenuSectionKeyBindings` methods as well.
>
> ```cs
> // ExampleModModule.cs
>
> namespace Celeste.Mod.ExampleMod;
>
> public class ExampleModModule : EverestModule
> {
>     public static ExampleModModule Instance;
>
>     public override Type SessionType => typeof(ExampleModSession);
>     public static ExampleModSession Session => (ExampleModSession)Instance._Session;
>
>     public override Type SaveDataType => typeof(ExampleModSaveData);
>     public static ExampleModSaveData SaveData => (ExampleModSaveData)Instance._SaveData;
>
>     public override Type SettingsType => typeof(ExampleModSettings);
>     public static ExampleModSettings Settings => (ExampleModSettings)Instance._Settings;
>
>     public ExampleModModule()
>     {
>         Instance = this;
>     }
>
>     protected override void CreateModMenuSectionHeader(TextMenu menu, bool inGame, EventInstance pauseSnapshot)
>     {
>         // Create your Mod Options section header
>     }
>
>     protected override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance pauseSnapshot)
>     {
>         // Create your Mod Options settings
>         // Make sure to create the header first!
>     }
>
>     protected override void CreateModMenuSectionKeyBindings(TextMenu menu, bool inGame, EventInstance pauseSnapshot)
>     {
>         // Create your Mod Options section key bindings buttons
>         // (the "Keyboard Config" / "Controller Config" buttons)
>     }
> }
> ```
