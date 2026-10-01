using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Musiques de menu (Champ de Duel) et de combat (Victoire Rapide), en boucle, avec les réglages de Godot :
    /// volume musique 0.36, volume effets 0.80, coupure de la musique. Les réglages sont sauvegardés (PlayerPrefs).
    /// Aucun effet sonore n'est joué dans le jeu Godot d'origine ; le volume SFX est conservé pour les futurs bruitages.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        AudioSource menuSource, battleSource;
        public float MusicVolume { get; private set; } = 0.36f;
        public float SfxVolume { get; private set; } = 0.80f;
        public bool MusicMuted { get; private set; }

        public static AudioManager Ensure(RBGameAssets assets)
        {
            if (I != null) return I;
            var go = new GameObject("RB_AudioManager");
            I = go.AddComponent<AudioManager>();
            I.Init(assets);
            return I;
        }

        void Init(RBGameAssets assets)
        {
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("rb_music", 0.36f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("rb_sfx", 0.80f));
            MusicMuted = PlayerPrefs.GetInt("rb_music_muted", 0) == 1;
            menuSource = MakeSource("Menu", assets != null ? assets.menuMusic : null);
            battleSource = MakeSource("Battle", assets != null ? assets.battleMusic : null);
            Apply();
        }

        AudioSource MakeSource(string name, AudioClip clip)
        {
            var s = new GameObject("Music_" + name).AddComponent<AudioSource>();
            s.transform.SetParent(transform, false);
            s.clip = clip; s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f;
            return s;
        }

        void Awake() { I = this; }

        public void PlayMenu()
        {
            if (battleSource != null) battleSource.Stop();
            if (menuSource != null && menuSource.clip != null && !menuSource.isPlaying) menuSource.Play();
        }

        public void PlayBattle()
        {
            if (menuSource != null) menuSource.Stop();
            if (battleSource != null && battleSource.clip != null && !battleSource.isPlaying) battleSource.Play();
        }

        public void SetMusicVolume(float v) { MusicVolume = Mathf.Clamp01(v); Apply(); Save(); }
        public void SetSfxVolume(float v) { SfxVolume = Mathf.Clamp01(v); Apply(); Save(); }
        public void SetMuted(bool m) { MusicMuted = m; Apply(); Save(); }

        void Apply()
        {
            float v = MusicMuted ? 0f : MusicVolume;
            if (menuSource != null) menuSource.volume = v;
            if (battleSource != null) battleSource.volume = v;
        }

        void Save()
        {
            PlayerPrefs.SetFloat("rb_music", MusicVolume);
            PlayerPrefs.SetFloat("rb_sfx", SfxVolume);
            PlayerPrefs.SetInt("rb_music_muted", MusicMuted ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
