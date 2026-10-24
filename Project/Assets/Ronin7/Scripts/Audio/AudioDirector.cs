using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
using Ronin7.Ship;
using UnityEngine;
using UnityEngine.Audio;

namespace Ronin7.Audio
{
    /// <summary>
    /// Persistent, event-driven audio. Subscribes to gameplay events on the <see cref="EventBus"/>
    /// and plays assigned clips — so it needs no hard references to the systems that emit them
    /// (Core + Combat + Ship only for the event types). One-shots fire at the world point of the
    /// event via <see cref="AudioSource.PlayClipAtPoint"/>; per-mode ambience and combat/explore
    /// music crossfade on looping sources, and an engine bed tracks ship speed while flying.
    ///
    /// Assets are user-supplied: drop AudioClips into Assets/Ronin7/Audio and assign them in
    /// the Inspector. Every clip field is optional — unassigned clips simply stay silent, so the
    /// loop is fully playable before audio art lands.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        /// <summary>
        /// Live mix setters used by the settings menu. These only retarget the bed/one-shot
        /// volumes; <see cref="Update"/> eases the looping beds toward the new level on its own,
        /// so changes are click-free and allocation-free.
        /// </summary>
        public void SetSfxVolume(float v) { if (!SetMixerDb("SfxVol", v)) sfxVolume = Mathf.Clamp01(v); }
        public void SetMusicVolume(float v)
        {
            if (mixer == null) { musicVolume = Mathf.Clamp01(v); return; }
            musicSliderDb = SettingsService.VolumeToDb(v);
            ApplyMusicDb();
        }
        public void SetAmbienceVolume(float v) { if (!SetMixerDb("AmbienceVol", v)) ambienceVolume = Mathf.Clamp01(v); }

        // P6-01: with a mixer assigned the sliders drive exposed dB params (SettingsService.VolumeToDb) and the
        // authored bed levels below stay put; without one the old per-source volume scaling still applies.
        private bool SetMixerDb(string param, float v) =>
            mixer != null && mixer.SetFloat(param, SettingsService.VolumeToDb(v));

        private void ApplyMusicDb() => mixer.SetFloat("MusicVol", musicSliderDb + lastDuckDb);

        [Header("Combat one-shots")]
        [SerializeField] private AudioClip swordDeflect;
        [SerializeField] private AudioClip swordImpact;
        [SerializeField] private AudioClip playerHit;
        [Tooltip("P3-03: dull metallic clank when a hit lands on a resisted side.")]
        [SerializeField] private AudioClip hitResisted;
        [Tooltip("P3-03: bright shing + rising tone when a hit exploits a weakness.")]
        [SerializeField] private AudioClip weaknessHit;

        [Header("Space-combat one-shots")]
        [SerializeField] private AudioClip shipGunFire;
        [SerializeField] private AudioClip boltImpact;
        [SerializeField] private AudioClip enemyExplosion;
        [SerializeField] private AudioClip shipHullHit;

        [Header("Flow one-shots")]
        [SerializeField] private AudioClip landing;
        [SerializeField] private AudioClip extraction;

        [Header("Combat feedback one-shots")]
        [Tooltip("Spatialized telegraph cue for an enemy's attack windup (see AttackWindupStarted) — " +
                 "the readable-behind-you parry cue now that art enemies have no visible blade.")]
        [SerializeField] private AudioClip attackWindup;
        [SerializeField] private AudioClip enemyDefeated;
        [SerializeField] private AudioClip abilityActivate;

        [Header("Roguelike run one-shots")]
        [SerializeField] private AudioClip boonChosen;
        [Tooltip("P2-02 death beat sting (RunEnded, lost).")]
        [SerializeField] private AudioClip runLost;
        [Tooltip("P2-02 victory beat sting (RunEnded, won).")]
        [SerializeField] private AudioClip runWon;
        [Tooltip("P2-06 spawn telegraph cue; falls back to the attack-windup cue when unassigned.")]
        [SerializeField] private AudioClip enemySpawning;

        [Header("P6-01 run cues")]
        [SerializeField] private AudioClip runStarted;
        [SerializeField] private AudioClip roomCleared;
        [SerializeField] private AudioClip boonOffered;
        [SerializeField] private AudioClip bossPhase;

        [Header("P6-01 per-archetype windups (attackWindup stays the plain melee cue; unset falls back to it)")]
        [SerializeField] private AudioClip windupCharger;
        [SerializeField] private AudioClip windupBlink;
        [SerializeField] private AudioClip windupDrone;
        [SerializeField] private AudioClip windupRanged;

        [Header("P6-01 low health / menu")]
        [Tooltip("Looping heartbeat; fades in below 25% HP while a run is active.")]
        [SerializeField] private AudioClip lowHealthLoop;
        [SerializeField] private AudioClip menuAmbience;

        [Header("P6-01 run music, indexed by MusicSlot (9 loops: sector x explore/combat/boss)")]
        [SerializeField] private AudioClip[] runMusic = new AudioClip[9];

        [Header("P6-01 mixer (Audio/Ronin7.mixer; exposed params MasterVol/MusicVol/SfxVol/AmbienceVol/UiVol)")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;
        [SerializeField] private AudioMixerGroup uiGroup;

        [Header("Ambience (looping, per mode)")]
        [SerializeField] private AudioClip flightAmbience;
        [SerializeField] private AudioClip onFootAmbience;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.5f;
        [SerializeField] private float ambienceFade = 1.5f;

        [Header("Roguelike arena ambience (looping, per sector — see RunMapGenerator.SectorCount)")]
        [Tooltip("Sector 0 (\"rust\") — see ArenaRoomLibrary's biome ids.")]
        [SerializeField] private AudioClip sectorAmbienceRust;
        [Tooltip("Sector 1 (\"program\").")]
        [SerializeField] private AudioClip sectorAmbienceProgram;
        [Tooltip("Sector 2 (\"garden\").")]
        [SerializeField] private AudioClip sectorAmbienceGarden;

        [Header("Music (looping, by combat state)")]
        [SerializeField] private AudioClip exploreMusic;
        [SerializeField] private AudioClip combatMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.45f;
        [SerializeField] private float musicFade = 1.5f;

        [Header("Engine loop (2D, throttle-driven)")]
        [SerializeField] private AudioClip engineLoop;
        [SerializeField, Range(0f, 1f)] private float engineVolume = 0.5f;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        // Engine response constants (kept out of the Inspector to limit knobs).
        private const float EngineIdleFraction = 0.4f; // volume while flying at zero speed
        private const float EnginePitchIdle = 0.85f;
        private const float EnginePitchFull = 1.5f;

        private AudioSource ambience;
        private AudioClip ambienceTarget;
        private float ambienceLevel; // smoothed current volume

        // P6-01: two music sources so explore <-> combat truly crossfades (index musicCur is the live one).
        private readonly AudioSource[] music = new AudioSource[2];
        private readonly float[] musicLevel = new float[2];
        private int musicCur;
        private AudioClip musicTarget;

        private AudioSource lowHealth;
        private float lowHealthLevel, nextHealthSearch;
        private Health playerHealth;
        private const float LowHealthFraction = 0.25f;
        private const float LowHealthVolume = 0.8f;

        private MusicDuck duck;
        private float musicSliderDb, lastDuckDb;
        private bool inRunNode;
        private int runSector;
        private RoomKind runKind;
        private static readonly HashSet<string> missingLogged = new();

        private AudioSource engine;
        private float engineLevel;

        private GameMode currentMode;
        private ShipController ship; // cached; re-found per scene for the engine loop
        private OneShotPool oneShots;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Destroy is deferred to end of frame, but Update would still run on this
                // duplicate THIS frame with ambience/music/engine never created (we return
                // before building them) — a guaranteed NRE on every boot-scene reload
                // (Return to Menu / Game Over). Disabling first suppresses that.
                enabled = false;
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            ambience = NewLoopSource(ambienceGroup);
            music[0] = NewLoopSource(musicGroup);
            music[1] = NewLoopSource(musicGroup);
            engine = NewLoopSource(sfxGroup);
            lowHealth = NewLoopSource(sfxGroup);
            ambienceTarget = menuAmbience; // P6-01: the menu bed until a run node / game mode sets another

            // 3D one-shot pool replaces AudioSource.PlayClipAtPoint, which allocates a
            // temporary GameObject + AudioSource per call. Lives as a child so it shares
            // our DontDestroyOnLoad lifetime.
            var poolGo = new GameObject("OneShotPool");
            poolGo.transform.SetParent(transform, false);
            oneShots = poolGo.AddComponent<OneShotPool>();
            oneShots.SetOutput(sfxGroup);
        }

        /// <summary>A 2D, silent, looping source used for the ambience/music/engine beds.</summary>
        private AudioSource NewLoopSource(AudioMixerGroup group)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = group;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D bed
            src.volume = 0f;
            return src;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SwordDeflected>(OnDeflect);
            EventBus.Subscribe<SwordImpact>(OnImpact);
            EventBus.Subscribe<PlayerHit>(OnPlayerHit);
            EventBus.Subscribe<LandingRequested>(OnLanding);
            EventBus.Subscribe<ZoneCompleted>(OnExtracted);
            EventBus.Subscribe<GameModeChanged>(OnModeChanged);
            EventBus.Subscribe<ShipWeaponFired>(OnShipFired);
            EventBus.Subscribe<ProjectileImpact>(OnBoltImpact);
            EventBus.Subscribe<EnemyShipDestroyed>(OnEnemyDestroyed);
            EventBus.Subscribe<PlayerShipDamaged>(OnPlayerShipDamaged);
            EventBus.Subscribe<SpaceEncounterStarted>(OnEncounterStarted);
            EventBus.Subscribe<SpaceEncounterCleared>(OnEncounterCleared);
            EventBus.Subscribe<AttackWindupStarted>(OnAttackWindup);
            EventBus.Subscribe<EntityDied>(OnEntityDied);
            EventBus.Subscribe<AbilityActivated>(OnAbilityActivated);
            EventBus.Subscribe<BoonChosen>(OnBoonChosen);
            EventBus.Subscribe<EnemySpawning>(OnEnemySpawning);
            EventBus.Subscribe<RunNodeEntered>(OnRunNodeEntered);
            EventBus.Subscribe<RunEnded>(OnRunEnded);
            EventBus.Subscribe<HitResisted>(OnHitResisted);
            EventBus.Subscribe<WeaknessExploited>(OnWeaknessExploited);
            EventBus.Subscribe<RunStarted>(OnRunStarted);
            EventBus.Subscribe<RoomCleared>(OnRoomCleared);
            EventBus.Subscribe<BoonOffered>(OnBoonOffered);
            EventBus.Subscribe<PostureBroken>(OnPostureBroken);
            EventBus.Subscribe<BossPhaseChanged>(OnBossPhase);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SwordDeflected>(OnDeflect);
            EventBus.Unsubscribe<SwordImpact>(OnImpact);
            EventBus.Unsubscribe<PlayerHit>(OnPlayerHit);
            EventBus.Unsubscribe<LandingRequested>(OnLanding);
            EventBus.Unsubscribe<ZoneCompleted>(OnExtracted);
            EventBus.Unsubscribe<GameModeChanged>(OnModeChanged);
            EventBus.Unsubscribe<ShipWeaponFired>(OnShipFired);
            EventBus.Unsubscribe<ProjectileImpact>(OnBoltImpact);
            EventBus.Unsubscribe<EnemyShipDestroyed>(OnEnemyDestroyed);
            EventBus.Unsubscribe<PlayerShipDamaged>(OnPlayerShipDamaged);
            EventBus.Unsubscribe<SpaceEncounterStarted>(OnEncounterStarted);
            EventBus.Unsubscribe<SpaceEncounterCleared>(OnEncounterCleared);
            EventBus.Unsubscribe<AttackWindupStarted>(OnAttackWindup);
            EventBus.Unsubscribe<EntityDied>(OnEntityDied);
            EventBus.Unsubscribe<AbilityActivated>(OnAbilityActivated);
            EventBus.Unsubscribe<BoonChosen>(OnBoonChosen);
            EventBus.Unsubscribe<EnemySpawning>(OnEnemySpawning);
            EventBus.Unsubscribe<RunNodeEntered>(OnRunNodeEntered);
            EventBus.Unsubscribe<RunEnded>(OnRunEnded);
            EventBus.Unsubscribe<HitResisted>(OnHitResisted);
            EventBus.Unsubscribe<WeaknessExploited>(OnWeaknessExploited);
            EventBus.Unsubscribe<RunStarted>(OnRunStarted);
            EventBus.Unsubscribe<RoomCleared>(OnRoomCleared);
            EventBus.Unsubscribe<BoonOffered>(OnBoonOffered);
            EventBus.Unsubscribe<PostureBroken>(OnPostureBroken);
            EventBus.Unsubscribe<BossPhaseChanged>(OnBossPhase);
        }

        private void Update()
        {
            UpdateRunMusic();
            ambienceLevel = StepCrossfade(ambience, ambienceTarget, ambienceVolume, ambienceFade, ambienceLevel);
            StepMusic();
            UpdateDuck();
            UpdateLowHealth();
            UpdateEngine();
        }

        /// <summary>
        /// P6-01 equal-time crossfade between two music sources. A target change starts the new clip on the idle
        /// source (time-synced when both loops are the same length, which the explore/combat pairs are, so the beat
        /// carries across) while the old one fades out. No allocation.
        /// </summary>
        private void StepMusic()
        {
            var cur = music[musicCur];
            if (cur.clip != musicTarget)
            {
                int next = 1 - musicCur;
                var nxt = music[next];
                nxt.Stop();
                nxt.clip = musicTarget;
                musicLevel[next] = 0f;
                if (musicTarget != null)
                {
                    nxt.time = (cur.clip != null && cur.isPlaying && Mathf.Approximately(cur.clip.length, musicTarget.length)) ? cur.time : 0f;
                    nxt.Play();
                }
                musicCur = next;
            }
            float rate = musicFade > 0f ? musicVolume / musicFade : musicVolume;
            float step = rate * Time.unscaledDeltaTime;
            for (int i = 0; i < 2; i++)
            {
                var src = music[i];
                bool live = i == musicCur && src.clip != null;
                musicLevel[i] = Mathf.MoveTowards(musicLevel[i], live ? musicVolume : 0f, step);
                src.volume = musicLevel[i];
                if (!live && musicLevel[i] <= 0.001f && src.clip != null) { src.Stop(); src.clip = null; }
            }
        }

        private void UpdateDuck()
        {
            if (mixer == null) return;
            float db = duck.DbAt(Time.unscaledTime);
            if (Mathf.Approximately(db, lastDuckDb)) return;
            lastDuckDb = db;
            ApplyMusicDb();
        }

        /// <summary>P6-01: heartbeat loop under 25% HP, tied to the player Health HeartbeatHaptics reads
        /// (found lazily at most once a second; no per-frame Find).</summary>
        private void UpdateLowHealth()
        {
            float target = 0f;
            if (RunState.InRun && lowHealthLoop != null)
            {
                if (playerHealth == null && Time.unscaledTime >= nextHealthSearch)
                {
                    nextHealthSearch = Time.unscaledTime + 1f;
                    var hb = FindAnyObjectByType<HeartbeatHaptics>();
                    if (hb != null) hb.TryGetComponent(out playerHealth);
                }
                if (playerHealth != null && playerHealth.Max > 0f)
                    target = LowHealthTarget(playerHealth.Current / playerHealth.Max);
            }
            lowHealthLevel = Mathf.MoveTowards(lowHealthLevel, target, 2f * Time.unscaledDeltaTime);
            if (lowHealthLevel > 0.001f)
            {
                if (lowHealth.clip != lowHealthLoop) lowHealth.clip = lowHealthLoop;
                if (!lowHealth.isPlaying) lowHealth.Play();
                lowHealth.volume = lowHealthLevel * LowHealthVolume;
            }
            else if (lowHealth.isPlaying) lowHealth.Stop();
        }

        /// <summary>Pure: 1 while alive and below 25% HP, else 0 (dead is silent). Test seam.</summary>
        internal static float LowHealthTarget(float fraction) => fraction > 0f && fraction < LowHealthFraction ? 1f : 0f;

        /// <summary>
        /// Roguelike run explore/combat crossfade: while a run is active, the music bed simply tracks
        /// <see cref="CombatActivity.OnFootAggro"/> (already maintained by every MeleeAttacker's FSM,
        /// zero extra bookkeeping) — no aggro'd enemy means explore, any aggro'd enemy means combat.
        /// Gated on <see cref="RunState.InRun"/> so campaign chapters (which drive music via
        /// <see cref="OnModeChanged"/>/space-encounter events instead) are untouched. A static bool
        /// read; no allocation.
        /// </summary>
        private void UpdateRunMusic()
        {
            if (!RunState.InRun) return;
            AudioClip legacy = CombatOrExploreMusic(CombatActivity.OnFootAggro, exploreMusic, combatMusic);
            if (!inRunNode) { musicTarget = legacy; return; }
            int slot = (int)MusicFor(runKind, runSector, CombatActivity.OnFootAggro);
            AudioClip clip = runMusic != null && slot < runMusic.Length ? runMusic[slot] : null;
            musicTarget = clip != null ? clip : legacy;
        }

        /// <summary>P6-01 pure selector: the run-music slot for a node (boss nodes play the sector's boss loop,
        /// everything else its explore loop). Sectors wrap like <see cref="SectorAmbience"/>.</summary>
        public static MusicSlot MusicFor(RoomKind kind, int sector) => MusicFor(kind, sector, false);

        /// <summary>As above, with combat aggro choosing the sector's combat loop on non-boss nodes.</summary>
        public static MusicSlot MusicFor(RoomKind kind, int sector, bool aggro)
        {
            const int SectorCount = 3;
            int index = ((sector % SectorCount) + SectorCount) % SectorCount;
            int offset = kind == RoomKind.Boss ? 2 : (aggro ? 1 : 0);
            return (MusicSlot)(index * 3 + offset);
        }

        /// <summary>Pure decision: which music bed the run should be on right now. A7.10-style test seam.</summary>
        internal static AudioClip CombatOrExploreMusic(bool aggro, AudioClip exploreMusic, AudioClip combatMusic) =>
            aggro ? combatMusic : exploreMusic;

        /// <summary>
        /// Drive a looping bed toward <paramref name="target"/>: if the playing clip isn't the
        /// desired one, fade fully out, swap, then fade in; otherwise hold at <paramref name="volume"/>
        /// (or 0 when there's no clip). Returns the updated smoothed level.
        /// </summary>
        private static float StepCrossfade(AudioSource src, AudioClip target, float volume, float fade, float level)
        {
            bool needSwap = src.clip != target;
            float dest = needSwap ? 0f : (target != null ? volume : 0f);
            float rate = fade > 0f ? (volume / fade) : volume;
            level = Mathf.MoveTowards(level, dest, rate * Time.unscaledDeltaTime);
            src.volume = level;

            if (needSwap && level <= 0.001f)
            {
                src.clip = target;
                if (target != null) src.Play();
                else src.Stop();
            }
            return level;
        }

        /// <summary>Engine hum whose volume + pitch track ship speed; only while flying.</summary>
        private void UpdateEngine()
        {
            if (currentMode == GameMode.SpaceFlight)
            {
                if (ship == null) ship = ShipController.Instance;
            }
            else ship = null;

            if (engine.clip != engineLoop)
            {
                engine.clip = engineLoop;
                if (engineLoop != null) engine.Play();
                else engine.Stop();
            }

            float targetVol = 0f, targetPitch = 1f;
            if (ship != null && engineLoop != null && engineVolume > 0f)
            {
                float t = ship.MaxSpeed > 0f ? Mathf.Clamp01(Mathf.Abs(ship.CurrentSpeed) / ship.MaxSpeed) : 0f;
                targetVol = engineVolume * (EngineIdleFraction + (1f - EngineIdleFraction) * t);
                targetPitch = Mathf.Lerp(EnginePitchIdle, EnginePitchFull, t);
            }

            float rate = engineVolume > 0f ? engineVolume / 0.3f : 1f; // ~0.3s to full
            engineLevel = Mathf.MoveTowards(engineLevel, targetVol, rate * Time.unscaledDeltaTime);
            engine.volume = engineLevel;
            engine.pitch = Mathf.Lerp(engine.pitch, targetPitch, 8f * Time.unscaledDeltaTime);
        }

        private void OnDeflect(SwordDeflected e) => PlayAt(swordDeflect, e.Point);
        private void OnImpact(SwordImpact e)
        {
            if (latch.Suppresses(e.Victim)) return; // BUG-39: the resist/exploit clip is that hit's only sound
            PlayAt(swordImpact, e.Point);
        }
        private void OnPlayerHit(PlayerHit e) => PlayAt(playerHit, e.Point);
        private void OnRunStarted(RunStarted _) { PlayAt(runStarted, ListenerPoint()); RouteVoice(); }
        private void OnRoomCleared(RoomCleared _) => PlayAt(roomCleared, ListenerPoint());
        private void OnBoonOffered(BoonOffered _) => PlayAt(boonOffered, ListenerPoint());
        private void OnPostureBroken(PostureBroken _) => duck.Trigger(Time.unscaledTime);
        private void OnBossPhase(BossPhaseChanged e)
        {
            duck.Trigger(Time.unscaledTime);
            PlayAt(bossPhase, e.Boss != null ? e.Boss.transform.position : ListenerPoint());
        }

        /// <summary>RunVoice lives in Flow (it can't see this assembly), so the director routes its source onto
        /// the SFX group when a run starts and when a node is entered.</summary>
        private void RouteVoice()
        {
            if (sfxGroup == null) return;
            var voice = FindAnyObjectByType<RunVoice>();
            if (voice != null && voice.TryGetComponent(out AudioSource src)) src.outputAudioMixerGroup = sfxGroup;
        }
        private void OnHitResisted(HitResisted e) { latch.Mark(e.Victim); PlayAt(hitResisted, e.Point); }
        private void OnWeaknessExploited(WeaknessExploited e) { duck.Trigger(Time.unscaledTime); latch.Mark(e.Victim); PlayAt(weaknessHit, e.Point, ExploitGain); }
        private void OnLanding(LandingRequested _) => PlayAt(landing, ListenerPoint());
        private void OnExtracted(ZoneCompleted _) => PlayAt(extraction, ListenerPoint());

        private void OnAttackWindup(AttackWindupStarted e) => PlayAt(WindupFor(e.Attacker), e.Point);

        /// <summary>P6-01: per-archetype telegraph voice (distinct pitch and timbre, so it reads without colour).
        /// TryGetComponent does not allocate; an unset variant falls back to the plain melee cue.</summary>
        private AudioClip WindupFor(GameObject attacker)
        {
            AudioClip c = null;
            if (attacker != null)
            {
                if (attacker.TryGetComponent<ChargerEnemy>(out _)) c = windupCharger;
                else if (attacker.TryGetComponent<BlinkEnemy>(out _)) c = windupBlink;
                else if (attacker.TryGetComponent<SupportDrone>(out _)) c = windupDrone;
                else if (attacker.TryGetComponent<RangedEnemy>(out _)) c = windupRanged;
            }
            return c != null ? c : attackWindup;
        }

        // A7.4-style: don't fire a "kill" thud on the player's own death — that has its own game-over
        // flow (RunDirector/GameFlowManager); this is enemy-defeated combat feedback only.
        private void OnEntityDied(EntityDied e)
        {
            var playerObj = VRRig.Instance != null ? VRRig.Instance.gameObject : null;
            if (playerObj != null && e.Entity == playerObj) return;
            PlayAt(enemyDefeated, e.Entity != null ? e.Entity.transform.position : ListenerPoint());
        }

        private void OnAbilityActivated(AbilityActivated _) => PlayAt(abilityActivate, ListenerPoint());
        private void OnBoonChosen(BoonChosen _) => PlayAt(boonChosen, ListenerPoint());
        private void OnRunEnded(RunEnded e)
        {
            PlayAt(e.Won ? runWon : runLost, ListenerPoint());
            inRunNode = false;
            ambienceTarget = menuAmbience; // back to the menu bed; the run music fades out
            musicTarget = null;
        }

        private void OnEnemySpawning(EnemySpawning e) => PlayAt(enemySpawning != null ? enemySpawning : attackWindup, e.Point);

        /// <summary>Sets the arena's looping ambience bed to the entered node's sector theme (rust /
        /// program / garden — see <see cref="SectorAmbience"/>); Update's StepCrossfade handles the
        /// actual fade-out/swap/fade-in.</summary>
        private void OnRunNodeEntered(RunNodeEntered e)
        {
            ambienceTarget = SectorAmbience(e.Sector, sectorAmbienceRust, sectorAmbienceProgram, sectorAmbienceGarden);
            inRunNode = true; runSector = e.Sector; runKind = e.Kind; // UpdateRunMusic picks the loop each frame
            RouteVoice();
        }

        /// <summary>Pure sector -> ambience clip mapping (0 = rust, 1 = program, 2 = garden, matching
        /// ArenaRoomLibrary's biome order); wraps like ArenaRoomLibrary.ForSector so an out-of-range
        /// sector never throws. A7.10-style test seam.</summary>
        internal static AudioClip SectorAmbience(int sector, AudioClip rust, AudioClip program, AudioClip garden)
        {
            const int SectorCount = 3;
            int index = ((sector % SectorCount) + SectorCount) % SectorCount;
            return index switch
            {
                0 => rust,
                1 => program,
                _ => garden,
            };
        }

        private void OnShipFired(ShipWeaponFired e) => PlayAt(shipGunFire, e.WorldPoint);
        private void OnBoltImpact(ProjectileImpact e) => PlayAt(boltImpact, e.WorldPoint);
        private void OnPlayerShipDamaged(PlayerShipDamaged e) => PlayAt(shipHullHit, e.WorldPoint);
        private void OnEnemyDestroyed(EnemyShipDestroyed e) =>
            PlayAt(enemyExplosion, e.Ship != null ? e.Ship.transform.position : ListenerPoint());

        // Combat music swaps in for the duration of an encounter, then back to the explore bed.
        private void OnEncounterStarted(SpaceEncounterStarted _) => musicTarget = combatMusic;
        private void OnEncounterCleared(SpaceEncounterCleared _) => musicTarget = exploreMusic;

        private void OnModeChanged(GameModeChanged e)
        {
            currentMode = e.Current;
            // Set the desired beds; Update() handles the fade-out/swap/fade-in. The explore bed is
            // the default music outside an active encounter (encounter events override it).
            ambienceTarget = e.Current switch
            {
                GameMode.SpaceFlight => flightAmbience,
                GameMode.OnFoot => onFootAmbience,
                _ => ambienceTarget
            };
            if (e.Current == GameMode.SpaceFlight || e.Current == GameMode.OnFoot)
                musicTarget = exploreMusic;
        }

        private void PlayAt(AudioClip clip, Vector3 point, float gain = 1f, [CallerMemberName] string cue = "")
        {
            if (clip == null)
            {
                if (NoteMissing(cue)) Debug.LogWarning($"[AudioDirector] No clip assigned for {cue}; the cue stays silent.");
                return;
            }
            if (sfxVolume <= 0f) return;
            oneShots.Play(clip, point, Mathf.Min(1f, sfxVolume * gain)); // AudioSource.volume caps at 1
        }

        /// <summary>P6-01 dev aid: true the first time <paramref name="cue"/> played with no clip, so the
        /// warning fires once per cue instead of every event. Test seam.</summary>
        internal static bool NoteMissing(string cue) => missingLogged.Add(cue);

        // BUG-39: +3 dB on the exploit clip (only audible below full SFX volume; at 1.0 it is capped).
        private const float ExploitGain = 1.41f;
        private HitConfirmLatch latch;

        /// <summary>World point near the listener, so non-positional cues (landing/extract) read as present.</summary>
        private static Vector3 ListenerPoint()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.zero;
        }
    }
}
