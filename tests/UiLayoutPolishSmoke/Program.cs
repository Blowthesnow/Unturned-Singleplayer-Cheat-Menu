using UnturnedSingleplayerCheatMenu.Services;

// Source-contract checks, not a substitute for Unity layout/render acceptance.
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/UnturnedSingleplayerCheatMenu"));
string Read(string file) => File.ReadAllText(Path.Combine(root, "UI", "CheatMenuOverlayUi." + file + ".cs"));
var failures = new List<string>();
void Check(bool ok, string name) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}"); if (!ok) failures.Add(name); }
string minimap = Read("MinimapToolsView");
Check(minimap.Contains("settings.Enabled ?") && !minimap.Contains("默认关闭  ·"), "Minimap summary reflects current enabled state");
Check(!minimap.Contains("圆形与正方形视口均已预创建"), "No renderer implementation prose in general settings");
string character = Read("CharacterView");
Check(character.Split("width: 0f").Length - 1 == 6 && !character.Contains("width: 260f"),
    "Six numeric groups share the full form width in equal flexible columns");
Check(character.Contains("CreateFlexibleSpacer(applyRow.transform)"),
    "Apply action aligns with the right edge of the full-width form");
Check(character.Split("CreateRow(statGrid.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2)").Length - 1 == 3,
    "Numeric rows use the same column gap as the toggle row");
string inventory = Read("InventoryToolsView");
Check(inventory.Contains("GameObject selectorRow = CreateRow(panel, 44f, CheatMenuStyle.Theme.Space2)") &&
    inventory.Split("CreateRow(selectorRow.transform, 44f, CheatMenuStyle.Theme.Space2)").Length - 1 == 2 &&
    inventory.Contains("SetLayout(gameObject, 0f, -1f, 1f)") && inventory.Contains("SetLayout(scopeRow, 0f, -1f, 1f)"),
    "Inventory preset and scope share one full-width row with equal column weights");
Check(inventory.Contains("SetLayout(presetButton.gameObject, 0f, -1f, 1f)") &&
    inventory.Contains("SetLayout(scopeButton.gameObject, 0f, -1f, 1f)"),
    "Inventory selectors fill their own columns rather than leaving a blank right half");
Check(!inventory.Contains("150f, 34f, CheatMenuStyle.Accent"), "Broader inventory operation is visually secondary");
string shell = Read("FluentShell");
Check(shell.Contains("44f, 36f, CheatMenuStyle.Surface, 23") && !shell.Contains("SetFocusIndicatorVisible(visible: false)"), "Close is neutral and retains keyboard focus visibility");
Check(shell.Contains("\"切换界面语言并立即重建当前菜单。\", 58f, 40f, CheatMenuStyle.Surface"), "Language switch is secondary");
Check(Read("ToolsShell").Contains("SetOutlineVisible(visible: true)") &&
    !Read("ToolsShell").Contains("SetOutlineVisible(visible: false)"),
    "Secondary navigation preserves both idle and accent interaction borders");
Check(Read("ToolsShell").Contains("_fluentControls.MarkNavigation(button)") &&
    Read("ToolsShell").Contains("pair.Key == tab ? CheatMenuStyle.Accent : CheatMenuStyle.Surface"),
    "Secondary navigation keeps selected-page accent routing");
PluginLocalization.Initialize("English", "Schinese");
Check(PluginLocalization.Translate("状态：{0}  ·  {1}  ·  {2}") == "Status: {0}  ·  {1}  ·  {2}" &&
    PluginLocalization.Translate("小地图已开启") == "Minimap enabled" && PluginLocalization.Translate("小地图已关闭") == "Minimap disabled",
    "English summary format and both enabled states are localized");
PluginLocalization.Initialize("Chinese", "Schinese");
Check(PluginLocalization.Translate("小地图已开启") == "小地图已开启" && PluginLocalization.Translate("小地图已关闭") == "小地图已关闭",
    "Chinese summary states remain Chinese");
string translation = Read("TranslationToolsView");
string toolsShell = Read("ToolsShell");
Check(toolsShell.Contains("BuildWorkshopLocalizationHeader(panel.transform)") &&
    toolsShell.IndexOf("BuildWorkshopLocalizationHeader(panel.transform)") < toolsShell.IndexOf("CreateShortcutScroll(panel.transform)"),
    "Translation task header is outside and before the settings scroll");
Check(translation.Contains("_workshopStartButton.interactable = !workshopLocalization.IsTranslationRunning") &&
    translation.Contains("_workshopStopButton.interactable = workshopLocalization.IsTranslationRunning"),
    "Translation actions reflect the running state without rebuilding inputs");
Check(translation.Contains("_workshopApiExpanded ?? !workshopLocalization.ApiConfigured"),
    "Unconfigured API is expanded by default while configured API is compact");
Check(translation.Contains("if (_workshopLocalizationApiText.text != apiDescription)") &&
    translation.Replace("\r\n", "\n").Contains("_workshopLocalizationApiText.text = apiDescription;\n\t\t\t\tRebuildWorkshopLocalizationScrollLayout();"),
    "Changed API description refreshes scroll height without rebuilding credentials");
Check(translation.Contains("CreateWorkshopDisclosure(panel, \"WorkshopAdvancedSection\"") &&
    translation.Contains("CreateWorkshopDisclosure(panel, \"WorkshopApiSection\""),
    "API settings and advanced cache operations have separate disclosures");
int disclosureStart = translation.IndexOf("private GameObject CreateWorkshopDisclosure(");
int disclosureEnd = disclosureStart < 0 ? -1 : translation.IndexOf("private void BuildWorkshopLocalizationSettingsRegion()", disclosureStart);
string disclosure = disclosureStart >= 0 && disclosureEnd > disclosureStart ? translation[disclosureStart..disclosureEnd] : "";
Check(disclosure.Contains("content.SetActive(expanded)") && !disclosure.Contains("ClearResultHost") &&
    !disclosure.Contains("ResetWorkshopApiKeyDrafts") && disclosure.Contains("RebuildWorkshopLocalizationScrollLayout()"),
    "Disclosure toggles visibility without destroying credentials or resetting drafts");
string world = Read("WorldView");
int environmentCardIndex = world.IndexOf("CreateWorldCard(detailsColumn.transform, \"EnvironmentStatusCard\"", StringComparison.Ordinal);
int eventCardIndex = world.IndexOf("CreateWorldCard(detailsColumn.transform, \"WorldEventsCard\"", StringComparison.Ordinal);
int scenarioCardIndex = world.IndexOf("CreateWorldCard(detailsColumn.transform, \"WorldScenarioCard\"", StringComparison.Ordinal);
int assetOverviewIndex = world.IndexOf("CreateWorldCard(transform, \"AssetOverviewCard\"", StringComparison.Ordinal);
int maintenanceIndex = world.IndexOf("BuildWorldMaintenanceView(transform)", StringComparison.Ordinal);
Check(environmentCardIndex >= 0 && eventCardIndex > environmentCardIndex && scenarioCardIndex > eventCardIndex &&
    assetOverviewIndex > scenarioCardIndex && maintenanceIndex > assetOverviewIndex,
    "World page orders environment, events, scenarios, asset overview, and maintenance without an empty right card");
Check(world.Contains("CreateRow(transform, 0f,") && world.Contains("childAlignment = TextAnchor.UpperLeft") &&
    world.Contains("\"EnvironmentStatusCard\", -1f") && world.Contains("\"WorldEventsCard\", -1f") &&
    world.Contains("\"WorldScenarioCard\", -1f") && world.Contains("\"AssetOverviewCard\", -1f"),
    "World row and all secondary cards derive height from content rather than stretching the time card");
Check(world.Contains("SetLayout(timeCard, 360f, primaryCardHeight, 1f, 0f)") &&
    world.Contains("SetLayout(detailsColumn, 360f, -1f, 1f, 0f)"),
    "World columns have equal preferred widths independent of long localized text");
string flowPath = Path.Combine(root, "UI", "CheatMenuOverlayUi.LayoutPolish.cs");
string flow = File.Exists(flowPath) ? File.ReadAllText(flowPath) : "";
Check(flow.Contains("preferredHeight = -1f") && flow.Contains("minHeight = 24f") && !flow.Contains("ContentSizeFitter"),
    "Readable flow labels use text preferred height under the parent layout");
string overlay = File.ReadAllText(Path.Combine(root, "UI", "CheatMenuOverlayUi.cs"));
int geometryStart = overlay.IndexOf("private void RefreshWindowGeometry(");
int geometryEnd = overlay.IndexOf("private void RefreshInfiniteStorageGeometry()", geometryStart);
Check(geometryStart >= 0 && geometryEnd > geometryStart &&
    overlay[geometryStart..geometryEnd].Contains("RebuildWorkshopLocalizationScrollLayout()"),
    "Resolution and scale changes reflow translation scroll content");
PluginLocalization.Initialize("English", "Schinese");
Check(PluginLocalization.Translate("接口与密钥") == "Connection and keys" &&
    PluginLocalization.Translate("高级与缓存维护") == "Advanced and cache maintenance",
    "Disclosure section labels are localized");
Environment.ExitCode = failures.Count == 0 ? 0 : 1;
