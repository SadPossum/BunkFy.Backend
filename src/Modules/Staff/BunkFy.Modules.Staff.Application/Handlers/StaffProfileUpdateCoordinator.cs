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
    IStaffMemberMutationOperationRepository operations,
    ISystemClock clock,
    IIdGenerator ids)
{
    public Task<Result<StaffMemberMutationReceiptDto>> ExecuteAsync(
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
        return this.ExecuteCoreAsync(
            member,
            operationId,
            expectedVersion,
            values,
            fingerprint,
            cancellationToken);
    }

    public Task<Result<StaffMemberMutationReceiptDto>> ExecuteSelfServiceAsync(
        StaffMember member,
        Guid operationId,
        long expectedVersion,
        StaffProfileUpdateValues values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(values);
        Result<StaffProfile> profile = StaffProfile.Create(
            values.Profile.DisplayName,
            values.Profile.LegalName,
            values.Profile.WorkEmail,
            values.Profile.WorkPhone,
            member.EmployeeNumber,
            values.Profile.JobTitle,
            values.Profile.Department,
            authSubjectId: null);
        if (profile.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<StaffMemberMutationReceiptDto>(profile.Error));
        }

        string fingerprint = StaffSelfProfileUpdateFingerprint.Compute(
            member.Id,
            expectedVersion,
            values.Profile);
        return this.ExecuteCoreAsync(
            member,
            operationId,
            expectedVersion,
            new StaffProfileUpdateValues(profile.Value, values.Actor),
            fingerprint,
            cancellationToken);
    }

    private async Task<Result<StaffMemberMutationReceiptDto>> ExecuteCoreAsync(
        StaffMember member,
        Guid operationId,
        long expectedVersion,
        StaffProfileUpdateValues values,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        StaffMemberMutationOperationRecord? existing =
            await operations.GetAsync(
                member.Id,
                operationId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Matches(
                StaffMemberMutationKind.ProfileUpdate,
                member.Id,
                expectedVersion,
                fingerprint)
                ? Result.Success(existing.ToReceipt())
                : Result.Failure<StaffMemberMutationReceiptDto>(
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
            return Result.Failure<StaffMemberMutationReceiptDto>(
                uniqueness.Error);
        }

        Result<StaffProfile> profile = values.ForAuthSubject(
            member.AuthSubjectId);
        if (profile.IsFailure)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
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
            return Result.Failure<StaffMemberMutationReceiptDto>(
                updated.Error);
        }

        StaffMemberMutationReceiptDto receipt = new(
            member.Id,
            StaffMappings.MapStatus(member.Status),
            updated.Value.CurrentVersion,
            nowUtc);
        await operations.AddAsync(
            new(
                operationId,
                member.ScopeId,
                member.Id,
                StaffMemberMutationKind.ProfileUpdate,
                expectedVersion,
                fingerprint,
                receipt.Status,
                receipt.Version,
                receipt.CompletedAtUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
