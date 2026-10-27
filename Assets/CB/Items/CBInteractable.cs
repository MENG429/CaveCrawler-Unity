namespace CB
{
    /// <summary>Anything the player can activate with the Interact key.</summary>
    public interface ICBInteractable
    {
        float InteractRadius { get; }
        string Prompt { get; }
        bool IsAvailable { get; }
        void Interact(CBPlayerController player);
    }
}
