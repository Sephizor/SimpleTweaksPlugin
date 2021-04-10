﻿using System;
using System.Collections.Generic;
using System.Text;
using Dalamud;
using Dalamud.Hooking;
using Dalamud.Interface;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class NoFreeCompanyOnNamePlate : UiAdjustments.SubTweak {

        public class Configs : TweakConfig {
            public bool KeepWandererTag;
            public List<string> KeepNameVisible = new();
        }

        private Configs config;
        
        private IntPtr playerNamePlateSetTextAddress;
        private Hook<PlayerNamePlateSetText> playerNamePlateSetTextHook;
        private delegate IntPtr PlayerNamePlateSetText(byte* a1, byte a2, byte a3, byte* a4, byte* a5, byte* a6, uint a7);

        public override string Name => "Hide FC Name on Name Plate";
        public override string Description => "Hides the free company tags (and wanderer tag) from player nameplates.";

        public override void Setup() {
            try {
                playerNamePlateSetTextAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B 5C 24 ?? 45 38 A7 ?? ?? ?? ??");
                base.Setup();
            } catch (Exception ex) {
                SimpleLog.Log($"Failed Setup of {GetType().Name}: {ex.Message}");
            }
        }

        public override void Enable() {
            config = LoadConfig<Configs>() ?? new Configs();
            playerNamePlateSetTextHook ??= new Hook<PlayerNamePlateSetText>(playerNamePlateSetTextAddress, new PlayerNamePlateSetText(NamePlateDetour));
            playerNamePlateSetTextHook?.Enable();
            base.Enable();
        }

        public override void Disable() {
            SaveConfig(config);
            playerNamePlateSetTextHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            playerNamePlateSetTextHook?.Dispose();
            base.Dispose();
        }

        private IntPtr NamePlateDetour(byte* a1, byte a2, byte a3, byte* a4, byte* a5, byte* a6, uint a7) {
            var isHidden = true;
            if (config.KeepWandererTag || config.KeepNameVisible.Count > 0) {
                var i = 0;
                for (i = 0; i < 20; i++) if (a6[i] == 0) break;
                if (i >= 1) {
                    var str = Encoding.UTF8.GetString(a6, i).Trim(' ', '«', '»');
                    var isWanderer = config.KeepWandererTag && PluginInterface.ClientState.ClientLanguage switch {
                        ClientLanguage.German => str == "Wanderin" || str == "Wanderer",
                        ClientLanguage.French => str == "Baroudeuse" || str == "Baroudeur",
                        _ => str == "Wanderer",
                    };

                    if (isWanderer || config.KeepNameVisible.Contains(str)) {
                        isHidden = false;
                    }
                }
            } 

            if (isHidden && a6 != null) a6[0] = 0;
            return playerNamePlateSetTextHook.Original(a1, a2, a3, a4, a5, a6, a7);
        }


        private string inputStringIgnoreTag = string.Empty;
        protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
            ImGui.Checkbox("Don't Hide Wanderer Tag", ref config.KeepWandererTag);
            ImGui.Text("Unhidden FC Tags:");
            ImGui.Indent();
            foreach (var keep in config.KeepNameVisible) {
                ImGui.Text(keep);
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.SmallButton($"{(char) FontAwesomeIcon.Times}##removeIgnoredFC_{keep}")) {
                    config.KeepNameVisible.Remove(keep);
                    ImGui.PopFont();
                    break;
                }
                ImGui.PopFont();
            }

            ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
            ImGui.InputText("###addIgnoredFCTag", ref inputStringIgnoreTag, 5);
            ImGui.SameLine();
            if (ImGui.SmallButton("Add##allowNamePlateFC")) {
                if (inputStringIgnoreTag.Length > 0 && !config.KeepNameVisible.Contains(inputStringIgnoreTag)) {
                    config.KeepNameVisible.Add(inputStringIgnoreTag);
                    inputStringIgnoreTag = string.Empty;
                }
            }

            ImGui.Unindent();
        };
        
    }
}
