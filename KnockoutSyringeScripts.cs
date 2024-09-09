using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using FistVR;
using HarmonyLib;
using UnityEngine;

namespace mehongo
{
	[BepInPlugin("h3vr.mehongo.KnockoutScripts", "Knockout Scripts", "1.0.1")]
	public class KnockoutSyringeScripts : BaseUnityPlugin
	{
		internal static KnockoutSyringeScripts Instance { get; private set; }
		internal static BepInEx.Configuration.ConfigEntry<float> headshotSFXVolume;
		internal static BepInEx.Configuration.ConfigEntry<bool> headshotKnocksout;
		internal static BepInEx.Configuration.ConfigEntry<bool> multiShotTimerDecay;
		internal static BepInEx.Configuration.ConfigEntry<bool> lowPressureCycle;
		private const string knockoutScriptsCatName = "Knockout Syringe Scripts";

		private void Awake()
		{
			KnockoutSyringeScripts.Instance = this;
			Harmony.CreateAndPatchAll(base.GetType(), "MeHongo-KnockoutScripts");
			// Mod Settings
			headshotSFXVolume = Config.Bind(knockoutScriptsCatName, "Headshot SFX Volume", 1f, //Ciarence - Default value, only applied if the config entry does not exist
			"Volume of the headshot sound effect that plays when shooting a tranquilizer round into a sosig's dumb long head");
			tranqHeadshotSound.VolumeRange = Vector2.one * headshotSFXVolume.Value; //Ciarence - Assign the volume, shorthand for Vector2(headshotSFXVolume.Value, headshotSFXVolume.Value)
			headshotSFXVolume.SettingChanged += (sender, eventArgs) => //Ciarence - Lambda expression, update the value when the player changes the value in the config
			{
				tranqHeadshotSound.VolumeRange = Vector2.one * headshotSFXVolume.Value;
			};
			headshotKnocksout = Config.Bind(knockoutScriptsCatName, "Headshot Instantly Knocks Out", true, "Enables/Disables enemies being instantly knocked out when headshot with a tranquilizer.");
			lowPressureCycle = Config.Bind(knockoutScriptsCatName, "Cycle Low Pressure Tranquilizer Rounds", false, "Enables/Disables low pressure tranquilizer rounds auto-cycling the chamber in semi-auto firearms. If off, it will mean that the chamber must be manually cycled (Recommended off due to High Pressure variants existing)");
			multiShotTimerDecay = Config.Bind(knockoutScriptsCatName, "Multi Shot Timer Decay", true, "Enable/Disable shooting an enemy with a tranquilizer while it is already counting down to sleep will decrease the timer");
			// End of Mod Settings
			// Load Sound
			string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			bool flag = Directory.Exists(directoryName);
			if (flag)
			{
				foreach (string text in Directory.GetFiles(directoryName))
				{
					bool flag2 = text.Contains(".wav");
					if (flag2)
					{
						WWW www = new WWW("file://" + text);
						base.StartCoroutine(this.LoadHeadshotSound(www));
					}
				}
			}
			else
			{
				base.Logger.LogError("Specified path doesn't exist");
			}
		}

		private IEnumerator LoadHeadshotSound(WWW www)
		{
			while (!www.isDone)
			{
				base.Logger.LogMessage(string.Format("Tranq Headshot sound effect loading Progress: {0}", www.progress));
				yield return null;
			}
			AudioClip audioClip = www.GetAudioClip(false, true, AudioType.WAV);
			audioClip.name = "TranqHeadshotSound";
			KnockoutSyringeScripts.tranqHeadshotSound.Clips.Add(audioClip);
			yield break;
		}

		//Disables Collisions on impact
		private static void DisableSyringeCollisions(SyringeProjectile syringe) //Ciarence - Makes shooting multiple syringes into one sosig not make they/them explode anymore, we don't care about their collisions after they deliver their payload, because they'll despawn after a while.
		{
			Transform bladeTrans = syringe.transform.Find("Blade");
			if (bladeTrans && bladeTrans.GetComponent<Collider>())
			{
				bladeTrans.GetComponent<Collider>().enabled = false;
			}

			Transform physTrans = syringe.transform.Find("Phys");
			if (physTrans && physTrans.GetComponent<Collider>())
			{
				physTrans.GetComponent<Collider>().enabled = false;
			}
		}

		//Low Pressure Round Cycle
		//Handgun
		/*[HarmonyPatch(typeof(HandgunSlide), nameof(HandgunSlide.ImpartFiringImpulse))]
		[HarmonyPrefix]
		public static void LowPressureSlide(ref bool __runOriginal, HandgunSlide __instance)
		{
			if (lowPressureCycle.Value == false)
			{
				if (!__runOriginal) //Ciarence - Is there another patch that wants to skip?
				{ 
					return;
				}
				if (__instance.Handgun != null)
				{
					if (__instance.Handgun.Chamber != null)
					{
						FistVR.FVRFireArmRound bullet = __instance.Handgun.Chamber.GetRound();
						if (!bullet.IsHighPressure)
						{
							__runOriginal = false;
						}
					}
				}
			}
		}*/
		[HarmonyPatch(typeof(Handgun), nameof(Handgun.Fire))]
		[HarmonyPrefix]
		private static void LowPressureSlidePrefix(Handgun __instance, ref bool ___m_isSlideLockMechanismEngaged, ref bool __state)
        {
			if (!lowPressureCycle.Value && __instance.Chamber && __instance.Chamber.GetRound())
			{
				__state = ___m_isSlideLockMechanismEngaged;
				if (!__instance.Chamber.GetRound().IsHighPressure)
				{
					___m_isSlideLockMechanismEngaged = true;
				}
			}
		}
		[HarmonyPatch(typeof(Handgun), nameof(Handgun.Fire))]
		[HarmonyPostfix]
		private static void LowPressureSlidePostfix(Handgun __instance, ref bool ___m_isSlideLockMechanismEngaged, ref bool __state)
		{
			if (!lowPressureCycle.Value && __instance.Chamber && __instance.Chamber.GetRound())
			{
				if (!__instance.Chamber.GetRound().IsHighPressure)
				{
					___m_isSlideLockMechanismEngaged = __state;
				}
			}
		}
		//Closed Bolt
		[HarmonyPatch(typeof(ClosedBolt), nameof(ClosedBolt.ImpartFiringImpulse))]
		[HarmonyPrefix]
		public static void LowPressureClosedBolt(ref bool __runOriginal, ClosedBolt __instance)
		{
			if (!lowPressureCycle.Value && __runOriginal && __instance.Weapon && __instance.Weapon.Chamber)
			{
				FistVR.FVRFireArmRound bullet = __instance.Weapon.Chamber.GetRound();
				if (bullet && !bullet.IsHighPressure)
				{
					__runOriginal = false;
				}
				
			}
		}
		//Open Bolt
		[HarmonyPatch(typeof(OpenBoltReceiverBolt), nameof(OpenBoltReceiverBolt.ImpartFiringImpulse))]
		[HarmonyPrefix]
		public static void LowPressureOpenBolt(ref bool __runOriginal, OpenBoltReceiverBolt __instance)
		{
			if (!lowPressureCycle.Value && __runOriginal && __instance.Receiver && __instance.Receiver.Chamber)
			{
				FistVR.FVRFireArmRound bullet = __instance.Receiver.Chamber.GetRound();
				if (bullet && !bullet.IsHighPressure)
				{
					__runOriginal = false;
				}
			}
		}

		//Multi Shot Timer Decay
		[HarmonyPatch(typeof(Sosig), "DelayedKnockout")]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> SosigMultiSyringeKnockoutPostFix(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
		{

			Label label = default(Label);
			CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
			Label label2;
			codeMatcher.MatchForward(true, new CodeMatch[]
			{
			new CodeMatch(new CodeInstruction(OpCodes.Ldarg_1, null), null),
			new CodeMatch(new CodeInstruction(OpCodes.Add, null), null),
			new CodeMatch(new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(Sosig), "m_unconsciousTime")), null),
			new CodeMatch(new CodeInstruction(OpCodes.Ldarg_0, null), null)
			}).CreateLabel(out label).End()
				.CreateLabel(out label2)
				.MatchBack(true, new CodeMatch[]
				{
				new CodeMatch(new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(Sosig), "m_unconsciousTime")), null),
				new CodeMatch(new CodeInstruction(OpCodes.Ldarg_0, null), null)
				});
			bool flag = !codeMatcher.ReportFailure(__originalMethod, new Action<string>(KnockoutSyringeScripts.Instance.Logger.LogFatal));
			if (flag)
			{
				codeMatcher.InsertAndAdvance(new CodeInstruction[]
				{
				new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(KnockoutSyringeScripts), nameof(KnockoutSyringeScripts.multiShotTimerDecay))),
				new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(BepInEx.Configuration.ConfigEntry<bool>), nameof(BepInEx.Configuration.ConfigEntry<bool>.Value))),
				new CodeInstruction(OpCodes.Brfalse, label),
				new CodeInstruction(OpCodes.Ldarg_0, null),
				new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Sosig), "m_isDelayedKnockingOut")),
				new CodeInstruction(OpCodes.Brfalse_S, label),
				new CodeInstruction(OpCodes.Ldarg_0, null),
				new CodeInstruction(OpCodes.Ldarg_0, null),
				new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Sosig), "m_timeTilKnockout")),
				new CodeInstruction(OpCodes.Ldc_R4, 2f),
				new CodeInstruction(OpCodes.Div, null),
				new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(Sosig), "m_timeTilKnockout")),
				new CodeInstruction(OpCodes.Br_S, label2)
				});
			}
			List<CodeInstruction> list = codeMatcher.Instructions();
			return codeMatcher.InstructionEnumeration();
		}

		//Headshot Knockout
		[HarmonyPatch(typeof(SyringeProjectile), "FVRUpdate")]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> MakeSyringeHeadshotsInstaKnockTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
		{
			CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
			codeMatcher.MatchForward(true, new CodeMatch[]
			{
				new CodeMatch(new CodeInstruction(OpCodes.Ldarg_0, null), null),
				new CodeMatch(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(SyringeProjectile), "DoesKnockoutDamage")), null),
				new CodeMatch(new CodeInstruction(OpCodes.Brfalse, null), null)
			});
			bool flag = !codeMatcher.ReportFailure(__originalMethod, new Action<string>(KnockoutSyringeScripts.Instance.Logger.LogFatal));
			if (flag)
			{
				Label label = (Label)codeMatcher.Instruction.operand;
				List<CodeInstruction> list = new List<CodeInstruction>();
				Label label2;
				codeMatcher.Advance(1).CreateLabel(out label2).InsertAndAdvance(new CodeInstruction[]
				{
					new CodeInstruction(OpCodes.Ldarg_0),
					new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(KnockoutSyringeScripts), nameof(KnockoutSyringeScripts.DisableSyringeCollisions))),
					new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(KnockoutSyringeScripts), nameof(KnockoutSyringeScripts.headshotKnocksout))),
					new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(BepInEx.Configuration.ConfigEntry<bool>), nameof(BepInEx.Configuration.ConfigEntry<bool>.Value))),
					new CodeInstruction(OpCodes.Brfalse, label2),
					new CodeInstruction(OpCodes.Ldarg_0, null),
					new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(FVRPhysicalObject), "MP")),
					new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(FVRPhysicalObject.MeleeParams), "GetStabLink", null, null)),
					new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(SosigLink), "BodyPart")),
					new CodeInstruction(OpCodes.Brtrue_S, label2),
					new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(KnockoutSyringeScripts), "tranqHeadshotSound")),
					new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(GM), "CurrentPlayerBody")),
					new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(FVRPlayerBody), "Head")),
					new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Transform), "position")),
					new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SM), "PlayGenericSound", null, null)),
					new CodeInstruction(OpCodes.Ldloc_0, null),
					new CodeInstruction(OpCodes.Ldarg_0, null),
					new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(SyringeProjectile), "KnockoutDamage_Amount")),
					new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Sosig), "KnockUnconscious", null, null)),
					new CodeInstruction(OpCodes.Br_S, label)
				});
			}
			List<CodeInstruction> list2 = codeMatcher.Instructions();
			return codeMatcher.InstructionEnumeration();
		}

		private static AudioEvent tranqHeadshotSound = new AudioEvent
		{
			PitchRange = new Vector2(0.8f, 1.2f)
		};

		private const string HarmonyID = "MeHongo-KnockoutScripts";
	}
}

