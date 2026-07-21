namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Contracts.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RunAdapterTaskHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Task_bridge_records_adapter_completion_against_gma_task_identity()
    {
        FakeTaskCommandDispatcher dispatcher = new();
        FakeRunner runner = new();
        ITaskHandler<RunAdapterTaskPayload> handler = CreateHandler(dispatcher, runner);
        TaskExecutionContext context = CreateExecutionContext();

        await handler.HandleAsync(
            new RunAdapterTaskPayload(dispatcher.Start.ConnectionId),
            context,
            CancellationToken.None);

        CompleteAdapterRunCommand completion = Assert.Single(dispatcher.Completions);
        Assert.Equal(context.RunId, completion.TaskRunId);
        Assert.Equal(context.Attempt, completion.TaskAttempt);
        Assert.Equal(2, completion.ObservedCount);
        Assert.Equal("cursor-2", completion.AcceptedCheckpoint);
        Assert.NotNull(runner.Assignment);
        Assert.True(runner.MaterialWasAvailable);
        Assert.True(runner.MaterialWasDisposedAfterRun);
    }

    [Fact]
    public async Task Missing_runner_records_failed_source_execution_and_fails_task()
    {
        FakeTaskCommandDispatcher dispatcher = new();
        ITaskHandler<RunAdapterTaskPayload> handler = CreateHandler(dispatcher, runner: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new RunAdapterTaskPayload(dispatcher.Start.ConnectionId),
            CreateExecutionContext(),
            CancellationToken.None));

        CompleteAdapterRunCommand failure = Assert.Single(dispatcher.Completions);
        Assert.Equal(AdapterRunOutcome.Failed, failure.Outcome);
        Assert.Equal(IngestionApplicationErrors.AdapterRunnerNotRegistered.Code.ToLowerInvariant(), failure.ErrorCode);
    }

    [Fact]
    public async Task Material_resolution_failure_records_one_failed_source_execution_without_running_adapter()
    {
        FakeTaskCommandDispatcher dispatcher = new();
        FakeRunner runner = new();
        ITaskHandler<RunAdapterTaskPayload> handler = CreateHandler(
            dispatcher,
            runner,
            new FailingMaterialResolver());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new RunAdapterTaskPayload(dispatcher.Start.ConnectionId),
            CreateExecutionContext(),
            CancellationToken.None));

        CompleteAdapterRunCommand failure = Assert.Single(dispatcher.Completions);
        Assert.Equal(AdapterRunOutcome.Failed, failure.Outcome);
        Assert.Equal(
            IngestionApplicationErrors.AdapterConfigurationMaterialNotFound.Code.ToLowerInvariant(),
            failure.ErrorCode);
        Assert.Null(runner.Assignment);
    }

    [Fact]
    public async Task Oversized_framework_error_code_is_replaced_before_source_run_persistence()
    {
        FakeTaskCommandDispatcher dispatcher = new();
        Error oversized = new($"Provider.{new string('x', 200)}", "Provider resolution failed.");
        ITaskHandler<RunAdapterTaskPayload> handler = CreateHandler(
            dispatcher,
            new FakeRunner(),
            new FailingMaterialResolver(oversized));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(
                new RunAdapterTaskPayload(dispatcher.Start.ConnectionId),
                CreateExecutionContext(),
                CancellationToken.None));

        CompleteAdapterRunCommand failure = Assert.Single(dispatcher.Completions);
        Assert.Equal("ingestion.adapter-execution-failed", failure.ErrorCode);
        Assert.Equal(failure.ErrorCode, exception.Message);
    }

    [Fact]
    public async Task Descriptor_drift_records_failure_before_material_or_runner_execution()
    {
        FakeTaskCommandDispatcher dispatcher = new();
        FakeRunner runner = new();
        AdapterDescriptor drifted = new(
            "fake.http", protocolVersion: 2, configurationSchemaVersion: 1,
            [AdapterExecutionMode.Polling]);
        ITaskHandler<RunAdapterTaskPayload> handler = CreateHandler(
            dispatcher, runner, descriptor: drifted);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new RunAdapterTaskPayload(dispatcher.Start.ConnectionId),
            CreateExecutionContext(),
            CancellationToken.None));

        CompleteAdapterRunCommand failure = Assert.Single(dispatcher.Completions);
        Assert.Equal(IngestionApplicationErrors.AdapterDescriptorMismatch.Code.ToLowerInvariant(), failure.ErrorCode);
        Assert.Null(runner.Assignment);
    }

    private static ITaskHandler<RunAdapterTaskPayload> CreateHandler(
        FakeTaskCommandDispatcher dispatcher,
        IAdapterRunner? runner,
        IAdapterConfigurationMaterialResolver? materialResolver = null,
        AdapterDescriptor? descriptor = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<ITaskCommandDispatcher>(dispatcher);
        services.AddSingleton<IAdapterDescriptorRegistry>(new FakeDescriptorRegistry(
            descriptor ?? runner?.Descriptor ?? new AdapterDescriptor(
                "fake.http", 1, 1, [AdapterExecutionMode.Polling])));
        services.AddSingleton<IAdapterRunnerRegistry>(new FakeRunnerRegistry(runner));
        services.AddSingleton(materialResolver ?? new FakeMaterialResolver());
        services.AddSingleton<IAdapterObservationSinkFactory>(new FakeSinkFactory());
        services.AddSingleton<ISystemClock>(new TestClock());
        services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        services.AddIngestionTaskHandlers();
        ServiceProvider provider = services.BuildServiceProvider();
        TaskHandlerRegistration registration = provider.GetRequiredService<ITaskHandlerRegistry>()
            .Find(IngestionModuleMetadata.Name, RunAdapterTaskPayload.TaskName)!;
        return (ITaskHandler<RunAdapterTaskPayload>)provider.GetRequiredService(registration.HandlerType);
    }

    private static TaskExecutionContext CreateExecutionContext() => new(
        Guid.NewGuid(),
        IngestionModuleMetadata.Name,
        RunAdapterTaskPayload.TaskName,
        IngestionModuleMetadata.AdapterWorkerGroup,
        "worker-1",
        "node-1",
        attempt: 2,
        scopeId: "tenant-a",
        leaseExtension: TimeSpan.FromMinutes(5));

    private sealed class FakeTaskCommandDispatcher : ITaskCommandDispatcher
    {
        public AdapterRunStart Start { get; } = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "tenant-a",
            "fake.http",
            AdapterExecutionMode.Polling,
            "cursor-1",
            "configuration://fake-main",
            "secret://fake-main");

        public List<CompleteAdapterRunCommand> Completions { get; } = [];

        public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
            TaskExecutionContext context,
            TCommand command,
            CancellationToken cancellationToken)
            where TCommand : ICommand<TResponse>
        {
            object result = command switch
            {
                StartAdapterRunCommand => Result.Success(this.Start),
                CompleteAdapterRunCommand completion => this.Complete(completion),
                _ => throw new InvalidOperationException($"Unexpected command {command.GetType().Name}.")
            };
            return Task.FromResult((Result<TResponse>)result);
        }

        private Result<Unit> Complete(CompleteAdapterRunCommand command)
        {
            this.Completions.Add(command);
            return Result.Success(Unit.Value);
        }
    }

    private sealed class FakeRunnerRegistry(IAdapterRunner? runner) : IAdapterRunnerRegistry
    {
        public bool TryGet(string adapterType, out IAdapterRunner? selected)
        {
            selected = runner;
            return selected is not null;
        }
    }

    private sealed class FakeDescriptorRegistry(AdapterDescriptor descriptor) : IAdapterDescriptorRegistry
    {
        public IReadOnlyCollection<AdapterDescriptor> GetAll() => [descriptor];

        public bool TryGet(string adapterType, out AdapterDescriptor? selected)
        {
            selected = adapterType == descriptor.AdapterType ? descriptor : null;
            return selected is not null;
        }
    }

    private sealed class FakeRunner : IAdapterRunner
    {
        public AdapterDescriptor Descriptor { get; } = new(
            "fake.http",
            protocolVersion: 1,
            configurationSchemaVersion: 1,
            [AdapterExecutionMode.Polling]);

        public AdapterRunAssignment? Assignment { get; private set; }
        public bool MaterialWasAvailable { get; private set; }
        public bool MaterialWasDisposedAfterRun
        {
            get
            {
                Assert.NotNull(this.Material);
                Assert.Throws<ObjectDisposedException>(() => _ = this.Material.Configuration);
                return true;
            }
        }

        private AdapterConfigurationMaterial? Material { get; set; }

        public Task<AdapterRunCompletion> RunAsync(
            AdapterRunAssignment assignment,
            AdapterConfigurationMaterial material,
            IAdapterObservationSink sink,
            CancellationToken cancellationToken)
        {
            this.Assignment = assignment;
            this.Material = material;
            this.MaterialWasAvailable = material.Configuration.Length > 0 && material.Secret.Length > 0;
            return Task.FromResult(new AdapterRunCompletion(
                assignment.RunId,
                assignment.LeaseId,
                AdapterRunOutcome.Succeeded,
                observedCount: 2,
                acceptedCount: 2,
                rejectedCount: 0,
                "cursor-2",
                errorCode: null,
                errorMessage: null));
        }
    }

    private sealed class FakeMaterialResolver : IAdapterConfigurationMaterialResolver
    {
        public Task<Result<AdapterConfigurationMaterial>> ResolveAsync(
            AdapterConfigurationMaterialRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(new AdapterConfigurationMaterial(
                request.ExpectedSchemaVersion,
                "application/json",
                "{}"u8,
                "application/json",
                "{}"u8)));
    }

    private sealed class FailingMaterialResolver(Error? error = null) : IAdapterConfigurationMaterialResolver
    {
        public Task<Result<AdapterConfigurationMaterial>> ResolveAsync(
            AdapterConfigurationMaterialRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<AdapterConfigurationMaterial>(
                error ?? IngestionApplicationErrors.AdapterConfigurationMaterialNotFound));
    }

    private sealed class FakeSinkFactory : IAdapterObservationSinkFactory
    {
        public IAdapterObservationSink Create(AdapterRunAssignment assignment) => new NoOpSink();

        private sealed class NoOpSink : IAdapterObservationSink
        {
            public Task<AdapterObservationAcknowledgement> SubmitAsync(
                AdapterObservationSubmission submission,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
