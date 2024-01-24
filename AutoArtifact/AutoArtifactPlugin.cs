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
    [BepInPlugin("com.Moffein.AutoArtifact", "AutoArtifact", "1.0.0")]
    public class AutoArtifactPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<string> artifactListString;
        public List<ArtifactInfo> artifactInfoList = new List<ArtifactInfo>();

        public void Awake()
        {
            ReadConfig();
            RoR2.RoR2Application.onLoad += ParseArtifacts;
            RoR2.Stage.onServerStageBegin += Stage_onServerStageBegin;
        }

        private void Stage_onServerStageBegin(Stage obj)
        {
            if (!Run.instance || !RunArtifactManager.instance) return;
            foreach (ArtifactInfo info in artifactInfoList)
            {
                if (Run.instance.stageClearCount >= info.stageClearCount && !RunArtifactManager.instance.IsArtifactEnabled(info.artifactDef))
                {
                    RunArtifactManager.instance.SetArtifactEnabledServer(info.artifactDef, true);
                }
            }
        }

        private void ReadConfig()
        {
            artifactListString = base.Config.Bind<string>(new ConfigDefinition("Settings", "Artifact List"), "", new ConfigDescription("List of artifacts separated by commas. Format is ArtifactName:StageClearCount (ex. Command:5, Honor:10)"));
            artifactListString.SettingChanged += ArtifactListString_SettingChanged;
        }

        private void ArtifactListString_SettingChanged(object sender, EventArgs e)
        {
            ParseArtifacts();
        }

        private void ParseArtifacts()
        {
            Debug.Log("AutoArtifact: Parsing Artifact List");
            artifactInfoList.Clear();
            string[] strArray = artifactListString.Value.Split(',');
            foreach (string str in strArray)
            {
                string trim = str.Trim();
                if (trim.Length > 0)
                {

                    ArtifactDef artifact = null;
                    int stageCompletions = 0;

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
                    }

                    artifactInfoList.Add(new ArtifactInfo(artifact, stageCompletions));
                    Debug.Log("AutoArtifact: Added " + artifact.cachedName + " : " + stageCompletions);
                }
            }
        }

        public class ArtifactInfo
        {
            public ArtifactDef artifactDef;
            public int stageClearCount;

            public ArtifactInfo(ArtifactDef artifactDef, int minStages = 0)
            {
                this.artifactDef = artifactDef;
                this.stageClearCount = minStages;
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
