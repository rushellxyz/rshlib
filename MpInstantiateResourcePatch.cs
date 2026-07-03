using UnityEngine;

namespace RshLib
{
    internal class InstantiateResourcePatch
    {
        static bool Prefix(ref GameObject __result, string resourceId, in Vector2 pos)
        { // Niice creature for UnfoundResourceIds
            int suffixIndex = resourceId.IndexOf('$');
            string lookupName = resourceId;
            if (0 < suffixIndex)
                lookupName = resourceId.Substring(0, suffixIndex);
            if (Plugin.itemRegistry.ContainsKey(lookupName))
            {
                __result = Utils.Create(resourceId, pos, 0f);
                return false;
            }
            return true;
        }
    }
}
