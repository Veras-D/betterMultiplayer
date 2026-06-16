namespace ItemSyncMod
{
    // Mirrors the original fireb0rn/ItemSync API surface that
    // HKMP addons (DeathSync, PlayerTrail, etc.) hook into. The
    // original ItemSync.dll doesn't work on Unity 6, so we
    // reimplement the same static API in our mod: addons compiled
    // against the original can be repointed at this namespace.
    //
    // Usage from an addon:
    //   ItemSyncMod.ItemSyncMod.Connection.OnDataReceived += handler;
    //   ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(label, data);
    //   string me = ItemSyncMod.ItemSyncMod.ISSettings.UserName;
    public static class ItemSyncMod
    {
        public static ISSettings ISSettings { get; } = new ISSettings();
        public static Connection Connection { get; } = new Connection();

        // Default labels our own mod uses for its built-in sync
        // (PlayerData bools/ints, persistent items, lists). Addons
        // are free to use their own labels — the field is public
        // so addon code can reference these constants without
        // hardcoding the strings.
        public static class Labels
        {
            public const string ItemSync    = "itemsync";
            public const string PersistBool = "persistbool";
            public const string PersistInt  = "persistint";
            public const string ListAdd     = "listadd";
        }
    }
}
