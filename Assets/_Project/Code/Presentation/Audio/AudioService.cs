using System.Collections.Generic;
using Match3.Core;
using UnityEngine;

namespace Match3.Presentation
{
    /// <summary>
    /// Thin wrapper around two AudioSources -- one for one-shot SFX, one looping for music -- that
    /// resolves clips by name from <c>Resources/Audio</c>.
    /// <para>
    /// A missing clip is not an error: every Play call is a silent no-op, so the game is fully
    /// playable with zero audio assets. Drop a clip named e.g. "match.ogg" into Resources/Audio and
    /// it starts playing itself the next time that event fires; nothing else needs to change. See
    /// Resources/Audio/README.txt for the full list of names this looks for.
    /// </para>
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        private const string Folder = "Audio";

        private AudioSource _sfx;
        private AudioSource _music;

        // Caches misses too (as null), so a name with no clip is only ever looked up once.
        private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        public static AudioService Create(Transform parent)
        {
            var go = new GameObject("audio");
            go.transform.SetParent(parent, false);

            var service = go.AddComponent<AudioService>();

            service._sfx = go.AddComponent<AudioSource>();
            service._sfx.playOnAwake = false;

            service._music = go.AddComponent<AudioSource>();
            service._music.playOnAwake = false;
            service._music.loop = true;
            service._music.volume = 0.5f;

            return service;
        }

        public void PlaySwap() => PlayOneShot("swap");
        public void PlayMatch() => PlayOneShot("match");
        public void PlayVictory() => PlayOneShot("victory");
        public void PlayDefeat() => PlayOneShot("defeat");
        public void PlayUiClick() => PlayOneShot("ui_click");

        public void PlayBooster(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Line: PlayOneShot("booster_line"); break;
                case BoosterType.Bomb: PlayOneShot("booster_bomb"); break;
                case BoosterType.Rainbow: PlayOneShot("booster_rainbow"); break;
                case BoosterType.Plane: PlayOneShot("booster_plane"); break;
            }
        }

        /// <summary>Starts the background loop if it isn't already playing. Safe to call repeatedly.</summary>
        public void PlayMusic()
        {
            AudioClip clip = Load("music");
            if (clip == null || _music.isPlaying)
                return;

            _music.clip = clip;
            _music.Play();
        }

        private void PlayOneShot(string name)
        {
            AudioClip clip = Load(name);
            if (clip != null)
                _sfx.PlayOneShot(clip);
        }

        private AudioClip Load(string name)
        {
            if (_cache.TryGetValue(name, out AudioClip cached))
                return cached;

            AudioClip clip = Resources.Load<AudioClip>($"{Folder}/{name}");
            _cache[name] = clip;
            return clip;
        }
    }
}
