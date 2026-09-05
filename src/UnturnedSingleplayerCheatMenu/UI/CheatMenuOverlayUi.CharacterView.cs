using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using SDG.Unturned;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.InventoryArrange;
using UnturnedSingleplayerCheatMenu.InfiniteStorage;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;
using InputField = UnityEngine.UI.InputField;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	private void BuildCharacterTab()
	{
		Transform transform = _contentHost.transform;
		AddVerticalLayout(_contentHost, CheatMenuStyle.Theme.Space3, new RectOffset(12, 12, 8, 8)).childForceExpandHeight = false;
		CharacterPageSnapshot snapshot = _characterPresenter.CaptureSnapshot();
		if (!snapshot.IsReady)
		{
			CreateLabel(transform, "玩家状态尚未准备完成。", 17);
			return;
		}
		CreateSection(transform, "生存状态");
		CreateSummaryPanel(transform, $"生命 {snapshot.Health}/100    饱食 {snapshot.Food}/100    水分 {snapshot.Water}/100    免疫 {snapshot.Virus}/100    体力 {snapshot.Stamina}/100    氧气 {snapshot.Oxygen}/100");
		GameObject gameObject = CreateRow(transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateToggleAction(gameObject.transform, "无敌模式", snapshot.GodModeEnabled, delegate(bool value)
		{
			_characterPresenter.SetGodMode(value);
			SetStatus(value ? "无敌模式已开启。" : "无敌模式已关闭。");
		});
		CreateToggleAction(gameObject.transform, "无限生存状态", snapshot.InfiniteNeedsEnabled, delegate(bool value)
		{
			_characterPresenter.SetInfiniteNeeds(value);
			SetStatus(value ? "无限生存状态已开启。" : "无限生存状态已关闭。");
		});
		CreateLabel(transform, "设置指定数值（0–100；生命最低 1）", 12, CheatMenuStyle.Muted);
		GameObject statGrid = CreateObject("CharacterStatTwoColumnGrid", transform);
		AddVerticalLayout(statGrid, CheatMenuStyle.Theme.Space2, new RectOffset(0, 0, 0, 0));
		SetLayout(statGrid, -1f, CheatMenuStyle.Theme.ControlDefault * 3f + CheatMenuStyle.Theme.Space2 * 2f, 1f, 0f);
		GameObject statRow1 = CreateRow(statGrid.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateLabeledInput(statRow1.transform, "生命", _healthTarget, delegate(string value)
		{
			_healthTarget = value;
		}, width: 0f);
		CreateLabeledInput(statRow1.transform, "饱食", _foodTarget, delegate(string value)
		{
			_foodTarget = value;
		}, width: 0f);
		GameObject statRow2 = CreateRow(statGrid.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateLabeledInput(statRow2.transform, "水分", _waterTarget, delegate(string value)
		{
			_waterTarget = value;
		}, width: 0f);
		CreateLabeledInput(statRow2.transform, "免疫", _virusTarget, delegate(string value)
		{
			_virusTarget = value;
		}, width: 0f);
		GameObject statRow3 = CreateRow(statGrid.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateLabeledInput(statRow3.transform, "体力", _staminaTarget, delegate(string value)
		{
			_staminaTarget = value;
		}, width: 0f);
		CreateLabeledInput(statRow3.transform, "氧气", _oxygenTarget, delegate(string value)
		{
			_oxygenTarget = value;
		}, width: 0f);
		GameObject applyRow = CreateRow(transform, CheatMenuStyle.Theme.ControlCompact, CheatMenuStyle.Theme.Space2);
		CreateFlexibleSpacer(applyRow.transform);
		CreateButton(applyRow.transform, "应用", delegate
		{
			bool flag = _characterPresenter.SetLifeStats((byte)ParseClamped(_healthTarget, 1, 100, 100), (byte)ParseClamped(_foodTarget, 0, 100, 100), (byte)ParseClamped(_waterTarget, 0, 100, 100), (byte)ParseClamped(_virusTarget, 0, 100, 100), (byte)ParseClamped(_staminaTarget, 0, 100, 100), (byte)ParseClamped(_oxygenTarget, 0, 100, 100));
			SetStatus(flag ? "角色生存数值已设置。" : "设置失败。", 3f, flag ? StatusKind.Success : StatusKind.Error);
		}, "将六个输入框中的数值写入当前角色的生命与生存状态。", 120f, CheatMenuStyle.Theme.ControlCompact, CheatMenuStyle.Accent);
		GameObject gameObject3 = CreateRow(transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateAction(gameObject3.transform, "全部恢复", delegate
		{
			SetStatus(_characterPresenter.RestoreEverything() ? "角色状态已全部恢复。" : "恢复失败。");
		}, "恢复生命、生存状态并治疗当前伤势。");
		CreateAction(gameObject3.transform, "补满生存状态", delegate
		{
			SetStatus(_characterPresenter.RefillNeeds() ? "生存状态已补满。" : "操作失败。");
		}, "将饱食、水分、免疫、体力和氧气全部补满。");
		CreateAction(gameObject3.transform, "治疗流血/骨折", delegate
		{
			SetStatus(_characterPresenter.CureInjuries() ? "伤势已治疗。" : "操作失败。");
		}, "停止流血并治愈骨折，不改动其他角色数值。");
		CreateSection(transform, "经验、声望与技能");
		CreateLabel(transform, $"经验 {snapshot.Experience:N0}    声望 {snapshot.Reputation:N0}", 14);
		GameObject gameObject4 = CreateRow(transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateLabeledInput(gameObject4.transform, "经验", _experienceAmount, delegate(string value)
		{
			_experienceAmount = value;
		}, 150f);
		CreateButton(gameObject4.transform, "增加经验", delegate
		{
			uint num = (uint)ParseClamped(_experienceAmount, 1, 100000000, 1000);
			SetStatus(_characterPresenter.AddExperience(num) ? $"已增加 {num:N0} 经验。" : "增加经验失败。");
		}, "按左侧输入值增加角色经验，单次范围为 1 到 100000000。", 110f, 34f);
		CreateLabeledInput(gameObject4.transform, "声望", _reputationAmount, delegate(string value)
		{
			_reputationAmount = value;
		}, 150f);
		CreateButton(gameObject4.transform, "变更声望", delegate
		{
			int num = ParseClamped(_reputationAmount, -1000000, 1000000, 100);
			SetStatus(_characterPresenter.AddReputation(num) ? $"已变更 {num:N0} 声望。" : "变更声望失败。");
		}, "按左侧输入的正数或负数增减角色声望。", 110f, 34f);
		CreateAction(gameObject4.transform, "全部技能满级", delegate
		{
			int num = _characterPresenter.MaxAllSkills();
			SetStatus($"技能已处理，实际变更 {num} 项。");
		}, "将当前角色所有尚未满级的技能提升到满级。");
	}
}
