using System;

namespace ItemSyncMod
{
    // Event args delivered to handlers subscribed to
    // Connection.OnDataReceived. Mirrors the original ItemSync
    // shape: Label, Data, From, Handled. Set Handled = true to
    // stop other handlers from also processing the same packet
    // (the original mod uses this so multiple addons listening
    // for the same label don't double-process).
    public class DataReceivedEvent : EventArgs
    {
        public string Label { get; set; }
        public string Data  { get; set; }
        public string From  { get; set; }
        public bool   Handled { get; set; }
    }
}
