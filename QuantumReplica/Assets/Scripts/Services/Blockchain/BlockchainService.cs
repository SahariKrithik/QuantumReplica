using UnityEngine;

public static class BlockchainService
{
    public static int SimulateVRFResult()
    {
        return Random.Range(0, 100);
    }

    // Future hooks
    public static void PlaceBet(int amount, int number, bool isOver) { }
    public static string GetResult() => "Pending...";
}
