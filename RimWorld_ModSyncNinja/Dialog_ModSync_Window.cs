using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace RimWorld_ModSyncNinja
{
    public class Dialog_ModSync_Window : Window
    {
        private List<ModSyncModMetaData> _relevantMods;
        private static Vector2 _listScrollPosition = Vector2.zero;
        private static float _scrollViewHeight = 0.0f;
        private Dictionary<string, string> _truncatedModNamesCache = new Dictionary<string, string>();
        private Spinner _spinner = new Spinner();
        private string _userRequestStr = string.Empty;
        private string _errorCode = String.Empty;

        public enum CurrentSyncState
        {
            Unchecked,
            ClientOffline,
            ModSyncOffline,
            Done,
            RequestStarted,
            ModSyncError
        }
        public enum InternetConnectivity
        {
            Unchecked,
            Offline,
            Online
        }

        public CurrentSyncState CurrSyncState = CurrentSyncState.Unchecked;
        public InternetConnectivity ClientInternetConnectivity = InternetConnectivity.Unchecked;
        private class NetworkIndicators
        {
            public Texture2D Working;
            public Texture2D Synced;
            public Texture2D Error;
            public Texture2D Offline;
            private static NetworkIndicators _instance = null;

            private NetworkIndicators()
            {
                Working = ContentFinder<Texture2D>.Get("UI/Indicators/networkIndicator_working");
                Synced = ContentFinder<Texture2D>.Get("UI/Indicators/networkIndicator_requestcompleted");
                Error = ContentFinder<Texture2D>.Get("UI/Indicators/networkIndicator_error");
                Offline = ContentFinder<Texture2D>.Get("UI/Indicators/networkIndicator_offline");
            }

            public static NetworkIndicators Get()
            {
                if (_instance == null) _instance = new NetworkIndicators();
                return _instance;
            }
        }

        public Dialog_ModSync_Window()
        {
            MSLog.Log("New window", MSLog.Level.All);
            try
            {
                CurrSyncState = CurrentSyncState.RequestStarted;
                ClientInternetConnectivity = InternetConnectivity.Unchecked;
                closeOnClickedOutside = false;
                FetchRelevantMods();

                string userRequest = NetworkManager.GenerateServerRequestString(_relevantMods);
                _userRequestStr = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userRequest));
                NetworkManager.CheckForInternetConnectionAsync(
                    this.ClientInternetConnectivity, (InternetConnectivity clientInternetConnectivity) =>
                    {
                        this.ClientInternetConnectivity = clientInternetConnectivity;
                        this.OnNetworkConnectionTestCompleted();
                    });
            }
            catch (Exception e)
            {
                MSLog.Log("ERROR:", MSLog.Level.All);
                MSLog.Log(e.Message, MSLog.Level.All);
            }
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(725f, 65f + Mathf.Max(200f, 700f));

            }
        }

        private void FetchRelevantMods()
        {
            if (_relevantMods == null || _relevantMods.Count == 0)
            {
                _relevantMods = new List<ModSyncModMetaData>();
                foreach (ModMetaData modMetaData in ModsConfig.ActiveModsInLoadOrder)
                {

                    if (modMetaData.OnSteamWorkshop == false && modMetaData.IsCoreMod == false)
                    {
                        _relevantMods.Add(new ModSyncModMetaData()
                        {
                            IsModSyncMod = false,
                            MetaData = modMetaData,
                            ModDirName = modMetaData.RootDir.Name,
                            ServerLoadedModData = false,
                            LocalModData = new LocalModData() { Version = FileUtil.GetModSyncVersionForMod(modMetaData.RootDir) }
                        });

                    }

                }
                _relevantMods = _relevantMods.OrderByDescending(x => x.MetaData.Name.Equals("ModSync Ninja")).ThenBy(x => x.MetaData.Name).ToList();
            }
        }
        protected Rect GetMainRect(Rect rect, float extraTopSpace = 0, bool ignoreTitle = false)
        {
            float y = 0.0f;
            if (!ignoreTitle)
                y = 45f + extraTopSpace;
            return new Rect(0.0f, y, rect.width, (float)((double)rect.height - 38.0 - (double)y - 17.0));
        }

        public override void DoWindowContents(Rect rect)
        {
            Rect outRect = new Rect(0.0f, 0.0f, rect.width + 30f, rect.height);
            List<ModSyncModMetaData> mods = _relevantMods;


            float pushY = 70f;
            // sync mods title
            DoTopBar(outRect, pushY);
            // place labels ontop of table
            PlaceTableLabels(outRect, pushY);

            Rect rect4 = new Rect(0f, 40f + pushY, outRect.width - 30f, outRect.height - 110f - 60f - pushY);
            Rect safeUpdatesRect = new Rect(15f + 28f, rect4.height + rect4.y + 15f, rect4.width - 100f, 80f);
            GUI.DrawTexture(new Rect(15f, rect4.height + rect4.y + 15f, 24f, 24f), (Texture)Widgets.CheckboxOnTex);
            Widgets.Label(safeUpdatesRect, " - " + "ModSync.ModNotBreakingSave".Translate());
            Widgets.DrawMenuSection(rect4);
            
            // long scroller
            //float height = (float)(ModLister.AllInstalledMods.Count<ModMetaData>() * 34 *60+ 300);
            // real scroller

            float height = (float)(ModLister.AllInstalledMods.Count<ModMetaData>() * 34 + 300);
            AddModList(rect4, height, mods);

            AddCloseBtn(rect);
            AddBrandLogo(rect);

            // reset UI settings
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.EndGroup();
            GUI.color = Color.white;
        }

        private void DoTopBar(Rect rect, float height)
        {
            _spinner.OnDoWindowContents();

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width / 2, height), "ModSync.UpdateMods".Translate().ToUpper());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 30f, rect.width / 2, height-30f), "ModSync.ShowingActiveMods".Translate().ToUpper());
            //CurrSyncState = CurrentSyncState.ModSyncError;
            if (CurrSyncState == CurrentSyncState.ClientOffline || CurrSyncState == CurrentSyncState.ModSyncOffline)
            {
                PlaceConnectionStatusBar(new Rect(rect.width / 2f, 0, rect.width / 2f, height),
                    CurrSyncState == CurrentSyncState.ClientOffline
                        ? " " + "ModSync.YouAreOffline".Translate()
                        : " " + "ModSync.CantConnectToModSync".Translate(),
                    NetworkIndicators.Get().Offline);
            }
            else if (CurrSyncState == CurrentSyncState.ModSyncError)
            {
                PlaceConnectionStatusBar(new Rect(rect.width / 2f, 0, rect.width / 2f, height), "ModSync.UnknwonError".Translate(), NetworkIndicators.Get().Error);

                TooltipHandler.TipRegion(new Rect(rect.width / 2f, 0, rect.width / 2f, height), new TipSignal("Error processing request: "+_errorCode+"\nPlease try again.\nIf this problem persists, open the game logger and find the error with your request and response to the server.\nUpload the details to us on the forums!", "error".GetHashCode() * 3311));
                //PlaceConnectionStatusBar(new Rect(rect.width / 2f, 0, rect.width / 2f, height), " We are live! ", NetworkIndicators.Get().Synced);
            }
            else if (CurrSyncState == CurrentSyncState.RequestStarted)
            {
                PlaceConnectionStatusBar(new Rect(rect.width / 2f, 0, rect.width / 2f, height), "ModSync.PleaseWait".Translate() + " " + _spinner.GetSpinnerDots(), NetworkIndicators.Get().Working);
            }
            else if (CurrSyncState == CurrentSyncState.Done)
            {
                PlaceConnectionStatusBar(new Rect(rect.width / 2f, 0, rect.width / 2f, height), "ModSync.Synced".Translate(), NetworkIndicators.Get().Synced);
            }
        }

        private void AddModList(Rect rect4, float height, List<ModSyncModMetaData> mods)
        {

            Rect rect5 = new Rect(0f, 0f, rect4.width - 16f, height);
            Widgets.BeginScrollView(rect4, ref _listScrollPosition, rect5, true);
            Rect rect6 = rect5.ContractedBy(4f);
            Listing_Standard modListUi = new Listing_Standard();
            modListUi.ColumnWidth = rect6.width;
            modListUi.Begin(rect6);
            /*int reorderableGroup = ReorderableWidget.NewGroup(delegate(int from, int to)
            {
                ModsConfig.Reorder(from, to);
                //SoundDefOf.TickHigh.PlayOneShotOnCamera(null);
            });*/
            int num2 = 0;
            foreach (ModSyncModMetaData current in mods)
            {
                this.DoModRow(modListUi, current, num2, 0);
                num2++;
            }
            modListUi.End();
            Widgets.EndScrollView();
        }

        private void AddBrandLogo(Rect rect)
        {

            Texture2D brand = ContentFinder<Texture2D>.Get("UI/logo", true);
            if (Widgets.ButtonImage(new Rect(rect.xMax - 235f, rect.yMax - 80f, 225f, 64f), brand, Color.white, Color.white))
            {
                NetworkManager.OpenModSyncUrl();
            }
        }

        private void AddCloseBtn(Rect rect)
        {
            // close btn
            if (Widgets.ButtonText(new Rect((rect.xMax / 2) - (80 / 2f), rect.yMax - 65f, 80f, 50f), "CloseButton".Translate(), true, false, true))
            {
                Find.WindowStack.TryRemove(this.GetType(), true);
            }
        }

        private void PlaceTableLabels(Rect inRect, float distanceFromTop)
        {
            float labelPos = 0f;
            Rect lblModName = new Rect(labelPos, distanceFromTop, inRect.width, 40f);
            labelPos += 250f;
            Rect lblModLocalVer = new Rect(labelPos, distanceFromTop, inRect.width, 40f);
            labelPos += 150f;
            Rect lblModModSyncVer = new Rect(labelPos, distanceFromTop, inRect.width, 40f);
            labelPos += 150f;
            Rect lblModUpToDate = new Rect(labelPos, distanceFromTop, inRect.width, 40f);
            Widgets.Label(lblModName, "ModSync.ModName".Translate());
            Widgets.Label(lblModLocalVer, "ModSync.LocalVersion".Translate());
            Widgets.Label(lblModModSyncVer, "ModSync.AvailableVersion".Translate());
            Widgets.Label(lblModUpToDate, "ModSync.GetUpdate".Translate());
        }

        private void PlaceConnectionStatusBar(Rect rect, string connectionMessage, Texture2D connectionIndicator)
        {
            float imgWidth = 40f;
            float imgHeight = 20f;
            float paddingFromImg = 2f;
            GUI.DrawTexture(new Rect(rect.xMax - (imgWidth * 2) - paddingFromImg, rect.y + 5f, imgWidth, imgHeight), connectionIndicator);
            Text.Anchor = TextAnchor.UpperRight;
            DoBoldLabel(new Rect(rect.x, rect.y, rect.width - (imgWidth * 2) - (paddingFromImg * 2), rect.height), connectionMessage);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DoBoldLabel(Rect rect, string label)
        {
            Widgets.Label(rect, label);
            Widgets.Label(rect, label);
            Widgets.Label(rect, label);
        }

        private void DoModRow(Listing_Standard listing, ModSyncModMetaData modSyncMod, int index, int reorderableGroup)
        {
            Rect rect = listing.GetRect(26f);
            ModMetaData mod = modSyncMod.MetaData;

            //ContentSourceUtility.DrawContentSource(rect, mod.Source, null);
            rect.xMin += 5f;
            bool flag = false;
            bool active = mod.Active;
            Rect rect2 = rect;
            string text = string.Empty;
            bool isModSyncUser = modSyncMod.IsModSyncMod;
            bool serverDataLoaded = modSyncMod.ServerLoadedModData;
            Text.Anchor = TextAnchor.UpperLeft;
            if (modSyncMod.LocalModData.Version.Equals(String.Empty) && isModSyncUser)
            {
                text = "ModSync.UnknownVersion".Translate();
            }
            if (!isModSyncUser && CurrSyncState == CurrentSyncState.Done)
            {
                text = "ModSync.NotSupportedByModSync".Translate();
            }

            if (!text.NullOrEmpty())
            {
                text = text.Replace("{mod_name}", modSyncMod.MetaData.Name);
                TooltipHandler.TipRegion(rect2, new TipSignal(text, mod.GetHashCode() * 3311));
            }
            float num = rect2.width - 24f;
            if (mod.Active)
            {
                Rect position = new Rect(rect2.xMax - 48f + 2f, rect2.y, 24f, 24f);
                //GUI.DrawTexture(position, TexButton.DragHash);
                num -= 24f;
            }
            Text.Font = GameFont.Small;
            string label = mod.Name.Truncate(220, this._truncatedModNamesCache);
            Texture2D texture2D = Widgets.CheckboxPartialTex;//!active ? Widgets.CheckboxOffTex : Widgets.CheckboxOnTex;

            Widgets.Label(rect2, label);

            float labelPos = 250f;
            Rect lblModLocalVer = new Rect(labelPos, rect2.y, 150f, 24f);
            labelPos += 150f;
            Rect lblModModSyncVer = new Rect(labelPos, rect2.y, 150f, 24f);
            //GUI.color = Color.red;
            // no version on client
            if (modSyncMod.LocalModData.Version.Equals(String.Empty) && serverDataLoaded &&
                modSyncMod.RemoteData.IsModSyncMod)
            {
                Widgets.Label(lblModLocalVer, "?");
            }
            else
            {
                if (isModSyncUser && !modSyncMod.LocalModData.Version.Equals(modSyncMod.RemoteData.Version))
                {
                    GUI.color = Color.red;
                }

                Widgets.Label(lblModLocalVer, (!modSyncMod.LocalModData.Version.Equals(String.Empty)) ? modSyncMod.LocalModData.Version : "N/A on ModSync");
            }

            GUI.color = Color.white;
            // not a save breaking mod
            if (modSyncMod.IsModSyncMod && modSyncMod.ServerLoadedModData && !modSyncMod.RemoteData.IsSaveBreaking && !modSyncMod.LocalModData.Version.Equals(modSyncMod.RemoteData.Version))
            {
                GUI.DrawTexture(new Rect(rect.xMax - 140f - 28f, lblModModSyncVer.y, 24f, 24f), (Texture)Widgets.CheckboxOnTex);
            }

            // remote version
            if (serverDataLoaded && modSyncMod.RemoteData.Version != String.Empty)
            {
                Widgets.Label(lblModModSyncVer, modSyncMod.RemoteData.Version);
            }
            else if (CurrSyncState == CurrentSyncState.Done && !serverDataLoaded)
            {
                Widgets.Label(lblModModSyncVer, "ModSync.NAModSync".Translate());
            }

            //GUI.DrawTexture(new Rect(rect.xMax - 24f, rect.y, 24f, 24f), (Texture)texture2D);
            // draw download if needed
            if (serverDataLoaded && !modSyncMod.LocalModData.Version.Equals(modSyncMod.RemoteData.Version))
            {
                if (Widgets.ButtonText(new Rect(rect.xMax - 140f, rect.y, 140f, 24f), "ModSync.Update".Translate(), true, false, true))
                {
                    Application.OpenURL(modSyncMod.RemoteData.DownloadUrl);
                }
            }

            GUI.color = Color.white;
        }
        public override void PreClose()
        {
            base.PreClose();
        }

        public void OnNetworkConnectionTestCompleted()
        {
            // load data from server
            MSLog.Log("OnNetworkConnectionTestCompleted",MSLog.Level.All);
            if (ClientInternetConnectivity == InternetConnectivity.Online)
            {
                NetworkManager.CreateRequestHttpRequest(
                    _userRequestStr, 
                    (CurrentSyncState syncState) => this.CurrSyncState = syncState, 
                    (string responseStr, bool success, string errorCode) => 
                        this.OnRequestFromServerResponse(responseStr, success, errorCode));
            }
            else
            {
                CurrSyncState = CurrentSyncState.ClientOffline;
            }
        }
        [Serializable]
        public class ModDetailsResponse
        {
            public string MF;
            public string V;
            public string S;
            public bool SB;
        }

        public void OnRequestFromServerResponse(string responseStr, bool success, string errorCode = "")
        {
            MSLog.Log("OnRequestFromServerResponse, GOT: response:" + responseStr + "\nSuccess: " + success +"\nErrorCode: " + errorCode);
            if (!success)
            {
                // server not offline, this is a real error
                if (CurrSyncState != CurrentSyncState.ModSyncOffline)
                {
                    CurrSyncState = CurrentSyncState.ModSyncError;
                    _errorCode = errorCode;
                }

            }
            else
            {
                // clear previous errors
                _errorCode = String.Empty;
                // empty response
                if (responseStr.Length == 0)
                {
                    CurrSyncState = CurrentSyncState.Done;
                    return;
                }
                string responseData;
                ModDetailsResponse[] responseDataObj;
                try
                {
                    responseData = Encoding.UTF8.GetString(Convert.FromBase64String(responseStr));
                }
                catch (Exception e)
                {
                    // Failed to parse BASE 64
                    MSLog.Log("Failed to parse BASE 64");
                    MSLog.Log(e.Message);
                    CurrSyncState = CurrentSyncState.ModSyncError;
                    _errorCode = "151";
                    return;
                }
                try
                {
                    MSLog.Log("Response data BASE 64 set: " + responseData);
                    string[] rows = Regex.Split(responseData.Trim('\"'), "{%}");
                    if (rows.Length == 0)
                    {
                        CurrSyncState = CurrentSyncState.Done;
                        return;
                    }
                    MSLog.Log("Trying to parse data rows");
                    try
                    {
                        foreach (string row in rows)
                        {
                            string rowData = Encoding.UTF8.GetString(Convert.FromBase64String(row));
                            MSLog.Log("Raw row data: " + rowData);
                            ModDetailsResponse rowDataParsed = JsonUtility.FromJson<ModDetailsResponse>(rowData);
                            if (rowDataParsed != null)
                            {
                                MSLog.Log("MF:" + rowDataParsed.MF + " V:" + rowDataParsed.V + " S" +
                                          rowDataParsed.S + " SB" + rowDataParsed.SB);

                                var modData = _relevantMods.FirstOrDefault(x => x.ModDirName.ToUpper().Equals(rowDataParsed.MF.ToUpper()));
                                if (modData == null) continue;
                                modData.RemoteData = new RemoteModData();
                                modData.IsModSyncMod = true;
                                modData.RemoteData.IsModSyncMod = true;
                                modData.RemoteData.Version = rowDataParsed.V;
                                modData.RemoteData.DownloadUrl = rowDataParsed.S;
                                modData.RemoteData.IsSaveBreaking = rowDataParsed.SB;
                                modData.ServerLoadedModData = true;
                            }
                            else
                            {
                                MSLog.Log("Received invalid row, ignoring.");
                            }
                        }
                        
                    }
                    catch (Exception e)
                    {
                        MSLog.Log("Exception:");
                        MSLog.Log(e.Message);
                        CurrSyncState = CurrentSyncState.ModSyncError;
                        _errorCode = "152";
                    }
                    MSLog.Log("Finished reading json");
                }
                catch (Exception e)
                {
                    // failed to parse JSON
                    MSLog.Log("Failed to parse JSON");
                    MSLog.Log(e.Message);
                    CurrSyncState = CurrentSyncState.ModSyncError;
                    _errorCode = "153";
                    return;
                }
                //CurrSyncState = CurrentSyncState.RequestStarted;
                CurrSyncState = CurrentSyncState.Done;
            }
        }

    }
}
