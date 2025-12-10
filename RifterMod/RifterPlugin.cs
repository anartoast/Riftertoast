using BepInEx;
using RifterMod.Survivors.Rifter;
using R2API.Utils;
using RoR2;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Security;
using System.Security.Permissions;
using R2API;
using UnityEngine;
using RifterMod.Modules;
using RiskOfOptions.Options;
using BepInEx.Bootstrap;
using System.Runtime.CompilerServices;
using RiskOfOptions;
using EmotesAPI;
using TMPro;
using R2API.Networking;
using RifterMod.Modules.Networking;
using RifterMod.Survivors.NemRifter;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using RifterMod.Characters.Survivors.NemRifter.Components.Old;
using RifterMod.Characters.Survivors.NemRifter.Components;


[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]


//rename this namespace
namespace RifterMod
{
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInPlugin(MODUID, MODNAME, MODVERSION)]
    [BepInDependency("com.weliveinasociety.CustomEmotesAPI", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.DestroyedClone.AncientScepter", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(NetworkingAPI.PluginGUID)]
    public class RifterPlugin : BaseUnityPlugin
    {
        // if you do not change this, you are giving permission to deprecate the mod-
        //  please change the names to your own stuff, thanks
        //   this shouldn't even have to be said
        public const string MODUID = "com.blake.RifterMod";
        public const string MODNAME = "RifterMod";
        public const string MODVERSION = "1.0.0";

        // a prefix for name tokens to prevent conflicts- please capitalize all name tokens for convention
        public const string DEVELOPER_PREFIX = "BLAKE";

        public static RifterPlugin instance;

        public static BodyIndex rifterIndex;
        public static BodyIndex nemRifterIndex;

        public static GameObject hudInstance;

        public static List<BodyIndex> blacklist = new List<BodyIndex>();

        public static List<string> blacklistBodyNames = new List<string> { "MinorConstructBody(Clone)", "VoidBarnacleBody(Clone)" };

        public static bool ScepterInstalled;

        public static bool riskOfOptionsLoaded;

        void Awake()
        {
            instance = this;
            //easy to use logger
            Log.Init(Logger);

            // used when you want to properly set up language folders
            Modules.Language.Init();

            //ScepterInstalled = Chainloader.PluginInfos.ContainsKey("com.DestroyedClone.AncientScepter");
            if (Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions"))
            {
                riskOfOptionsLoaded = true;
            }

            NetworkingAPI.RegisterMessageType<TeleportOnBodyRequest>();
            NetworkingAPI.RegisterMessageType<AddGameObjectOnRequest>();
            // character initialization
            new RifterSurvivor().Initialize();
            //new NemRifterSurvivor().Initialize();

            // make a content pack and add it. this has to be last
            new Modules.ContentPacks().Initialize();

            Hook();

        }

        public void Start()
        {
          
            if (riskOfOptionsLoaded)
            {
                SetupRifterPlugin();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupRifterPlugin()
        {       
            if (riskOfOptionsLoaded)
            {
                //ModSettingsManager.AddOption(new CheckBoxOption(RifterConfig.teleportYourFriends));
                ModSettingsManager.AddOption(new CheckBoxOption(RifterConfig.cursed));
            }
        }

        private static void Hook()
        {
            On.RoR2.BodyCatalog.Init += BodyCatalog_Init;
            On.RoR2.GlobalEventManager.OnHitEnemy += GlobalEventManager_OnHitEnemy;
            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
            On.RoR2.CharacterBody.OnBuffFinalStackLost += CharacterBody_OnBuffFinalStackLost;            
            On.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += CharacterBody_UpdateAllTemporaryVisualEffects;
            On.RoR2.CharacterMaster.OnBodyStart += CharacterMaster_OnBodyStart;
            On.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
            if (Chainloader.PluginInfos.ContainsKey("com.weliveinasociety.CustomEmotesAPI"))
            {
                On.RoR2.SurvivorCatalog.Init += SurvivorCatalog_Init;
            }
        }

        private static void GlobalEventManager_OnCharacterDeath(On.RoR2.GlobalEventManager.orig_OnCharacterDeath orig, GlobalEventManager self, DamageReport damageReport)
        {           
            CharacterBody victim = damageReport.victim.body;
            if (victim != null)
            {
                int buffCount = victim.GetBuffCount(NemRifterBuffs.instabilityDebuff);
                if (buffCount > 0)
                {
                    NemRifterOwnerHolder nemRifterOwnerHolder = victim.gameObject.GetComponent<NemRifterOwnerHolder>();

                    if (nemRifterOwnerHolder.owner != null)
                    {
                            float radius = buffCount;
                            float expireDuration = buffCount + 5f > 30f ? 30f : buffCount + 5f;
                            GameObject riftZoneInstance = Object.Instantiate(NemRifterAssets.riftZonePillar, victim.corePosition, Util.QuaternionSafeLookRotation(Vector3.forward));
                            riftZoneInstance.GetComponent<DestroyOnTimer>().duration = expireDuration;
                            riftZoneInstance.GetComponent<GenericOwnership>().ownerObject = nemRifterOwnerHolder.owner;
                            TeamFilter teamFilter = riftZoneInstance.GetComponent<TeamFilter>();
                            teamFilter.teamIndex = TeamIndex.Player;
                            TeamFilter teamFilterBuffWard = riftZoneInstance.transform.GetChild(0).GetComponent<TeamFilter>();
                            teamFilterBuffWard.teamIndex = TeamIndex.Player;
                            BuffWard buffWardTeam = riftZoneInstance.transform.GetChild(0).GetComponent<BuffWard>();
                            if (buffWardTeam != null)
                            {
                                buffWardTeam.radius = radius;
                                buffWardTeam.buffDef = NemRifterBuffs.negateDebuff;
                                buffWardTeam.expireDuration = expireDuration;
                            }
                            NetworkServer.Spawn(riftZoneInstance);
                        }
                    }
                }
            orig(self, damageReport);
        }

        private static void CharacterMaster_OnBodyStart(On.RoR2.CharacterMaster.orig_OnBodyStart orig, CharacterMaster self, CharacterBody body)
        {
            orig (self, body);
            if (body.bodyIndex == nemRifterIndex && NetworkServer.active)
            {
                float radius = 40f;
                float expireDuration = 30f;
                GameObject riftZoneInstance = Object.Instantiate(NemRifterAssets.riftZonePillar, body.corePosition, body.transform.rotation);
                riftZoneInstance.GetComponent<DestroyOnTimer>().duration = expireDuration;
                riftZoneInstance.GetComponent<GenericOwnership>().ownerObject = self.gameObject;
                TeamFilter teamFilter = riftZoneInstance.GetComponent<TeamFilter>();
                teamFilter.teamIndex = TeamIndex.Player;
                TeamFilter teamFilterBuffWard = riftZoneInstance.transform.GetChild(0).GetComponent<TeamFilter>();
                teamFilterBuffWard.teamIndex = TeamIndex.Player;
                BuffWard buffWardTeam = riftZoneInstance.transform.GetChild(0).GetComponent<BuffWard>();
                if (buffWardTeam != null)
                {
                    buffWardTeam.radius = radius;
                    buffWardTeam.buffDef = NemRifterBuffs.negateDebuff;
                    buffWardTeam.expireDuration = expireDuration;
                }
                NetworkServer.Spawn(riftZoneInstance);
            }
        }

        private static void CharacterBody_UpdateAllTemporaryVisualEffects(On.RoR2.CharacterBody.orig_UpdateAllTemporaryVisualEffects orig, CharacterBody self)
        {
            orig(self);
            TemporaryVisualEffect tempEffect = null;
            self.UpdateSingleTemporaryVisualEffect(ref tempEffect, RifterAssets.shatterStackVisual, self.radius, self.HasBuff(RifterBuffs.superShatterDebuff));
        }

        private static void SurvivorCatalog_Init(On.RoR2.SurvivorCatalog.orig_Init orig)
        {
            orig();
            foreach (var item in SurvivorCatalog.allSurvivorDefs)
            {
                if (item.bodyPrefab.name == "RifterBody")
                {
                    var skele = RifterAssets.emoteSkele;
                    CustomEmotesAPI.ImportArmature(item.bodyPrefab, skele);
                }
            }
        }

        private static void CharacterBody_OnBuffFinalStackLost(On.RoR2.CharacterBody.orig_OnBuffFinalStackLost orig, CharacterBody self, BuffDef buffDef)
        {
            orig(self, buffDef);
            if (buffDef == RifterBuffs.superShatterDebuff && !self.isFlying)
            {
                self.characterMotor.useGravity = true;
            }

            if (buffDef == NemRifterBuffs.instabilityTriggerDebuff && NetworkServer.active)
            {
                NemRifterOwnerHolder nemRifterOwnerHolder = self.gameObject.GetComponent<NemRifterOwnerHolder>();

                if (nemRifterOwnerHolder && nemRifterOwnerHolder.owner != null)
                {
                    int buffCount = self.GetBuffCount(NemRifterBuffs.instabilityDebuff);
                    if (buffCount > 0)
                    {
                        for (int i = 0; (i < buffCount); i++)
                        {
                            self.RemoveBuff(NemRifterBuffs.instabilityDebuff);
                        }
                        float radius = buffCount;
                        float expireDuration = buffCount + 5f > 30f ? 30f : buffCount + 5f;
                        GameObject riftZoneInstance = Object.Instantiate(NemRifterAssets.riftZonePillar, self.corePosition, Util.QuaternionSafeLookRotation(Vector3.forward));
                        riftZoneInstance.GetComponent<DestroyOnTimer>().duration = expireDuration;
                        riftZoneInstance.GetComponent<GenericOwnership>().ownerObject = nemRifterOwnerHolder.owner;
                        TeamFilter teamFilter = riftZoneInstance.GetComponent<TeamFilter>();
                        teamFilter.teamIndex = TeamIndex.Player;
                        TeamFilter teamFilterBuffWard = riftZoneInstance.transform.GetChild(0).GetComponent<TeamFilter>();
                        teamFilterBuffWard.teamIndex = TeamIndex.Player;
                        BuffWard buffWardTeam = riftZoneInstance.transform.GetChild(0).GetComponent<BuffWard>();
                        if (buffWardTeam != null)
                        {
                            buffWardTeam.radius = radius;
                            buffWardTeam.buffDef = NemRifterBuffs.negateDebuff;
                            buffWardTeam.expireDuration = expireDuration;
                        }
                        NetworkServer.Spawn(riftZoneInstance);
                    }
                }
            }
        }

        private static void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);
            if (!self)
            {
                return;
            }
            if (self.HasBuff(RifterBuffs.superShatterDebuff))
            {
                for (int i = 0; i < 10; i++)
                {
                    self.AddTimedBuff(RifterBuffs.shatterDebuff, i + 1f, 10);
                }
                CharacterMotor motor = self.characterMotor;
                if (motor)
                {
                    motor.useGravity = false;
                }
            }
            if (self.HasBuff(RifterBuffs.shatterDebuff))
            {
                int shatterStacks = self.GetBuffCount(RifterBuffs.shatterDebuff);
                self.moveSpeed *= 1 - shatterStacks / 10;
                self.armor -= shatterStacks * 5;
            }
            
            if (self.HasBuff(NemRifterBuffs.collapseRifts))
            {
                self.moveSpeed *= 1.5f;
            }

            if (self.HasBuff(NemRifterBuffs.negateDebuff))
            {
                self.baseRegen = 2.5f;
            }
        }

        private static System.Collections.IEnumerator BodyCatalog_Init(On.RoR2.BodyCatalog.orig_Init orig)
        {           
            yield return orig();
            rifterIndex = BodyCatalog.FindBodyIndex("RifterBody(Clone)");
            //nemRifterIndex = BodyCatalog.FindBodyIndex("NemRifterBody(Clone)");
        }

        //private static void HUD_Update(On.RoR2.UI.HUD.orig_Update orig, RoR2.UI.HUD self)
        //{
        //    orig(self);
        //    CharacterBody body = self.targetBodyObject.GetComponent<CharacterBody>();
        //    if (RifterConfig.HUD.Value == true && body && body.bodyIndex == rifterIndex)
        //    {
        //        hudInstance.SetActive(true);
        //    }
        //    else
        //    {
        //        hudInstance.SetActive(false);
        //    }
        //}

        private static void GlobalEventManager_OnHitEnemy(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            orig(self, damageInfo, victim);
            if (!self)
            {
                return;
            }
            CharacterBody body = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
            CharacterBody victimBody = victim.GetComponent<CharacterBody>();
            
            if (body && body.bodyIndex == rifterIndex)
            {
                if (victimBody != null && DamageAPI.HasModdedDamageType(damageInfo, RifterDamage.riftDamage))
                {
                    if (victimBody.GetBuffCount(RifterBuffs.shatterDebuff) <= 5)
                    {
                        victimBody.ClearTimedBuffs(RifterBuffs.shatterDebuff);
                        for (int i = 0; i < 5; i++)
                        {
                            victimBody.AddTimedBuff(RifterBuffs.shatterDebuff, i + 1f, 5);
                        }                      
                    }
                }
                if (victimBody != null && DamageAPI.HasModdedDamageType(damageInfo, RifterDamage.riftSuperDamage))
                {
                    victimBody.AddTimedBuff(RifterBuffs.superShatterDebuff, 1f);
                }
            }

            if (body && body.bodyIndex == nemRifterIndex)
            {
                if (victimBody != null && DamageAPI.HasModdedDamageType(damageInfo, NemRifterDamage.instabilityProcDamage))
                {
                    if (NetworkServer.active)
                    {
                        if (!victimBody.HasBuff(NemRifterBuffs.instabilityDebuff))
                        {
                            victimBody.AddTimedBuff(NemRifterBuffs.instabilityTriggerDebuff, 5f);
                            if (!victim.GetComponent<NemRifterOwnerHolder>())
                            {
                                victim.gameObject.AddComponent<NemRifterOwnerHolder>();
                            }
                            victim.GetComponent<NemRifterOwnerHolder>().owner = body.gameObject;
                        }
                        victimBody.AddBuff(NemRifterBuffs.instabilityDebuff);
                    }
                }

                if (victimBody != null && DamageAPI.HasModdedDamageType(damageInfo, NemRifterDamage.instabilityTriggerDamage))
                {
                    if (NetworkServer.active)
                    {
                        if (victimBody.HasBuff(NemRifterBuffs.instabilityTriggerDebuff))
                        {
                            victimBody.RemoveBuff(NemRifterBuffs.instabilityTriggerDebuff);
                        }
                    }

                }

                //if (body && body.bodyIndex == nemRifterIndex)
                //{
                //    if (victimBody != null && DamageAPI.HasModdedDamageType(damageInfo, NemRifterDamage.screenSlashDamage))
                //    {
                //        if (NetworkServer.active)
                //        {
                //            GameObject riftZoneInstance = Object.Instantiate(NemRifterAssets.riftZonePillar, victimBody.corePosition, victimBody.transform.rotation);
                //            BuffWard buffWardTeam = riftZoneInstance.transform.GetChild(0).GetComponent<BuffWard>();
                //            if (buffWardTeam != null)
                //            {
                //                buffWardTeam.buffDef = NemRifterBuffs.negateDebuff;
                //            }
                //            BuffWard buffWardEnemy = riftZoneInstance.transform.GetChild(1).GetComponent<BuffWard>();
                //            TeamFilter teamFilter = riftZoneInstance.GetComponent<TeamFilter>();
                //            teamFilter.teamIndex = TeamIndex.Player;
                //            if (buffWardEnemy != null)
                //            {
                //                buffWardEnemy.radius = 5f;
                //                buffWardEnemy.buffDef = NemRifterBuffs.riftZoneDebuff;
                //                buffWardEnemy.buffTimer = .2f;
                //                buffWardEnemy.interval = .2f;
                //                buffWardEnemy.invertTeamFilter = true;
                //            }
                //            NetworkServer.Spawn(riftZoneInstance);
                //        }
                //    }
            }
        }


        //private static void HUD_Awake(On.RoR2.UI.HUD.orig_Awake orig, RoR2.UI.HUD self)
        //{
        //    orig(self);
        //    hudInstance = Object.Instantiate(RifterAssets.overchargeHUD);
        //    hudInstance.transform.SetParent(self.mainContainer.transform);
        //    RectTransform component = hudInstance.GetComponent<RectTransform>();
        //    //component.anchorMin = new Vector2(.5f, .5f);
        //    //component.anchorMax = new Vector2(.5f, .5f);
        //    //component.sizeDelta = Vector2.zero;
        //    //component.localScale = Vector2.zero;
        //    component.anchoredPosition = new Vector2(50, 0);
        //    OverchargeMeter.fill = hudInstance.GetComponent<Image>();
        //    OverchargeMeter.counter = hudInstance.transform.GetChild(0).transform.GetChild(0).GetComponent<Text>();
        //}

        public static void AddBodyToBlacklist(string bodyName)
        {
            BodyIndex bodyIndex = BodyCatalog.FindBodyIndex(bodyName);

            if (bodyIndex != BodyIndex.None)
            {
                if (blacklistBodyNames.Contains(bodyName))
                {
                    blacklist.Add(bodyIndex);
                }

            }
        }
    }
}
