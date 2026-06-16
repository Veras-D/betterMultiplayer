namespace ItemSyncMod
{
    // Mirrors the original fireb0rn/ItemSync ISSettings: identity
    // (UserName) + enabled flag (IsItemSync). Addons read these to
    // know who sent a message and whether the mod is active.
    public class ISSettings
    {
        // The current player's display name. Set by the mod on
        // connect (and updated whenever the user changes their name
        // in the menu). The original mod reads this from the
        // Hollow Knight save; we just default to the OS user name.
        public string UserName { get; set; } =
            System.Environment.UserName ?? "knight";

        // Whether item sync is enabled. Addons check this before
        // hooking OnDataReceived so they don't waste cycles when
        // the user has turned item sync off.
        public bool IsItemSync { get; set; } = true;
    }
}
