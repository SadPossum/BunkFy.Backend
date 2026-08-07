namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Converge one prepared Reservation primary-Guest Record link.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(ReservationsModuleMetadata.GuestRecordLinkWorkerGroup)]
[ScopeAware]
public sealed record ExecuteReservationGuestRecordLinkPayload(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId,
    int DispatchRevision) : ITaskPayload
{
    public const string TaskName = "execute-reservation-guest-record-link";
    public const int PayloadVersion = 1;
    public const int MaximumAttempts = 5;
}
