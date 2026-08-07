namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Models;
using BunkFy.Modules.Staff.Domain.ValueObjects;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class StaffProfileUpdateCoordinator(
    IStaffMemberRepository members,
    IStaffProfileUpdateOperationRepository operations,
    ISystemClock clock,
    IIdGenerator ids)
{
    public async Task<Result<StaffProfileMutationReceiptDto>> ExecuteAsync(
        StaffMember member,
        Guid operationId,
        long expectedVersion,
        StaffProfileUpdateValues values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(values);
        string fingerprint = StaffProfileUpdateFingerprint.Compute(
            member.Id,
            expectedVersion,
            values.Profile);
        StaffProfileUpdateOperationRecord? existing =
            await operations.GetAsync(
                member.Id,
                operationId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Matches(
                member.Id,
                expectedVersion,
                fingerprint)
                ? Result.Success(existing.ToReceipt())
                : Result.Failure<StaffProfileMutationReceiptDto>(
                    StaffApplicationErrors.ProfileUpdateOperationConflict);
        }

        Result uniqueness = await StaffMemberUniqueness.EnsureAsync(
            members,
            values.Profile.EmployeeNumber,
            member.AuthSubjectId,
            member.Id,
            cancellationToken).ConfigureAwait(false);
        if (uniqueness.IsFailure)
        {
            return Result.Failure<StaffProfileMutationReceiptDto>(
                uniqueness.Error);
        }

        Result<StaffProfile> profile = values.ForAuthSubject(
            member.AuthSubjectId);
        if (profile.IsFailure)
        {
            return Result.Failure<StaffProfileMutationReceiptDto>(
                profile.Error);
        }

        DateTimeOffset nowUtc = StaffMutationTime.Normalize(clock.UtcNow);
        Result<StaffProfileUpdateOutcome> updated =
            member.UpdateProfileWithOutcome(
                profile.Value,
                expectedVersion,
                values.Actor,
                ids.NewId(),
                nowUtc);
        if (updated.IsFailure)
        {
            return Result.Failure<StaffProfileMutationReceiptDto>(
                updated.Error);
        }

        StaffProfileMutationReceiptDto receipt = new(
            member.Id,
            StaffMappings.MapStatus(member.Status),
            updated.Value.CurrentVersion,
            nowUtc);
        await operations.AddAsync(
            new(
                operationId,
                member.ScopeId,
                member.Id,
                expectedVersion,
                fingerprint,
                receipt.Status,
                receipt.Version,
                receipt.CompletedAtUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
