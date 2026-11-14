using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime
{
    /// <summary>
    /// ScriptableObject wrapper around SimConfig so balance numbers can be edited in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "OutpostConfig", menuName = "Outpost/Sim Config", order = 0)]
    public class OutpostConfigAsset : ScriptableObject
    {
        public SimConfig Config = new SimConfig();
    }
}
