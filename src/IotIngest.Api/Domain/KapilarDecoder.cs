namespace IotIngest.Api.Domain;

public static class KapilarDecoder
{
    public static (int NodeNum, byte? PinIndex) Decode(int kapilarId)
    {
        // Heartbeat check: kapilar_id % 10 == 0
        if (kapilarId % 10 == 0)
        {
            var nodeNum = kapilarId / 10;
            return (nodeNum, null); // Heartbeat
        }

        // Normal pin input
        var px = (kapilarId - 1) % 10;
        var node = (kapilarId - 1 - px) / 10;
        
        // Validate pin index is within expected range (0-7)
        if (px < 0 || px > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(kapilarId), 
                $"Invalid kapilar_id {kapilarId}: pin index {px} out of range 0-7");
        }

        return (node, (byte)px);
    }

    public static int Encode(int nodeNum, byte? pinIndex)
    {
        if (pinIndex == null)
        {
            // Heartbeat: nodeNum * 10
            return nodeNum * 10;
        }

        if (pinIndex < 0 || pinIndex > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(pinIndex), 
                "Pin index must be between 0 and 7");
        }

        // Normal: 1 + pinIndex + 10 * nodeNum
        return 1 + pinIndex.Value + 10 * nodeNum;
    }
}