using UnityEngine;

namespace RimWorld_ModSyncNinja
{
    static class ClipboardUtil
    {
        public static string ClipBoard
        {
            get
            {
                return GUIUtility.systemCopyBuffer;
            }
            set
            {
                GUIUtility.systemCopyBuffer = value;
            }
        }
    }
}
