namespace Bombd;

public class BombdConfiguration
{
    /// <summary>
    ///     The IP this server will listen on.
    /// </summary>
    public string ListenIP { get; set; }

    /// <summary>
    ///     The external IP of this server
    /// </summary>
    public string ExternalIP { get; set; }

    /// <summary>
    ///     The address of the Web API
    /// </summary>
    public string ApiURL { get; set; }
}