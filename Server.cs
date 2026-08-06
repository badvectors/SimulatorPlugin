namespace Simulator.Plugin
{
    /// <summary>
    /// One entry from Servers.json, which is embedded in this assembly and is the only place the server
    /// list lives - both the Settings &gt; Simulator dropdown and the URL selections are sent to come from
    /// it, so adding a sweatbox is a one line change to that file.
    /// </summary>
    public class Server
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}
