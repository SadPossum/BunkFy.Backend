namespace BunkFy.Modules.DataRights.Domain.Entities;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;

public sealed partial class DataRightsProcessingLedgerEntry
{
    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(
            canonical,
            this.ContractVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.Id.ToString("N"));
        Append(canonical, this.ScopeId);
        Append(
            canonical,
            this.TenantSequence.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.WorkItemId.ToString("N"));
        Append(canonical, this.CaseId.ToString("N"));
        Append(
            canonical,
            this.ApprovalRevision.ToString(CultureInfo.InvariantCulture));
        Append(
            canonical,
            this.OperationRevision.ToString(CultureInfo.InvariantCulture));
        Append(
            canonical,
            ((int)this.Operation).ToString(CultureInfo.InvariantCulture));
        if (this.ContractVersion < CurrentContractVersion)
        {
            Append(canonical, this.RoutingPropertyId!.Value.ToString("N"));
        }
        else
        {
            Append(
                canonical,
                ((int)this.CaseKind).ToString(CultureInfo.InvariantCulture));
            Append(
                canonical,
                ((int)this.ScopeKind).ToString(CultureInfo.InvariantCulture));
            Append(canonical, Coordinate(this.RoutingPropertyId));
        }

        Append(canonical, this.OwnerKey);
        Append(canonical, this.RecordType);
        Append(
            canonical,
            this.RecordPseudonymKeyVersion.ToString(
                CultureInfo.InvariantCulture));
        Append(canonical, this.RecordPseudonymSha256);
        Append(canonical, this.DispositionCode);
        Append(canonical, this.ReasonCode);
        Append(
            canonical,
            this.CompletedAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture));
        Append(
            canonical,
            this.PolicyEvidenceSchemaVersion.ToString(
                CultureInfo.InvariantCulture));
        Append(canonical, this.PolicyId);
        Append(
            canonical,
            this.PolicyVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.PolicyContentSha256);
        Append(canonical, this.RetentionPolicyId);
        Append(
            canonical,
            this.RetentionPolicyVersion.ToString(
                CultureInfo.InvariantCulture));
        Append(
            canonical,
            this.OwnerReceiptContractVersion.ToString(
                CultureInfo.InvariantCulture));
        Append(canonical, this.OwnerReceiptId.ToString("N"));
        Append(canonical, this.OwnerReceiptSha256);
        if (this.ContractVersion >= GuestResultVersionContractVersion)
        {
            Append(
                canonical,
                this.ResultingRecordVersion!.Value.ToString(
                    CultureInfo.InvariantCulture));
        }

        if (this.ContractVersion >= CurrentContractVersion)
        {
            Append(
                canonical,
                this.PolicyPropertyVersion!.Value.ToString(
                    CultureInfo.InvariantCulture));
            Append(canonical, this.PolicyOperatingCountryCode!);
            Append(canonical, this.PolicyPurposeCode!);
            Append(canonical, this.PolicySurface!);
            Append(canonical, this.PolicySourceProvenance!);
            Append(canonical, this.PolicyRetentionDataClass!);
            Append(canonical, this.PolicyRetentionTrigger!);
            Append(
                canonical,
                Timestamp(this.PolicyRetentionTriggeredAtUtc));
            Append(
                canonical,
                Timestamp(this.PolicyRetentionDeadlineUtc));
            Append(canonical, Timestamp(this.PolicyEvaluatedAtUtc));
            Append(canonical, this.PolicyStateBindingsJson!);
            Append(canonical, this.PolicyStateBindingsSha256!);
            Append(
                canonical,
                this.PolicyRequiresDistinctExecutor == true ? "1" : "0");
        }

        Append(canonical, this.PreviousEntrySha256);
        Append(canonical, Coordinate(this.ReplayOfLedgerEntryId));
        Append(canonical, Coordinate(this.SupersedesLedgerEntryId));
        return Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private bool HasValidScopeContract()
    {
        if (this.ContractVersion < CurrentContractVersion)
        {
            return this.CaseKind == DataRightsCaseKind.Unknown &&
                this.ScopeKind == DataRightsCaseScopeKind.Unknown &&
                this.RoutingPropertyId is Guid propertyId &&
                propertyId != Guid.Empty;
        }

        return DataRightsExecutionScope.Create(
            this.CaseKind,
            this.ScopeKind,
            this.RoutingPropertyId).IsSuccess;
    }

    private bool HasValidPolicyContract()
    {
        if (this.ContractVersion < CurrentContractVersion)
        {
            return this.PolicyEvidenceSchemaVersion ==
                    DataRightsApprovalPolicyEvidence
                        .MinimumSupportedSchemaVersion &&
                this.PolicyPropertyVersion is null &&
                this.PolicyOperatingCountryCode is null &&
                this.PolicyPurposeCode is null &&
                this.PolicySurface is null &&
                this.PolicySourceProvenance is null &&
                this.PolicyRetentionDataClass is null &&
                this.PolicyRetentionTrigger is null &&
                this.PolicyRetentionTriggeredAtUtc is null &&
                this.PolicyRetentionDeadlineUtc is null &&
                this.PolicyEvaluatedAtUtc is null &&
                this.PolicyStateBindingsJson is null &&
                this.PolicyStateBindingsSha256 is null &&
                this.PolicyRequiresDistinctExecutor is null;
        }

        bool propertyVersionValid = this.ScopeKind switch
        {
            DataRightsCaseScopeKind.Property =>
                this.PolicyPropertyVersion > 0,
            DataRightsCaseScopeKind.Tenant =>
                this.PolicyPropertyVersion == 0,
            _ => false
        };
        return this.PolicyEvidenceSchemaVersion ==
                DataRightsApprovalPolicyEvidence.CurrentSchemaVersion &&
            propertyVersionValid &&
            this.PolicyOperatingCountryCode is
            {
                Length: DataRightsApprovalPolicyEvidence.CountryCodeLength
            } country &&
            country.All(character => character is >= 'A' and <= 'Z') &&
            HasCanonicalCode(
                this.PolicyPurposeCode,
                DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
            HasCanonicalCode(
                this.PolicySurface,
                DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
            HasCanonicalCode(
                this.PolicySourceProvenance,
                DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
            HasCanonicalCode(
                this.PolicyRetentionDataClass,
                DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
            HasCanonicalCode(
                this.PolicyRetentionTrigger,
                DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
            this.PolicyRetentionTriggeredAtUtc is DateTimeOffset triggered &&
            triggered.Offset == TimeSpan.Zero &&
            this.PolicyRetentionDeadlineUtc is DateTimeOffset deadline &&
            deadline.Offset == TimeSpan.Zero &&
            deadline > triggered &&
            this.PolicyEvaluatedAtUtc is DateTimeOffset evaluated &&
            evaluated.Offset == TimeSpan.Zero &&
            deadline <= evaluated &&
            this.PolicyStateBindingsJson is not null &&
            this.PolicyStateBindingsSha256 is not null &&
            DataRightsApprovalPolicyEvidence.HasValidStateBindings(
                this.PolicyStateBindingsJson,
                this.PolicyStateBindingsSha256) &&
            this.PolicyRequiresDistinctExecutor == true;
    }
}
