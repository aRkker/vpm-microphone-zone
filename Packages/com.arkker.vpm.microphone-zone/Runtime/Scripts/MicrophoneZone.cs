
using Texel;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MicrophoneZone : UdonSharpBehaviour
{
    [Header("Microphone Zone Settings")]

    [Tooltip("The range at which the voice will be silent")]
    [SerializeField] private float voiceRangeFar = 25f;
    [Tooltip("The range at which the voice will be at full volume")]
    [SerializeField] private float voiceRangeNear = 17f;
    [SerializeField] private bool useLowPassFilter = false;

    [Tooltip("The voice gain value for the player when on stage. Default = 0")]
    [SerializeField] private float voiceGain = 0;

    [Tooltip("The volumetric voice radius. Default 0")]
    [SerializeField] private float volumetricVoiceRadius = 0;

    [Header("The player that will be synced with this zone")]
    [SerializeField] private SyncPlayer TXLPlayer;

    // Local player in zone

    private bool localPlayerInZone = false;

    private bool songPlaying = false;
    private bool TXLPlayerPlaying = false;
    private bool localSingerOffsetActive = false;
    private float localSingerOffset = 0f;
    private float localSingerOffsetVideoTime = 0f;
    private float localSingerOffsetRealtime = 0f;

    private void Start()
    {
        if (TXLPlayer != null)
        {
            TXLPlayer._Register(TXLVideoPlayer.EVENT_VIDEO_STATE_UPDATE, this, nameof(_OnTXLVideoStateUpdate));
            _OnTXLVideoStateUpdate();
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        player.SetVoiceDistanceFar(voiceRangeFar);
        player.SetVoiceDistanceNear(voiceRangeNear);
        player.SetVoiceGain(0);
        player.SetVoiceLowpass(useLowPassFilter);
        player.SetVoiceGain(voiceGain);
        player.SetVoiceVolumetricRadius(volumetricVoiceRadius);

        if (player.isLocal && TXLPlayer != null)
        {
            localPlayerInZone = true;
            if (songPlaying)
                ApplyLocalSingerOffset(player);
        }

    }

    public override void OnVideoPlay()
    {
        Debug.Log("Video Playing");
        songPlaying = true;

        if (localPlayerInZone)
        {
            SendCustomEventDelayedFrames(nameof(_ApplyLocalSingerOffset), 1);
        }
    }

    public override void OnVideoEnd()
    {
        Debug.Log("Video Ended");
        songPlaying = false;
        TXLPlayerPlaying = false;
        ResetLocalSingerOffsetState();
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        player.SetVoiceGain(15); // 15 is default according to VRC documentation
        player.SetVoiceDistanceFar(25); // 25 is default according to VRC documentation
        player.SetVoiceDistanceNear(0);
        player.SetVoiceVolumetricRadius(0);


        if (!useLowPassFilter)
        {
            player.SetVoiceLowpass(true);
        }

        if (player.isLocal && TXLPlayer != null)
        {
            localPlayerInZone = false;
            ClearLocalSingerOffset();
        }
    }

    public void _OnTXLVideoStateUpdate()
    {
        if (TXLPlayer == null)
        {
            songPlaying = false;
            TXLPlayerPlaying = false;
            ResetLocalSingerOffsetState();
            return;
        }

        bool isPlaying = TXLPlayer.playerState == TXLVideoPlayer.VIDEO_STATE_PLAYING;
        bool startedPlaying = isPlaying && !TXLPlayerPlaying;

        songPlaying = isPlaying;
        TXLPlayerPlaying = isPlaying;

        if (!isPlaying)
            ResetLocalSingerOffsetState();

        if (startedPlaying && localPlayerInZone)
            SendCustomEventDelayedFrames(nameof(_ApplyLocalSingerOffset), 1);
    }

    public void _ApplyLocalSingerOffset()
    {
        ApplyLocalSingerOffset(Networking.LocalPlayer);
    }

    private void ApplyLocalSingerOffset(VRCPlayerApi player)
    {
        if (TXLPlayer == null || player == null || !player.isLocal || !songPlaying)
            return;

        VideoManager videoManager = TXLPlayer.VideoManager;
        if (videoManager == null || !videoManager.VideoIsSeekable)
            return;

        float now = Time.realtimeSinceStartup;
        float latency = (now - Networking.SimulationTime(player)) + .2f;
        float duration = videoManager.VideoDuration;
        float baseTime = videoManager.VideoTime;

        if (localSingerOffsetActive)
        {
            if (LocalSingerOffsetStillApplied(videoManager))
                baseTime = Mathf.Max(0f, baseTime - localSingerOffset);
            else
                ResetLocalSingerOffsetState();
        }

        float targetTime = Mathf.Clamp(baseTime + latency, 0f, duration - 1f);

        Debug.Log("Latency: " + latency);
        videoManager._VideoSetTime(targetTime);

        localSingerOffset = targetTime - baseTime;
        localSingerOffsetVideoTime = targetTime;
        localSingerOffsetRealtime = now;
        localSingerOffsetActive = localSingerOffset > 0.001f;
    }

    private void ClearLocalSingerOffset()
    {
        if (!localSingerOffsetActive || TXLPlayer == null)
        {
            ResetLocalSingerOffsetState();
            return;
        }

        VideoManager videoManager = TXLPlayer.VideoManager;
        if (videoManager != null && videoManager.VideoIsSeekable && LocalSingerOffsetStillApplied(videoManager))
        {
            float duration = videoManager.VideoDuration;
            float targetTime = Mathf.Clamp(videoManager.VideoTime - localSingerOffset, 0f, duration - 1f);
            videoManager._VideoSetTime(targetTime);
        }

        ResetLocalSingerOffsetState();
    }

    private bool LocalSingerOffsetStillApplied(VideoManager videoManager)
    {
        float expectedTime = localSingerOffsetVideoTime + (Time.realtimeSinceStartup - localSingerOffsetRealtime);
        float tolerance = Mathf.Max(0.35f, localSingerOffset * 0.5f);
        return Mathf.Abs(videoManager.VideoTime - expectedTime) <= tolerance;
    }

    private void ResetLocalSingerOffsetState()
    {
        localSingerOffsetActive = false;
        localSingerOffset = 0f;
        localSingerOffsetVideoTime = 0f;
        localSingerOffsetRealtime = 0f;
    }
}
