using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityModManagerNet;

namespace StutterFixZh
{
    public static class Main
    {
        internal const string HarmonyId = "StutterFixZh";
        internal const string ChineseLanguage = "zh";

        private static UnityModManager.ModEntry entry;
        private static Harmony harmony;
        private static bool installed;
        private static bool missingLogged;
        private static Dictionary<string, string> translations;
        private static string currentLanguage = "";
        private static int languageRefreshFrame;
        private static object activeWindow;

        private static Type windowType;
        private static FieldInfo configField;
        private static FieldInfo languageField;
        private static FieldInfo rectField;
        private static FieldInfo tabField;
        private static FieldInfo tabOnField;
        private static MethodInfo setLanguageMethod;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            entry = modEntry;
            modEntry.OnUpdate = OnUpdate;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;
            LoadTranslations();
            ReadInitialLanguage();
            TryInstall();
            return true;
        }

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaSeconds)
        {
            if (!installed)
                TryInstall();
            else
            {
                // The original mod owns Settings.Language; refresh it so a missed
                // Harmony callback cannot leave the three-state UI stale.
                languageRefreshFrame++;
                if (languageRefreshFrame >= 2)
                {
                    languageRefreshFrame = 0;
                    ReadInitialLanguage();
                }
            }
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (value)
                TryInstall();
            else
                Uninstall();
            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            Uninstall();
            return true;
        }

        private static void TryInstall()
        {
            if (installed)
                return;

            try
            {
                windowType = AccessTools.TypeByName("StutterFix.SettingsWindow");
                var mainType = AccessTools.TypeByName("StutterFix.Main");
                if (windowType == null || mainType == null)
                {
                    LogMissing("StutterFix.SettingsWindow or StutterFix.Main");
                    return;
                }

                configField = AccessTools.Field(mainType, "Config");
                var translate = AccessTools.Method(windowType, "T", new[] { typeof(string), typeof(string) });
                var drawContents = AccessTools.Method(windowType, "DrawContents", Type.EmptyTypes);
                if (configField == null || translate == null || drawContents == null)
                {
                    LogMissing("language or DrawContents method");
                    return;
                }

                languageField = AccessTools.Field(configField.FieldType, "Language");
                rectField = AccessTools.Field(windowType, "rect");
                tabField = AccessTools.Field(windowType, "sTab");
                tabOnField = AccessTools.Field(windowType, "sTabOn");
                setLanguageMethod = AccessTools.Method(windowType, "SetLanguage", new[] { typeof(string) });
                if (languageField == null || rectField == null || tabField == null || tabOnField == null || setLanguageMethod == null)
                {
                    LogMissing("SettingsWindow private fields");
                    return;
                }
                ReadInitialLanguage();

                harmony = new Harmony(HarmonyId);
                harmony.Patch(translate,
                    prefix: new HarmonyMethod(typeof(Main), nameof(TranslatePrefix)));
                harmony.Patch(setLanguageMethod,
                    postfix: new HarmonyMethod(typeof(Main), nameof(SetLanguagePostfix)));
                harmony.Patch(drawContents,
                    transpiler: new HarmonyMethod(typeof(Main), nameof(DrawContentsTranspiler)));
                installed = true;
                missingLogged = false;
                entry?.Logger.Log("[Chinese] installed; language=" + (currentLanguage.Length == 0 ? "auto" : currentLanguage)
                    + ", translations=" + translations.Count);
            }
            catch (Exception ex)
            {
                Uninstall();
                entry?.Logger.Error("[Chinese] install failed: " + ex);
            }
        }

        private static void Uninstall()
        {
            if (!installed)
                return;
            installed = false;
            activeWindow = null;
            try { harmony?.UnpatchAll(HarmonyId); }
            catch (Exception ex) { entry?.Logger.Error("[Chinese] uninstall failed: " + ex.Message); }
            harmony = null;
        }

        private static void LogMissing(string what)
        {
            if (missingLogged)
                return;
            missingLogged = true;
            entry?.Logger.Log("[Chinese] waiting for " + what);
        }

        private static void ReadInitialLanguage()
        {
            try
            {
                if (configField == null || languageField == null)
                {
                    var mainType = AccessTools.TypeByName("StutterFix.Main");
                    var config = mainType == null ? null : AccessTools.Field(mainType, "Config")?.GetValue(null);
                    if (config != null)
                    {
                        configField = AccessTools.Field(mainType, "Config");
                        languageField = AccessTools.Field(config.GetType(), "Language");
                    }
                }
                var cfg = configField?.GetValue(null);
                currentLanguage = languageField?.GetValue(cfg) as string ?? "";
            }
            catch { currentLanguage = ""; }
        }

        private static void LoadTranslations()
        {
            if (translations != null)
                return;
            try
            {
                using (var stream = typeof(Main).Assembly.GetManifestResourceStream("StutterFixZh.zh-CN.json"))
                {
                    if (stream == null)
                        throw new InvalidOperationException("embedded zh-CN.json not found");
                    using (var reader = new StreamReader(stream))
                        translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd())
                        ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                translations = new Dictionary<string, string>();
                entry?.Logger.Error("[Chinese] translation load failed: " + ex.Message);
            }
        }

        private static bool IsChinese()
        {
            return currentLanguage == ChineseLanguage;
        }

        private static string ConfiguredLanguage()
        {
            try
            {
                if (configField != null && languageField != null)
                {
                    var config = configField.GetValue(null);
                    var language = languageField.GetValue(config) as string;
                    if (language != null)
                    {
                        currentLanguage = language;
                        return language;
                    }
                }
            }
            catch { }
            return currentLanguage;
        }

        private static string SelectedLanguage()
        {
            var language = ConfiguredLanguage();
            if (language == "ko" || language == "en" || language == ChineseLanguage)
                return language;
            return Application.systemLanguage == SystemLanguage.Korean ? "ko" : "en";
        }

        private static Dictionary<string, string> normalizedTranslations;

        private static string NormalizeKey(string text)
        {
            return string.Join(" ", text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static Dictionary<string, string> GetNormalizedTranslations()
        {
            if (normalizedTranslations != null)
                return normalizedTranslations;
            normalizedTranslations = new Dictionary<string, string>();
            foreach (var pair in translations)
            {
                var key = NormalizeKey(pair.Key);
                if (!normalizedTranslations.ContainsKey(key))
                    normalizedTranslations[key] = pair.Value;
            }
            return normalizedTranslations;
        }

        // Some upstream strings contain UTF-8 bytes reinterpreted as Latin-1
        // (for example U+00E2 U+0086 U+0092 for "→"). Only try decoding strings
        // with C1 control characters, which are a strong signal of this corruption.
        private static string FixMojibake(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var hasC1Control = false;
            foreach (var c in text)
            {
                if (c >= '\u0080' && c <= '\u009F') { hasC1Control = true; break; }
            }
            if (!hasC1Control || text.Any(c => c > '\u00FF'))
                return text;

            try
            {
                var bytes = System.Text.Encoding.GetEncoding("iso-8859-1").GetBytes(text);
                return new System.Text.UTF8Encoding(false, true).GetString(bytes);
            }
            catch (System.Text.DecoderFallbackException)
            {
                return text;
            }
        }

        private static Dictionary<string, string> caseInsensitiveTranslations;

        private static Dictionary<string, string> GetCaseInsensitiveTranslations()
        {
            if (caseInsensitiveTranslations != null)
                return caseInsensitiveTranslations;
            caseInsensitiveTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in translations)
            {
                if (!caseInsensitiveTranslations.ContainsKey(pair.Key))
                    caseInsensitiveTranslations[pair.Key] = pair.Value;
            }
            return caseInsensitiveTranslations;
        }

        private static bool TryLookup(string text, out string zh)
        {
            zh = null;
            if (string.IsNullOrEmpty(text))
                return false;
            if (translations.TryGetValue(text, out zh) && !string.IsNullOrEmpty(zh))
                return true;
            var normalized = NormalizeKey(text);
            if (normalized.Length > 0 && normalized != text
                && GetNormalizedTranslations().TryGetValue(normalized, out zh) && !string.IsNullOrEmpty(zh))
                return true;
            // The upstream lower-cases some dynamic fragments (e.g. effect names in
            // "Turn off <name>"), while the dictionary keys keep their original casing.
            if (GetCaseInsensitiveTranslations().TryGetValue(text, out zh) && !string.IsNullOrEmpty(zh))
                return true;
            return false;
        }

        public static bool TranslatePrefix(string __0, string __1, ref string __result)
        {
            if (!installed || !IsChinese() || __1 == null)
                return true;
            if (translations == null)
                LoadTranslations();
            if (TryLookup(__1, out var zh))
            {
                __result = zh;
                return false;
            }
            var fixedText = FixMojibake(__1);
            if (fixedText != __1 && TryLookup(fixedText, out zh))
            {
                __result = zh;
                return false;
            }
            if (TryTranslateDynamic(__1, out zh) && !string.IsNullOrEmpty(zh))
            {
                __result = zh;
                return false;
            }
            return true;
        }

        private static bool TryTranslateDynamic(string english, out string chinese)
        {
            chinese = null;
            if (string.IsNullOrEmpty(english))
                return false;

            if (english.StartsWith("Advanced · ", StringComparison.Ordinal))
            {
                chinese = "高级 · " + english.Substring("Advanced · ".Length);
                return true;
            }

            if (english.StartsWith("Turn off ", StringComparison.OrdinalIgnoreCase))
            {
                var rest = english.Substring("Turn off ".Length);
                if (TryLookup(rest, out var matched))
                {
                    chinese = "关闭 " + matched;
                    return true;
                }
                chinese = "关闭 " + rest;
                return true;
            }

            if (english.StartsWith("Cycle with ", StringComparison.Ordinal) && english.Contains(" in game", StringComparison.Ordinal))
            {
                var middle = english.Substring("Cycle with ".Length);
                var suffix = " in game";
                var idx = middle.IndexOf(suffix, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    middle = middle.Substring(0, idx);
                    chinese = "游戏中按 " + middle + " 依次切换（在 Home 页更改按键）。点击图标可展开详细。";
                    return true;
                }
            }

            if (english.StartsWith("   Press <b>", StringComparison.Ordinal) && english.Contains("</b>", StringComparison.Ordinal))
            {
                var start = "   Press <b>".Length;
                var end = english.IndexOf("</b>", StringComparison.Ordinal);
                if (end > start)
                {
                    chinese = "   游戏中随时按 <b>" + english.Substring(start, end - start) + "</b> 打开或关闭设置。";
                    return true;
                }
            }

            if (english.StartsWith("Windows on top of the game: ", StringComparison.Ordinal)
                && english.Contains("Closing these programs", StringComparison.Ordinal))
            {
                var left = "Windows on top of the game: ".Length;
                var right = english.IndexOf(". Closing these programs", StringComparison.Ordinal);
                if (right > left)
                {
                    chinese = "游戏画面上方的窗口：" + english.Substring(left, right - left) + "。关闭这些程序（桌面宠物、叠加层等）应能提升 FPS。";
                    return true;
                }
            }

            if (english.StartsWith("Programs capturing the screen: ", StringComparison.Ordinal)
                && english.Contains("Closing them while playing should raise FPS.", StringComparison.Ordinal))
            {
                var left = "Programs capturing the screen: ".Length;
                var right = english.IndexOf(" (remote desktop, recording, streaming).", StringComparison.Ordinal);
                if (right > left)
                {
                    chinese = "正在捕获屏幕的程序：" + english.Substring(left, right - left) + "（远程桌面、录制、串流）。游玩时关闭它们应能提升 FPS。";
                    return true;
                }
            }

            if (english.StartsWith("The game ended abnormally last time (during: ", StringComparison.Ordinal)
                && english.Contains("). If it happens again, safe mode turns on automatically.", StringComparison.Ordinal))
            {
                var left = "The game ended abnormally last time (during: ".Length;
                var right = english.IndexOf("). If it happens again, safe mode turns on automatically.", StringComparison.Ordinal);
                if (right > left)
                {
                    chinese = "上次游戏异常结束（当时处于：" + english.Substring(left, right - left) + "）。如果再次发生，将自动进入安全模式。";
                    return true;
                }
            }

            if (english.StartsWith("Safe mode: ", StringComparison.Ordinal)
                && english.Contains("Features that hook deep into the game are off for this run", StringComparison.Ordinal))
            {
                var left = "Safe mode: ".Length;
                var right = english.IndexOf(". Features that hook deep into the game are off for this run", StringComparison.Ordinal);
                if (right > left)
                {
                    chinese = "安全模式：" + english.Substring(left, right - left) + "。本次运行会关闭深度挂钩游戏的功能，正常退出后下次启动恢复。";
                    return true;
                }
            }

            return false;
        }

        public static void SetLanguagePostfix(string lang)
        {
            var previous = currentLanguage;
            currentLanguage = lang ?? "";
            if (string.IsNullOrEmpty(currentLanguage))
                currentLanguage = SelectedLanguage();
            if (!string.Equals(previous, currentLanguage, StringComparison.Ordinal))
                entry?.Logger.Log("[Chinese] language " + (previous.Length == 0 ? "auto" : previous)
                    + " -> " + (currentLanguage.Length == 0 ? "auto" : currentLanguage));
        }

        private static bool KoreanSelected()
        {
            var language = ConfiguredLanguage();
            if (language == "ko") return true;
            if (language == "en" || language == ChineseLanguage) return false;
            return Application.systemLanguage == SystemLanguage.Korean;
        }

        private static bool EnglishSelected()
        {
            var language = ConfiguredLanguage();
            if (language == "en") return true;
            if (language == "ko" || language == ChineseLanguage) return false;
            return Application.systemLanguage != SystemLanguage.Korean;
        }

        private static void DrawChineseButton(object window)
        {
            if (!installed || window == null || rectField == null || tabField == null || tabOnField == null)
                return;

            activeWindow = window;
            var rect = (Rect)rectField.GetValue(window);
            var button = new Rect(rect.width - 230f, 18f, 48f, 26f);
            LanguageButton(button, "中文", null);
        }

        private static bool TryApplySelectedLanguage(string label)
        {
            if (activeWindow == null || setLanguageMethod == null)
                return false;

            string language = label switch
            {
                "中文" => ChineseLanguage,
                "한국어" => "ko",
                "English" => "en",
                _ => null,
            };
            if (language == null)
                return false;

            try
            {
                setLanguageMethod.Invoke(activeWindow, new object[] { language });
                currentLanguage = language;
                return true;
            }
            catch (Exception ex)
            {
                entry?.Logger.Error("[Chinese] language switch failed: " + ex.Message);
                return false;
            }
        }

        // Replaces the two original buttons to supply a third selected state;
        // their original click branches remain responsible for saving ko/en.
        // The injected Chinese button has no upstream click branch to handle it.
        private static bool LanguageButton(Rect rect, string label, GUIStyle originalStyle)
        {
            if (activeWindow == null || tabField == null || tabOnField == null)
                return GUI.Button(rect, label, originalStyle);

            var selected = (label == "中文" && SelectedLanguage() == ChineseLanguage)
                || (label == "한국어" && SelectedLanguage() == "ko")
                || (label == "English" && SelectedLanguage() == "en");
            var style = ((selected ? tabOnField : tabField).GetValue(activeWindow) as GUIStyle) ?? originalStyle;
            if (style == null)
                style = GUI.skin.button;

            var clicked = GUI.Button(rect, label, style);
            // The upstream Korean/English click branches call SetLanguage themselves.
            // Only the injected Chinese button needs to apply its language here.
            if (clicked && label == "中文")
                TryApplySelectedLanguage(label);
            return clicked;
        }

        private static IEnumerable<CodeInstruction> DrawContentsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var inserted = false;
            var stylePatches = 0;
            var languageButtonPatches = 0;

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].opcode != OpCodes.Call || !(list[i].operand is MethodInfo method)
                    || method.Name != "Button" || method.DeclaringType != typeof(GUI)
                    || method.GetParameters().Length != 3)
                    continue;

                string label = null;
                var start = Math.Max(0, i - 16);
                for (var j = i - 1; j >= start; j--)
                {
                    if (list[j].opcode != OpCodes.Ldstr)
                        continue;
                    var candidate = list[j].operand as string;
                    if (candidate == "한국어" || candidate == "English")
                    {
                        label = candidate;
                        break;
                    }
                }
                if (label == null)
                    continue;

                list[i].operand = AccessTools.Method(typeof(Main), nameof(LanguageButton),
                    new[] { typeof(Rect), typeof(string), typeof(GUIStyle) });
                languageButtonPatches++;
            }

            // Always add the dedicated Chinese button. Even when the original Korean/English
            // buttons are detected and replaced, we still need a third language toggle in the settings
            // header. This keeps the compatibility patch stable across minor UI changes and makes the
            // Chinese option visible again in the current upstream layout.
            var retIndex = list.FindLastIndex(i => i.opcode == OpCodes.Ret);
            if (retIndex >= 0)
            {
                list.InsertRange(retIndex, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(Main), nameof(DrawChineseButton), new[] { typeof(object) }))
                });
                inserted = true;
            }

            for (var i = 1; i < list.Count; i++)
            {
                if (list[i].opcode != OpCodes.Call || !(list[i].operand is MethodInfo method) || method.Name != "get_English")
                    continue;
                if (list[i - 1].opcode != OpCodes.Ldstr)
                    continue;
                var label = list[i - 1].operand as string;
                if (label == "한국어")
                {
                    list[i].operand = AccessTools.Method(typeof(Main), nameof(KoreanSelected));
                    stylePatches++;
                }
                else if (label == "English")
                {
                    list[i].operand = AccessTools.Method(typeof(Main), nameof(EnglishSelected));
                    stylePatches++;
                }
            }

            entry?.Logger.Log("[Chinese] DrawContents pattern: button=" + inserted
                + ", styles=" + stylePatches + ", languageButtons=" + languageButtonPatches);
            return list;
        }

        private static bool IsLoadLocal0(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Ldloc_0
                || (instruction.opcode == OpCodes.Ldloc && Equals(instruction.operand, 0));
        }

        private static bool IsLdcR4(CodeInstruction instruction, float value)
        {
            return instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float f && Math.Abs(f - value) < 0.001f;
        }
    }
}
