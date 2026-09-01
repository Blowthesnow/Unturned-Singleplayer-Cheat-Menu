# Legacy uGUI upstream manifest

- Upstream root: `E:\控键库`
- Upstream assembly: `ControlGallery.Ugui`
- Upstream version: `1.1.0`
- Snapshot date: `2026-08-30`
- Production source roots: `ControlGallery/Ugui/Core`, `Controls`, `Runtime`
- Production source file count: `22`
- Plan-recorded source manifest SHA-256: `E445784E7118BEDD8FCDCFC33DDA226C7435AF175F34D4874C6E7FD6E32F1E58`
- Upstream DLL SHA-256: `F65D4A4A277BCDCEF1181DEC1B4952B8E481C0BDA57B5BBFD29CA174A59AD18D`
- Adaptation: `TextMeshProUGUI` -> `UiText`; `TMP_InputField` -> `UnityEngine.UI.InputField`; `TMP_FontAsset` -> injected `UnityEngine.Font`.
- Excluded: gallery builder, icon packs, SVG/PNG assets, WPF code, demo pages, BepInEx/game APIs.
- Ownership: handles unsubscribe listeners and release visual references; the existing page host remains the only owner which queues/destroys GameObjects.
- Time-ring port: `FluentTimeRingOptions`, the ellipse/time-ring geometry from `FluentVectorGraphics.cs` and `FluentHudGraphics.cs`, plus the `FluentTimeRingControl` value/label contract; TMP labels are projected to `UiText` and the host retains localization/data binding.

## Captured upstream files

| Relative path | SHA-256 |
|---|---|
| `ControlGallery/Ugui/Controls/FluentAdvancedControls.cs` | `12CB1365B4D028C7F87239E26A541A1643ACD1C9C8C676EEAF9401CC3B79436D` |
| `ControlGallery/Ugui/Controls/FluentCompositeControls.cs` | `4134A2935C9AEE9F9A161C706D54415AB140832B1CDB1A4F05705C85CB45FEAA` |
| `ControlGallery/Ugui/Controls/FluentControl.cs` | `6048AC8497059E881804D0A79FF5821B3652AC023DE8822C7C67AF8D2A25D164` |
| `ControlGallery/Ugui/Controls/FluentHudControls.cs` | `BF765A004DF2F58D37DA1EC7097DF468F4B7C349DA720C2A36907495CC077793` |
| `ControlGallery/Ugui/Controls/FluentInputControls.cs` | `480549206D80ACB90D70C135681559B2483BEB455295C5F7A43F850C1A0C3BF5` |
| `ControlGallery/Ugui/Core/FluentControlOptions.cs` | `457862AEB976533433E35D67FD9221BAE602B8C9F313F8E9F001E0812BEC0470` |
| `ControlGallery/Ugui/Core/FluentHudOptions.cs` | `A80BDD2872F4193D857F4BB471043500DF0BDFE8EAAA0EC8C02EF43551D4FBB0` |
| `ControlGallery/Ugui/Core/FluentTheme.cs` | `604680A01CF4DDEB63BFC89B681A4F8F2CB3342DC31F44DB58EA24AC159BC2D3` |
| `ControlGallery/Ugui/Core/FluentThemeController.cs` | `4607D6B6935CD944B7A163CB0DECF8D465E3DEFC1AA0418BAB417BB4274D64B1` |
| `ControlGallery/Ugui/Core/FluentThemes.cs` | `9DFE50A737C2E1D2733761F337D4CBE36B0301C794C190D9E1A1F1D986A82C9E` |
| `ControlGallery/Ugui/Core/RgbaColor.cs` | `F4A0C81E4F84C68509314ED73912619DCDD53505062CF21B0F5E4F4659A4DBCD` |
| `ControlGallery/Ugui/Runtime/FluentControlFactory.Advanced.cs` | `31EB84A0ECC31C568AE44D297481BD417E285465C6511ABC92F4E053C31DC131` |
| `ControlGallery/Ugui/Runtime/FluentControlFactory.Composite.cs` | `F6ABEAFBE1B3994018ECF40A52B2C7E128BABD2F3ABBFE31D0EE77A911493643` |
| `ControlGallery/Ugui/Runtime/FluentControlFactory.cs` | `7C38CCC3732F35907DFCCC9004465806C9662F307B8BFE243D7FA619EDC7E7FF` |
| `ControlGallery/Ugui/Runtime/FluentControlFactory.Hud.cs` | `4159F08F37A7DA21F94052DF058DD982A16189B8CBA0CD76A0DF2308CEABEBB9` |
| `ControlGallery/Ugui/Runtime/FluentControlFactory.Input.cs` | `1E51AD409E9FF475822776E242469231CFDBCEFC2B6611FAC12D755ACCBE2066` |
| `ControlGallery/Ugui/Runtime/FluentFocusVisual.cs` | `F04B655283F96F497A62CDD6A06AB7E75095AD6AFE99958309C5512923EDB089` |
| `ControlGallery/Ugui/Runtime/FluentHudGraphics.cs` | `CF0B27D4BC5AFB140339D7AFD6AB894B7735B4590A54335DB7E7CB90722993AE` |
| `ControlGallery/Ugui/Runtime/FluentRepeatButton.cs` | `0375E6CCFE8DEC3EFEB7E56058633ECAD55ED844144B1B974155E7D736803468` |
| `ControlGallery/Ugui/Runtime/FluentRoundedGraphic.cs` | `74373C3E95CAB47E981B3C0AB313E6A8925879E095CCCFD19C60BF05E7D02D65` |
| `ControlGallery/Ugui/Runtime/FluentUnityColor.cs` | `F8D05B0F31D0719D18D5F82A42AD24869FB9C63347257A1F74B730B16572F232` |
| `ControlGallery/Ugui/Runtime/FluentVectorGraphics.cs` | `C18CB56F60D484B2C5F84BBB921A3F31AB9E26AC91048ADB2BBD4575BE516B16` |

Machine-readable capture evidence is also retained in
`artifacts/ugui-redesign-20260830/stage0-baseline/upstream-files.json`.
