using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using FistVR;
using HarmonyLib;
using mehongo;
using UnityEngine;

namespace KnockoutScripts
{
    [BepInPlugin("h3vr.mehongo.KnockoutScripts", "Knockout Scripts", "1.0.1")]
    public class NonLethalOverhaul: BaseUnityPlugin
    {
        internal static NonLethalOverhaul Instance { get; private set; }
        private const string knockoutScriptsCatName = "Knockout Syringe Scripts";

        private void Awake()
        {
            NonLethalOverhaul.Instance = this;
            Harmony.CreateAndPatchAll(base.GetType(), "MeHongo-KnockoutScripts");
        }

        [HarmonyPatch(typeof(BallisticProjectile), "MoveBullet")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> nonLethalBulletProjectileCalc(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
            // s
            //if (!codeMatcher.ReportFailure(__originalMethod, new Action<string>(NonLethalOverhaul.Instance.Logger.LogFatal)));
            //...
            List<CodeInstruction> list2 = codeMatcher.Instructions();
            return codeMatcher.InstructionEnumeration();
        }

    }
    public class NonLethalDamage: Damage
    {
        public float nonLethalDamage;
        public bool isTickDownDamage;
        public float tickInterval;
    }


}
