using BepInEx;
using RWCustom;
using System.Linq;
using System.Security.Permissions;
using UnityEngine;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace Aquaphobia;

[BepInPlugin("com.dual.aquaphobia", "Aquaphobia", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    GameObject go;
    AudioClip drown;
    AudioClip bubbles;

    AudioSource audio;

    bool justUpdatedAudio = false;

    public void OnEnable()
    {
        On.RainWorld.OnModsInit += OnModsInit;
        On.RainWorldGame.GrafUpdate += RainWorldGame_GrafUpdate;
        On.Music.MusicPiece.SubTrack.Update += SubTrack_Update;

        On.Player.LungUpdate += Player_LungUpdate;
        On.Player.Die += Player_Die;
    }

    bool lung;
    private void Player_LungUpdate(On.Player.orig_LungUpdate orig, Player self)
    {
        try {
            lung = true;
            orig(self);
        }
        finally {
            lung = false;
        }
    }

    private void Player_Die(On.Player.orig_Die orig, Player self)
    {
        orig(self);

        if (lung) {
            audio.time = 0;
            audio.pitch = 0.9f;
            audio.clip = bubbles;
            audio.Play();
        }
    }

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (audio != null) return;

        try {
            drown = AssetManager.SafeWWWAudioClip($"file://{AssetManager.ResolveFilePath("sounds/drown.wav")}", false, true, AudioType.WAV);
            bubbles = AssetManager.SafeWWWAudioClip($"file://{AssetManager.ResolveFilePath("sounds/bubble.wav")}", false, true, AudioType.WAV);

            go = new("sfx sonic drowning stuff");
            audio = go.AddComponent<AudioSource>();
            audio.pitch = 1;
            audio.spatialBlend = 0;
            audio.loop = false;
            audio.panStereo = 0;
        }
        catch (System.Exception e) {
            Logger.LogError("Aquaphobia failed to load: " + e);
        }
    }

    private void RainWorldGame_GrafUpdate(On.RainWorldGame.orig_GrafUpdate orig, RainWorldGame self, float timeStacker)
    {
        orig(self, timeStacker);

        if (audio == null) return;

        VirtualMicrophone mic = self.cameras[0].virtualMicrophone;

        audio.volume = Mathf.Clamp01(Mathf.Pow(0.2f * mic.volumeGroups[0] * self.rainWorld.options.soundEffectsVolume, mic.soundLoader.volumeExponent));

        if (audio.clip == bubbles && audio.isPlaying) {
            return;
        }

        if (self.manager.upcomingProcess != null || !self.IsStorySession || self.cameras[0].followAbstractCreature?.realizedObject is not Player focus) {
            audio.time = 0;
            audio.Stop();
            return;
        }
        bool losingAir = !focus.dead && focus.airInLungs < 0.85f && focus.firstChunk.submersion > 0.9f && !focus.room.game.setupValues.invincibility && !focus.chatlog;
        if (!losingAir) {
            audio.time = 0;
            audio.Stop();
            return;
        }

        if (audio.clip != drown) {
            audio.clip = drown;
        }

        if (!audio.isPlaying) {
            audio.pitch = 1;
            audio.time = Custom.LerpMap(focus.airInLungs, 0.85f, 0f, 0f, drown.length);
            audio.Play();
        }

        if (!justUpdatedAudio && focus.waterJumpDelay > 0) {
            justUpdatedAudio = true;
            audio.time = Custom.LerpMap(focus.airInLungs, 0.85f, 0f, 0f, drown.length);
        }
        if (focus.waterJumpDelay <= 0) {
            justUpdatedAudio = false;
        }
    }

    private void SubTrack_Update(On.Music.MusicPiece.SubTrack.orig_Update orig, Music.MusicPiece.SubTrack self)
    {
        orig(self);

        if (self.source != null && audio != null && audio.isPlaying && audio.clip == drown) {
            self.source.volume *= 0.5f;
        }
    }
}
