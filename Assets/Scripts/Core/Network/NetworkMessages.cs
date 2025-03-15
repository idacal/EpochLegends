using Mirror;

namespace EpochLegends.Core.Network
{
    // Network message definitions
    public struct GameStateRequestMessage : NetworkMessage
    {
        // Empty message - just requesting current state
    }
    
    public struct GameStateResponseMessage : NetworkMessage
    {
        public int connectedPlayerCount;
        public EpochLegends.GameState currentGameState;
    }
}