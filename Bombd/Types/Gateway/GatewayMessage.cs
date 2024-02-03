namespace Bombd.Types.Gateway
{
    public class GatewayMessage
    {
        public string Type { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
