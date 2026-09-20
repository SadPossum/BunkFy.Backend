namespace BunkFy.Modules.Stations.Contracts;

/// <summary>Reserved owner contract for the later authenticated transport adapter; P1 does not register an implementation.</summary>
public interface IStationSessionReader
{
    Task<StationSessionSnapshot?> ReadAsync(string opaqueCredential, CancellationToken cancellationToken = default);
}

