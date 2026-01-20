namespace Octoporty.Shared.Options;

public class GatewayOptions
{
    public string ApiKey { get; set; } = "";
    public string CaddyAdminUrl { get; set; } = "http://localhost:2019";
    public int ListenPort { get; set; } = 5000;
    public bool DebugJson { get; set; }
}
