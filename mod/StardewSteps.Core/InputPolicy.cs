namespace StardewSteps.Core;

public static class InputPolicy
{
    /// <summary>
    /// Movement input is only hidden from the game while the farmer is walking around freely.
    /// Menus, dialogue and arcade minigames keep normal keyboard/controller navigation.
    /// </summary>
    public static bool SuppressMoveInput(bool playerFree, bool inMinigame) => playerFree && !inMinigame;
}
