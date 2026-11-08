using UnityEngine;

namespace PianoGlass.Song
{
    /// <summary>A .mid file Unity can ship inside the app. MidiImporter makes
    /// one for each file in Assets/Songs.</summary>
    public sealed class MidiSongAsset : ScriptableObject
    {
        [SerializeField, HideInInspector] private byte[] bytes;
        [SerializeField] private string fileName;

        public byte[] Bytes => bytes;
        public string FileName => fileName;

        public void Set(string name, byte[] data)
        {
            fileName = name;
            bytes = data;
        }
    }
}
