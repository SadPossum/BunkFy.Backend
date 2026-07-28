namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.Cqrs;

public sealed record ResumeAdapterIngressGloballyCommand(
    long ExpectedVersion,
    string ReasonCode,
    string Actor)
    : ITransactionalCommand<AdapterIngressGlobalControlDto>;
