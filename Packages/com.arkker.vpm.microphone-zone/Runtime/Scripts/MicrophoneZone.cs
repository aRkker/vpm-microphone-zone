
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



    private bool localPlayerInZone = false;

    private bool songPlaying = false;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        player.SetVoiceDistanceFar(voiceRangeFar);
        player.SetVoiceDistanceNear(voiceRangeNear);
        player.SetVoiceGain(0);
        player.SetVoiceLowpass(useLowPassFilter);
        player.SetVoiceGain(voiceGain);
        player.SetVoiceVolumetricRadius(volumetricVoiceRadius);

        if (player.isLocal && TXLPlayer != null && songPlaying)
        {
            localPlayerInZone = true;
            float latency = ((Time.realtimeSinceStartup - Networking.SimulationTime(player))) + .2f;
            Debug.Log("Latency: " + latency);
            TXLPlayer._SetTargetTime(TXLPlayer.transform.Find("Video Manager").GetComponent<VideoManager>().VideoTime + latency);
            TXLPlayer.transform.Find("Video Manager").GetComponent<VideoManager>()._VideoSetTime(TXLPlayer.transform.Find("Video Manager").GetComponent<VideoManager>().VideoTime + latency);

        }

    }

    public override void OnVideoPlay()
    {
        Debug.Log("Video Playing");
        songPlaying = true;

        if (localPlayerInZone)
        {
            float latency = ((Time.realtimeSinceStartup - Networking.SimulationTime(Networking.LocalPlayer))) + .2f;
            Debug.Log("Latency: " + latency);
            TXLPlayer._SetTargetTime(TXLPlayer.transform.Find("Video Manager").GetComponent<VideoManager>().VideoTime + latency);
            TXLPlayer.transform.Find("Video Manager").GetComponent<VideoManager>()._VideoSetTime(TXLPlayer.transform.Find("Video Manager").GetComponent<VideoManager>().VideoTime + latency);
        }
    }

    public override void OnVideoEnd()
    {
        Debug.Log("Video Ended");
        songPlaying = false;
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
        }
    }
}
