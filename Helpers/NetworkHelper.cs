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
            // Both Host and NotNetworked are treated as authoritative, as this has been
            // introduced to distinguish singleplayer (no active network session). Either
            // way, the player's own local simulation is the source of truth. NotInGame
            // correctly fails the check for obvious reasons, and Client fails since the
            // client making these changes can fail to sync or desync for other players.
            return Session.NetworkedPlayState == NetworkedPlayState.Host
                || Session.NetworkedPlayState == NetworkedPlayState.NotNetworked;
        }
    }
}
