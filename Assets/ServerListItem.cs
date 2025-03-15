using UnityEngine;
using UnityEngine.UI;
using Mirror.Discovery;
using System;

public class ServerListItem : MonoBehaviour
{
    [SerializeField] private Text serverNameText;
    [SerializeField] private Text playerCountText;
    [SerializeField] private Text pingText;
    [SerializeField] private Button joinButton;
    
    public event Action<ServerResponse> OnServerSelected;
    private ServerResponse serverInfo;

    private void Start()
    {
        if (joinButton)
            joinButton.onClick.AddListener(OnJoinButtonClicked);
    }

    public void Initialize(ServerResponse info)
    {
        serverInfo = info;
        
        if (serverNameText)
            serverNameText.text = info.uri.ToString(); // Usa URI como nombre por defecto
            
        if (playerCountText)
            playerCountText.text = "Players: ?/?"; // Placeholder
            
        if (pingText)
            pingText.text = "Ping: ?ms"; // Placeholder
    }
    
    private void OnJoinButtonClicked()
    {
        OnServerSelected?.Invoke(serverInfo);
    }
}