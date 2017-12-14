using System;
using Verse;

namespace RimWorld_ModSyncNinja
{
    [Serializable]
    internal class ModSyncModMetaData
    {
        public ModMetaData MetaData { get; set; }
        public bool IsModSyncMod { get; set; }
        public bool ServerLoadedModData { get; set; }
        public string ModDirName { get; set; }
        internal RemoteModData RemoteData { get; set; }
        internal LocalModData LocalModData { get; set; }
    }

    [Serializable]
    internal class RemoteModData
    {
        public bool IsModSyncMod { get; set; }
        public bool IsSaveBreaking { get; set; }
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
    }
    [Serializable]
    internal class LocalModData
    {
        public string Version { get; set; }
    }
    [Serializable]
    public class PlayerModAndVersion
    {
        public string MF;
        public string V;
    }
}
