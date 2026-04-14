using Marten;
using Marten.Services;
using System.Diagnostics;

namespace TrafficSigns.Infrastructure.Persistence.Interceptors;

public class MartenTraceInterceptor : IDocumentSessionListener
{
    public void BeforeSaveChanges(IDocumentSession session)
    {
        ApplyCorrelationId(session);
    }

    public Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken token)
    {
        ApplyCorrelationId(session); 
        return Task.CompletedTask;
    }

    private void ApplyCorrelationId(IDocumentSession session)
    {
        var traceId = Activity.Current?.TraceId.ToString()
                      ?? Activity.Current?.RootId
                      ?? Guid.NewGuid().ToString();

        session.SetHeader("correlation-id", traceId);

        foreach (var stream in session.PendingChanges.Streams())
        {
            foreach (var @event in stream.Events)
            {
                @event.SetHeader("correlation-id", traceId);
            }
        }
    }

    public void AfterCommit(IDocumentSession session, IChangeSet commit) { }

    public Task AfterCommitAsync(IDocumentSession session, IChangeSet commit, CancellationToken token) => Task.CompletedTask;

    public void DocumentLoaded(object id, object document) { }

    public void DocumentAddedForStorage(object id, object document) { }
}