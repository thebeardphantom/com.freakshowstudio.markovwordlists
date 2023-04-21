using FreakshowStudio.MarkovWordLists.Runtime;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace FreakshowStudio.MarkovWordLists.Editor
{
    [ScriptedImporter(1, "markov")]
    public class MarkovWordlistImporter : ScriptedImporter
    {
        #region Properties

        [field: Min(1)]
        [field: SerializeField]
        private int Order { get; set; } = 4;

        #endregion

        #region Methods

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var words = File.ReadLines(ctx.assetPath);
            var asset = MarkovWordlist.FromData(Order, words);
            ctx.AddObjectToAsset("MarkovWordlist", asset);
            ctx.SetMainObject(asset);
        }

        #endregion
    }
}