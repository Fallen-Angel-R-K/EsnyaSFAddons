using UnityEditor;

namespace EsnyaSFAddons
{
    public class ESFAMenu
    {
        private static readonly BuildTargetGroup[] buildTargetGroups = {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.Android,
        };
        private static void AddDefinition(string symbol)
        {
            foreach (var buildTargetGroup in buildTargetGroups)
            {
                var syms = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, $"{symbol};{syms}");
            }
        }

        private static void RemoveDefinition(string symbol)
        {
            foreach (var buildTargetGroup in buildTargetGroups)
            {
                var syms = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, syms.Replace(symbol, "").Replace(";;", ";"));
            }
        }

#if !ESFA_UCS
        [MenuItem("SaccFlight/EsnyaSFAddons/Install UdonChips")]
        public static void EnableUCS()
        {
            AddDefinition("ESFA_UCS");
        }
#else
        [MenuItem("SaccFlight/EsnyaSFAddons/Uninstall UdonChips")]
        public static void EnableUCS()
        {
            RemoveDefinition("ESFA_UCS");
        }
#endif
    }
}
