using BepInEx.Configuration;
using EntityStates;
using RifterMod.Characters.Survivors.Rifter.Components;
using RifterMod.Characters.Survivors.Rifter.SkillStates;
using RifterMod.Modules;
using RifterMod.Modules.Characters;
using RifterMod.Survivors.Rifter.Components;
using RifterMod.Survivors.Rifter.SkillStates;
using RoR2;
using System.Collections.Generic;
using UnityEngine;
using RoR2.Skills;
using static R2API.DeployableAPI;
using RifterMod.Characters.Survivors.Rifter.SkillStates.UnusedStates;
using R2API;

namespace RifterMod.Survivors.Rifter
{
    public class RifterSurvivor : SurvivorBase<RifterSurvivor>
    {
        //used to load the assetbundle for this character. must be unique
        public override string assetBundleName => "rifterassetbundle"; //if you do not change this, you are giving permission to deprecate the mod

        //the name of the prefab we will create. conventionally ending in "Body". must be unique
        public override string bodyName => "RifterBody"; //if you do not change this, you get the point by now

        //name of the ai master for vengeance and goobo. must be unique
        public override string masterName => "RifterMonsterMaster"; //if you do not

        //the names of the prefabs you set up in unity that we will use to build your character
        public override string modelPrefabName => "mdlRifter";
        public override string displayPrefabName => "RifterDisplay";

        public const string RIFTER_PREFIX = RifterPlugin.DEVELOPER_PREFIX + "_RIFTER_";

        //used when registering your survivor's language tokens
        public override string survivorTokenPrefix => RIFTER_PREFIX;

        public override BodyInfo bodyInfo => new BodyInfo
        {
            bodyName = bodyName,
            bodyNameToken = RIFTER_PREFIX + "NAME",
            subtitleNameToken = RIFTER_PREFIX + "SUBTITLE",

            characterPortrait = assetBundle.LoadAsset<Texture>("texRifterIcon"),
            bodyColor = new Color(0.329f, 0.42f, 0.651f),
            sortPosition = 100,

            crosshair = RifterMod.Modules.MyCharacterAssets.LoadCrosshair("SimpleDot"),
            podPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/SurvivorPod"),

            maxHealth = 110f,
            healthRegen = 1.5f,
            armor = 0f,

            jumpCount = 1,

            damage = 14f, //changed from 12
            damageGrowth = 14f * 0.2f, //changed 12f to 14f

            aimOriginPosition = new Vector3(0,1.5f,0)
    };

        public override CustomRendererInfo[] customRendererInfos => new CustomRendererInfo[]
        {
                new CustomRendererInfo
                {
                    childName = "HeadPiece"
                },
                new CustomRendererInfo
                {
                    childName = "GlassArm"
                },
                new CustomRendererInfo
                {
                    childName = "GlassLeg"
                },
                new CustomRendererInfo
                {
                    childName = "MainBody",
                },
                //new CustomRendererInfo
                //{
                //    childName = "BackVentTransparent"
                //}
        };

        public override UnlockableDef characterUnlockableDef => RifterUnlockables.characterUnlockableDef;

        public override ItemDisplaysBase itemDisplays => new RifterItemDisplays();

        //set in base classes
        public override AssetBundle assetBundle { get; protected set; }

        public override GameObject bodyPrefab { get; protected set; }
        public override CharacterBody prefabCharacterBody { get; protected set; }
        public override GameObject characterModelObject { get; protected set; }
        public override CharacterModel prefabCharacterModel { get; protected set; }
        public override GameObject displayPrefab { get; protected set; }

        public static DeployableSlot timelockSlot;

        public static DeployableSlot portalSlot;

        
        public static int maxTimelocks = 3;
        public static int maxPortals = 3;

        public override void Initialize()
        {
            //uncomment if you have multiple characters
            //ConfigEntry<bool> characterEnabled = Config.CharacterEnableConfig("Survivors", "Rifter");

            //if (!characterEnabled.Value)
            //    return;

            base.Initialize();

            GetDeployableSameSlotLimit timelockLimit = (CharacterMaster self, int deployableCountMultiplier) => maxTimelocks;
            timelockSlot = DeployableAPI.RegisterDeployableSlot(timelockLimit);
            GetDeployableSameSlotLimit portalLimit = (CharacterMaster self, int deployableCountMultiplier) => maxPortals;
            portalSlot = DeployableAPI.RegisterDeployableSlot(portalLimit);
        }

        public override void InitializeCharacter()
        {
            //need the character unlockable before you initialize the survivordef
            RifterUnlockables.Init();

            base.InitializeCharacter();

            RifterConfig.Init();
            RifterStates.Init();
            RifterTokens.Init();

            RifterAssets.Init(assetBundle);
            RifterBuffs.Init(assetBundle);
            RifterDamage.SetupModdedDamage();

            InitializeEntityStateMachines();
            InitializeSkills();
            InitializeSkins();
            InitializeCharacterMaster();

            AdditionalBodySetup();
            
        }

        private void AdditionalBodySetup()
        {
            AddHitboxes();
            bodyPrefab.AddComponent<RifterOverchargePassive>();
            bodyPrefab.AddComponent<RiftAndFracture>();
            //bodyPrefab.AddComponent<OverchargeMeter>();
            //bodyPrefab.GetComponent<OverchargeMeter>().passive = bodyPrefab.GetComponent<RifterOverchargePassive>();
            bodyPrefab.AddComponent<RifterTracker>();
        }

        public void AddHitboxes()
        {
            
        }

        public override void InitializeEntityStateMachines()
        {
            //clear existing state machines from your cloned body (probably commando)
            //omit all this if you want to just keep theirs
            Prefabs.ClearEntityStateMachines(bodyPrefab);

            //if you set up a custom main characterstate, set it up here
            //don't forget to register custom entitystates in your RifterStates.cs
            //the main "body" state machine has some special properties
            Prefabs.AddMainEntityStateMachine(bodyPrefab, "Body", typeof(RifterMain), typeof(EntityStates.SpawnTeleporterState));

            Prefabs.AddEntityStateMachine(bodyPrefab, "Weapon");
            Prefabs.AddEntityStateMachine(bodyPrefab, "Weapon2");
            bodyPrefab.GetComponent<CharacterBody>().vehicleIdleStateMachine = new EntityStateMachine[]
{
                EntityStateMachine.FindByCustomName(bodyPrefab, "Body"),
                EntityStateMachine.FindByCustomName(bodyPrefab, "Weapon"),
                EntityStateMachine.FindByCustomName(bodyPrefab, "Weapon2")
};
        }

        #region skills
        public override void InitializeSkills()
        {
            //remove the genericskills from the commando body we cloned
            Skills.ClearGenericSkills(bodyPrefab);
            //add our own
            AddPassiveSkills();
            Skills.CreateSkillFamilies(bodyPrefab);

            AddPrimarySkills();
            AddSecondarySkills();
            AddUtiitySkills();
            AddSpecialSkills();
            //if (RifterPlugin.ScepterInstalled)
            //{
            //    InitializeScepter();
            //}
        }

        //if this is your first look at skilldef creation, take a look at Secondary first
        private void AddPassiveSkills()
        {
            SkillLocator skillLocator = bodyPrefab.GetComponent<SkillLocator>();
            skillLocator.passiveSkill.enabled = true;
            skillLocator.passiveSkill.skillNameToken = RIFTER_PREFIX + "PASSIVE_RIFT_BOOST";
            skillLocator.passiveSkill.skillDescriptionToken = RIFTER_PREFIX + "PASSIVE_RIFT_BOOST_DESCRIPTION";
            skillLocator.passiveSkill.icon = assetBundle.LoadAsset<Sprite>("texImperfectionFinal");
        }

        private void AddPrimarySkills()
        {
            SkillDef primarySkillDef1 = Skills.CreateSkillDef<SkillDef>(new SkillDefInfo
                (
                    "FocusedRifts",
                    RIFTER_PREFIX + "PRIMARY_GAUNTLET_RANGED",
                    RIFTER_PREFIX + "PRIMARY_GAUNTLET_RANGED_DESCRIPTION",
                    assetBundle.LoadAsset<Sprite>("texFocusedRiftFinal"),
                    new EntityStates.SerializableEntityStateType(typeof(RiftFocus)),
                    "Weapon",
                    false
                ));
            primarySkillDef1.keywordTokens = new[] { Tokens.fractureKeyword };
            //Skills.AddPrimarySkills(bodyPrefab, primarySkillDef1);

            SkillDef primarySkillDef2 = Skills.CreateSkillDef<SkillDef>(new SkillDefInfo
               (
                   "GauntletBuckshot",
                   RIFTER_PREFIX + "PRIMARY_BUCKSHOT",
                   RIFTER_PREFIX + "PRIMARY_BUCKSHOT_DESCRIPTION",
                   assetBundle.LoadAsset<Sprite>("texScatteredRiftFinal"),
                   new EntityStates.SerializableEntityStateType(typeof(RiftBuckshot)),
                   "Weapon",
                   false
               ));
            primarySkillDef2.keywordTokens = new[] { Tokens.fractureKeyword };
            Skills.AddPrimarySkills(bodyPrefab, primarySkillDef1, primarySkillDef2);
            Skills.AddUnlockablesToFamily(bodyPrefab.GetComponent<SkillLocator>().primary.skillFamily, null, RifterUnlockables.buckshotUnlockableDef);
        }

        private void AddSecondarySkills()
        {

            SkillDef secondarySkillDef1 = Skills.CreateSkillDef<SkillDef>(new SkillDefInfo
            {
                skillName = "FractureShot",
                skillNameToken = RIFTER_PREFIX + "SECONDARY_FRACTURE",
                skillDescriptionToken = RIFTER_PREFIX + "SECONDARY_FRACTURE_DESCRIPTION",
                skillIcon = assetBundle.LoadAsset<Sprite>("texFractureFinal"),
                keywordTokens = new[] { Tokens.fractureKeyword},
                activationState = new EntityStates.SerializableEntityStateType(typeof(FractureShot)),
                activationStateMachineName = "Weapon",
                interruptPriority = InterruptPriority.Any,

                baseMaxStock = 1,

                baseRechargeInterval = 4f,

                stockToConsume = 1,

                mustKeyPress = false,

                isCombatSkill = true,
                canceledFromSprinting = false,
                cancelSprintingOnActivation = true,
                forceSprintDuringState = false,

            });


            SkillDef secondarySkillDef2 = Skills.CreateSkillDef<SkillDef>(new SkillDefInfo
            {
                skillName = "WanderingRift",
                skillNameToken = RIFTER_PREFIX + "SECONDARY_FAULT_LINE",
                skillDescriptionToken = RIFTER_PREFIX + "SECONDARY_FAULT_LINE_DESCRIPTION",
                skillIcon = assetBundle.LoadAsset<Sprite>("texWanderRift"),
                //keywordTokens = new[] { Tokens.overchargedChainedKeyword },
                activationState = new EntityStates.SerializableEntityStateType(typeof(FireRiftProjectile)),
                activationStateMachineName = "Weapon",
                interruptPriority = InterruptPriority.Any,

                baseMaxStock = 1,

                baseRechargeInterval = 7f,

                mustKeyPress = false,

                isCombatSkill = true,
                canceledFromSprinting = false,
                cancelSprintingOnActivation = true,
                forceSprintDuringState = false,

            });
            Skills.AddSecondarySkills(bodyPrefab, secondarySkillDef1, secondarySkillDef2);
        }

        private void AddUtiitySkills()
        {

            RifterSkillDef utilitySkillDef1 = Skills.CreateSkillDef<RifterSkillDef>(new SkillDefInfo
            {
                skillName = "Slipstream",
                skillNameToken = RIFTER_PREFIX + (RifterConfig.cursed.Value == true ? "UTILITY_PRESTIGE_SLIPSTREAM" : "UTILITY_SLIPSTREAM"),
                skillDescriptionToken = RIFTER_PREFIX + "UTILITY_SLIPSTREAM_DESCRIPTION",
                skillIcon = RifterConfig.cursed.Value == true ? assetBundle.LoadAsset<Sprite>("texPrestigeSlipstream") : assetBundle.LoadAsset<Sprite>("texSlipstreamFinal"),

                activationState = new EntityStates.SerializableEntityStateType(typeof(Slipstream)),
                //setting this to the "weapon2" EntityStateMachine allows us to cast this skill at the same time primary, which is set to the "weapon" EntityStateMachine
                activationStateMachineName = "Body",
                interruptPriority = InterruptPriority.PrioritySkill,
                //keywordTokens = new[] {},
                baseMaxStock = 3,
                baseRechargeInterval = 4f,

                stockToConsume = 1,

                forceSprintDuringState = true,
                canceledFromSprinting = false,
                isCombatSkill = false,
                mustKeyPress = true,
                beginSkillCooldownOnSkillEnd = true
            });
            utilitySkillDef1.overcharges = true;
            utilitySkillDef1.usesOvercharge = false;

            SkillDef utilitySkillDef2 = Skills.CreateSkillDef<SkillDef>(new SkillDefInfo
            {
                skillName = "QuantumGates",
                skillNameToken = RIFTER_PREFIX + "UTILITY_QUANTUM_RIFT",
                skillDescriptionToken = RIFTER_PREFIX + "UTILITY_QUANTUM_RIFT_DESCRIPTION",
                skillIcon = assetBundle.LoadAsset<Sprite>("texPortalUtilityFinal"),

                activationState = new EntityStates.SerializableEntityStateType(typeof(PortalLocate)),
                //setting this to the "weapon2" EntityStateMachine allows us to cast this skill at the same time primary, which is set to the "weapon" EntityStateMachine
                activationStateMachineName = "Body",
                interruptPriority = InterruptPriority.PrioritySkill,
                //keywordTokens = new[] { Tokens.overchargedKeyword },
                baseMaxStock = 1,
                baseRechargeInterval = 20f,

                stockToConsume = 1,

                forceSprintDuringState = true,
                canceledFromSprinting = false,
                isCombatSkill = false,
                mustKeyPress = true,
                beginSkillCooldownOnSkillEnd = true
            });

            
            Skills.AddUtilitySkills(bodyPrefab, utilitySkillDef1, utilitySkillDef2);

        }

        private void AddSpecialSkills()
        {
            //a basic skill
            RifterSkillDef specialSkillDef1 = Skills.CreateSkillDef<RifterSkillDef>(new SkillDefInfo
            {
                skillName = "Timelock",
                skillNameToken = RIFTER_PREFIX + "SPECIAL_TIMELOCK",
                skillDescriptionToken = RIFTER_PREFIX + "SPECIAL_TIMELOCK_DESCRIPTION",
                skillIcon = assetBundle.LoadAsset<Sprite>("texTimelockFinal"),
                keywordTokens = new[] {Tokens.crushingKeyword},
                activationState = new EntityStates.SerializableEntityStateType(typeof(TimelockDrop)),
                activationStateMachineName = "Weapon",
                interruptPriority = InterruptPriority.PrioritySkill,

                baseMaxStock = 1,
                baseRechargeInterval = 20f,

                beginSkillCooldownOnSkillEnd = true,
                isCombatSkill = true,
                mustKeyPress = false,
                cancelSprintingOnActivation = true,
            });
            specialSkillDef1.usesOvercharge = false;

            SkillDef specialSkillDef2 = Skills.CreateSkillDef<SkillDef>(new SkillDefInfo
            {
                skillName = "ChainedWorlds",
                skillNameToken = RIFTER_PREFIX + "UTILITY_CHAINED_WORLDS",
                skillDescriptionToken = RIFTER_PREFIX + "UTILITY_CHAINED_WORLDS_DESCRIPTION",
                skillIcon = assetBundle.LoadAsset<Sprite>("texChainRift"),

                activationState = new EntityStates.SerializableEntityStateType(typeof(ChainedWorldsStartup)),
                //setting this to the "weapon2" EntityStateMachine allows us to cast this skill at the same time primary, which is set to the "weapon" EntityStateMachine
                activationStateMachineName = "Weapon",
                interruptPriority = InterruptPriority.Skill,
                //keywordTokens = new[] { Tokens.overchargedKeyword },
                baseMaxStock = 1,
                baseRechargeInterval = 12f,

                stockToConsume = 1,

                forceSprintDuringState = true,
                canceledFromSprinting = false,
                isCombatSkill = false,
                mustKeyPress = true,
                beginSkillCooldownOnSkillEnd = true
            });
            Skills.AddSpecialSkills(bodyPrefab, specialSkillDef1, specialSkillDef2);

           
        }


        private void InitializeScepter()
        {
            RifterSkillDef scepterSkillDef1 = Skills.CreateSkillDef<RifterSkillDef>(new SkillDefInfo
            {
                skillName = "ToSingularity",
                skillNameToken = RIFTER_PREFIX + "SPECIAL_RECURSION_SCEPTER",
                skillDescriptionToken = RIFTER_PREFIX + "SPECIAL_RECURSION_DESCRIPTION_SCEPTER",
                skillIcon = assetBundle.LoadAsset<Sprite>("texSpecialIcon"),
                //keywordTokens = new[] { Tokens.overchargedKeyword},
                //activationState = new EntityStates.SerializableEntityStateType(typeof(RecursionLocateScepter)),
                activationStateMachineName = "Weapon2",
                interruptPriority = InterruptPriority.PrioritySkill,

                baseMaxStock = 1,
                baseRechargeInterval = 12f,

                beginSkillCooldownOnSkillEnd = true,
                isCombatSkill = true,
                mustKeyPress = false,
                cancelSprintingOnActivation = true,
            });
            scepterSkillDef1.usesOvercharge = false;
            //ItemBase<AncientScepterItem>.instance.RegisterScepterSkill(scepterSkillDef1, bodyName, SkillSlot.Special, 0);
        }

        #endregion skills

        #region skins
        public override void InitializeSkins()
        {
            ModelSkinController skinController = prefabCharacterModel.gameObject.AddComponent<ModelSkinController>();
            ChildLocator childLocator = prefabCharacterModel.GetComponent<ChildLocator>();

            CharacterModel.RendererInfo[] defaultRendererinfos = prefabCharacterModel.baseRendererInfos;

            List<SkinDef> skins = new List<SkinDef>();

            #region DefaultSkin
            //this creates a SkinDef with all default fields
            SkinDef defaultSkin = Skins.CreateSkinDef("DEFAULT_SKIN",
                assetBundle.LoadAsset<Sprite>("texRifterSkin"),
                defaultRendererinfos,
                prefabCharacterModel.gameObject);

            //these are your Mesh Replacements. The order here is based on your CustomRendererInfos from earlier
            //pass in meshes as they are named in your assetbundle
            //currently not needed as with only 1 skin they will simply take the default meshes
            //uncomment this when you have another skin
            //defaultSkin.meshReplacements = Modules.Skins.getMeshReplacements(assetBundle, defaultRendererinfos,
            //    "meshRifterSword",
            //    "meshRifterGun",
            //    "meshRifter");

            //add new skindef to our list of skindefs. this is what we'll be passing to the SkinController
            skins.Add(defaultSkin);
            #endregion

            //uncomment this when you have a mastery skin
            #region MasterySkin

            ////creating a new skindef as we did before
            //SkinDef masterySkin = Modules.Skins.CreateSkinDef(RIFTER_PREFIX + "MASTERY_SKIN_NAME",
            //    assetBundle.LoadAsset<Sprite>("texMasteryAchievement"),
            //    defaultRendererinfos,
            //    prefabCharacterModel.gameObject,
            //    RifterUnlockables.masterySkinUnlockableDef);

            ////adding the mesh replacements as above. 
            ////if you don't want to replace the mesh (for example, you only want to replace the material), pass in null so the order is preserved
            //masterySkin.meshReplacements = Modules.Skins.getMeshReplacements(assetBundle, defaultRendererinfos,
            //    "meshRifterSwordAlt",
            //    null,//no gun mesh replacement. use same gun mesh
            //    "meshRifterAlt");

            ////masterySkin has a new set of RendererInfos (based on default rendererinfos)
            ////you can simply access the RendererInfos' materials and set them to the new materials for your skin.
            //masterySkin.rendererInfos[0].defaultMaterial = assetBundle.LoadMaterial("matRifterAlt");
            //masterySkin.rendererInfos[1].defaultMaterial = assetBundle.LoadMaterial("matRifterAlt");
            //masterySkin.rendererInfos[2].defaultMaterial = assetBundle.LoadMaterial("matRifterAlt");

            ////here's a barebones example of using gameobjectactivations that could probably be streamlined or rewritten entirely, truthfully, but it works
            //masterySkin.gameObjectActivations = new SkinDef.GameObjectActivation[]
            //{
            //    new SkinDef.GameObjectActivation
            //    {
            //        gameObject = childLocator.FindChildGameObject("GunModel"),
            //        shouldActivate = false,
            //    }
            //};
            ////simply find an object on your child locator you want to activate/deactivate and set if you want to activate/deacitvate it with this skin

            //skins.Add(masterySkin);

            #endregion

            skinController.skins = skins.ToArray();
        }
        #endregion skins

        //Character Master is what governs the AI of your character when it is not controlled by a player (artifact of vengeance, goobo)
        public override void InitializeCharacterMaster()
        {
            //you must only do one of these. adding duplicate masters breaks the game.

            //if you're lazy or prototyping you can simply copy the AI of a different character to be used
            //Modules.Prefabs.CloneDopplegangerMaster(bodyPrefab, masterName, "Merc");

            //how to set up AI in code
            RifterAI.Init(bodyPrefab, masterName);

            //how to load a master set up in unity, can be an empty gameobject with just AISkillDriver components
            //assetBundle.LoadMaster(bodyPrefab, masterName);
        }

    }
}