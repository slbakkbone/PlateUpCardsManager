using Kitchen;
using Platforms;

namespace KitchenCardsManager.Helpers
{
    internal class NetworkHelper
    {
        internal static bool IsHost()
        {
            // Game v1.4.4+ removed Session.CurrentGameNetworkMode / GameNetworkMode entirely.
            // Host/client state is now exposed directly as Session.NetworkedPlayState
            // (Platforms.NetworkedPlayState: NotInGame, NotNetworked, Host, Client).
            return Session.NetworkedPlayState == NetworkedPlayState.Host;
        }
    }
}
