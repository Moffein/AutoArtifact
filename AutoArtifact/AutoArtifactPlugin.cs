using RoR2;
using BepInEx;
using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;
using MonoMod.Cil;
using System.Text.RegularExpressions;
using UnityEngine;

namespace R2API.Utils
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ManualNetworkRegistrationAttribute : Attribute
    {
    }
}

namespace AutoArtifact
{
    [BepInPlugin("com.Moffein.AutoArtifact", "AutoArtifact", "1.1.0")]
    public class AutoArtifactPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<string> artifactListString;
        public List<ArtifactInfo> artifactInfoList = new List<ArtifactInfo>();

        public void Awake()
        {
            ReadConfig();
            RoR2.RoR2Application.onLoad += ParseArtifacts;
            On.RoR2.SceneDirector.Start += SceneDirector_Start;
        }

        //Need to hook this way to fix Sacrifice.
        private void SceneDirector_Start(On.RoR2.SceneDirector.orig_Start orig, SceneDirector self)
        {
            if (NetworkServer.active && Run.instance && RunArtifactManager.instance)
            {
                foreach (ArtifactInfo info in artifactInfoList)
                {
                    bool alreadyEnabled = RunArtifactManager.instance.IsArtifactEnabled(info.artifactDef);
                    bool meetsStages = info.stageClearCount >= 0 && Run.instance.stageClearCount >= info.stageClearCount;

                    bool usePlayerCheck = info.playerCount >= 0;
                    int playerDiff = Run.instance.participatingPlayerCount - info.playerCount;

                    bool meetsPlayers = usePlayerCheck && playerDiff >= 0;
                    if (meetsStages || meetsPlayers)
                    {
                        if (!alreadyEnabled)
                        {
                            Debug.Log("AutoArtifact: Enabled " + info.artifactDef.cachedName + ", Meets StageClearCount Requirement: " + meetsStages + ", Meets PlayerCount Requirement: " + meetsPlayers);
                            RunArtifactManager.instance.SetArtifactEnabledServer(info.artifactDef, true);
                        }
                    }
                    else if (usePlayerCheck && playerDiff < 0 && alreadyEnabled)
                    {
                        Debug.Log("AutoArtifact: Disabled " + info.artifactDef.cachedName + " due to PlayerCount.");
                        RunArtifactManager.instance.SetArtifactEnabledServer(info.artifactDef, false);
                    }
                }
            }
            orig(self);
        }

        private void ReadConfig()
        {
            artifactListString = base.Config.Bind<string>(new ConfigDefinition("Settings", "Artifact List"), "", new ConfigDescription("List of artifacts separated by commas. Format is ArtifactName:StageClearCount:PlayerCount (ex. Command, Honor:10, Sacrifice:-1:5). Use negative numbers to skip a check (ex. Sacrifice:-1:5 means that StageCount requirement will always be treated as False, meaning only PlayerCount will determine whether the artifact should be activated)."));
            artifactListString.SettingChanged += ArtifactListString_SettingChanged;
        }

        private void ArtifactListString_SettingChanged(object sender, EventArgs e)
        {
            ParseArtifacts();
        }

        private void ParseArtifacts()
        {
            Debug.Log("AutoArtifact: Parsing Artifact Stagecount List");
            artifactInfoList.Clear();
            string[] strArray = artifactListString.Value.Split(',');
            foreach (string str in strArray)
            {
                string trim = str.Trim();
                if (trim.Length > 0)
                {

                    ArtifactDef artifact = null;
                    int stageCompletions = 0;   //Set to 0 by default. Overridden if actual stages are listed.
                    int playerCount = -1;

                    string[] info = str.Split(':');
                    if (info.Length > 0)
                    {
                        string artifactName = info[0].Trim();
                        artifact = GetArtifactDefFromString(artifactName);
                    }
                    if (artifact == null) continue;

                    if (info.Length > 1)
                    {
                        int.TryParse(info[1].Trim(), out stageCompletions);

                        if (info.Length > 2)
                        {
                            int.TryParse(info[2].Trim(), out playerCount);
                        }
                    }

                    artifactInfoList.Add(new ArtifactInfo(artifact, stageCompletions, playerCount));
                    Debug.Log("AutoArtifact: Added " + artifact.cachedName + " : " + stageCompletions + " : " + playerCount);
                }
            }
        }

        public class ArtifactInfo
        {
            public ArtifactDef artifactDef;
            public int stageClearCount;
            public int playerCount;

            public ArtifactInfo(ArtifactDef artifactDef, int stageClearCount, int playerCount)
            {
                this.artifactDef = artifactDef;
                this.stageClearCount = stageClearCount;
                this.playerCount = playerCount;
            }
        }

        //Using code from MidRunArtifacts since there's no easy way that I know of for users to find internal artifact names.
        //Taken from https://github.com/KingEnderBrine/-RoR2-MidRunArtifacts/
        private static ArtifactDef GetArtifactDefFromString(string partialName)
        {
            //Attempt to match internal name before doing a partial name match
            ArtifactDef match = ArtifactCatalog.FindArtifactDef(partialName);
            if (match) return match;

            foreach (var artifact in ArtifactCatalog.artifactDefs)
            {
                if (GetArgNameForAtrifact(artifact).ToLower().Contains(partialName.ToLower()))
                {
                    return artifact;
                }
            }
            return null;
        }
        //Taken from https://github.com/KingEnderBrine/-RoR2-MidRunArtifacts/
        private static string GetArgNameForAtrifact(ArtifactDef artifactDef)
        {
            return Regex.Replace(Language.GetString(artifactDef.nameToken), "[ '-]", String.Empty);
        }
    }
}
