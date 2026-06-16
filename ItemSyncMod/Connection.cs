using System;
using BetterMultiplayer;

namespace ItemSyncMod
{
    // Mirrors the original fireb0rn/ItemSync Connection class:
    // a label-based broadcast/receive API that any addon can hook
    // into. SendDataToAll serializes (label, data, from) into our
    // own ISC packet and sends it over TCP. Incoming ISC packets
    // are dispatched to OnDataReceived by PacketHandler.
    //
    // The original Connection is a static class. We mirror that
    // (instance class exposed via ItemSyncMod.Connection) so addon
    // code that does `ItemSync.Connection.SendDataToAll(...)`
    // (the DeathSync pattern) works unchanged.
    public class Connection
    {
        // Fired when a labeled data packet arrives from the peer.
        // Addons subscribe with += and filter by e.Label.
        public event EventHandler<DataReceivedEvent> OnDataReceived;

        // Broadcasts (label, data) to the connected peer. data is
        // an arbitrary string — addons are free to encode whatever
        // they want (JSON, pipe-delimited fields, base64, etc.).
        // Our own ItemSync uses pipe-delimited fields. The "from"
        // field is filled in automatically from ISSettings.UserName
        // before the packet goes out.
        public void SendDataToAll(string label, string data)
        {
            if (string.IsNullOrEmpty(label)) return;
            if (!NetworkManager.IsClientConnected) return;

            // ISC|<label>|<data>
            // data may contain pipes; we keep the protocol simple
            // by treating the first 2 pipes as the label/data split.
            // Callers should escape any pipes in their data if they
            // need them — none of our built-in sync paths use pipes
            // in their data.
            NetworkManager.SendPacket(
                "ISC|" + label + "|" + (data ?? ""));
        }

        // Called by PacketHandler when an ISC packet arrives. Fires
        // OnDataReceived so any subscribed addon gets the event.
        // Returns true if any handler set e.Handled = true.
        internal bool Dispatch(string label, string data, string from)
        {
            var ev = new DataReceivedEvent
            {
                Label = label,
                Data  = data,
                From  = from,
                Handled = false,
            };
            OnDataReceived?.Invoke(this, ev);
            return ev.Handled;
        }
    }
}
