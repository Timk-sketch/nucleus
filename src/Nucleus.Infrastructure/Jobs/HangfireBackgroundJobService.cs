using Hangfire;

namespace Nucleus.Infrastructure.Jobs;

public class HangfireBackgroundJobService : IBackgroundJobService
{
    public string Enqueue<T>(System.Linq.Expressions.Expression<Action<T>> methodCall)
        => BackgroundJob.Enqueue(methodCall);

    public string Schedule<T>(System.Linq.Expressions.Expression<Action<T>> methodCall, TimeSpan delay)
        => BackgroundJob.Schedule(methodCall, delay);

    public void AddOrUpdateRecurring<T>(string jobId, System.Linq.Expressions.Expression<Action<T>> methodCall, string cronExpression)
        => RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression);
}
