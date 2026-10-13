using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// The game's sound system. On startup it synthesizes every <see cref="GameSound"/>
    /// (<see cref="SoundSynthesizer"/>), builds a small pool of AudioSources and installs
    /// <see cref="GameAudio.Handler"/>. Match and UI sounds play in 2D; sounds reported with a
    /// position (shots, intakes, scoring, climbs) are partly spatialized. Rapid-fire sounds are
    /// rate-limited so six robots shooting at once never turn into noise.
    /// </summary>
    public class GameAudioSynth : MonoBehaviour
    {
        const int PoolSize = 12;
        const float SpamInterval = 0.05f;
        const string MutedPrefKey = "FRC6874.Muted";

        /// <summary>Overall loudness applied on top of the per-sound levels.</summary>
        public float MasterVolume = 0.85f;

        AudioClip[] clips;
        AudioSource[] voices;
        float[] voiceStartTimes;
        float[] lastPlayTimes;
        bool muted;

        /// <summary>Mutes every game sound (remembered between sessions). Muting stops what is playing.</summary>
        public bool Muted
        {
            get => muted;
            set
            {
                if (muted == value) return;
                muted = value;
                PlayerPrefs.SetInt(MutedPrefKey, muted ? 1 : 0);
                if (muted) StopAll();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Supports "Enter Play Mode Options" with domain reload disabled.
            GameAudio.Handler = null;
        }

        void Awake()
        {
            muted = PlayerPrefs.GetInt(MutedPrefKey, 0) == 1;
            BuildClips();
            BuildVoices();
            GameAudio.Handler = HandleSound;
        }

        void OnDestroy()
        {
            if (GameAudio.Handler == (System.Action<GameSound, Vector3?>)HandleSound) GameAudio.Handler = null;
            if (clips == null) return;
            foreach (AudioClip clip in clips)
                if (clip != null) Destroy(clip);
        }

        /// <summary>Stops every sound this system is playing.</summary>
        public void StopAll()
        {
            if (voices == null) return;
            foreach (AudioSource voice in voices)
                if (voice != null) voice.Stop();
        }

        void BuildClips()
        {
            int count = System.Enum.GetValues(typeof(GameSound)).Length;
            clips = new AudioClip[count];
            lastPlayTimes = new float[count];
            for (int i = 0; i < count; i++)
            {
                clips[i] = SoundSynthesizer.CreateClip((GameSound)i);
                lastPlayTimes[i] = -1f;
            }
        }

        void BuildVoices()
        {
            voices = new AudioSource[PoolSize];
            voiceStartTimes = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Voice " + i);
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 3f;
                source.maxDistance = 60f;
                source.spatialBlend = 0f;
                voices[i] = source;
                voiceStartTimes[i] = -1f;
            }
        }

        void HandleSound(GameSound sound, Vector3? position)
        {
            if (muted || clips == null || voices == null) return;
            int index = (int)sound;
            if (index < 0 || index >= clips.Length || clips[index] == null) return;

            float now = Time.unscaledTime;
            if (IsRapidFire(sound))
            {
                if (lastPlayTimes[index] >= 0f && now - lastPlayTimes[index] < SpamInterval) return;
            }
            lastPlayTimes[index] = now;

            int voiceIndex = AcquireVoice();
            AudioSource voice = voices[voiceIndex];
            if (voice == null) return;
            voiceStartTimes[voiceIndex] = now;

            bool positional = position.HasValue && IsWorldSound(sound);
            if (positional) voice.transform.position = position.Value;
            else voice.transform.localPosition = Vector3.zero;
            voice.spatialBlend = positional ? 0.6f : 0f;
            voice.ignoreListenerPause = sound == GameSound.UIClick;   // menu clicks still play while paused
            voice.pitch = IsRapidFire(sound) ? Random.Range(0.95f, 1.05f) : 1f;
            voice.volume = Mathf.Clamp01(BaseVolume(sound) * MasterVolume);
            voice.clip = clips[index];
            voice.Play();
        }

        /// <summary>A free voice, or the one that started longest ago.</summary>
        int AcquireVoice()
        {
            int oldest = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] != null && !voices[i].isPlaying) return i;
                if (voiceStartTimes[i] < voiceStartTimes[oldest]) oldest = i;
            }
            return oldest;
        }

        static bool IsRapidFire(GameSound sound) =>
            sound == GameSound.Shoot || sound == GameSound.Intake || sound == GameSound.Score;

        static bool IsWorldSound(GameSound sound) =>
            sound == GameSound.Shoot || sound == GameSound.Intake || sound == GameSound.Score || sound == GameSound.Climb;

        static float BaseVolume(GameSound sound)
        {
            switch (sound)
            {
                case GameSound.MatchStart: return 0.6f;
                case GameSound.AutoEnd: return 0.5f;
                case GameSound.TeleopStart: return 0.5f;
                case GameSound.EndgameWarning: return 0.5f;
                case GameSound.MatchEnd: return 0.55f;
                case GameSound.Score: return 0.32f;
                case GameSound.Shoot: return 0.4f;
                case GameSound.Intake: return 0.22f;
                case GameSound.Climb: return 0.35f;
                case GameSound.UIClick: return 0.35f;
                case GameSound.Countdown: return 0.45f;
                default: return 0.4f;
            }
        }
    }
}
