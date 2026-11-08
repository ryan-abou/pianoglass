using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using PianoGlass.Song;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// Tells Unity how to read a .mid file: as a MidiSongAsset holding the
    /// file's bytes, which can then ship inside the app. The notes are read
    /// when the app runs, by the same code as a player's own files.
    /// </summary>
    [ScriptedImporter(1, "mid")]
    public class MidiImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var song = ScriptableObject.CreateInstance<MidiSongAsset>();
            song.Set(Path.GetFileName(ctx.assetPath), File.ReadAllBytes(ctx.assetPath));
            ctx.AddObjectToAsset("song", song);
            ctx.SetMainObject(song);
        }
    }
}
