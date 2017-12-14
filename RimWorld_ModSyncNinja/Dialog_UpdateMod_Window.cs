using ModSyncNinjaApiBridge;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Verse;


namespace RimWorld_ModSyncNinja
{
    internal class Dialog_UpdateMod_Window : Window
    {
        private readonly ModMetaData ModToUpdate;
        private readonly string RemoteVersion;
        private readonly string LocalVersion;
        private readonly string AssemblyVersion;

        private string about;
        private string modId;
        private string modKey;
        private string publishedField;
        private string version;

        private bool bugFix = false;
        private bool featureAdded = false;
        private bool translationUpdate = false;
        private bool isSaveBreaking = false;
        private string patchNotes = String.Empty;

        private Spinner Spinner = new Spinner();

        private bool isSubmitting = false;

        //private string errorString = String.Empty;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="modToUpdate">The mod to update</param>
        /// <param name="localVersion">The mod-to-update's version as defined in the local ModSync.xml</param>
        /// <param name="remoteVersion">The mod-to-update's version as defined in the ModSync.ninja database</param>
        /// <param name="assemblyVersion">The mod-to-update's assembly's version. Null if no assembly.</param>
        public Dialog_UpdateMod_Window(ModMetaData modToUpdate, string localVersion, string remoteVersion, string assemblyVersion)
        {
            this.ModToUpdate = modToUpdate;
            this.RemoteVersion = remoteVersion;
            this.LocalVersion = localVersion;
            this.AssemblyVersion = assemblyVersion;

            closeOnClickedOutside = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();

            // Initialize UpdateModRequest
            this.about = FileUtil.GetAboutFileText(this.ModToUpdate.RootDir);
            if (String.IsNullOrEmpty(this.about))
            {
                MSLog.Log("Unable to find About.xml for " + this.ModToUpdate.Name, MSLog.Level.All, true);
                this.Close();
            }
            this.modId = FileUtil.GetModSyncId(this.ModToUpdate.RootDir);
            this.modKey = String.Empty;
            this.publishedField = FileUtil.GetSteamPublishedField(this.ModToUpdate.RootDir);
            this.version = this.LocalVersion;
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(400, 660);

            }
        }
        public override void DoWindowContents(Rect rect)
        {
            this.Spinner.OnDoWindowContents();

            const int LEFT = 25;
            const int BUTTON_LENGTH = 65;
            int lineLength = (int)rect.xMax - LEFT * 2;
            int buttonBuffer = (int)(((int)(lineLength * 0.5f) - BUTTON_LENGTH) * 0.5f);
            int y = 0;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(LEFT, y, lineLength, 64), "ModSync.UpdateOnModSyncNinja".Translate());
            Text.Font = GameFont.Small;
            y += 52;

            // Label saying "Update Mod <mod name>"
            Widgets.Label(new Rect(LEFT, y, lineLength, 64), this.ModToUpdate.Name);
            y += 42;

            // Label saying "Previous version: <version>
            Widgets.Label(new Rect(LEFT, y, lineLength, 64), "ModSync.PreviousVersion".Translate().Replace("{previous_version}", this.RemoteVersion));
            y += 40;

            // User input for new version
            Widgets.Label(new Rect(LEFT, y, 80, 32), "ModSync.NewVersion".Translate());
            this.version = Widgets.TextField(new Rect(LEFT + 100, y, lineLength - 100, 32), this.version).Trim();
            y += 42;

            // Quick copy version buttons
            Widgets.Label(new Rect(LEFT, y, 80, 32), "ModSync.QuickCopy".Translate());
            string v = this.GetNextVersion(this.LocalVersion);
            if (Widgets.ButtonText(new Rect(LEFT + 100, y, 80, 32), v))
            {
                this.version = v;
            }
            if (!String.IsNullOrEmpty(this.AssemblyVersion))
            {
                if (Widgets.ButtonText(new Rect(LEFT + 200, y, 80, 32), this.AssemblyVersion))
                {
                    this.version = this.AssemblyVersion;
                }
            }
            y += 42;

            // User input security key
            Widgets.Label(new Rect(LEFT, y, 80, 32), "ModSync.SecurityKey".Translate());
            this.modKey = Widgets.TextField(new Rect(LEFT + 100, y, lineLength - 100, 32), this.modKey);
            y += 50;

            // "Patch Notes (optional)" label
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(LEFT, y, lineLength, 64), "ModSync.PatchNotes".Translate());
            Text.Font = GameFont.Small;
            y += 52;

            // Patch Attribute check boxes
            Widgets.CheckboxLabeled(new Rect(LEFT, y, lineLength - 100, 32), "ModSync.BugFix".Translate(), ref this.bugFix);
            y += 40;
            Widgets.CheckboxLabeled(new Rect(LEFT, y, lineLength - 100, 32), "ModSync.FeatureAdded".Translate(), ref this.featureAdded);
            y += 40;
            Widgets.CheckboxLabeled(new Rect(LEFT, y, lineLength - 100, 32), "ModSync.LanguageUpdate".Translate(), ref this.translationUpdate);
            y += 50;

            // Is Save Breaking
            Widgets.CheckboxLabeled(new Rect(LEFT, y, lineLength - 100, 32), "ModSync.IsSaveBreaking".Translate(), ref this.isSaveBreaking);
            y += 40;

            // Patch Notes
            this.patchNotes = Widgets.TextArea(new Rect(LEFT, y, lineLength, 96), this.patchNotes);
            y += 104;

            // Submit button
            if (this.isSubmitting)
            {
                Widgets.Label(new Rect(LEFT + buttonBuffer - 20, y, 105, 32), "ModSync.PleaseWait".Translate() + " " + Spinner.GetSpinnerDots());
            }
            else
            {
                bool canSubmit = !String.IsNullOrEmpty(this.modKey) && !String.IsNullOrEmpty(this.version);
                if (Widgets.ButtonText(new Rect(LEFT + buttonBuffer, y, 65, 32), "Confirm".Translate(), canSubmit, false, canSubmit)) // Using Confirm as it's translated already in the base game
                {
                    try
                    {
                        this.isSubmitting = true;
                        if (this.SubmitUpdate())
                        {
                            base.Close();
                        }
                    }
                    finally
                    {
                        this.isSubmitting = false;
                    }
                }
            }
            // Cancel Button
            if (Widgets.ButtonText(new Rect((int)(rect.width * 0.5f) + buttonBuffer, y, 65, 32), "CancelButton".Translate())) // Using CancelButton as it's translated already in the base game
            {
                base.Close();
            }

            /*if (!String.IsNullOrEmpty(errorString))
            {
                y += 40;
                Color orig = GUI.color;
                GUI.color = Color.red;
                Widgets.Label(new Rect(50, y, 250, 64), this.errorString);
                GUI.color = orig;
            }*/
        }

        private string GetNextVersion(string version)
        {
            if (String.IsNullOrEmpty(version))
                return "0";
            char c = version[version.Length - 1];
            if (c >= '0' && c <= '9')
            {
                List<char> chars = new List<char>();
                int end = version.Length - 1;
                while (end >= 0 && version[end] >= '0' && version[end] <= '9')
                {
                    chars.Add(version[end]);
                    --end;
                }

                int num = 0;
                for (int i = chars.Count - 1; i >= 0; --i)
                {
                    if (i != chars.Count - 1)
                        num *= 10;
                    num += (int)char.GetNumericValue(chars[i]);
                }
                ++num;
                if (end == -1)
                {
                    return num.ToString();
                }
                return version.Substring(0, end + 1) + num.ToString();
            }
            return version;
        }

        private bool SubmitUpdate()
        {
            UpdateModRequest request = new UpdateModRequest();

            request.About = FileUtil.GetAboutFileText(this.ModToUpdate.RootDir);
            request.ModId = this.modId;
            request.ModKey = this.modKey;
            request.PublishedField = this.publishedField;
            request.SaveBreaking = this.isSaveBreaking;
            request.Version = this.version;
            request.Languages = this.GetLangauges();

            Patch patch = new Patch();
            StringBuilder sb = new StringBuilder();
            sb.Append(Convert.ToByte(this.bugFix).ToString());
            sb.Append(Convert.ToByte(this.featureAdded).ToString());
            sb.Append(Convert.ToByte(this.translationUpdate).ToString());
            patch.Attributes = sb.ToString();
            patch.Notes = this.patchNotes;
            request.Patch = patch;
#if DEBUG_CLIENT
            Log.Warning("Sending " + request.ToString());
#endif

            // Send the actual Request
            ModSyncApi msa = new ModSyncApi();
#if DEBUG_CLIENT
            msa.URL = "https://localhost:44300/api2/";
#endif
            ResponseStatus status = msa.UpdateMod(request);
#if DEBUG_CLIENT
            Log.Warning("Response: " + status.ToString());
#endif
            if (status.Success)
            {
                // Update the ModSync.xml file
                FileUtil.UpdateModSyncXml(this.ModToUpdate.RootDir, request);
                return true;
            }
            else
            {
                if (status.Error == (int)UpdateModApiErrors.OldVersion)
                    Log.Error("ModSync.OldVersion".Translate());
                else if (status.Error == (int)UpdateModApiErrors.NotFound)
                    Log.Error("ModSync.NotFound".Translate());
                else if (status.Error == (int)UpdateModApiErrors.InvalidKey)
                    Log.Error("ModSync.InvalidKey".Translate());
                else if (status.Error == (int)UpdateModApiErrors.NoVersion)
                    Log.Error("ModSync.NoVersion".Translate());
                else if (status.Error == (int)UpdateModApiErrors.DeletedMod)
                    Log.Error("ModSync.DeletedMod".Translate());
                else if (status.Error == (int)UpdateModApiErrors.UnverifiedMod)
                    Log.Error("ModSync.UnverifiedMod".Translate());
                else if (status.Error == (int)UpdateModApiErrors.DatabaseError)
                    Log.Error("ModSync.DatabaseError".Translate());
                else if (status.Error == (int)UpdateModApiErrors.InvalidPatchNotes)
                    Log.Error("ModSync.InvalidPatchNotes".Translate());
                else
                {
                    Log.Warning("Response: " + status.ToString());
                    Log.Error("ModSync.UnknownError");
                }
                return false;
            }
        }

        private string GetLangauges()
        {
            StringBuilder sb = new StringBuilder();

            string dir = this.ModToUpdate.RootDir + "/Languages";
            if (Directory.Exists(dir))
            {
                foreach (string d in Directory.GetDirectories(dir))
                {
                    if (sb.Length > 0)
                        sb.Append(",");
                    DirectoryInfo info = new DirectoryInfo(d);
                    sb.Append(info.Name);
                }
            }
            return sb.ToString();
        }
    }
}
 