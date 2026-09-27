#if UNITY_EDITOR
using ParrelSync;
#endif

public static class ClonePrefs // NEW: makes a ParrelSync clone Editor instance use its own separate PlayerPrefs entries instead of sharing the original project's Windows Registry key - every PlayerPrefs call in the project should go through Key(...) instead of using its raw key string directly, so that testing with two Editor windows (one hosting, one joining) actually behaves like two independent machines instead of silently sharing every saved setting
{
    public static string Key(string baseKey) // wrap any PlayerPrefs key with this before reading/writing it
    {
#if UNITY_EDITOR
        if (ClonesManager.IsClone())
        {
            return baseKey + "_Clone"; // only affects the ParrelSync clone Editor instance - the original project and any real build are completely unaffected
        }
#endif
        return baseKey;
    }
}
