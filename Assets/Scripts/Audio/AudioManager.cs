using UnityEngine;
using System.Collections;

// NEW: a single always-available SFX player. Other scripts call AudioManager.Instance.PlayXSFX()
// from anywhere without needing a scene reference wired in the Inspector.
//
// Each array here matches one of your actual sound families (slide/place/shove/shuffle/fan) -
// NOT a specific game action. That's on purpose: which family plays for which action (hover,
// board placement, drawing, etc.) is a decision you make when you wire up the gameplay code later,
// and you can freely change your mind then without ever touching this file again.
//
// Drag as many clips as you want into each array. On every call, ONE random clip from that array
// is picked and played with a small random pitch shift, so repeated actions don't sound identical.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Your card-slide clips (8 variants)")]
    public AudioClip[] _SlideClips;

    [Header("Your card-place clips (4 variants)")]
    public AudioClip[] _PlaceClips;

    [Header("Your card-shove clips (4 variants)")]
    public AudioClip[] _ShoveClips;

    [Header("Your card-shuffle clip(s)")]
    public AudioClip[] _ShuffleClips;

    [Header("Your card-fan clips (2 variants)")]
    public AudioClip[] _FanClips;

    [Header("NEW: UI sounds - menu/settings button clicks (bong_001)")]
    public AudioClip[] _ClickClips;

    [Header("NEW: UI sounds - confirming a rematch (confirmation_002)")]
    public AudioClip[] _ConfirmClips;

    [Header("NEW: UI sounds - declining a rematch (error_005)")]
    public AudioClip[] _DeclineClips;

    [Header("NEW: UI sounds - flipping a toggle (switch_001)")]
    public AudioClip[] _ToggleClips;

    [Header("NEW: red missile hitting its target (played the instant it arrives/vanishes)")]
    public AudioClip[] _RedMissileHitClips;

    [Header("NEW: white missile hitting its target (played the instant it arrives/vanishes)")]
    public AudioClip[] _WhiteMissileHitClips;

    [Header("NEW: a Submarine being revealed for the first time")]
    public AudioClip[] _SubmarineDiscoveredClips;

    [Header("How much the pitch randomly shifts per play, e.g. 0.05 = +/-5%")]
    [Range(0f, 0.5f)]
    public float _PitchVariance = 0.05f;

    void Awake()
    {
        // Simple singleton - if one already exists (e.g. you accidentally have two AudioManagers
        // in the scene, or one survived from a previous scene load), destroy this duplicate.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // NEW: named after the SOUND, not the action - call whichever one fits the action you're
    // wiring up. E.g. if you decide hover should use the slide sound, call PlaySlideSFX() from
    // your hand-card hover code. Changed your mind later? Just call a different one - no need
    // to touch this script.
    public void PlaySlideSFX()
    {
        PlayRandomClip(_SlideClips);
    }

    public void PlayPlaceSFX()
    {
        PlayRandomClip(_PlaceClips);
    }

    public void PlayShoveSFX()
    {
        PlayRandomClip(_ShoveClips);
    }

    public void PlayShuffleSFX()
    {
        PlayRandomClip(_ShuffleClips);
    }

    public void PlayShuffleSFX(float maxDuration) // NEW: cuts the clip short (with a quick fade, not an abrupt stop) so it doesn't outlast a fast visual shuffle
    {
        PlayRandomClip(_ShuffleClips, maxDuration);
    }

    public void PlayFanSFX()
    {
        PlayRandomClip(_FanClips);
    }

    // NEW: UI feedback sounds - same random-pick-from-array/pitch-variance machinery as the
    // gameplay sounds above, just fed a single clip each for now. Drag in more variants later
    // if you want, same as any other array here.
    public void PlayClickSFX()
    {
        PlayRandomClip(_ClickClips);
    }

    public void PlayConfirmSFX()
    {
        PlayRandomClip(_ConfirmClips);
    }

    public void PlayDeclineSFX()
    {
        PlayRandomClip(_DeclineClips);
    }

    public void PlayToggleSFX()
    {
        PlayRandomClip(_ToggleClips);
    }

    // NEW: missile + Submarine-discovery SFX - same random-pick/pitch-variance machinery as
    // everything else here, just fed whichever clips you drag into each array above. Drop in one
    // clip or several variants for any of these, exactly like the arrays above.
    public void PlayRedMissileHitSFX()
    {
        PlayRandomClip(_RedMissileHitClips);
    }

    public void PlayWhiteMissileHitSFX()
    {
        PlayRandomClip(_WhiteMissileHitClips);
    }

    public void PlaySubmarineDiscoveredSFX()
    {
        PlayRandomClip(_SubmarineDiscoveredClips);
    }

    private void PlayRandomClip(AudioClip[] clips, float maxDuration = -1f) // CHANGED: optional maxDuration cuts the clip short instead of always playing it in full
    {
        if (clips == null || clips.Length == 0)
        {
            return; // nothing assigned yet for this sound family - safe to skip
        }

        if (PlayerPrefs.GetInt(SoundSettings.MutedKey, 0) == 1)
        {
            return; // muted - respect the Sound tab's toggle
        }

        float volume = PlayerPrefs.GetFloat(SoundSettings.MasterVolumeKey, 1f) * PlayerPrefs.GetFloat(SoundSettings.SFXVolumeKey, 1f);
        if (volume <= 0f)
        {
            return;
        }

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        float pitch = Random.Range(1f - _PitchVariance, 1f + _PitchVariance);

        // NEW: each play gets its own temporary AudioSource instead of sharing one on this
        // GameObject. If two SFX overlap (e.g. quick double-hover), sharing a single AudioSource
        // would mean the second Play's pitch overwrites the first one still playing. A short-lived
        // temp AudioSource per play keeps every play's pitch fully independent.
        GameObject tempGO = new GameObject("SFX_" + clip.name);
        AudioSource source = tempGO.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();

        float naturalDuration = clip.length / pitch;

        if (maxDuration > 0f && maxDuration < naturalDuration) // NEW: only cut it short if the caller actually asked for less than the clip's own length
        {
            StartCoroutine(FadeOutAndDestroy(source, tempGO, maxDuration));
        }
        else
        {
            Destroy(tempGO, naturalDuration);
        }
    }

    private IEnumerator FadeOutAndDestroy(AudioSource source, GameObject go, float playDuration) // NEW: cuts a clip short with a quick fade instead of an abrupt stop, so it doesn't pop
    {
        float fadeTime = Mathf.Min(0.08f, playDuration * 0.5f);
        float waitTime = Mathf.Max(0f, playDuration - fadeTime);

        yield return new WaitForSeconds(waitTime);

        float startVolume = source != null ? source.volume : 0f;
        float t = 0f;
        while (t < fadeTime)
        {
            if (source == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, t / fadeTime);
            yield return null;
        }

        Destroy(go);
    }
}
